using StarterAssets;
using UnityEngine;

namespace Combat
{
    /* How easy the player is to see: crouching and darkness hide, a lit flashlight gives you away.
     * Enemies multiply their sight range by Visibility. Lighting is sampled from the base's lamps
     * a few times per second, not every frame.
     */
    public class PerceptionTarget : MonoBehaviour
    {
        [Range(0, 1)] public float CrouchFactor = 0.55f;
        [Tooltip("Visibility out of any lamp's light")]
        [Range(0, 1)] public float DarkFactor = 0.6f;
        [Tooltip("A lit flashlight makes you easy to spot, even in the dark")]
        public float FlashlightFactor = 1.35f;
        public float ChestHeight = 1.2f;
        public float LightCheckInterval = 0.3f;

        public float Visibility { get; private set; } = 1.0f;
        public bool InLight { get; private set; }

        private ThirdPersonController _controller;
        private PlayerCombat _combat;
        private float _nextLightCheck;

        private void Awake()
        {
            _controller = GetComponent<ThirdPersonController>();
            _combat = GetComponent<PlayerCombat>();
        }

        private void Update()
        {
            if (Time.time >= _nextLightCheck)
            {
                _nextLightCheck = Time.time + LightCheckInterval;
                InLight = LightFlicker.IsLit(transform.position + Vector3.up);
            }

            float crouch = _controller != null ? _controller.CrouchBlend : 0.0f;
            ChestHeight = Mathf.Lerp(1.2f, 0.8f, crouch);

            float visibility = Mathf.Lerp(1.0f, CrouchFactor, crouch) * (InLight ? 1.0f : DarkFactor);
            if (_combat != null && _combat.FlashlightOn) visibility = Mathf.Max(visibility, FlashlightFactor * Mathf.Lerp(1.0f, 0.8f, crouch));
            Visibility = visibility;
        }
    }
}
