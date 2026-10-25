using System;
using StarterAssets;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Rendering;

namespace Combat
{
    /* Connects the player's input, camera and inventory to the WeaponHolder:
     * aiming through the camera, two weapon slots, picking up / dropping / switching guns,
     * camera zoom and recoil, and dying / respawning.
     */
    [DefaultExecutionOrder(20)]
    [RequireComponent(typeof(WeaponHolder))]
    [RequireComponent(typeof(Health))]
    public class PlayerCombat : MonoBehaviour
    {
        [Header("Camera")]
        public float HipCameraDistance = 3.2f;
        [Tooltip("Over the right shoulder, so the character never covers the crosshair")]
        public Vector3 HipShoulderOffset = new Vector3(0.85f, 0.25f, 0.0f);
        public float AimCameraDistance = 1.9f;
        public Vector3 AimShoulderOffset = new Vector3(0.6f, 0.1f, 0.0f);
        public float CameraBlendSpeed = 8.0f;

        [Header("Aim Down Sights")]
        [Tooltip("Aiming moves the camera to the weapon's sights (first person)")]
        public bool FirstPersonAim = true;
        [Tooltip("Where the sights are held: up from the camera pivot, then along the view. The pivot doesn't bob or lean, so the view stays steady")]
        public Vector3 SightOffsetFromHead = new Vector3(0.0f, 0.12f, 0.34f);
        [Tooltip("Distance from the eye to the sights")]
        public float EyeRelief = 0.1f;
        public float AimNearClip = 0.02f;

        [Header("Movement")]
        [Tooltip("Move speed multiplier while aiming down sights")]
        public float AimMoveSpeed = 0.55f;
        [Tooltip("Seconds the character keeps facing the aim after shooting")]
        public float CombatStanceTime = 1.2f;

        [Header("Inventory")]
        public float PickupRange = 2.0f;
        public WeaponData StartingWeapon;

        [Header("Death")]
        public float RespawnDelay = 4.0f;
        [Tooltip("Seconds of protection after respawning")]
        public float SpawnProtection = 3.0f;
        [Tooltip("Seconds of invulnerability at the start of a roll")]
        public float RollProtection = 0.4f;
        public AudioClip RollSound;
        [Tooltip("Overrides the shared player hurt / death sounds")]
        public AudioClip[] HurtSounds;
        public AudioClip DeathSound;

        [Header("Flashlight")]
        [Tooltip("The light itself is the TacticalFlashlight component; this is its on/off state, read by perception and the HUD")]
        public bool FlashlightOn = true;
        public AudioClip FlashlightSound;

        // the game flow turns this off when the last life is lost
        [NonSerialized] public bool AllowRespawn = true;

        // not serialized: Unity would fill the slots with empty instances instead of null
        [NonSerialized] public WeaponInstance[] Slots = new WeaponInstance[2];
        public int ActiveSlot { get; private set; }
        public WeaponInstance Current => Slots[ActiveSlot];
        public WeaponInstance Other => Slots[1 - ActiveSlot];
        public WeaponPickup NearbyPickup { get; private set; }
        public LootCrate NearbyCrate { get; private set; }
        // a door, generator or anything else usable with the interact key
        public IInteractable NearbyInteractable { get; private set; }
        public bool ShoulderRight { get; private set; } = true;
        public WeaponHolder Holder => _holder;
        public Health Health => _health;
        public int Kills { get; private set; }
        public float RespawnTime { get; private set; }
        public ThirdPersonController Controller => _controller;
        public bool IsRolling => _controller != null && _controller.IsRolling;

        public static PlayerCombat Instance { get; private set; }

        // unarmed start (the campaign's first objective is to find a gun)
        [NonSerialized] public bool StartUnarmed;

        public event Action<DamageInfo> PlayerDied;

        public event Action<HitReport> LandedHit;
        public event Action<DamageInfo> TookDamage;
        public event Action<string> Notify;

        private WeaponHolder _holder;
        private Health _health;
        private StarterAssetsInputs _input;
        private ThirdPersonController _controller;
        private ClimbingSystem _climbing;
        private CharacterController _characterController;
        private RagdollController _ragdoll;
        private RespawnPlayer _respawn;

