using UnityEngine;

namespace Combat
{
    /* Switches a character between animation and ragdoll physics.
     * While animated, the ragdoll bodies are kinematic and their colliders act as hitboxes.
     */
    public class RagdollController : MonoBehaviour
    {
        // bodies thrown around by physics at the same time; the oldest beyond this are frozen where they lie
        public const int MaxSimulated = 10;
        private static readonly System.Collections.Generic.List<RagdollController> Simulated = new System.Collections.Generic.List<RagdollController>();

        [Tooltip("A body at rest for this long stops simulating (it can't twitch or slide away later)")]
        public float SettleTime = 3.5f;

        public bool IsRagdoll { get; private set; }
        private float _ragdollSince;
        private bool _frozen;

        private Rigidbody[] _bodies;
        private Collider[] _colliders;
        private Animator _animator;

        private void Awake()
        {
            _animator = GetComponent<Animator>();
            _bodies = GetComponentsInChildren<Rigidbody>();
            _colliders = new Collider[_bodies.Length];
            for (int i = 0; i < _bodies.Length; i++)
            {
                _colliders[i] = _bodies[i].GetComponent<Collider>();
            }

            SetRagdoll(false);
            foreach (Rigidbody body in _bodies)
            {
                // a limb spawned inside the floor pops out gently instead of launching the body
                body.maxDepenetrationVelocity = 2.5f;
                body.solverIterations = 10;
            }
        }

        private void OnDisable()
        {
            Simulated.Remove(this);
        }

        private void Update()
        {
            if (!IsRagdoll || _frozen) return;
            if (Time.time - _ragdollSince < SettleTime) return;
            float motion = 0.0f;
            foreach (Rigidbody body in _bodies) motion = Mathf.Max(motion, body.linearVelocity.sqrMagnitude);
            if (motion < 0.04f) Freeze();
        }

        public void SetRagdoll(bool ragdoll)
        {
            IsRagdoll = ragdoll;
            _frozen = false;
            if (_animator != null) _animator.enabled = !ragdoll;
            Simulated.Remove(this);
            if (ragdoll)
            {
                _ragdollSince = Time.time;
                Simulated.Add(this);
                while (Simulated.Count > MaxSimulated)
                {
                    RagdollController oldest = Simulated[0];
                    Simulated.RemoveAt(0);
                    if (oldest != null) oldest.Freeze();
                }
            }

            foreach (Rigidbody body in _bodies)
            {
                body.isKinematic = !ragdoll;
                body.interpolation = ragdoll ? RigidbodyInterpolation.Interpolate : RigidbodyInterpolation.None;
                body.collisionDetectionMode = ragdoll ? CollisionDetectionMode.ContinuousSpeculative : CollisionDetectionMode.Discrete;
                if (ragdoll)
                {
                    body.linearVelocity = Vector3.zero;
                    body.angularVelocity = Vector3.zero;
                }
            }
        }

        // stops the physics where the body lies, so the corpse can be moved as a whole
        public void Freeze()
        {
            _frozen = true;
            Simulated.Remove(this);
            foreach (Rigidbody body in _bodies)
            {
                body.isKinematic = true;
            }
        }

        public void Push(Rigidbody body, Vector3 point, Vector3 impulse)
        {
            if (!IsRagdoll) return;

            if (body == null && _bodies.Length > 0) body = _bodies[0];
            if (body == null) return;
            body.AddForceAtPosition(impulse, point, ForceMode.Impulse);
        }

        // keeps the character's own capsule from fighting with its ragdoll colliders
        public void IgnoreCollisionsWith(Collider other)
        {
            foreach (Collider col in _colliders)
            {
                if (col != null && other != null) Physics.IgnoreCollision(col, other, true);
            }
        }
    }
}
