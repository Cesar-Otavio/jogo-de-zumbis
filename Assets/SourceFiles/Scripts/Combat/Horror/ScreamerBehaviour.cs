using System;
using UnityEngine;

namespace Combat
{
    /* The screamer: when it spots prey it stops, fills its lungs (a rising, audible wind-up the
     * player can interrupt by hurting or shoving it) and then shrieks, calling every infected
     * nearby straight to the prey. Added by ZombieAI when its variant is a Screamer.
     */
    [RequireComponent(typeof(ZombieAI))]
    public class ScreamerBehaviour : MonoBehaviour
    {
        public float ChargeTime = 2.4f;
        public float CallRadius = 55.0f;
        public float Cooldown = 14.0f;
        [Tooltip("Damage taken during the wind-up that chokes the scream")]
        public float InterruptDamage = 8.0f;

        public AudioClip ChargeSound;
        public AudioClip ScreamSound;
        public AudioClip ChokeSound;

        public bool IsCharging { get; private set; }

        // for HUD warnings
        public static event Action<ScreamerBehaviour> ChargeStarted;
        public static event Action<ScreamerBehaviour> Screamed;

        private ZombieAI _zombie;
        private EnemyPerception _perception;
        private Health _health;
        private Animator _animator;
        private float _chargeStarted;
        private float _readyAt;

        private void Awake()
        {
            _zombie = GetComponent<ZombieAI>();
            _perception = GetComponent<EnemyPerception>();
            _health = GetComponent<Health>();
            _animator = GetComponentInChildren<Animator>();
            if (ChargeSound == null && _zombie.AppliedVariant != null) ChargeSound = _zombie.AppliedVariant.WindupSound;
            if (ScreamSound == null && _zombie.AppliedVariant != null) ScreamSound = _zombie.AppliedVariant.DetectSound;
        }

        private void Start()
        {
            _perception.StateChanged += OnAwarenessChanged;
            _health.Damaged += OnDamaged;
        }

        private void OnAwarenessChanged(AwarenessState previous, AwarenessState state)
        {
            if (state == AwarenessState.Chase && previous != AwarenessState.Attack && Time.time >= _readyAt && !_health.IsDead) BeginCharge();
        }

        private void BeginCharge()
        {
            IsCharging = true;
            _chargeStarted = Time.time;
            _zombie.HoldUntil = Time.time + ChargeTime + 0.6f;
            if (CombatEffects.Instance != null) CombatEffects.Instance.PlaySound(ChargeSound, transform.position + Vector3.up * 1.6f, 1.0f, 0.02f, 1.0f, 4.0f, 60.0f);
            Subtitles.Show("[gritador puxando o ar]", transform.position, ChargeTime);
            ChargeStarted?.Invoke(this);
        }

        // time spent here, for the performance probe (Diagnostics/PerfProbe)
        private static readonly Unity.Profiling.ProfilerMarker UpdateMarker = new Unity.Profiling.ProfilerMarker("Combat.ScreamerBehaviour.Update");

        private void Update()
        {
            using (UpdateMarker.Auto()) UpdateMarked();
        }

        private void UpdateMarked()
        {
            if (!IsCharging) return;

            if (_health.IsDead)
            {
                IsCharging = false;
                return;
            }

            // head thrown back: the animation holds still, the body shudders
            if (_animator != null) _animator.speed = 0.15f;
            if (Time.time - _chargeStarted >= ChargeTime) Scream();
        }

        private void Scream()
        {
            IsCharging = false;
            _readyAt = Time.time + Cooldown;
            if (_animator != null) _animator.speed = 1.0f;
            _zombie.HoldUntil = Time.time + 0.6f;

            if (CombatEffects.Instance != null) CombatEffects.Instance.PlaySound(ScreamSound, transform.position + Vector3.up * 1.6f, 1.0f, 0.03f, 1.0f, 8.0f, 120.0f);
            Subtitles.Show("[GRITO ESTRIDENTE]", transform.position, 3.0f);

            // every infected in earshot comes running to the prey
            Health prey = _perception.Target;
            Vector3 where = prey != null ? prey.transform.position : _perception.LastKnownPosition;
            foreach (EnemyPerception other in EnemyPerception.All)
            {
                if (other == _perception || other.GetComponent<ZombieAI>() == null) continue;
                if (Vector3.Distance(other.transform.position, transform.position) <= CallRadius) other.Alert(prey, where);
            }

            // and the living hear it too
            NoiseSystem.Emit(transform.position, NoiseSystem.DefaultRadius(NoiseCategory.Scream), NoiseCategory.Scream, _health.Team, gameObject);
            Screamed?.Invoke(this);
        }

        private void OnDamaged(DamageInfo info)
        {
            if (IsCharging && info.Amount >= InterruptDamage) Interrupt();
        }

        // shoving or hurting it mid wind-up chokes the scream
        public void Interrupt()
        {
            if (!IsCharging) return;
            IsCharging = false;
            _readyAt = Time.time + Cooldown * 0.5f;
            _zombie.HoldUntil = 0.0f;
            if (_animator != null) _animator.speed = 1.0f;
            if (CombatEffects.Instance != null) CombatEffects.Instance.PlaySound(ChokeSound, transform.position + Vector3.up * 1.6f, 0.9f, 0.05f, 1.0f, 3.0f, 40.0f);
            Subtitles.Show("[grito interrompido]", transform.position, 2.0f);
        }
    }
}
