using UnityEngine;

namespace Combat
{
    /* Directional hit reactions layered over whatever the character is playing (walk, attack, aim):
     * the upper body snaps away from the shot and springs back. Small hits twitch; a big hit (or several
     * in a row) is a stagger: a deeper bend, and the head follows. It only bends the pose at IK time,
     * so the AI, the NavMesh agent and the attack timing are untouched. Humanoid rigs only.
     */
    [RequireComponent(typeof(Health))]
    public class HitReactor : MonoBehaviour
    {
        [Tooltip("Degrees of bend per point of damage")]
        public float DegreesPerDamage = 0.9f;
        public float MaxBend = 22.0f;
        [Tooltip("Damage within half a second that counts as a stagger")]
        public float StaggerDamage = 45.0f;
        public float StaggerBend = 32.0f;
        public float Stiffness = 120.0f;
        public float Damping = 11.0f;

        public bool Staggered => Time.time < _staggerUntil;

        private Animator _animator;
        private Health _health;
        private WeaponHolder _holder;
        private Vector2 _bend;            // x: pitch (forward/back), y: roll (side to side), degrees
        private Vector2 _velocity;
        private float _recentDamage;
        private float _recentStarted;
        private float _staggerUntil;

        private void Awake()
        {
            _animator = GetComponent<Animator>();
            _health = GetComponent<Health>();
            _holder = GetComponent<WeaponHolder>();
            _health.Damaged += OnDamaged;
        }

        private void OnDestroy()
        {
            if (_health != null) _health.Damaged -= OnDamaged;
        }

        private void OnDamaged(DamageInfo info)
        {
            if (_health.IsDead) return;
            Vector3 direction = info.Direction.sqrMagnitude > 0.001f ? info.Direction.normalized : -transform.forward;
            Vector3 local = transform.InverseTransformDirection(direction);

            if (Time.time - _recentStarted > 0.5f)
            {
                _recentStarted = Time.time;
                _recentDamage = 0.0f;
            }

            _recentDamage += info.Amount;
            bool stagger = _recentDamage >= StaggerDamage || info.Headshot;
            if (stagger) _staggerUntil = Time.time + 0.45f;

            float strength = Mathf.Min(info.Amount * DegreesPerDamage, stagger ? StaggerBend : MaxBend);
            // pushed along the shot: hit from the front bends back, from the side bends sideways
            _velocity += new Vector2(local.z, -local.x) * strength * 14.0f;
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0.0f) return;
            int steps = Mathf.Clamp(Mathf.CeilToInt(dt / 0.016f), 1, 8);
            float step = dt / steps;
            for (int i = 0; i < steps; i++)
            {
                _velocity += (-Stiffness * _bend - Damping * _velocity) * step;
                _bend += _velocity * step;
            }

            _bend = Vector2.ClampMagnitude(_bend, StaggerBend);
            if (float.IsNaN(_bend.x)) _bend = _velocity = Vector2.zero;
        }

        private void OnAnimatorIK(int layerIndex)
        {
            if (_animator == null || !_animator.isHuman || _health.IsDead || _bend.sqrMagnitude < 0.01f) return;
            Quaternion bend = Quaternion.AngleAxis(_bend.x, transform.right) * Quaternion.AngleAxis(_bend.y, transform.forward);
            // most of it in the chest; the hips barely move, so the feet stay planted
            _animator.bodyRotation = Quaternion.Slerp(Quaternion.identity, bend, 0.35f) * _animator.bodyRotation;
            // armed characters aim with the look-at; leave it to their weapon code
            if (_holder != null) return;
            _animator.SetLookAtWeight(Mathf.Clamp01(_bend.magnitude / StaggerBend) * 0.6f, 0.6f, 1.0f, 0.0f, 0.5f);
            _animator.SetLookAtPosition(_animator.GetBoneTransform(HumanBodyBones.Head) != null
                ? _animator.GetBoneTransform(HumanBodyBones.Head).position + bend * transform.forward * 2.0f
                : transform.position + transform.forward * 2.0f);
        }
    }
}