        private Camera _camera;
        private CinemachineCamera _virtualCamera;
        private CinemachineThirdPersonFollow _follow;
        private CinemachineBasicMultiChannelPerlin _noise;
        private float _baseFov;
        private float _baseDistance;
        private Vector3 _baseShoulder;
        private float _baseCameraSide;
        private float _baseNoise;
        private float _armedBlend;
        private float _shake;

        private float _lastFireTime = -10.0f;
        private float _cameraSide = 1.0f;

        private Transform _head;
        private LeanController _lean;
        private float _queuedSwitchUntil = -1.0f;
        private float _baseNearClip;
        private SkinnedMeshRenderer[] _bodyRenderers;
        private bool _bodyHidden;

        private void Awake()
        {
            Instance = this;
            _holder = GetComponent<WeaponHolder>();
            _health = GetComponent<Health>();
            _input = GetComponent<StarterAssetsInputs>();
            _controller = GetComponent<ThirdPersonController>();
            _climbing = GetComponent<ClimbingSystem>();
            _characterController = GetComponent<CharacterController>();
            _ragdoll = GetComponent<RagdollController>();
            _respawn = GetComponent<RespawnPlayer>();
            _holder.IsPlayer = true;
            var animator = GetComponent<Animator>();
            if (animator != null && animator.isHuman) _head = animator.GetBoneTransform(HumanBodyBones.Head);
            _bodyRenderers = GetComponentsInChildren<SkinnedMeshRenderer>(true);
            _lean = GetComponent<LeanController>();
        }


        // looking down the sights, the body (big hands right in front of the eye) only casts its shadow
        private void SetBodyHidden(bool hidden)
        {
            if (_bodyHidden == hidden) return;
            _bodyHidden = hidden;
            foreach (SkinnedMeshRenderer body in _bodyRenderers)
            {
                if (body != null) body.shadowCastingMode = hidden ? ShadowCastingMode.ShadowsOnly : ShadowCastingMode.On;
            }
        }

        private void OnEnable()
        {
            RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
            WeaponUpgrades.Changed += OnUpgradesChanged;
        }

        private void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
            WeaponUpgrades.Changed -= OnUpgradesChanged;
        }

        // a new upgrade: guns of that kind switch to the upgraded data, the one in hand is re-equipped
        private void OnUpgradesChanged(WeaponData original)
        {
            bool current = false;
            for (int i = 0; i < Slots.Length; i++)
            {
                if (Slots[i] == null || !WeaponUpgrades.SameWeapon(Slots[i].Data, original)) continue;
                Slots[i].Data = WeaponUpgrades.Resolve(original);
                current |= i == ActiveSlot;
            }

            if (current)
            {
                _holder.Unequip();
                EquipActive();
            }
        }

        public void AddShake(float amount)
        {
            _shake = Mathf.Min(_shake + amount, 2.0f);
        }

        // runs after Cinemachine has placed the camera: while aiming, move it behind the weapon's sights
        private void OnBeginCameraRendering(ScriptableRenderContext context, Camera cam)
        {
            if (cam != _camera) return;

            float aim = FirstPersonAim && Current != null && _holder.HasVisual && !_health.IsDead ? _holder.AimBlend : 0.0f;
            float t = Mathf.SmoothStep(0.0f, 1.0f, Mathf.InverseLerp(0.05f, 0.95f, aim));
            cam.nearClipPlane = Mathf.Lerp(_baseNearClip, AimNearClip, t);
            SetBodyHidden(t > 0.5f);
            if (t <= 0.0f) return;

            // follow the sights' direction but never roll the view
            // (leaning rolls it on purpose, together with the gun)
            Vector3 viewUp = Quaternion.LookRotation(_holder.SightForward, Vector3.up) * (_lean != null ? _lean.ViewRollRotation : Quaternion.identity) * Vector3.up;
            Quaternion sightRotation = Quaternion.LookRotation(_holder.SightForward, viewUp);
            Vector3 eye = _holder.SightPosition - _holder.SightForward * EyeRelief;
            Transform camTransform = cam.transform;
            if (float.IsNaN(eye.x) || float.IsNaN(camTransform.position.x)) return;
            camTransform.SetPositionAndRotation(Vector3.Lerp(camTransform.position, eye, t),
                Quaternion.Slerp(camTransform.rotation, sightRotation, t));
        }

