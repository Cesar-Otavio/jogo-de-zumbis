using StarterAssets;
using UnityEngine;

namespace Combat
{
    /* Emergency moves up close: a rifle-butt / knife strike (V) that finishes weakened enemies,
     * and a shove (B) that knocks an attacker back and can interrupt its blow. Both cost stamina
     * and have a cooldown, so guns stay the main way to fight.
     */
    public class PlayerCloseCombat : MonoBehaviour
    {
        [Header("Strike")]
        public float StrikeDamage = 40.0f;
        public float StrikeRange = 1.7f;
        public float StrikeRadius = 0.75f;
        public float StrikeStamina = 15.0f;
        public float StrikeCooldown = 0.75f;
        [Tooltip("Seconds from the start of the swing to the impact")]
        public float StrikeHitDelay = 0.15f;
        public float StrikeForce = 6.0f;

        [Header("Shove")]
        public float ShoveRange = 2.0f;
        public float ShoveAngle = 75.0f;
        public float ShoveStamina = 20.0f;
        public float ShoveCooldown = 1.2f;
        public float ShoveStrength = 1.0f;

        [Header("Audio")]
        public AudioClip[] SwingSounds;
        public AudioClip[] StrikeHitSounds;
        public AudioClip ShoveSound;
        public AudioClip[] EffortSounds;

        public float StrikeReadyAt { get; private set; }
        public float ShoveReadyAt { get; private set; }

        private PlayerCombat _player;
        private ThirdPersonController _controller;
        private StarterAssetsInputs _input;
        private ClimbingSystem _climbing;
        private Health _health;
        private float _pendingHitAt = -1.0f;
        private readonly Collider[] _overlap = new Collider[32];

        private void Awake()
        {
            _player = GetComponent<PlayerCombat>();
            _controller = GetComponent<ThirdPersonController>();
            _input = GetComponent<StarterAssetsInputs>();
            _climbing = GetComponent<ClimbingSystem>();
            _health = GetComponent<Health>();
        }

        private bool Busy => _health.IsDead || !_player.enabled || _controller.IsRolling || (_climbing != null && _climbing.IsClimbing);

        private void Update()
        {
            if (_input.melee)
            {
                _input.melee = false;
                if (!Busy) TryStrike();
            }

            if (_input.shove)
            {
                _input.shove = false;
                if (!Busy) TryShove();
            }

            if (_pendingHitAt > 0.0f && Time.time >= _pendingHitAt)
            {
                _pendingHitAt = -1.0f;
                if (!_health.IsDead) ResolveStrike();
            }
        }

        private Vector3 Forward
        {
            get
            {
                Camera cam = Camera.main;
                Vector3 forward = cam != null ? cam.transform.forward : transform.forward;
                forward.y = 0.0f;
                return forward.sqrMagnitude > 0.001f ? forward.normalized : transform.forward;
            }
        }

        // ---------------------------------------------------------------- strike

        private void TryStrike()
        {
            if (Time.time < StrikeReadyAt || !_controller.TrySpendStamina(StrikeStamina)) return;

            StrikeReadyAt = Time.time + StrikeCooldown;
            _pendingHitAt = Time.time + StrikeHitDelay;
            _player.EnterCombatStance();
            _player.Holder.MeleeThrust();
            Play(SwingSounds, 0.8f);
            Play(EffortSounds, 0.5f);
        }

        private void ResolveStrike()
        {
            Health target = FindTarget(StrikeRange, 70.0f, out Rigidbody body, out Vector3 point);
            NoiseSystem.Emit(transform.position, NoiseSystem.DefaultRadius(NoiseCategory.Melee), NoiseCategory.Melee, Factions.Player, gameObject);
            if (target == null) return;

            Vector3 direction = Forward;
            bool wasDead = target.IsDead;
            target.TakeDamage(new DamageInfo
            {
                Amount = StrikeDamage,
                Point = point,
                Direction = direction,
                Force = StrikeForce,
                Instigator = gameObject,
                HitBody = body
            });

            // the blow staggers what it doesn't kill
            if (!target.IsDead) Push(target, direction, 0.5f);
            if (!wasDead) _player.RegisterHit(new HitReport { Target = target, Damage = StrikeDamage, Killed = target.IsDead });

            Play(StrikeHitSounds, 1.0f);
            if (CombatEffects.Instance != null) CombatEffects.Instance.FleshImpact(point, -direction, direction);
        }

        // ---------------------------------------------------------------- shove

        private void TryShove()
        {
            if (Time.time < ShoveReadyAt) return;
            Health target = FindTarget(ShoveRange, ShoveAngle, out _, out _);
            if (target == null || !_controller.TrySpendStamina(ShoveStamina)) return;

            ShoveReadyAt = Time.time + ShoveCooldown;
            _player.EnterCombatStance();
            _player.Holder.MeleeThrust();
            Vector3 direction = target.transform.position - transform.position;
            direction.y = 0.0f;
            bool moved = Push(target, direction.normalized, ShoveStrength);
            Play(ShoveSound, 0.9f);
            Play(EffortSounds, 0.6f);
            if (!moved) _player.ShowNotice("Pesado demais para empurrar");
            NoiseSystem.Emit(transform.position, NoiseSystem.DefaultRadius(NoiseCategory.Melee), NoiseCategory.Melee, Factions.Player, gameObject);
        }

        private static bool Push(Health target, Vector3 direction, float strength)
        {
            if (target.TryGetComponent(out ZombieAI zombie)) return zombie.Shove(direction, strength);
            if (target.TryGetComponent(out EnemyShooterAI soldier)) return soldier.Shove(direction, strength);
            return false;
        }

        // the nearest living hostile in front, within reach
        private Health FindTarget(float range, float angle, out Rigidbody body, out Vector3 point)
        {
            body = null;
            point = transform.position + Vector3.up * 1.2f + Forward;
            Vector3 center = transform.position + Vector3.up * 1.0f + Forward * (range * 0.55f);
            int mask = (1 << CombatLayers.Hitbox) | (1 << CombatLayers.Enemy);
            int count = Physics.OverlapSphereNonAlloc(center, range * 0.55f + StrikeRadius, _overlap, mask, QueryTriggerInteraction.Collide);

            Health best = null;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                Collider col = _overlap[i];
                Health owner = col.TryGetComponent(out Hitbox hitbox) && hitbox.Owner != null ? hitbox.Owner : col.GetComponentInParent<Health>();
                if (owner == null || owner == _health || owner.IsDead || !Factions.AreHostile(_health.Team, owner.Team)) continue;

                Vector3 closest = col.ClosestPoint(transform.position + Vector3.up * 1.1f);
                Vector3 offset = closest - transform.position;
                offset.y = 0.0f;
                float distance = offset.magnitude;
                if (distance > range || (distance > 0.4f && Vector3.Angle(Forward, offset) > angle)) continue;
                if (distance >= bestDistance) continue;

                best = owner;
                bestDistance = distance;
                body = hitbox != null ? hitbox.Body : null;
                point = closest;
            }

            return best;
        }

        private void Play(AudioClip[] clips, float volume)
        {
            if (clips != null && clips.Length > 0) Play(clips[Random.Range(0, clips.Length)], volume);
        }

        private void Play(AudioClip clip, float volume)
        {
            if (clip != null && CombatEffects.Instance != null)
            {
                CombatEffects.Instance.PlaySound(clip, transform.position + Vector3.up * 1.3f, volume, 0.08f, 0.6f, 2.0f, 25.0f);
            }
        }
    }
}
