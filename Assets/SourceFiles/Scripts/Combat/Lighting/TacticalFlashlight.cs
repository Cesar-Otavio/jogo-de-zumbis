using StarterAssets;
using UnityEngine;
using UnityEngine.Rendering;

namespace Combat
{
    /* The player's chest-mounted tactical light (toggled with the Flashlight action, F).
     *
     * It used to hang from the camera, behind the character: the body threw a big shadow right in the
     * middle of the screen and the ground at the player's feet was blown out. Now it sits on the chest,
     * in front of the shoulders, and points where the camera looks (the weapon's aim point when armed).
     *
     *   beam:  a spot with a soft, procedural cookie (bright core, dimmer rim), real shadows
     *   spill: a weak, shadowless point light a little ahead, so the surroundings never go pitch black
     *   sway:  a small lag and bob from walking and sprinting, steadier while aiming
     *
     * PlayerCombat.FlashlightOn stays the single source of truth (perception and the HUD read it).
     */
    [RequireComponent(typeof(PlayerCombat))]
    public class TacticalFlashlight : MonoBehaviour
    {
        [Header("Beam")]
        public float Intensity = 320.0f;
        [Tooltip("Light range. URP fades a light out towards its range, so it is set well past the useful reach (~25 m)")]
        public float Range = 40.0f;
        public float OuterAngle = 54.0f;
        public float InnerAngle = 26.0f;
        public Color BeamColor = new Color(1.0f, 0.95f, 0.84f);
        [Tooltip("Distance below which the beam doesn't cast shadows (avoids the player's own arms)")]
        public float ShadowNearPlane = 0.35f;

        [Header("Spill")]
        public float SpillIntensity = 4.5f;
        public float SpillRange = 8.0f;
        public float SpillAhead = 1.0f;
        [Tooltip("Below this distance to what the beam hits, it dims (inverse square would blow out walls up close)")]
        public float FullPowerDistance = 11.0f;

        [Header("Mount (character space)")]
        public Vector3 ChestOffset = new Vector3(0.16f, 0.08f, 0.26f);
        [Tooltip("How quickly the beam follows the view, normal / aiming")]
        public float FollowSharpness = 11.0f;
        public float AimFollowSharpness = 26.0f;

        [Header("Sway (degrees)")]
        public float WalkSway = 0.8f;
        public float SprintSway = 2.4f;
        public float AimSway = 0.15f;

        public bool IsOn => _combat != null && _combat.FlashlightOn;
        public Light Beam => _beam;

        private PlayerCombat _combat;
        private ThirdPersonController _controller;
        private CharacterController _body;
        private StarterAssetsInputs _input;
        private WeaponHolder _holder;
        private Health _health;
        private Transform _chest;
        private Light _beam;
        private Light _spill;
        private Transform _rig;
        private Quaternion _direction = Quaternion.identity;
        private float _bob;
        private float _power = 1.0f;
        private float _iris = 1.0f;
        private float _hitDistance = 20.0f;
        private static Texture2D _cookie;

        private void Awake()
        {
            _combat = GetComponent<PlayerCombat>();
            _controller = GetComponent<ThirdPersonController>();
            _body = GetComponent<CharacterController>();
            _input = GetComponent<StarterAssetsInputs>();
            _holder = GetComponent<WeaponHolder>();
            _health = GetComponent<Health>();
            var animator = GetComponent<Animator>();
            if (animator != null && animator.isHuman)
            {
                _chest = animator.GetBoneTransform(HumanBodyBones.UpperChest);
                if (_chest == null) _chest = animator.GetBoneTransform(HumanBodyBones.Chest);
            }

            Build();
            _direction = transform.rotation;
        }

        private void Build()
        {
            // the rig isn't parented to a bone: animation jitter would shake the beam
            _rig = new GameObject("TacticalFlashlight").transform;
            _rig.SetParent(transform, false);

            _beam = new GameObject("Beam").AddComponent<Light>();
            _beam.transform.SetParent(_rig, false);
            _beam.type = LightType.Spot;
            _beam.color = BeamColor;
            _beam.shadows = LightShadows.Soft;
            _beam.shadowStrength = 0.9f;
            _beam.shadowNearPlane = ShadowNearPlane;
            _beam.shadowResolution = LightShadowResolution.VeryHigh;
            _beam.renderMode = LightRenderMode.ForcePixel;
            _beam.cookie = Cookie();

            _spill = new GameObject("Spill").AddComponent<Light>();
            _spill.transform.SetParent(_rig, false);
            _spill.transform.localPosition = Vector3.forward * SpillAhead;
            _spill.type = LightType.Point;
            _spill.color = Color.Lerp(BeamColor, new Color(0.7f, 0.8f, 1.0f), 0.35f);
            _spill.shadows = LightShadows.None;
            _spill.renderMode = LightRenderMode.ForcePixel;
        }