        private void Start()
        {
            _camera = Camera.main;
            _baseNearClip = _camera.nearClipPlane;
            _virtualCamera = FindAnyObjectByType<CinemachineCamera>();
            if (_virtualCamera != null)
            {
                _follow = _virtualCamera.GetComponent<CinemachineThirdPersonFollow>();
                _noise = _virtualCamera.GetComponent<CinemachineBasicMultiChannelPerlin>();
                _baseFov = _virtualCamera.Lens.FieldOfView;
            }

            if (_follow != null)
            {
                _baseDistance = _follow.CameraDistance;
                _baseShoulder = _follow.ShoulderOffset;
                _baseCameraSide = _follow.CameraSide;
            }

            if (_noise != null) _baseNoise = _noise.AmplitudeGain;
            if (_ragdoll != null) _ragdoll.IgnoreCollisionsWith(_characterController);

            _holder.Fired += OnFired;
            _holder.Hit += OnHit;
            _health.Damaged += OnDamaged;
            _health.Died += OnDied;
            _controller.Rolled += OnRolled;

            // the game flow may already have armed the player when starting a run
            if (StartingWeapon != null && Current == null && Other == null && !StartUnarmed)
            {
                Give(new WeaponInstance(StartingWeapon, StartingWeapon.MagazineSize, StartingWeapon.StartReserve));
            }
        }

        // time spent here, for the performance probe (Diagnostics/PerfProbe)
        private static readonly Unity.Profiling.ProfilerMarker UpdateMarker = new Unity.Profiling.ProfilerMarker("Combat.PlayerCombat.Update");

        private void Update()
        {
            using (UpdateMarker.Auto()) UpdateMarked();
        }

        private void UpdateMarked()
        {
            // co-op: down on the ground waiting for a teammate; no moving, shooting or using things
            if (IsDowned)
            {
                UpdateCamera(Time.deltaTime);
                return;
            }

            if (_health.IsDead)
            {
                if (AllowRespawn && Time.time >= RespawnTime) Respawn();
                UpdateCamera(Time.deltaTime);
                return;
            }

            bool climbing = _climbing != null && _climbing.IsClimbing;
            bool rolling = _controller.IsRolling;
            bool armed = Current != null;

            _holder.AimPoint = FindAimPoint();

            bool shopping = ShopManager.IsOpen;
            bool aim = armed && _input.aim && !climbing && !rolling && !shopping;
            bool fire = armed && _input.fire && !climbing && !rolling && !shopping;
            if (fire) _lastFireTime = Time.time;

            Vector3 velocity = _characterController.velocity;
            float horizontalSpeed = new Vector3(velocity.x, 0.0f, velocity.z).magnitude;
            bool moving = _input.move.sqrMagnitude > 0.01f;
            bool combatStance = armed && !climbing && (aim || fire || Time.time - _lastFireTime < CombatStanceTime);

            _controller.StrafeMode = combatStance;
            _controller.StrafeYaw = _camera.transform.eulerAngles.y;
            _controller.SpeedMultiplier = aim ? AimMoveSpeed : 1.0f;
            _controller.BlockSprint = combatStance;

            _holder.Aiming = aim;
            _holder.TriggerHeld = fire;
            _holder.Sprinting = armed && _input.sprint && moving && !combatStance && _controller.Grounded;
            // the gun goes away while climbing or rolling
            _holder.Holstered = climbing || rolling;
            _holder.Lowered = false;
            _holder.MoveSpeed01 = Mathf.Clamp01(horizontalSpeed / _controller.SprintSpeed);
            _holder.CrouchDrop = Mathf.SmoothStep(0.0f, 1.0f, _controller.CrouchBlend) * _controller.CrouchCameraDrop;

            if (_input.swapShoulder)
            {
                _input.swapShoulder = false;
                ShoulderRight = !ShoulderRight;
            }

            // where the sights go when aiming in first person: in front of the eyes, along the view.
            // anchored on the camera pivot rather than the head bone, which bobs with the walk and swings with the hips
            _holder.SightAim = FirstPersonAim;
            Transform pivot = _controller.CinemachineCameraTarget.transform;
            _holder.ViewRotation = _lean != null ? pivot.rotation * _lean.ViewRollRotation : pivot.rotation;
            _holder.SightAnchor = pivot.position + Vector3.up * SightOffsetFromHead.y +
                                  _holder.ViewRotation * new Vector3(SightOffsetFromHead.x, 0.0f, SightOffsetFromHead.z);


            if (_input.reload)
            {
                _input.reload = false;
                _holder.StartReload();
            }

            // Q / E while aiming lean instead (LeanController): the same press must not also switch or interact
            if (_lean != null && _lean.Holding)
            {
                _input.switchWeapon = false;
                _input.interact = false;
            }

            if (_input.switchWeapon)
            {
                _input.switchWeapon = false;
                SwitchSlot();
            }
            else if (_queuedSwitchUntil > 0.0f && !_holder.Busy)
            {
                if (Time.time <= _queuedSwitchUntil) SwitchSlot();
                _queuedSwitchUntil = -1.0f;
            }

            if (_input.drop)
            {
                _input.drop = false;
                DropCurrent();
            }

            UpdateNearbyPickup();
            if (_input.interact)
            {
                _input.interact = false;
                // whichever is closest: a weapon on the ground, a closed chest, a door or a generator
                switch (ClosestInteraction())
                {
                    case 1:
                        NearbyCrate.Open(this);
                        break;
                    case 2:
                        PickUp(NearbyPickup);
                        break;
                    case 3:
                        NearbyInteractable.Interact(this);
                        break;
                }
            }

            UpdateCamera(Time.deltaTime);
        }

