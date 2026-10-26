using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace Combat
{
    /* An enemy on a client: the same prefab, variant and gun as on the host, so it looks, sounds and dies
     * the same, but with its brain taken out (no AI, perception or navigation: the host decides all of
     * that). It follows the host's snapshots and plays what the host says happened (a scream, a shot);
     * a bullet that hits it becomes a damage request to the host.
     *   infected - animation speed, attacks (with their sound), death
     *   soldier  - its gun drawn by the same weapon rig, aiming or lowered; the host's shots as flashes
     *   boss     - the boss's body, size and animations, its health bar and state on this HUD
     */
    public class EnemyReplica : MonoBehaviour
    {
        // the boss of the host's game, if one is up (for the boss bar on this machine)
        public static EnemyReplica ActiveBoss
        {
            get
            {
                foreach (EnemyReplica replica in Bosses)
                    if (replica != null && !replica._dead) return replica;
                return null;
            }
        }

        private static readonly List<EnemyReplica> Bosses = new List<EnemyReplica>();

        // what this client was told happened (for the test log)
        public static int ShotsSeen { get; private set; }
        public static int ScreamsSeen { get; private set; }

        public ushort Id { get; private set; }
        public byte Kind { get; private set; }
        public Health Health => _health;
        public BossDefinition Boss { get; private set; }
        public bool BossVulnerable { get; private set; }
        public bool BossLit { get; private set; }

        private Animator _animator;
        private bool _hasSpeed;
        private bool _hasAttack;
        private bool _hasDead;
        private Health _health;
        private RagdollController _ragdoll;
        private WeaponHolder _holder;
        private AudioClip _deathSound;
        private AudioClip _attackSound;
        private AudioClip _windupSound;
        private AudioClip _screamCharge;
        private AudioClip _scream;
        private float _voicePitch = 1.0f;
        private Vector3 _from;
        private Vector3 _to;
        private float _fromYaw;
        private float _toYaw;
        private float _stateTime;
        private int _attacks = -1;
        private bool _dead;

        private static readonly int AnimSpeed = Animator.StringToHash("Speed");
        private static readonly int AnimAttack = Animator.StringToHash("Attack");
        private static readonly int AnimDead = Animator.StringToHash("Dead");

        // built inactive: the prefab's own Awake sets it up (the variant, for an infected), then its brain is
        // switched off before Start ever runs
        private static GameObject Build(GameObject prefab, EnemyState state, System.Action<GameObject> beforeAwake)
        {
            var holder = new GameObject("ReplicaBuild");
            holder.SetActive(false);
            GameObject copy = Instantiate(prefab, state.Position, Quaternion.Euler(0.0f, state.Yaw, 0.0f), holder.transform);
            beforeAwake?.Invoke(copy);
            copy.transform.SetParent(null, true);
            Destroy(holder);
            return copy;
        }

        // dependents before what they require (the soldier's AI requires its perception and agent), at once,
        // before their Start runs
        private static void RemoveBrain(GameObject copy)
        {
            foreach (Component brain in new Component[]
                     {
                         copy.GetComponent<BossAI>(), copy.GetComponent<ScreamerBehaviour>(), copy.GetComponent<ZombieAI>(),
                         copy.GetComponent<EnemyShooterAI>(), copy.GetComponent<EnemyPerception>(), copy.GetComponent<NavMeshAgent>()
                     })
            {
                if (brain != null) DestroyImmediate(brain);
            }
        }

        public static EnemyReplica Create(EnemyState state, GameObject prefab, ZombieVariant variant)
        {
            if (prefab == null) return null;
            GameObject copy = Build(prefab, state, c =>
            {
                var z = c.GetComponent<ZombieAI>();
                if (z != null) z.Variant = variant;
            });

            var zombie = copy.GetComponent<ZombieAI>();
            var screamer = copy.GetComponent<ScreamerBehaviour>();
            var replica = copy.AddComponent<EnemyReplica>();
            replica._deathSound = zombie != null ? zombie.DeathSound : null;
            replica._attackSound = zombie != null ? zombie.AttackSound : null;
            replica._windupSound = variant != null ? variant.WindupSound : null;
            replica._screamCharge = screamer != null ? screamer.ChargeSound : null;
            replica._scream = screamer != null ? screamer.ScreamSound : null;
            replica._voicePitch = variant != null ? variant.VoicePitch : 1.0f;
            RemoveBrain(copy);
            copy.name = $"Replica_{state.Id}_{prefab.name}";
            replica.Init(state);
            return replica;
        }

        public static EnemyReplica CreateSoldier(EnemyState state, GameObject prefab)
        {
            if (prefab == null) return null;
            GameObject copy = Build(prefab, state, null);
            var soldier = copy.GetComponent<EnemyShooterAI>();
            WeaponData gun = soldier != null && soldier.Loadout != null && state.Variant < soldier.Loadout.Length ? soldier.Loadout[state.Variant] : null;
            var replica = copy.AddComponent<EnemyReplica>();
            replica._deathSound = soldier != null ? soldier.DeathSound : null;
            RemoveBrain(copy);

            // the same gun on the same rig: the weapon holder stays, it only never pulls the trigger here
            replica._holder = copy.GetComponent<WeaponHolder>();
            if (replica._holder != null && gun != null) replica._holder.Equip(new WeaponInstance(gun, gun.MagazineSize, 0, true));
            copy.name = $"Replica_{state.Id}_{prefab.name}";
            replica.Init(state);
            return replica;
        }

        public static EnemyReplica CreateBoss(EnemyState state, BossDefinition definition, GameObject body)
        {
            if (body == null || definition == null) return null;
            // the way BossAI.Create shapes an infected body into the boss
            GameObject copy = Build(body, state, c =>
            {
                c.transform.localScale *= definition.Scale;
                var animator = c.GetComponentInChildren<Animator>();
                if (definition.Animator != null && animator != null) animator.runtimeAnimatorController = definition.Animator;
            });

            var zombie = copy.GetComponent<ZombieAI>();
            var replica = copy.AddComponent<EnemyReplica>();
            replica._deathSound = zombie != null ? zombie.DeathSound : null;
            replica.Boss = definition;
            RemoveBrain(copy);
            copy.GetComponent<Health>().SetMaxHealth(definition.MaxHealth);
            copy.name = $"Replica_{state.Id}_{definition.DisplayName}";
            replica.Init(state);
            Bosses.Add(replica);
            return replica;
        }

        private void Init(EnemyState state)
        {
            Id = state.Id;
            Kind = state.Kind;
            _animator = GetComponentInChildren<Animator>();
            _health = GetComponent<Health>();
            _ragdoll = GetComponentInChildren<RagdollController>();
            _from = _to = state.Position;
            _fromYaw = _toYaw = state.Yaw;
            _stateTime = Time.unscaledTime;
            if (_animator != null)
            {
                foreach (AnimatorControllerParameter parameter in _animator.parameters)
                {
                    if (parameter.name == "Speed" && parameter.type == AnimatorControllerParameterType.Float) _hasSpeed = true;
                    if (parameter.name == "Attack" && parameter.type == AnimatorControllerParameterType.Trigger) _hasAttack = true;
                    if (parameter.name == "Dead" && parameter.type == AnimatorControllerParameterType.Bool) _hasDead = true;
                    if (parameter.name == "CycleOffset") _animator.SetFloat("CycleOffset", Random.value);
                }
            }

            // a hit here is the host's business
            _health.Intercept = info =>
            {
                if (!_dead && NetGame.Instance != null) NetGame.Instance.RequestDamage(Id, info);
                return true;
            };
        }

        private void OnDestroy()
        {
            Bosses.Remove(this);
        }

        public void Apply(EnemyState state)
        {
            // blend from where it is drawn now to the new position over one snapshot interval
            _from = transform.position;
            _fromYaw = transform.eulerAngles.y;
            _to = state.Position;
            _toYaw = state.Yaw;
            _stateTime = Time.unscaledTime;
            _health.Mirror(state.Health / 255.0f * _health.MaxHealth, _health.MaxHealth);

            if (_animator != null && !_dead && _hasSpeed) _animator.SetFloat(AnimSpeed, state.Speed);
            switch (Kind)
            {
                case EnemyKind.Zombie:
                    int attacks = state.Flags >> 1;
                    if (_attacks >= 0 && attacks != _attacks && !_dead) Attack();
                    _attacks = attacks;
                    break;
                case EnemyKind.Soldier:
                    if (_holder != null && !_dead)
                    {
                        _holder.Aiming = (state.Flags & 2) != 0;
                        _holder.Lowered = (state.Flags & 4) != 0;
                        _holder.TriggerHeld = false;
                        _holder.AimPoint = state.Position + Quaternion.Euler(0.0f, state.Yaw, 0.0f) * Vector3.forward * 10.0f + Vector3.up * 1.3f;
                    }

                    break;
                case EnemyKind.Boss:
                    BossVulnerable = (state.Flags & 2) != 0;
                    BossLit = (state.Flags & 4) != 0;
                    break;
            }

            if ((state.Flags & 1) != 0 && !_dead) Die();
        }

        private void Attack()
        {
            if (_animator != null && _hasAttack) _animator.SetTrigger(AnimAttack);
            CombatEffects fx = CombatEffects.Instance;
            if (fx == null) return;
            if (_attackSound != null) fx.PlaySound(_attackSound, transform.position + Vector3.up * 1.6f, 0.9f, 0.05f, 1.0f, 2.0f, 40.0f, _voicePitch);
            // the tank's heavy blow announces itself
            if (_windupSound != null) fx.PlaySound(_windupSound, transform.position + Vector3.up * 1.6f, 1.0f, 0.04f, 1.0f, 3.0f, 50.0f);
        }

        // something the host's enemy did once (NetGame's events)
        public void PlayEvent(byte kind)
        {
            CombatEffects fx = CombatEffects.Instance;
            if (fx == null || _dead) return;
            if (kind == NetGame.EventScreamCharge && _screamCharge != null)
                fx.PlaySound(_screamCharge, transform.position + Vector3.up * 1.6f, 1.0f, 0.02f, 1.0f, 4.0f, 60.0f);
            if (kind == NetGame.EventScream) ScreamsSeen++;
            if (kind == NetGame.EventScream && _scream != null)
            {
                fx.PlaySound(_scream, transform.position + Vector3.up * 1.6f, 1.0f, 0.03f, 1.0f, 8.0f, 120.0f);
                Subtitles.Show("[grito agudo]", transform.position, 2.0f);
            }
        }

        // a soldier's shot on the host: flash, tracer and sound here (the hit was the host's)
        public void PlayShot(Vector3 from, Vector3 to)
        {
            CombatEffects fx = CombatEffects.Instance;
            if (fx == null || _dead) return;
            WeaponData gun = _holder != null && _holder.Current != null ? _holder.Current.Data : null;
            ShotsSeen++;
            fx.Tracer(from, to, 0.02f);
            if (gun != null && gun.ShotSounds != null && gun.ShotSounds.Length > 0)
                fx.PlaySound(gun.ShotSounds[Random.Range(0, gun.ShotSounds.Length)], from, 1.0f, 0.04f, 1.0f, 4.0f, 140.0f);
        }

        // time spent here, for the performance probe (Diagnostics/PerfProbe)
        private static readonly Unity.Profiling.ProfilerMarker UpdateMarker = new Unity.Profiling.ProfilerMarker("Combat.EnemyReplica.Update");

        private void Update()
        {
            using (UpdateMarker.Auto()) UpdateMarked();
        }

        private void UpdateMarked()
        {
            if (_dead && _ragdoll != null && _ragdoll.IsRagdoll) return;
            float t = Mathf.Clamp01((Time.unscaledTime - _stateTime) * 10.0f);
            transform.SetPositionAndRotation(Vector3.Lerp(_from, _to, t), Quaternion.Euler(0.0f, Mathf.LerpAngle(_fromYaw, _toYaw, t), 0.0f));
        }

        private void Die()
        {
            _dead = true;
            _health.Mirror(0.0f, _health.MaxHealth);
            if (_holder != null)
            {
                _holder.Aiming = false;
                _holder.TriggerHeld = false;
            }

            // soldiers fall as ragdolls, as on the host; the infected play their death
            if (Kind == EnemyKind.Soldier && _ragdoll != null) _ragdoll.SetRagdoll(true);
            else if (_ragdoll != null && !_hasDead) _ragdoll.SetRagdoll(true);
            else if (_animator != null) _animator.SetBool(AnimDead, true);
            if (CombatEffects.Instance != null && _deathSound != null)
            {
                CombatEffects.Instance.PlaySound(_deathSound, transform.position + Vector3.up * 1.2f, 1.0f, 0.05f, 1.0f, 2.0f, 50.0f, _voicePitch);
            }
        }
    }
}
