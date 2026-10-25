using System;
using UnityEngine;

namespace Combat
{
    public enum TensionPhase
    {
        Exploration,
        Suspicion,
        Threat,
        Recovery
    }

    /* Paces the horror. Watches the player's health and ammo, the enemies around and aware of them,
     * how long since the last fight, how intense recent fighting was and whether the lights are out,
     * and settles on a phase:
     *   Exploration - quiet, atmosphere; Suspicion - sounds, clues, distant events;
     *   Threat - an actual fight; Recovery - a breather after a hard fight.
     * The phase scales environmental events, enemy drift towards the player, spawn pacing,
     * special infected, power failures and the music. It never moves enemies that exist already
     * and never spawns anything next to the player: the wave spawner still decides where.
     */
    public class TensionDirector : MonoBehaviour
    {
        public static TensionDirector Instance { get; private set; }
        // how strongly wandering enemies drift towards the player's area
        public static float HuntPressure = 1.0f;
        // multiplies the game music volume
        public static float MusicScale = 1.0f;

        [Tooltip("How long the breather lasts after an intense fight")]
        public float RecoveryTime = 25.0f;
        [Tooltip("Intensity at which a fight counts as hard, earning a breather afterwards")]
        [Range(0, 1)] public float RecoveryThreshold = 0.65f;
        [Tooltip("How fast the intensity of recent fighting fades, per second")]
        public float IntensityDecay = 0.035f;
        [Tooltip("Seconds without fighting before suspicion turns into exploration")]
        public float CalmAfter = 40.0f;
        public float UpdateInterval = 0.5f;

        public TensionPhase Phase { get; private set; } = TensionPhase.Exploration;
        public float Tension { get; private set; }
        public float Intensity { get; private set; }
        public float TimeSinceCombat => Time.time - _lastCombat;
        public int EnemiesNear { get; private set; }
        public int EnemiesAware { get; private set; }

        public event Action<TensionPhase> PhaseChanged;

        private PlayerCombat _player;
        private EnemySpawner _spawner;
        private EnvironmentalEventDirector _events;
        private float _lastCombat = -100.0f;
        private float _peakIntensity;
        private float _recoveryUntil;
        private float _nextUpdate;

        private void Awake()
        {
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            HuntPressure = 1.0f;
            MusicScale = 1.0f;
        }

        private void Start()
        {
            _player = FindAnyObjectByType<PlayerCombat>();
            _spawner = FindAnyObjectByType<EnemySpawner>();
            _events = FindAnyObjectByType<EnvironmentalEventDirector>();
            if (_player != null)
            {
                _player.TookDamage += OnPlayerDamaged;
                _player.LandedHit += OnPlayerHit;
                _player.Holder.Fired += _ => OnCombat(0.012f);
            }
        }

        private void OnPlayerDamaged(DamageInfo info)
        {
            OnCombat(Mathf.Clamp01(info.Amount / 100.0f) * 0.9f);
        }

        private void OnPlayerHit(HitReport report)
        {
            OnCombat(report.Killed ? 0.08f : 0.02f);
        }

        private void OnCombat(float intensity)
        {
            _lastCombat = Time.time;
            Intensity = Mathf.Clamp01(Intensity + intensity);
            _peakIntensity = Mathf.Max(_peakIntensity, Intensity);
        }

        // time spent here, for the performance probe (Diagnostics/PerfProbe)
        private static readonly Unity.Profiling.ProfilerMarker UpdateMarker = new Unity.Profiling.ProfilerMarker("Combat.TensionDirector.Update");

        private void Update()
        {
            using (UpdateMarker.Auto()) UpdateMarked();
        }

        private void UpdateMarked()
        {
            Intensity = Mathf.Max(0.0f, Intensity - IntensityDecay * Time.deltaTime);
            if (Time.time < _nextUpdate || _player == null) return;
            _nextUpdate = Time.time + UpdateInterval;

            Evaluate();
            Apply();
        }