        // 0 nothing, 1 the chest, 2 the weapon, 3 the door / generator
        public int ClosestInteraction()
        {
            Vector3 center = transform.position + Vector3.up * 0.8f;
            float best = float.MaxValue;
            int choice = 0;
            if (NearbyCrate != null && Vector3.Distance(center, NearbyCrate.Center) < best)
            {
                best = Vector3.Distance(center, NearbyCrate.Center);
                choice = 1;
            }

            if (NearbyPickup != null && Vector3.Distance(center, NearbyPickup.Center) < best)
            {
                best = Vector3.Distance(center, NearbyPickup.Center);
                choice = 2;
            }

            if (NearbyInteractable != null && Vector3.Distance(center, NearbyInteractable.InteractionPoint) < best) choice = 3;
            return choice;
        }

        // face the view and hold the gun ready for a moment, e.g. for a melee strike
        public void EnterCombatStance()
        {
            _lastFireTime = Time.time;
        }

        // hits that don't come from the gun (melee) still count for kills, score and hit markers
        public void RegisterHit(HitReport report)
        {
            OnHit(report);
        }

        private Vector3 FindAimPoint()
        {
            Transform cam = _camera.transform;
            if (float.IsNaN(cam.position.x)) return transform.position + transform.forward * 50.0f + Vector3.up * 1.4f;
            Vector3 forward = cam.forward;

            // start the ray level with the player so nothing between the camera and the player is targeted
            Vector3 origin = cam.position;
            origin += forward * Mathf.Max(0.0f, Vector3.Dot(transform.position + Vector3.up * 1.4f - origin, forward));

            const float range = 300.0f;
            RaycastHit[] hits = Physics.RaycastAll(origin, forward, range, CombatLayers.Shootable & ~(1 << CombatLayers.Player),
                QueryTriggerInteraction.Ignore);
            float nearest = range;
            foreach (RaycastHit hit in hits)
            {
                if (hit.distance < nearest && !hit.collider.transform.IsChildOf(transform)) nearest = hit.distance;
            }

            return origin + forward * nearest;
        }

        private void UpdateCamera(float dt)
        {
            if (_follow == null) return;

            bool armed = Current != null && !_health.IsDead && !(_climbing != null && _climbing.IsClimbing);
            _armedBlend = Mathf.Lerp(_armedBlend, armed ? 1.0f : 0.0f, 1.0f - Mathf.Exp(-CameraBlendSpeed * 0.6f * dt));
            float aimBlend = _holder.AimBlend;

            float distance = Mathf.Lerp(Mathf.Lerp(_baseDistance, HipCameraDistance, _armedBlend), AimCameraDistance, aimBlend);
            Vector3 shoulder = Vector3.Lerp(Vector3.Lerp(_baseShoulder, HipShoulderOffset, _armedBlend), AimShoulderOffset, aimBlend);
            _follow.CameraDistance = distance;
            _follow.ShoulderOffset = shoulder;
            // CameraSide scales the shoulder offset; armed, the camera sits fully over the chosen shoulder (T swaps)
            _cameraSide = Mathf.Lerp(_cameraSide, ShoulderRight ? 1.0f : 0.0f, 1.0f - Mathf.Exp(-CameraBlendSpeed * dt));
            float unarmedSide = ShoulderRight ? _baseCameraSide : 1.0f - _baseCameraSide;
            _follow.CameraSide = Mathf.Lerp(unarmedSide, _cameraSide, _armedBlend);

            // the player's field of view option widens hip fire; aiming keeps each gun's zoom
            float hipFov = _baseFov + GameSettings.FieldOfViewOffset;
            float aimFov = Current != null ? Current.Data.AimFov : hipFov;
            float fov = Mathf.Lerp(hipFov, aimFov, aimBlend);
            LensSettings lens = _virtualCamera.Lens;
            lens.FieldOfView = fov;
            _virtualCamera.Lens = lens;
            _controller.LookSensitivityScale = fov / _baseFov;

            _shake = Mathf.MoveTowards(_shake, 0.0f, dt * 6.0f);
            if (_noise != null) _noise.AmplitudeGain = (_baseNoise + _shake) * GameSettings.CameraShake;
        }

