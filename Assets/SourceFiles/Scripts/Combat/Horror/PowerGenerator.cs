using UnityEngine;

namespace Combat
{
    /* A diesel generator that powers one sector of the grid. When the sector fails, the player
     * restarts it with the interact key: it cranks, sparks and smokes for a few seconds, then
     * the lights come back. A starting generator is loud and draws the infected.
     */
    public class PowerGenerator : MonoBehaviour, IInteractable
    {
        [Tooltip("Power grid sector, set by the PowerGridManager")]
        public int Sector = -1;
        [Tooltip("While a mission still needs this device (e.g. the fuse box), the generator won't start")]
        public string WaitsForDevice;
        public float StartDuration = 4.0f;
        [Tooltip("Seconds after starting during which the engine is loud enough to attract the infected")]
        public float NoisyTime = 12.0f;

        [Header("Parts")]
        public Light StatusLight;
        public Renderer StatusLamp;
        public Transform Body;
        public Transform Exhaust;

        [Header("Audio")]
        public AudioClip CrankSound;
        public AudioClip RunningLoop;
        public AudioClip FailSound;

        public bool IsStarting { get; private set; }
        public bool IsRunning => PowerGridManager.Instance == null || PowerGridManager.Instance.GetState(Sector) == PowerState.Normal;

        public Vector3 InteractionPoint => transform.position + Vector3.up * 0.9f;
        public float InteractionRange => 2.4f;

        public string Prompt
        {
            get
            {
                if (IsStarting) return "Ligando o gerador...";
                if (IsRunning) return "<color=#9BE39B>Gerador funcionando</color>";
                if (Blocked) return "<color=#FF8866>Gerador sem fusíveis</color> — instale-os primeiro";
                return Interactables.Key + "Religar gerador" + (PowerGridManager.Instance != null ? $" ({PowerGridManager.Instance.SectorName(Sector)})" : "");
            }
        }

        private float _startedAt;
        private float _noisyUntil;
        private float _nextNoise;
        private AudioSource _engine;
        private ParticleSystem _smoke;
        private ParticleSystem _sparks;
        private Vector3 _bodyRest;
        private MaterialPropertyBlock _block;

        private void Awake()
        {
            _engine = gameObject.AddComponent<AudioSource>();
            _engine.clip = RunningLoop;
            _engine.loop = true;
            _engine.spatialBlend = 1.0f;
            _engine.rolloffMode = AudioRolloffMode.Logarithmic;
            _engine.minDistance = 3.0f;
            _engine.maxDistance = 40.0f;
            _engine.volume = 0.6f;
            _engine.dopplerLevel = 0.0f;
            if (Body != null) _bodyRest = Body.localPosition;
            _block = new MaterialPropertyBlock();
            BuildEffects();
        }

        private void OnEnable()
        {
            Interactables.Register(this);
        }

        private void OnDisable()
        {
            Interactables.Unregister(this);
        }

        private void Start()
        {
            OnSectorStateChanged(PowerGridManager.Instance != null ? PowerGridManager.Instance.GetState(Sector) : PowerState.Normal);
        }

        private void BuildEffects()
        {
            CombatEffects fx = FindAnyObjectByType<CombatEffects>();
            Vector3 top = Exhaust != null ? Exhaust.position : transform.position + Vector3.up * 1.6f;
            _smoke = BuildParticles("Smoke", top, fx != null ? fx.SmokeMaterial : null, false);
            _sparks = BuildParticles("Sparks", transform.position + Vector3.up * 1.0f + transform.forward * 0.5f, fx != null ? fx.SparkMaterial : null, true);
        }

        private ParticleSystem BuildParticles(string name, Vector3 position, Material material, bool sparks)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.transform.position = position;
            go.transform.rotation = Quaternion.LookRotation(sparks ? transform.forward : Vector3.up);
            var particles = go.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystem.MainModule main = particles.main;
            main.playOnAwake = false;
            main.loop = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = sparks ? new ParticleSystem.MinMaxCurve(0.2f, 0.5f) : new ParticleSystem.MinMaxCurve(1.5f, 3.0f);
            main.startSpeed = sparks ? new ParticleSystem.MinMaxCurve(2.0f, 5.0f) : new ParticleSystem.MinMaxCurve(0.6f, 1.4f);
            main.startSize = sparks ? new ParticleSystem.MinMaxCurve(0.02f, 0.05f) : new ParticleSystem.MinMaxCurve(0.4f, 0.9f);
            main.startColor = sparks ? new Color(1.0f, 0.75f, 0.3f) : new Color(0.25f, 0.25f, 0.25f, 0.5f);
            main.gravityModifier = sparks ? 1.2f : -0.05f;
            main.maxParticles = 200;

