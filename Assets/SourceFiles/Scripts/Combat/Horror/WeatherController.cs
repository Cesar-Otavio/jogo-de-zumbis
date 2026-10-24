using System;
using UnityEngine;

namespace Combat
{
    public enum RainIntensity
    {
        None,
        Light,
        Moderate,
        Heavy
    }

    /* Night weather: fog that thickens and thins, rain in three strengths, gusts of wind with
     * drifting dust, lightning with thunder arriving later the farther away it struck, and clouds
     * dimming the moon. Particles follow the camera so the cost doesn't grow with the map.
     * Lighting never drops below a playable floor.
     */
    public class WeatherController : MonoBehaviour
    {
        public static WeatherController Instance { get; private set; }
        // how wet the ground is, 0..1 (footsteps splash)
        public static float Wetness { get; private set; }
        // 0..1, how much the rain covers other ambient sounds
        public static float AmbientMasking => Instance != null ? Instance.RainAmount * 0.5f : 0.0f;

        [Header("Weather changes")]
        public bool Dynamic = true;
        public RainIntensity Rain = RainIntensity.None;
        public Vector2 ChangeInterval = new Vector2(70.0f, 170.0f);
        [Tooltip("Chances of none, light, moderate and heavy rain when the weather changes")]
        public Vector4 RainWeights = new Vector4(0.35f, 0.3f, 0.22f, 0.13f);

        [Header("Fog")]
        public float FogMin = 0.014f;
        public float FogMax = 0.026f;
        [Tooltip("Extra fog density at full rain")]
        public float FogRainBoost = 0.008f;
        [Tooltip("How fast the fog drifts between its limits")]
        public float FogDriftSpeed = 0.012f;

        [Header("Rain")]
        public Material RainMaterial;
        public AudioClip RainLoop;
        public float LightRate = 700.0f;
        public float ModerateRate = 2000.0f;
        public float HeavyRate = 4500.0f;

        [Header("Wind")]
        public AudioClip WindLoop;
        public Vector2 GustInterval = new Vector2(15.0f, 40.0f);
        public Material DustMaterial;

        [Header("Lightning")]
        public AudioClip[] ThunderSounds;
        [Tooltip("Strikes per minute in moderate and heavy rain")]
        public float LightningPerMinute = 2.5f;
        public float LightningIntensity = 1.6f;
        public Color LightningColor = new Color(0.75f, 0.82f, 1.0f);

        [Header("Moon")]
        public Light Moon;
        [Range(0, 1)] [Tooltip("Clouds never dim the moon below this fraction, so the base stays navigable")]
        public float MoonFloor = 0.6f;

        public float RainAmount { get; private set; }
        public float Wind { get; private set; }

        public event Action<RainIntensity> RainChanged;

        private float _nextChange;
        private float _nextGust;
        private float _gustUntil;
        private float _gustStrength;
        private float _nextLightning;
        private float _baseMoon;
        private float _seed;

        private Transform _follower;
        private ParticleSystem _rain;
        private ParticleSystem _dust;
        private AudioSource _rainAudio;
        private AudioSource _windAudio;
        private Light _lightning;
        private float _flashUntil;
        private float _flashStart;
        private float _thunderAt = -1.0f;
        private float _thunderDistance;

        private void Awake()
        {
            Instance = this;
            _seed = UnityEngine.Random.value * 100.0f;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            Wetness = 0.0f;
        }

        private void Start()
        {
            if (Moon == null) Moon = RenderSettings.sun;
            if (Moon != null) _baseMoon = Moon.intensity;

            _follower = new GameObject("WeatherFollower").transform;
            _follower.SetParent(transform, false);
            _rain = BuildRain();
            _dust = BuildDust();
            _rainAudio = Loop("Rain", RainLoop);
            _windAudio = Loop("Wind", WindLoop);

            var lightningObject = new GameObject("Lightning");
            lightningObject.transform.SetParent(transform, false);
            lightningObject.transform.rotation = Quaternion.Euler(55.0f, UnityEngine.Random.Range(0.0f, 360.0f), 0.0f);
            _lightning = lightningObject.AddComponent<Light>();
            _lightning.type = LightType.Directional;
            _lightning.color = LightningColor;
            _lightning.intensity = 0.0f;
            _lightning.shadows = LightShadows.None;
            _lightning.enabled = false;

            _nextChange = Time.time + UnityEngine.Random.Range(ChangeInterval.x, ChangeInterval.y) * 0.5f;
            _nextGust = Time.time + UnityEngine.Random.Range(GustInterval.x, GustInterval.y);
            _nextLightning = Time.time + 20.0f;
        }

        private AudioSource Loop(string name, AudioClip clip)
        {
            var source = new GameObject(name).AddComponent<AudioSource>();
            source.transform.SetParent(transform, false);
            source.clip = clip;
            source.loop = true;
            source.spatialBlend = 0.0f;
            source.volume = 0.0f;
            if (clip != null) source.Play();
            return source;
        }

