using UnityEngine;

namespace Combat
{
    /* Switches a character between animation and ragdoll physics.
     * While animated, the ragdoll bodies are kinematic and their colliders act as hitboxes.
     */
    public class RagdollController : MonoBehaviour
    {
        public bool IsRagdoll { get; private set; }

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
        }

        public void SetRagdoll(bool ragdoll)
        {
            IsRagdoll = ragdoll;
            if (_animator != null) _animator.enabled = !ragdoll;

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
