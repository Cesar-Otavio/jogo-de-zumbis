using UnityEngine;

namespace Combat
{
    // A collider that forwards bullet hits to the Health that owns it
    public class Hitbox : MonoBehaviour
    {
        public Health Owner;

        [Tooltip("Damage multiplier for this body part")]
        public float Multiplier = 1.0f;

        public bool IsHead;

        [Tooltip("Flesh shows a red puff and a thud, otherwise sparks and a metal clank")]
        public bool Flesh = true;

        public Rigidbody Body { get; private set; }

        private void Awake()
        {
            Body = GetComponent<Rigidbody>();
            if (Owner == null) Owner = GetComponentInParent<Health>();
        }
    }
}