        private ParticleSystem BuildRain()
        {
            var go = new GameObject("Rain");
            go.transform.SetParent(_follower, false);
            go.transform.localPosition = Vector3.up * 14.0f;
            go.transform.localRotation = Quaternion.Euler(90.0f, 0.0f, 0.0f);
            var particles = go.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystem.MainModule main = particles.main;
            main.loop = true;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = 1.3f;
            main.startSpeed = new ParticleSystem.MinMaxCurve(13.0f, 17.0f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.025f, 0.04f);
            main.startColor = new Color(0.75f, 0.8f, 0.9f, 0.35f);
            main.maxParticles = 8000;

            ParticleSystem.EmissionModule emission = particles.emission;
            emission.rateOverTime = 0.0f;

            ParticleSystem.ShapeModule shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(44.0f, 44.0f, 1.0f);

            ParticleSystem.VelocityOverLifetimeModule velocity = particles.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.x = new ParticleSystem.MinMaxCurve(0.0f);
            velocity.y = new ParticleSystem.MinMaxCurve(0.0f);
            velocity.z = new ParticleSystem.MinMaxCurve(0.0f);

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.velocityScale = 0.05f;
            renderer.lengthScale = 6.0f;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            if (RainMaterial != null) renderer.sharedMaterial = RainMaterial;
            particles.Play();
            return particles;
        }

        private ParticleSystem BuildDust()
        {
            var go = new GameObject("Dust");
            go.transform.SetParent(_follower, false);
            go.transform.localPosition = Vector3.up * 2.0f;
            var particles = go.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystem.MainModule main = particles.main;
            main.loop = true;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(4.0f, 8.0f);
            main.startSpeed = 0.1f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.01f, 0.035f);
            main.startColor = new Color(0.8f, 0.75f, 0.65f, 0.35f);
            main.maxParticles = 400;

            ParticleSystem.EmissionModule emission = particles.emission;
            emission.rateOverTime = 25.0f;

            ParticleSystem.ShapeModule shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(24.0f, 4.0f, 24.0f);

            ParticleSystem.NoiseModule noise = particles.noise;
            noise.enabled = true;
            noise.strength = 0.25f;
            noise.frequency = 0.4f;

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            if (DustMaterial != null) renderer.sharedMaterial = DustMaterial;
            else if (RainMaterial != null) renderer.sharedMaterial = RainMaterial;
            particles.Play();
            return particles;
        }

        public void SetRain(RainIntensity rain)
        {
            if (rain == Rain) return;
            Rain = rain;
            RainChanged?.Invoke(rain);
            if (rain == RainIntensity.Heavy) Subtitles.Show("[chuva forte]", 3.0f);
        }

        private float TargetRain()
        {
            switch (Rain)
            {
                case RainIntensity.Light: return 0.33f;
                case RainIntensity.Moderate: return 0.66f;
                case RainIntensity.Heavy: return 1.0f;
                default: return 0.0f;
            }
        }

        private void Update()
        {
            Camera cam = Camera.main;
            if (cam != null) _follower.position = cam.transform.position;

            if (Dynamic && Time.time >= _nextChange)
            {
                _nextChange = Time.time + UnityEngine.Random.Range(ChangeInterval.x, ChangeInterval.y);
                SetRain(PickRain());
            }

            float dt = Time.deltaTime;
            RainAmount = Mathf.MoveTowards(RainAmount, TargetRain(), dt * 0.04f);
            // the ground soaks up slowly and dries slower still
            Wetness = Mathf.MoveTowards(Wetness, RainAmount > 0.1f ? Mathf.Max(Wetness, RainAmount) : 0.0f, dt * (RainAmount > Wetness ? 0.05f : 0.01f));

            UpdateWind(dt);
            UpdateRain();
            UpdateFog();
            UpdateMoon();
            UpdateLightning();
        }

        private RainIntensity PickRain()
        {
            float total = RainWeights.x + RainWeights.y + RainWeights.z + RainWeights.w;
            float roll = UnityEngine.Random.value * total;
            if ((roll -= RainWeights.x) < 0.0f) return RainIntensity.None;
            if ((roll -= RainWeights.y) < 0.0f) return RainIntensity.Light;
            if ((roll -= RainWeights.z) < 0.0f) return RainIntensity.Moderate;
            return RainIntensity.Heavy;
        }