        private void Evaluate()
        {
            Vector3 position = _player.transform.position;
            int near = 0;
            int aware = 0;
            foreach (EnemyPerception enemy in EnemyPerception.All)
            {
                if (enemy == null) continue;
                float distance = Vector3.Distance(enemy.transform.position, position);
                if (distance < 20.0f) near++;
                bool hunting = enemy.State == AwarenessState.Chase || enemy.State == AwarenessState.Attack;
                if (hunting && enemy.Target == _player.Health && distance < 35.0f) aware++;
            }

            EnemiesNear = near;
            EnemiesAware = aware;
            if (aware > 0) _lastCombat = Time.time;

            // pressure from the player's situation
            float health = _player.Health.Normalized;
            float ammo = AmmoFraction();
            bool dark = PowerGridManager.Instance != null && PowerGridManager.Instance.StateAt(position) != PowerState.Normal;
            float pressure = (1.0f - health) * 0.35f + (1.0f - ammo) * 0.15f + Mathf.Min(near, 6) / 6.0f * 0.25f + (dark ? 0.1f : 0.0f);
            Tension = Mathf.Clamp01(Mathf.Max(Intensity, pressure + Mathf.Min(aware, 4) * 0.12f));

            TensionPhase phase;
            if (Time.time < _recoveryUntil && aware == 0)
            {
                phase = TensionPhase.Recovery;
            }
            else if (aware > 0 || Intensity > 0.45f)
            {
                phase = TensionPhase.Threat;
            }
            else if (Phase == TensionPhase.Threat && _peakIntensity >= RecoveryThreshold)
            {
                // a hard fight just ended: back off for a while
                _recoveryUntil = Time.time + RecoveryTime;
                _peakIntensity = 0.0f;
                phase = TensionPhase.Recovery;
            }
            else if (TimeSinceCombat < CalmAfter || near > 0 || dark || Tension > 0.3f)
            {
                phase = TensionPhase.Suspicion;
            }
            else
            {
                phase = TensionPhase.Exploration;
            }

            if (phase != TensionPhase.Threat && phase != TensionPhase.Recovery) _peakIntensity = Mathf.Min(_peakIntensity, Intensity);
            if (phase != Phase)
            {
                Phase = phase;
                PhaseChanged?.Invoke(phase);
            }
        }

        private float AmmoFraction()
        {
            float have = 0.0f;
            float want = 0.0f;
            foreach (WeaponInstance weapon in _player.Slots)
            {
                if (weapon == null) continue;
                have += weapon.Magazine + weapon.Reserve;
                want += weapon.Data.MagazineSize * 4.0f;
            }

            return want > 0.0f ? Mathf.Clamp01(have / want) : 0.0f;
        }

        private void Apply()
        {
            float events;
            float hunt;
            float spawnInterval;
            float specials;
            bool failures;
            switch (Phase)
            {
                case TensionPhase.Exploration:
                    events = 1.3f; hunt = 1.0f; spawnInterval = 1.0f; specials = 1.2f; failures = true; MusicScale = 0.7f;
                    break;
                case TensionPhase.Suspicion:
                    events = 1.0f; hunt = 0.8f; spawnInterval = 1.0f; specials = 1.0f; failures = true; MusicScale = 0.85f;
                    break;
                case TensionPhase.Threat:
                    // already busy: no ambient scares, and ease off when the player is struggling
                    events = 0.2f; hunt = 1.0f; specials = 0.6f; failures = false; MusicScale = 1.0f;
                    spawnInterval = Tension > 0.8f ? 1.8f : 1.0f;
                    break;
                default:
                    events = 0.35f; hunt = 0.25f; spawnInterval = 2.5f; specials = 0.0f; failures = false; MusicScale = 0.6f;
                    break;
            }

            HuntPressure = hunt;
            if (_events != null) _events.RateScale = events;
            if (_spawner != null)
            {
                _spawner.SpawnIntervalScale = spawnInterval;
                _spawner.SpecialChanceScale = specials;
            }

            if (PowerGridManager.Instance != null) PowerGridManager.Instance.AllowFailures = failures;
        }
    }
}