        // a round beam with a hot core and a faint ring, like a real reflector, made once
        private static Texture2D Cookie()
        {
            if (_cookie != null) return _cookie;
            const int size = 128;
            _cookie = new Texture2D(size, size, TextureFormat.R8, false) { wrapMode = TextureWrapMode.Clamp, name = "FlashlightCookie" };
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f) / size * 2.0f - 1.0f;
                    float dy = (y + 0.5f) / size * 2.0f - 1.0f;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    float core = Mathf.Exp(-r * r * 7.0f);
                    float rim = 0.22f * Mathf.SmoothStep(1.0f, 0.55f, r);
                    float ring = 0.10f * Mathf.Exp(-Mathf.Pow((r - 0.42f) / 0.05f, 2.0f));
                    float v = Mathf.Clamp01(core + rim + ring) * Mathf.SmoothStep(1.0f, 0.88f, r);
                    pixels[y * size + x] = new Color(v, v, v, v);
                }
            }

            _cookie.SetPixels(pixels);
            _cookie.Apply(false, true);
            return _cookie;
        }

        private void LateUpdate()
        {
            float dt = Time.deltaTime;
            if (_beam == null) return;

            if (_input != null && _input.flashlight)
            {
                _input.flashlight = false;
                Toggle();
            }

            bool on = _combat.FlashlightOn && (_health == null || !_health.IsDead);
            // switching on/off takes a moment, so it doesn't pop
            _power = Mathf.MoveTowards(_power, on ? 1.0f : 0.0f, dt * 12.0f);
            _beam.enabled = _spill.enabled = _power > 0.001f;
            // an automatic iris: close walls don't turn into a white disc
            float iris = Mathf.Clamp(Mathf.Pow(_hitDistance / FullPowerDistance, 2.0f), 0.08f, 1.0f);
            _iris = Mathf.Lerp(_iris, iris, 1.0f - Mathf.Exp(-8.0f * dt));
            _beam.intensity = Intensity * _power * _iris * Brightness();
            _beam.range = Range;
            _beam.spotAngle = OuterAngle;
            _beam.innerSpotAngle = InnerAngle;
            _spill.intensity = SpillIntensity * _power * Brightness();
            _spill.range = SpillRange;
            if (!_beam.enabled) return;

            Place(dt);
        }

        public void Toggle()
        {
            _combat.FlashlightOn = !_combat.FlashlightOn;
            if (CombatEffects.Instance != null) CombatEffects.Instance.PlaySound(_combat.FlashlightSound, _rig.position, 0.5f, 0.05f, 0.0f);
        }

        // the accessibility brightness setting also lifts the light a little
        private static float Brightness() => 1.0f + Mathf.Max(0.0f, GameSettings.Brightness) * 0.35f;

        private void Place(float dt)
        {
            Quaternion yaw = Quaternion.Euler(0.0f, transform.eulerAngles.y, 0.0f);
            Vector3 anchor = _chest != null ? new Vector3(transform.position.x, _chest.position.y, transform.position.z) : transform.position + Vector3.up * 1.35f;
            Vector3 origin = anchor + yaw * ChestOffset;
            _rig.position = origin;

            // where the player is looking
            Camera cam = Camera.main;
            Vector3 target;
            if (_holder != null && _holder.Current != null && _holder.AimPoint != Vector3.zero) target = _holder.AimPoint;
            else if (cam != null)
            {
                Ray ray = new Ray(cam.transform.position, cam.transform.forward);
                target = Physics.Raycast(ray, out RaycastHit hit, Range * 1.5f, CombatLayers.Sight, QueryTriggerInteraction.Ignore) ? hit.point : ray.GetPoint(Range);
            }
            else target = origin + transform.forward * Range;

            // how far the beam reaches before it hits something, for the iris
            Vector3 beamDirection = _rig.forward;
            _hitDistance = Physics.Raycast(origin, beamDirection, out RaycastHit beamHit, Range, CombatLayers.Sight, QueryTriggerInteraction.Ignore) ? beamHit.distance : Range;

            Vector3 toTarget = target - origin;
            if (toTarget.sqrMagnitude < 0.25f) toTarget = yaw * Vector3.forward;
            Quaternion wanted = Quaternion.LookRotation(toTarget.normalized);

            // movement sway: a slow figure-eight that grows with speed, almost gone while aiming
            float aim = _holder != null ? _holder.AimBlend : 0.0f;
            float speed = _body != null ? new Vector3(_body.velocity.x, 0.0f, _body.velocity.z).magnitude : 0.0f;
            bool sprinting = _controller != null && _controller.IsSprinting;
            bool rolling = _controller != null && _controller.IsRolling;
            float amount = Mathf.Lerp(Mathf.Lerp(WalkSway * Mathf.Clamp01(speed / 3.0f), SprintSway, sprinting ? 1.0f : 0.0f), AimSway, aim);
            _bob += dt * Mathf.Lerp(1.2f, 9.0f, Mathf.Clamp01(speed / 6.0f));
            float swayYaw = Mathf.Sin(_bob) * amount;
            float swayPitch = Mathf.Abs(Mathf.Cos(_bob)) * amount * 0.8f + (Mathf.PerlinNoise(Time.time * 0.6f, 3.1f) - 0.5f) * 0.6f;
            wanted *= Quaternion.Euler(swayPitch, swayYaw, 0.0f);

            // a roll swings the light with the body, then it settles back on the view
            float sharpness = rolling ? 6.0f : Mathf.Lerp(FollowSharpness, AimFollowSharpness, aim);
            _direction = Quaternion.Slerp(_direction, wanted, 1.0f - Mathf.Exp(-sharpness * dt));
            _rig.rotation = _direction;
        }
    }
}
