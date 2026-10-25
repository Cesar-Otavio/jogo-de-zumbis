using System;
using System.Collections.Generic;
using TMPro;
using Unity.Netcode;
using UnityEngine;

namespace Combat
{
    /* Another player as seen on this machine: the real character model (the same prefab the local
     * player uses) with only what's needed to look alive: animator, outfit, colliders, health, ragdoll.
     * Input, camera, weapons logic and every system that assumes "the player" are taken off before it
     * ever wakes up. It follows the owner's state with interpolation, holds the owner's gun, draws the
     * owner's shots and shows their name.
     *
     * On the host the puppet is also what the infected see and hit: hits are handed to the owner.
     */
    public class NetPuppet : MonoBehaviour
    {
        // the parts of the character prefab a puppet keeps; everything else is removed
        private static readonly HashSet<Type> Keep = new HashSet<Type>
        {
            typeof(Transform), typeof(Animator), typeof(Health), typeof(Hitbox), typeof(RagdollController), typeof(PlayerOutfit),
            typeof(CapsuleCollider), typeof(BoxCollider), typeof(SphereCollider), typeof(Rigidbody), typeof(CharacterJoint),
            typeof(SkinnedMeshRenderer), typeof(MeshRenderer), typeof(MeshFilter)
        };

        // how far behind the newest state the puppet is drawn, so it always has two states to blend between
        public const float InterpolationDelay = 0.1f;

        public NetPlayer Owner { get; private set; }
        public Health Health { get; private set; }

        private Animator _animator;
        private RagdollController _ragdoll;
        private PlayerOutfit _outfit;
        private Transform _hand;
        private GameObject _weaponModel;
        private WeaponData _weapon;
        private Light _flashlight;
        private TextMeshPro _nameTag;
        private readonly List<(float time, PlayerState state)> _buffer = new List<(float, PlayerState)>();
        private PlayerState _last;
        private bool _hasState;
        private bool _dead;

        private static readonly int AnimSpeed = Animator.StringToHash("Speed");
        private static readonly int AnimMotionSpeed = Animator.StringToHash("MotionSpeed");
        private static readonly int AnimGrounded = Animator.StringToHash("Grounded");
        private static readonly int AnimJump = Animator.StringToHash("Jump");
        private static readonly int AnimFreeFall = Animator.StringToHash("FreeFall");
        private static readonly int AnimHanging = Animator.StringToHash("Hanging");
        private static readonly int AnimRoll = Animator.StringToHash("Roll");
        private static readonly int AnimClimbUp = Animator.StringToHash("ClimbUp");

        public static NetPuppet Create(NetPlayer owner, int[] outfit)
        {
            NetSessionPrefabs prefabs = NetworkManager.Singleton != null ? NetworkManager.Singleton.GetComponent<NetSessionPrefabs>() : null;
            if (prefabs == null || prefabs.Character == null) return null;

            // built while inactive: nothing on the copy gets to run Awake as if it were the local player
            var holder = new GameObject("PuppetBuild");
            holder.SetActive(false);
            GameObject copy = Instantiate(prefabs.Character, holder.transform);
            Transform body = copy.GetComponentInChildren<Animator>(true).transform;
            body.SetParent(holder.transform, true);
            Strip(body.gameObject);

            body.name = "Puppet_" + owner.DisplayName;
            body.SetParent(null, true);
            body.position = owner.State.Value.Position;
            var puppet = body.gameObject.AddComponent<NetPuppet>();
            puppet.Init(owner, outfit);
            Destroy(holder);
            return puppet;
        }

        private static void Strip(GameObject root)
        {
            var doomed = new List<Component>();
            foreach (Component component in root.GetComponentsInChildren<Component>(true))
            {
                if (component != null && !Keep.Contains(component.GetType())) doomed.Add(component);
            }

            // a component can only go once nothing left on its object requires it (the flashlight needs the
            // combat script, which needs the weapon holder...), so peel them off in passes
            for (int pass = 0; pass < 8 && doomed.Count > 0; pass++)
            {
                for (int i = doomed.Count - 1; i >= 0; i--)
                {
                    Component component = doomed[i];
                    if (component != null && IsRequired(component)) continue;
                    if (component != null) DestroyImmediate(component);
                    doomed.RemoveAt(i);
                }
            }

            // children made only for the local player (camera targets, lights)
            foreach (Light light in root.GetComponentsInChildren<Light>(true)) DestroyImmediate(light.gameObject);
        }