        // ---------------------------------------------------------------- inventory

        public void Give(WeaponInstance weapon)
        {
            Give(weapon, null);
        }

        // from: where the weapon lies in the world; the hands go and take it from there (no instant swap)
        public void Give(WeaponInstance weapon, Pose? from)
        {
            // guns of a kind that has upgrades this run get them
            weapon.Data = WeaponUpgrades.Resolve(weapon.Data);
            int slot = Slots[ActiveSlot] == null ? ActiveSlot : Slots[1 - ActiveSlot] == null ? 1 - ActiveSlot : ActiveSlot;
            if (Slots[slot] != null) DropSlot(slot);

            Slots[slot] = weapon;
            ActiveSlot = slot;
            if (from.HasValue && !_health.IsDead)
            {
                // the gun in the hands (if any) goes on the body first, then the new one is picked up
                _holder.Grab(weapon, from.Value);
                return;
            }

            EquipActive();
        }

        private void PickUp(WeaponPickup pickup)
        {
            // co-op: whoever the host gives it to (first come)
            if (!NetWorld.MayTake(pickup, () => { if (pickup != null) PickUp(pickup); })) return;
            // the same gun only gives its ammo
            foreach (WeaponInstance owned in Slots)
            {
                if (owned != null && WeaponUpgrades.SameWeapon(owned.Data, pickup.Data))
                {
                    int ammo = pickup.Magazine + pickup.Reserve;
                    owned.Reserve += ammo;
                    Destroy(pickup.gameObject);
                    PlayPickupSound();
                    PickupFeed.Ammo(owned.Data, ammo);
                    return;
                }
            }

            Pose where = pickup.VisualPose;
            WeaponInstance taken = pickup.Take();
            Give(taken, where);
            PlayPickupSound();
            PickupFeed.Weapon(taken.Data);
        }

        private void PlayPickupSound()
        {
            if (CombatEffects.Instance != null)
            {
                CombatEffects.Instance.PlaySound(CombatEffects.Instance.PickupSound, transform.position + Vector3.up, 0.9f, 0.05f, 0.0f);
            }
        }

        private void EquipActive()
        {
            _holder.Equip(Current);
            _holder.SetBackWeapon(Other?.Data);
        }

        private void SwitchSlot()
        {
            if (Other == null) return;
            // mid-way through putting a gun away or drawing one: remember the press for a moment
            if (_holder.Busy)
            {
                _queuedSwitchUntil = Time.time + 0.35f;
                return;
            }

            ActiveSlot = 1 - ActiveSlot;
            _holder.SwitchTo(Current);
        }

        private void DropCurrent()
        {
            if (Current == null) return;

            DropSlot(ActiveSlot);
            if (Other != null)
            {
                ActiveSlot = 1 - ActiveSlot;
                // the carried gun is drawn from the body
                _holder.SwitchTo(Current);
            }
        }

        private void DropSlot(int slot)
        {
            WeaponInstance weapon = Slots[slot];
            if (weapon == null) return;

            Slots[slot] = null;
            if (slot == ActiveSlot) _holder.Unequip();
            _holder.SetBackWeapon(slot == ActiveSlot ? Other?.Data : null);

            Vector3 position = transform.position + Vector3.up * 1.2f + transform.forward * 0.5f;
            Vector3 velocity = transform.forward * 3.0f + Vector3.up * 1.5f;
            // co-op client: the host puts it on the ground for everyone
            NetWorld.PlayerDropped(WeaponPickup.Drop(weapon, position, transform.rotation * Quaternion.Euler(0.0f, 90.0f, 0.0f), velocity));
        }

