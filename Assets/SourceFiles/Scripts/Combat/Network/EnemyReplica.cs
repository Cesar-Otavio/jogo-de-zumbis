using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace Combat
{
    /* An infected on a client: the same prefab and variant as on the host, so it looks, sounds and dies
     * the same, but with its brain taken out (no AI, perception or navigation: the host decides all of
     * that). It follows the host's snapshots; a bullet that hits it becomes a damage request to the host.
     */
    public class EnemyReplica : MonoBehaviour
    {
        public ushort Id { get; private set; }

        private Animator _animator;
        private Health _health;
        private RagdollController _ragdoll;
        private AudioClip _deathSound;
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

        public static EnemyReplica Create(EnemyState state, GameObject prefab, ZombieVariant variant)
        {
            if (prefab == null) return null;
            // built inactive: the variant is applied by the zombie's own Awake, then its brain is switched
            // off before Start ever runs
            var holder = new GameObject("ReplicaBuild");
            holder.SetActive(false);
            GameObject copy = Instantiate(prefab, state.Position, Quaternion.Euler(0.0f, state.Yaw, 0.0f), holder.transform);
            var zombie = copy.GetComponent<ZombieAI>();
            if (zombie != null) zombie.Variant = variant;
            copy.transform.SetParent(null, true);
            Destroy(holder);

            AudioClip death = zombie != null ? zombie.DeathSound : null;
            float pitch = variant != null ? variant.VoicePitch : 1.0f;
            foreach (MonoBehaviour brain in new MonoBehaviour[] { zombie, copy.GetComponent<EnemyPerception>(), copy.GetComponent<ScreamerBehaviour>() })
            {
                if (brain == null) continue;
                brain.enabled = false;
                Destroy(brain);
            }

            var agent = copy.GetComponent<NavMeshAgent>();
            if (agent != null)
            {
                agent.enabled = false;
                Destroy(agent);
            }

            copy.name = $"Replica_{state.Id}_{prefab.name}";
            var replica = copy.AddComponent<EnemyReplica>();
            replica.Init(state, death, pitch);
            return replica;
        }

        private void Init(EnemyState state, AudioClip deathSound, float voicePitch)
        {
            Id = state.Id;
            _animator = GetComponentInChildren<Animator>();
            _health = GetComponent<Health>();
            _ragdoll = GetComponentInChildren<RagdollController>();
            _deathSound = deathSound;
            _voicePitch = voicePitch;
            _from = _to = state.Position;
            _fromYaw = _toYaw = state.Yaw;
            _stateTime = Time.unscaledTime;
            if (_animator != null) _animator.SetFloat("CycleOffset", Random.value);

            // a hit here is the host's business
            _health.Intercept = info =>
            {
                if (!_dead && NetGame.Instance != null) NetGame.Instance.RequestDamage(Id, info);
                return true;
            };
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

            if (_animator != null && !_dead)
            {
                _animator.SetFloat(AnimSpeed, state.Speed);
                int attacks = state.Flags >> 1;
                if (_attacks >= 0 && attacks != _attacks) _animator.SetTrigger(AnimAttack);
                _attacks = attacks;
            }

            if ((state.Flags & 1) != 0 && !_dead) Die();
        }

        private void Update()
        {
            if (_dead && _ragdoll != null && _ragdoll.IsRagdoll) return;
            float t = Mathf.Clamp01((Time.unscaledTime - _stateTime) * 10.0f);
            transform.SetPositionAndRotation(Vector3.Lerp(_from, _to, t), Quaternion.Euler(0.0f, Mathf.LerpAngle(_fromYaw, _toYaw, t), 0.0f));
        }

        private void Die()
        {
            _dead = true;
            _health.Mirror(0.0f, _health.MaxHealth);
            if (_animator != null) _animator.SetBool(AnimDead, true);
            if (CombatEffects.Instance != null && _deathSound != null)
            {
                CombatEffects.Instance.PlaySound(_deathSound, transform.position + Vector3.up * 1.2f, 1.0f, 0.05f, 1.0f, 2.0f, 50.0f, _voicePitch);
            }
        }
    }
}