        private static bool IsRequired(Component component)
        {
            foreach (Component other in component.GetComponents<Component>())
            {
                if (other == null || other == component) continue;
                foreach (RequireComponent require in Attribute.GetCustomAttributes(other.GetType(), typeof(RequireComponent), true))
                {
                    if ((require.m_Type0 != null && require.m_Type0.IsInstanceOfType(component)) ||
                        (require.m_Type1 != null && require.m_Type1.IsInstanceOfType(component)) ||
                        (require.m_Type2 != null && require.m_Type2.IsInstanceOfType(component))) return true;
                }
            }

            return false;
        }

        private void Init(NetPlayer owner, int[] outfit)
        {
            Owner = owner;
            _animator = GetComponent<Animator>();
            _ragdoll = GetComponent<RagdollController>();
            _outfit = GetComponent<PlayerOutfit>();
            Health = GetComponent<Health>();
            Health.Team = Factions.Player;
            Health.RegenRate = 0.0f;
            // the owner keeps the real health: hits on the host's copy go to them, others ignore hits
            Health.Intercept = info =>
            {
                if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer && !_dead) Owner.SendDamage(info);
                return true;
            };

            if (_animator != null)
            {
                _animator.applyRootMotion = false;
                _hand = _animator.isHuman ? _animator.GetBoneTransform(HumanBodyBones.RightHand) : null;
            }

            SetOutfit(outfit);
            BuildFlashlight();
            BuildNameTag();
        }

        public void SetOutfit(int[] choices)
        {
            if (_outfit != null) _outfit.ShowChoices(choices);
        }

        // ---------------------------------------------------------------- following the owner

        public void Follow(PlayerState state)
        {
            if (!_hasState || !state.Equals(_last))
            {
                _buffer.Add((Time.unscaledTime, state));
                if (_buffer.Count > 20) _buffer.RemoveAt(0);
                if (_hasState) Triggers(_last, state);
                _last = state;
                _hasState = true;
                if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer && !Owner.PlausibleMove(state.Position))
                {
                    Debug.LogWarning($"[Net] {Owner.DisplayName} moved faster than any move allows (respawn or a problem)");
                }
            }