        private void UpdateNearbyPickup()
        {
            WeaponPickup best = null;
            float bestDistance = PickupRange;
            Vector3 center = transform.position + Vector3.up * 0.8f;
            foreach (WeaponPickup pickup in WeaponPickup.All)
            {
                float distance = Vector3.Distance(center, pickup.Center);
                if (distance < bestDistance)
                {
                    best = pickup;
                    bestDistance = distance;
                }
            }

            if (NearbyPickup != best)
            {
                if (NearbyPickup != null) NearbyPickup.Highlighted = false;
                if (best != null) best.Highlighted = true;
                NearbyPickup = best;
            }

            LootCrate crate = null;
            float crateDistance = PickupRange;
            foreach (LootCrate candidate in LootCrate.All)
            {
                if (candidate.IsOpen) continue;
                float distance = Vector3.Distance(center, candidate.Center);
                if (distance < crateDistance)
                {
                    crate = candidate;
                    crateDistance = distance;
                }
            }

            NearbyCrate = crate;
            NearbyInteractable = Interactables.Nearest(center, out _);
        }

        public void ShowNotice(string text)
        {
            Notify?.Invoke(text);
        }

        // ---------------------------------------------------------------- events

        private void OnFired(WeaponInstance weapon)
        {
            // anything beyond the starting pistol without upgrades breaks the "basic gear" challenge
            if (!WeaponUpgrades.SameWeapon(weapon.Data, StartingWeapon) || WeaponUpgrades.On(weapon.Data).Count > 0) RunState.BasicOnly = false;
            WeaponData data = weapon.Data;
            float aimFactor = Mathf.Lerp(1.0f, 0.6f, _holder.AimBlend);
            _controller.AddCameraRecoil(data.CameraPitchKick * aimFactor, UnityEngine.Random.Range(-1.0f, 1.0f) * data.CameraYawKick * aimFactor);
            _shake = Mathf.Min(_shake + data.KickPitch * 0.06f, 1.5f);
        }

        private void OnRolled()
        {
            _health.InvulnerableUntil = Mathf.Max(_health.InvulnerableUntil, Time.time + RollProtection);
            if (CombatEffects.Instance != null && RollSound != null)
            {
                CombatEffects.Instance.PlaySound(RollSound, transform.position + Vector3.up * 0.5f, 0.8f, 0.08f, 0.0f);
            }
        }

        // a fresh run: full health and stamina, starting pistol only, no kills
        public void ResetForNewGame()
        {
            Kills = 0;
            AllowRespawn = true;
            if (IsDowned) ExitDowned(1.0f);
            // the dead drop their guns, and respawning hands out the starting weapon
            if (_health.IsDead)
            {
                Respawn();
                return;
            }

            Slots[0] = null;
            Slots[1] = null;
            ActiveSlot = 0;
            _holder.Unequip();
            _holder.SetBackWeapon(null);
            _health.ResetHealth();
            _health.InvulnerableUntil = Time.time + SpawnProtection;
            _health.Armor = 0.0f;
            _controller.RefillStamina();
            if (StartingWeapon != null && !StartUnarmed) Give(new WeaponInstance(StartingWeapon, StartingWeapon.MagazineSize, StartingWeapon.StartReserve));
        }

        // ---------------------------------------------------------------- co-op: down, revived, out

        public bool IsDowned { get; private set; }

        // a blow that would have killed: on the ground, alive, ignored by the infected while teammates come
        public void EnterDowned()
        {
            if (IsDowned || _health.IsDead) return;
            IsDowned = true;
            _holder.TriggerHeld = false;
            _input.fire = false;
            _input.aim = false;
            _controller.StrafeMode = false;
            _controller.enabled = false;
            if (_climbing != null) _climbing.enabled = false;
            _characterController.enabled = false;
            _health.Untargetable = true;
            _health.InvulnerableUntil = float.MaxValue;
            if (_ragdoll != null) _ragdoll.SetRagdoll(true);
        }

