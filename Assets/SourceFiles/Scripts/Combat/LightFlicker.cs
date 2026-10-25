using System.Collections.Generic;
using UnityEngine;

namespace Combat
{
    // A failing street lamp: buzzes around its intensity, sometimes cutting out for a moment.
    // The power grid feeds it: full power, emergency (unstable, some cut out) or none.
    [RequireComponent(typeof(Light))]
    public class LightFlicker : MonoBehaviour
    {
        [Range(0, 1)] public float Flicker = 0.35f;
        [Tooltip("Chance per second of a short blackout")]
        public float BlackoutChance = 0.15f;
        [Tooltip("Renderer with the glowing bulb material, and the index of that material")]
        public Renderer Bulb;
        public int BulbMaterialIndex = -1;
        public Color BulbEmission = new Color(4.0f, 3.6f, 2.8f);
        [Tooltip("A burnt-out lamp: no light and a dark bulb")]
        public bool Dead;

        [Header("Power")]
        [Tooltip("Sector of the power grid, assigned by the PowerGridManager")]
        public int Sector = -1;

        // every lamp, for the power grid and for checking if a spot is lit
        public static readonly List<LightFlicker> All = new List<LightFlicker>();

        public PowerState Power { get; private set; } = PowerState.Normal;
        public float BaseIntensity => _baseIntensity;
        public bool IsOn => _light != null && _light.enabled && _light.intensity > _baseIntensity * 0.2f;
        public float Range => _light != null ? _light.range : 0.0f;
        public Light Light => _light;

        private Light _light;
        private float _baseIntensity;
        private float _seed;
        private float _blackoutUntil;
        private float _forcedFlickerUntil;
        private float _level = 1.0f;
        private bool _emergencyCut;
        private MaterialPropertyBlock _block;

        private void Awake()
        {
            _light = GetComponent<Light>();
            _baseIntensity = _light.intensity;
            _seed = Random.value * 100.0f;
            _block = new MaterialPropertyBlock();

            if (Dead)
            {
                _light.enabled = false;
                SetBulb(0.0f);
            }
        }

        private void OnEnable()
        {
            All.Add(this);
        }

        private void OnDisable()
        {
            All.Remove(this);
        }

        public void SetPower(PowerState power)
        {
            Power = power;
            // in an emergency about a third of the lamps drop out completely
            _emergencyCut = power == PowerState.Emergency && Mathf.Repeat(_seed, 3.0f) < 1.0f;
        }

        // a burst of flickering, e.g. a scripted scare
        public void ForceFlicker(float duration)
        {
            _forcedFlickerUntil = Time.time + duration;
        }

        private void Update()
        {
            if (Dead) return;

            float target;
            if (Power == PowerState.Blackout || _emergencyCut)
            {
                target = 0.0f;
            }
            else
            {
                bool unstable = Power == PowerState.Emergency || Time.time < _forcedFlickerUntil;
                float flicker = unstable ? Mathf.Max(Flicker, 0.75f) : Flicker;
                float blackoutChance = unstable ? Mathf.Max(BlackoutChance, 1.2f) : BlackoutChance;
                if (Random.value < blackoutChance * Time.deltaTime) _blackoutUntil = Time.time + Random.Range(0.05f, unstable ? 0.4f : 0.6f);

                float noise = Mathf.PerlinNoise(_seed, Time.time * (unstable ? 16.0f : 9.0f));
                target = Time.time < _blackoutUntil ? 0.05f : Mathf.Lerp(1.0f - flicker, 1.0f, noise);
                if (Power == PowerState.Emergency) target *= 0.7f;
            }

            // power going off or coming back dies down / warms up instead of snapping
            _level = target < _level ? Mathf.MoveTowards(_level, target, Time.deltaTime * 6.0f) : target;
            _light.enabled = _level > 0.01f;
            _light.intensity = _baseIntensity * _level;
            SetBulb(_level);
        }

        // only the bulb's material slot glows, not the whole lamp post
        private void SetBulb(float level)
        {
            if (Bulb == null || BulbMaterialIndex < 0) return;
            _block ??= new MaterialPropertyBlock();
            Bulb.GetPropertyBlock(_block, BulbMaterialIndex);
            _block.SetColor("_EmissionColor", BulbEmission * level);
            Bulb.SetPropertyBlock(_block, BulbMaterialIndex);
        }

        // is there a working lamp shining on this point
        public static bool IsLit(Vector3 point)
        {
            foreach (LightFlicker lamp in All)
            {
                if (!lamp.IsOn) continue;
                float reach = lamp.Range * 0.75f;
                Vector3 offset = point - lamp.transform.position;
                if (offset.sqrMagnitude > reach * reach) continue;
                // inside the cone of a spot lamp, with a margin for the soft edge
                Light light = lamp._light;
                if (light.type != LightType.Spot || Vector3.Angle(lamp.transform.forward, offset) < light.spotAngle * 0.45f) return true;
            }

            return false;
        }
    }
}