        private void UpdateWind(float dt)
        {
            if (Time.time >= _nextGust)
            {
                _nextGust = Time.time + UnityEngine.Random.Range(GustInterval.x, GustInterval.y) * Mathf.Lerp(1.0f, 0.5f, RainAmount);
                _gustUntil = Time.time + UnityEngine.Random.Range(2.5f, 6.0f);
                _gustStrength = UnityEngine.Random.Range(0.5f, 1.0f);
                if (_gustStrength > 0.85f) Subtitles.Show("[rajada de vento]", 2.0f);
            }

            float gust = Time.time < _gustUntil ? _gustStrength * Mathf.Sin(Mathf.InverseLerp(_gustUntil - 6.0f, _gustUntil, Time.time) * Mathf.PI) : 0.0f;
            float baseWind = 0.15f + RainAmount * 0.35f + Mathf.PerlinNoise(_seed, Time.time * 0.05f) * 0.15f;
            Wind = Mathf.Lerp(Wind, Mathf.Clamp01(baseWind + Mathf.Max(0.0f, gust)), dt * 2.0f);
            _windAudio.volume = (0.12f + Wind * 0.45f) * GameSettings.AmbientVolume;
            _windAudio.pitch = 0.85f + Wind * 0.3f;

            // dust and the rain lean with the wind
            Vector3 direction = Quaternion.Euler(0.0f, 40.0f + Mathf.PerlinNoise(_seed + 5.0f, Time.time * 0.02f) * 60.0f, 0.0f) * Vector3.forward;
            ParticleSystem.VelocityOverLifetimeModule rainVelocity = _rain.velocityOverLifetime;
            rainVelocity.x = direction.x * Wind * 6.0f;
            rainVelocity.z = direction.z * Wind * 6.0f;
            ParticleSystem.MainModule dust = _dust.main;
            dust.startSpeed = 0.1f + Wind * 3.0f;
            _dust.transform.rotation = Quaternion.LookRotation(direction);
            ParticleSystem.EmissionModule dustEmission = _dust.emission;
            dustEmission.rateOverTime = Mathf.Lerp(20.0f, 80.0f, Wind) * (1.0f - RainAmount * 0.8f);
        }

        private void UpdateRain()
        {
            float amount = RainAmount;
            float rate = amount <= 0.01f ? 0.0f : amount < 0.4f ? Mathf.Lerp(0.0f, LightRate, amount / 0.4f)
                : amount < 0.75f ? Mathf.Lerp(LightRate, ModerateRate, (amount - 0.4f) / 0.35f)
                : Mathf.Lerp(ModerateRate, HeavyRate, (amount - 0.75f) / 0.25f);
            ParticleSystem.EmissionModule emission = _rain.emission;
            emission.rateOverTime = rate;
            _rainAudio.volume = amount * 0.7f * GameSettings.AmbientVolume;
        }

        private void UpdateFog()
        {
            float drift = Mathf.PerlinNoise(_seed + 11.0f, Time.time * FogDriftSpeed);
            float target = Mathf.Lerp(FogMin, FogMax, drift) + RainAmount * FogRainBoost;
            RenderSettings.fogDensity = Mathf.Lerp(RenderSettings.fogDensity, target, Time.deltaTime * 0.5f);
        }

        private void UpdateMoon()
        {
            if (Moon == null) return;
            // clouds drifting over the moon
            float clouds = Mathf.PerlinNoise(_seed + 21.0f, Time.time * 0.03f);
            float level = Mathf.Lerp(1.1f, MoonFloor, clouds * 0.6f + RainAmount * 0.4f);
            Moon.intensity = _baseMoon * Mathf.Max(level, MoonFloor);
        }

        private void UpdateLightning()
        {
            // the flash: two or three quick pulses
            if (Time.time < _flashUntil)
            {
                float t = Time.time - _flashStart;
                float pulse = Mathf.Max(Mathf.Exp(-t * 18.0f), Mathf.Exp(-Mathf.Abs(t - 0.18f) * 30.0f) * 0.7f, Mathf.Exp(-Mathf.Abs(t - 0.33f) * 40.0f) * 0.4f);
                _lightning.enabled = true;
                _lightning.intensity = LightningIntensity * pulse * GameSettings.Flashes;
            }
            else if (_lightning.enabled)
            {
                _lightning.enabled = false;
            }

            if (_thunderAt > 0.0f && Time.time >= _thunderAt)
            {
                _thunderAt = -1.0f;
                if (ThunderSounds != null && ThunderSounds.Length > 0 && CombatEffects.Instance != null)
                {
                    float volume = Mathf.Lerp(1.0f, 0.35f, Mathf.InverseLerp(300.0f, 3000.0f, _thunderDistance));
                    CombatEffects.Instance.PlayAmbient(ThunderSounds[UnityEngine.Random.Range(0, ThunderSounds.Length)], transform.position, volume);
                    Subtitles.Show(_thunderDistance < 900.0f ? "[trovão próximo]" : "[trovão distante]", 3.0f);
                }
            }

            if (RainAmount < 0.5f || Time.time < _nextLightning) return;
            float perMinute = LightningPerMinute * Mathf.InverseLerp(0.5f, 1.0f, RainAmount) + 0.5f;
            _nextLightning = Time.time + UnityEngine.Random.Range(0.5f, 1.5f) * 60.0f / perMinute;
            Strike();
        }

        // lightning now, its thunder a while later: sound travels about 343 m/s
        public void Strike()
        {
            _flashStart = Time.time;
            _flashUntil = Time.time + 0.6f;
            _lightning.transform.rotation = Quaternion.Euler(UnityEngine.Random.Range(35.0f, 70.0f), UnityEngine.Random.Range(0.0f, 360.0f), 0.0f);
            _thunderDistance = UnityEngine.Random.Range(300.0f, 3000.0f);
            _thunderAt = Time.time + _thunderDistance / 343.0f;
        }
    }
}