        // a teammate got them up: standing where the body lay, with part of their health
        public void ExitDowned(float healthFraction)
        {
            if (!IsDowned) return;
            IsDowned = false;
            if (_ragdoll != null)
            {
                Vector3 body = _ragdoll.BodyPosition;
                transform.position = new Vector3(body.x, transform.position.y, body.z);
                _ragdoll.SetRagdoll(false);
            }

            _characterController.enabled = true;
            _controller.enabled = true;
            if (_climbing != null) _climbing.enabled = true;
            _controller.ResetMotion(0.0f);
            _health.Untargetable = false;
            _health.Revive(healthFraction);
            _health.InvulnerableUntil = Time.time + SpawnProtection;
        }

        // bled out or a final blow: dies for real; coming back is up to the host (next wave)
        public void KillNow()
        {
            AllowRespawn = false;
            bool wasDown = IsDowned;
            IsDowned = false;
            _health.Untargetable = false;
            _health.InvulnerableUntil = 0.0f;
            if (wasDown) _health.TakeDamage(new DamageInfo { Amount = _health.MaxHealth * 10.0f, Point = transform.position, Direction = Vector3.down });
        }

        // the host brings this player back (a new wave)
        public void RespawnNow()
        {
            AllowRespawn = true;
            RespawnTime = Time.time;
        }

        // co-op client: the host confirmed a kill this player's shot made (the infected died on the host)
        public void CountNetworkKill()
        {
            Kills++;
            CombatEffects fx = CombatEffects.Instance;
            if (fx != null) fx.PlayUI(fx.KillSound, 0.7f);
        }

        private void OnHit(HitReport report)
        {
            LandedHit?.Invoke(report);

            CombatEffects fx = CombatEffects.Instance;
            if (fx == null) return;

            if (report.Killed)
            {
                Kills++;
                fx.PlayUI(fx.KillSound, 0.7f);
            }
            else if (report.Headshot)
            {
                fx.PlayUI(fx.HeadshotSound, 0.5f);
            }
            else
            {
                fx.PlayUI(fx.HitMarkerSound, 0.45f);
            }
        }

        private void OnDamaged(DamageInfo info)
        {
            TookDamage?.Invoke(info);
            _holder.Flinch(info.Direction);
            _shake = Mathf.Min(_shake + 0.6f, 1.5f);

            CombatEffects fx = CombatEffects.Instance;
            if (fx != null)
            {
                AudioClip[] hurt = HurtSounds != null && HurtSounds.Length > 0 ? HurtSounds : fx.PlayerHurtSounds;
                fx.PlayRandom(hurt, transform.position + Vector3.up, 0.7f, 0.0f);
            }
        }

        private void OnDied(DamageInfo info)
        {
            if (Net.Active) Debug.Log($"[Net] morte local: {info.Amount:F0} de dano de {(info.Instigator != null ? info.Instigator.name : "?")}, caído {IsDowned}\n{System.Environment.StackTrace}");
            RespawnTime = Time.time + RespawnDelay;

            DropSlot(1 - ActiveSlot);
            DropSlot(ActiveSlot);
            _holder.TriggerHeld = false;

            _controller.StrafeMode = false;
            _controller.enabled = false;
            if (_climbing != null) _climbing.enabled = false;
            _characterController.enabled = false;

            if (_ragdoll != null)
            {
                _ragdoll.SetRagdoll(true);
                _ragdoll.Push(info.HitBody, info.Point, info.Direction * info.Force * 6.0f);
            }

            CombatEffects fx = CombatEffects.Instance;
            if (fx != null) fx.PlaySound(DeathSound != null ? DeathSound : fx.RobotDeathSound, transform.position + Vector3.up, 1.0f, 0.0f, 0.0f);
            PlayerDied?.Invoke(info);
        }

        private void Respawn()
        {
            IsDowned = false;
            _health.Untargetable = false;
            if (_ragdoll != null) _ragdoll.SetRagdoll(false);
            _characterController.enabled = true;
            _controller.enabled = true;
            if (_climbing != null) _climbing.enabled = true;

            if (_respawn != null) _respawn.ForceRespawn();
            _health.ResetHealth();
            _health.InvulnerableUntil = Time.time + SpawnProtection;
            _controller.ResetMotion(0.0f);
            _controller.RefillStamina();
            _controller.SetCrouch(false, true);
            _input.fire = false;
            _input.aim = false;

            if (StartingWeapon != null) Give(new WeaponInstance(StartingWeapon, StartingWeapon.MagazineSize, StartingWeapon.StartReserve));
        }
    }
}