            Interpolate();
            Health.Mirror(state.Health, state.MaxHealth);
            Animate(state);
            SetWeapon(NetMatch.Weapon(state.Weapon));
            if (_flashlight != null) _flashlight.enabled = state.Has(6) && !state.Has(7);
            SetDead(state.Has(7));
        }

        private void Interpolate()
        {
            if (_buffer.Count == 0) return;
            float renderTime = Time.unscaledTime - InterpolationDelay;
            // the two states either side of the render time
            int i = _buffer.Count - 1;
            while (i > 0 && _buffer[i - 1].time > renderTime) i--;
            if (i == 0)
            {
                Place(_buffer[0].state.Position, _buffer[0].state.Yaw);
                return;
            }

            var a = _buffer[i - 1];
            var b = _buffer[i];
            float t = Mathf.Clamp01((renderTime - a.time) / Mathf.Max(0.001f, b.time - a.time));
            // a jump of many metres is a respawn, not a run: snap instead of sliding across the map
            if ((b.state.Position - a.state.Position).sqrMagnitude > 25.0f) t = 1.0f;
            Place(Vector3.Lerp(a.state.Position, b.state.Position, t), Mathf.LerpAngle(a.state.Yaw, b.state.Yaw, t));
        }

        private void Place(Vector3 position, float yaw)
        {
            if (_dead && _ragdoll != null && _ragdoll.IsRagdoll) return;
            transform.SetPositionAndRotation(position, Quaternion.Euler(0.0f, yaw, 0.0f));
        }

        private void Animate(PlayerState state)
        {
            if (_animator == null || _dead) return;
            _animator.SetFloat(AnimSpeed, state.Speed);
            _animator.SetFloat(AnimMotionSpeed, 1.0f);
            _animator.SetBool(AnimGrounded, state.Has(0));
            _animator.SetBool(AnimJump, state.Has(1));
            _animator.SetBool(AnimFreeFall, state.Has(2));
            _animator.SetBool(AnimHanging, state.Has(3));
        }

        private void Triggers(PlayerState before, PlayerState now)
        {
            if (_animator == null) return;
            if (now.Rolls != before.Rolls) _animator.SetTrigger(AnimRoll);
            if (now.ClimbUps != before.ClimbUps) _animator.SetTrigger(AnimClimbUp);
        }

        private void SetDead(bool dead)
        {
            if (dead == _dead) return;
            _dead = dead;
            if (_ragdoll != null) _ragdoll.SetRagdoll(dead);
            if (_nameTag != null) _nameTag.color = dead ? new Color(1.0f, 0.4f, 0.35f) : Color.white;
        }

        // ---------------------------------------------------------------- the gun

        private void SetWeapon(WeaponData weapon)
        {
            if (weapon == _weapon) return;
            _weapon = weapon;
            if (_weaponModel != null) Destroy(_weaponModel);
            _weaponModel = null;
            if (weapon == null || weapon.Model == null) return;
            _weaponModel = Instantiate(weapon.Model);
            _weaponModel.name = "PuppetWeapon";
            foreach (Collider c in _weaponModel.GetComponentsInChildren<Collider>(true)) Destroy(c);
            foreach (Rigidbody r in _weaponModel.GetComponentsInChildren<Rigidbody>(true)) Destroy(r);
        }

        private void LateUpdate()
        {
            if (_weaponModel == null) return;
            bool show = !_dead;
            if (_weaponModel.activeSelf != show) _weaponModel.SetActive(show);
            if (!show) return;
            // held in the right hand, barrel (the model's -Z) along the body's facing, raised when aiming
            bool aiming = _hasState && _last.Has(5);
            Quaternion facing = transform.rotation * Quaternion.Euler(aiming ? -2.0f : 18.0f, 180.0f, 0.0f);
            Vector3 grip = _hand != null ? _hand.position : transform.position + Vector3.up * 1.2f + transform.right * 0.25f;
            _weaponModel.transform.SetPositionAndRotation(grip - facing * _weapon.GripPoint, facing);
        }

        public void PlayShot(Vector3 from, Vector3 to)
        {
            CombatEffects fx = CombatEffects.Instance;
            if (fx == null) return;
            Vector3 muzzle = _weaponModel != null && _weapon != null ? _weaponModel.transform.TransformPoint(_weapon.MuzzlePoint) : from;
            if (_weaponModel != null) fx.MuzzleFlash(_weaponModel.transform, muzzle, Quaternion.LookRotation(to - muzzle), 1.0f);
            fx.Tracer(muzzle, to, 0.02f);
            if (_weapon != null && _weapon.ShotSounds != null && _weapon.ShotSounds.Length > 0)
            {
                fx.PlaySound(_weapon.ShotSounds[UnityEngine.Random.Range(0, _weapon.ShotSounds.Length)], muzzle, 1.0f, 0.04f, 1.0f, 4.0f, 140.0f);
            }
        }

        // ---------------------------------------------------------------- dressing

        private void BuildFlashlight()
        {
            var go = new GameObject("PuppetFlashlight");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0.1f, 1.35f, 0.25f);
            _flashlight = go.AddComponent<Light>();
            _flashlight.type = LightType.Spot;
            _flashlight.range = 22.0f;
            _flashlight.spotAngle = 55.0f;
            _flashlight.intensity = 60.0f;
            _flashlight.color = new Color(1.0f, 0.96f, 0.88f);
            _flashlight.shadows = LightShadows.None;
            _flashlight.enabled = false;
        }

        private void BuildNameTag()
        {
            var go = new GameObject("NameTag", typeof(TextMeshPro));
            go.transform.SetParent(transform, false);
            go.transform.localPosition = Vector3.up * 2.15f;
            _nameTag = go.GetComponent<TextMeshPro>();
            _nameTag.text = Owner.DisplayName;
            _nameTag.fontSize = 2.2f;
            _nameTag.alignment = TextAlignmentOptions.Center;
            _nameTag.rectTransform.sizeDelta = new Vector2(3.0f, 0.5f);
        }

        private void Update()
        {
            // the name always faces this machine's camera
            Camera cam = Camera.main;
            if (_nameTag != null && cam != null) _nameTag.transform.rotation = Quaternion.LookRotation(_nameTag.transform.position - cam.transform.position);
        }

        private void OnDestroy()
        {
            if (_weaponModel != null) Destroy(_weaponModel);
        }
    }
}