            ParticleSystem.EmissionModule emission = particles.emission;
            emission.rateOverTime = sparks ? 0.0f : 6.0f;

            ParticleSystem.ShapeModule shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = sparks ? 35.0f : 12.0f;
            shape.radius = 0.05f;

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = sparks ? ParticleSystemRenderMode.Stretch : ParticleSystemRenderMode.Billboard;
            renderer.lengthScale = 2.0f;
            if (material != null) renderer.sharedMaterial = material;
            return particles;
        }

        private bool Blocked => !string.IsNullOrEmpty(WaitsForDevice) && MissionManager.Instance != null && MissionManager.Instance.IsNeeded(WaitsForDevice);

        public void Interact(PlayerCombat player)
        {
            if (IsStarting || IsRunning || Blocked) return;
            if (NetWorld.Redirect(this, NetWorld.ActGenerator))
            {
                if (player != null) player.ShowNotice("Ligando o gerador...");
                return;
            }

            IsStarting = true;
            _startedAt = Time.time;
            if (CombatEffects.Instance != null) CombatEffects.Instance.PlaySound(CrankSound, InteractionPoint, 1.0f, 0.03f, 1.0f, 3.0f, 60.0f);
            NoiseSystem.Emit(transform.position, NoiseSystem.DefaultRadius(NoiseCategory.Machine), NoiseCategory.Machine, Factions.Environment, gameObject);
            Subtitles.Show("[motor do gerador engasgando]", transform.position, StartDuration);
        }

        // the grid tells the generator what happened to its sector
        public void OnSectorStateChanged(PowerState state)
        {
            bool running = state == PowerState.Normal;
            if (running)
            {
                if (_engine.clip != null && !_engine.isPlaying) _engine.Play();
                _smoke.Play();
            }
            else
            {
                IsStarting = false;
                _engine.Stop();
                _smoke.Stop();
                if (Time.timeSinceLevelLoad > 1.0f)
                {
                    _sparks.Emit(40);
                    if (CombatEffects.Instance != null) CombatEffects.Instance.PlaySound(FailSound, InteractionPoint, 0.9f, 0.05f, 1.0f, 3.0f, 50.0f);
                }
            }

            SetStatus(running ? new Color(0.2f, 1.0f, 0.3f) : new Color(1.0f, 0.15f, 0.1f));
        }

        private void Update()
        {
            if (IsStarting)
            {
                float t = (Time.time - _startedAt) / StartDuration;
                // cranking: the engine shudders, sparks fly, the status light stutters
                if (Body != null) Body.localPosition = _bodyRest + Random.insideUnitSphere * 0.015f * (1.0f - t * 0.5f);
                if (Random.value < Time.deltaTime * 8.0f) _sparks.Emit(Random.Range(3, 10));
                SetStatus(Mathf.PingPong(Time.time * 6.0f, 1.0f) > 0.5f ? new Color(1.0f, 0.7f, 0.1f) : Color.black);

                if (t >= 1.0f)
                {
                    IsStarting = false;
                    if (Body != null) Body.localPosition = _bodyRest;
                    _noisyUntil = Time.time + NoisyTime;
                    if (PowerGridManager.Instance != null) PowerGridManager.Instance.Restore(Sector);
                    else OnSectorStateChanged(PowerState.Normal);
                    Subtitles.Show("[gerador ligado]", transform.position, 2.5f);
                }
            }

            // freshly started, it roars: the infected come to check
            if (Time.time < _noisyUntil && Time.time >= _nextNoise)
            {
                _nextNoise = Time.time + 3.0f;
                NoiseSystem.Emit(transform.position, NoiseSystem.DefaultRadius(NoiseCategory.Machine), NoiseCategory.Machine, Factions.Environment, gameObject);
            }

            _engine.volume = Time.time < _noisyUntil ? 0.9f : 0.5f;
        }

        private void SetStatus(Color color)
        {
            if (StatusLight != null) StatusLight.color = color;
            if (StatusLight != null) StatusLight.enabled = color.maxColorComponent > 0.01f;
            if (StatusLamp == null) return;
            _block ??= new MaterialPropertyBlock();
            StatusLamp.GetPropertyBlock(_block);
            _block.SetColor("_BaseColor", color);
            _block.SetColor("_EmissionColor", color * 3.0f);
            StatusLamp.SetPropertyBlock(_block);
        }
    }
}
