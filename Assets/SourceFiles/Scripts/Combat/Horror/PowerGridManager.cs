using System;
using System.Collections.Generic;
using UnityEngine;

namespace Combat
{
    public enum PowerState
    {
        Normal,
        Emergency,
        Blackout
    }

    [Serializable]
    public class PowerSector
    {
        public string Name = "Setor";
        [Tooltip("Ground area of the sector (only X and Z matter)")]
        public Vector3 Center;
        public Vector3 Size = new Vector3(50.0f, 30.0f, 50.0f);
        public PowerGenerator Generator;

        [NonSerialized] public PowerState State = PowerState.Normal;
        [NonSerialized] public float StateSince;
        [NonSerialized] public float BlackoutAt = -1.0f;
        [NonSerialized] public readonly List<LightFlicker> Lamps = new List<LightFlicker>();
        [NonSerialized] public readonly List<Light> EmergencyLights = new List<Light>();
        [NonSerialized] public readonly List<AudioSource> Hums = new List<AudioSource>();
        [NonSerialized] public AudioSource Alarm;
        [NonSerialized] public float AlarmUntil;
        [NonSerialized] public float NextAlarmNoise;

        public bool Contains(Vector3 point)
        {
            return Mathf.Abs(point.x - Center.x) <= Size.x * 0.5f && Mathf.Abs(point.z - Center.z) <= Size.z * 0.5f;
        }
    }

    /* The base's electricity, split into sectors that fail and come back independently:
     * NORMAL (lamps, hum), EMERGENCY (flickering, some lamps out, red lights, alarm) and
     * BLACKOUT (lamps off, only dim battery lights; the flashlight matters). Generators restore
     * a sector. Missions and other systems use SetState / TriggerFailure / Restore and SectorChanged.
     */
    public class PowerGridManager : MonoBehaviour
    {
        public static PowerGridManager Instance { get; private set; }

        public PowerSector[] Sectors;

        [Header("Failures")]
        public bool RandomFailures = true;
        public float FirstFailureDelay = 100.0f;
        public Vector2 FailureInterval = new Vector2(110.0f, 220.0f);
        [Tooltip("Seconds without random failures after a sector is restored")]
        public float RestoreGrace = 120.0f;
        [Tooltip("An emergency turns into a blackout if nobody restores the sector in time")]
        public float EmergencyDuration = 30.0f;
        [Tooltip("Set by the tension director: no new failures while false")]
        public bool AllowFailures = true;

        [Header("Emergency lights")]
        public int EmergencyLightsPerSector = 4;
        public Color EmergencyColor = new Color(1.0f, 0.12f, 0.08f);
        public float EmergencyIntensity = 9.0f;
        public float EmergencyRange = 11.0f;
        [Range(0, 1)] [Tooltip("Battery level of the red lights during a blackout, so the base is never pitch black")]
        public float BatteryLevel = 0.35f;

        [Header("Blackout")]
        [Tooltip("The night sky light is raised a little while the player's sector is dark, to keep it playable")]
        public float BlackoutAmbientBoost = 1.35f;

        [Header("Audio")]
        public AudioClip HumLoop;
        public AudioClip AlarmLoop;
        public AudioClip PowerDownSound;
        public AudioClip PowerUpSound;
        public float AlarmDuration = 9.0f;

        public event Action<int, PowerState, PowerState> SectorChanged;

        private float _nextFailure;
        private Color _ambientSky;
        private Color _ambientEquator;
        private Color _ambientGround;
        private float _ambientBlend;
        private Transform _player;

        private void Awake()
        {
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Start()
        {
            _ambientSky = RenderSettings.ambientSkyColor;
            _ambientEquator = RenderSettings.ambientEquatorColor;
            _ambientGround = RenderSettings.ambientGroundColor;
            var player = FindAnyObjectByType<PlayerCombat>();
            if (player != null) _player = player.transform;

            if (Sectors == null) Sectors = new PowerSector[0];
            foreach (LightFlicker lamp in LightFlicker.All)
            {
                int sector = SectorAt(lamp.transform.position);
                lamp.Sector = sector;
                if (sector >= 0) Sectors[sector].Lamps.Add(lamp);
            }

            for (int i = 0; i < Sectors.Length; i++)
            {
                BuildSectorFixtures(i);
                if (Sectors[i].Generator != null) Sectors[i].Generator.Sector = i;
                ApplyState(i);
            }

            _nextFailure = Time.time + FirstFailureDelay;
        }

        private void BuildSectorFixtures(int index)
        {
            PowerSector sector = Sectors[index];
            var root = new GameObject("Power_" + sector.Name).transform;
            root.SetParent(transform, false);

            // red beacons at the foot of a few lamps, spread over the sector
            int count = Mathf.Min(EmergencyLightsPerSector, sector.Lamps.Count);
            for (int i = 0; i < count; i++)
            {
                LightFlicker lamp = sector.Lamps[i * sector.Lamps.Count / Mathf.Max(count, 1)];
                Vector3 foot = lamp.transform.position;
                if (Physics.Raycast(foot, Vector3.down, out RaycastHit hit, 20.0f, CombatLayers.Sight, QueryTriggerInteraction.Ignore)) foot = hit.point;
                var go = new GameObject("EmergencyLight");
                go.transform.SetParent(root, false);
                go.transform.position = foot + Vector3.up * 2.6f;
                var light = go.AddComponent<Light>();
                light.type = LightType.Point;
                light.color = EmergencyColor;
                light.range = EmergencyRange;
                light.intensity = 0.0f;
                light.shadows = LightShadows.None;
                light.enabled = false;
                sector.EmergencyLights.Add(light);
            }

            // a battery lamp over the generator's door, so it can be found in the dark
            if (sector.Generator != null)
            {
                Transform shed = sector.Generator.transform.parent != null ? sector.Generator.transform.parent : sector.Generator.transform;
                var go = new GameObject("GeneratorBeacon");
                go.transform.SetParent(root, false);
                go.transform.position = shed.position + shed.forward * 3.0f + Vector3.up * 2.7f;
                var beacon = go.AddComponent<Light>();
                beacon.type = LightType.Point;
                beacon.color = new Color(1.0f, 0.55f, 0.1f);
                beacon.range = 9.0f;
                beacon.intensity = 0.0f;
                beacon.shadows = LightShadows.None;
                beacon.enabled = false;
                sector.EmergencyLights.Add(beacon);
            }

            // the electrical hum of working lamps
            if (HumLoop != null)
            {
                for (int i = 0; i < Mathf.Min(2, sector.Lamps.Count); i++)
                {
                    AudioSource hum = sector.Lamps[i * Mathf.Max(1, sector.Lamps.Count / 2)].gameObject.AddComponent<AudioSource>();
                    hum.clip = HumLoop;
                    hum.loop = true;
                    hum.spatialBlend = 1.0f;
                    hum.rolloffMode = AudioRolloffMode.Linear;
                    hum.minDistance = 2.0f;
                    hum.maxDistance = 16.0f;
                    hum.volume = 0.25f;
                    hum.dopplerLevel = 0.0f;
                    hum.time = UnityEngine.Random.Range(0.0f, HumLoop.length);
                    hum.Play();
                    sector.Hums.Add(hum);
                }
            }

            if (AlarmLoop != null)
            {
                var alarm = new GameObject("Alarm").AddComponent<AudioSource>();
                alarm.transform.SetParent(root, false);
                alarm.transform.position = sector.Center + Vector3.up * 6.0f;
                alarm.clip = AlarmLoop;
                alarm.loop = true;
                alarm.spatialBlend = 1.0f;
                alarm.rolloffMode = AudioRolloffMode.Logarithmic;
                alarm.minDistance = 8.0f;
                alarm.maxDistance = 120.0f;
                alarm.volume = 0.55f;
                alarm.dopplerLevel = 0.0f;
                sector.Alarm = alarm;
            }
        }

        // ---------------------------------------------------------------- queries

        public int SectorAt(Vector3 point)
        {
            if (Sectors == null) return -1;
            for (int i = 0; i < Sectors.Length; i++)
            {
                if (Sectors[i].Contains(point)) return i;
            }

            return -1;
        }

        public PowerState GetState(int sector)
        {
            return sector >= 0 && Sectors != null && sector < Sectors.Length ? Sectors[sector].State : PowerState.Normal;
        }

        public PowerState StateAt(Vector3 point)
        {
            return GetState(SectorAt(point));
        }

        public string SectorName(int sector)
        {
            return sector >= 0 && sector < Sectors.Length ? Sectors[sector].Name : "";
        }

        // ---------------------------------------------------------------- control

        public void SetState(int index, PowerState state, bool alarm = true)
        {
            if (index < 0 || index >= Sectors.Length) return;
            PowerSector sector = Sectors[index];
            if (sector.State == state) return;

            PowerState previous = sector.State;
            sector.State = state;
            sector.StateSince = Time.time;
            sector.BlackoutAt = state == PowerState.Emergency ? Time.time + EmergencyDuration : -1.0f;
            ApplyState(index);

            CombatEffects fx = CombatEffects.Instance;
            Vector3 center = sector.Center + Vector3.up * 3.0f;
            if (state == PowerState.Normal)
            {
                if (fx != null) fx.PlaySound(PowerUpSound, center, 0.8f, 0.0f, 0.6f, 10.0f, 150.0f);
                StopAlarm(sector);
            }
            else
            {
                if (fx != null && previous == PowerState.Normal) fx.PlaySound(PowerDownSound, center, 0.9f, 0.0f, 0.6f, 10.0f, 150.0f);
                if (alarm && state == PowerState.Emergency) StartAlarm(sector);
                if (state == PowerState.Blackout) StopAlarm(sector);
            }

            if (sector.Generator != null) sector.Generator.OnSectorStateChanged(state);
            SectorChanged?.Invoke(index, previous, state);
        }

        // a fault: the sector drops to emergency power, then to a blackout if left alone
        public void TriggerFailure(int index)
        {
            if (index < 0 || index >= Sectors.Length || Sectors[index].State != PowerState.Normal) return;
            SetState(index, PowerState.Emergency);
        }

        public void Restore(int index)
        {
            SetState(index, PowerState.Normal, false);
            // power the player just fought to bring back holds for a while before the next random failure
            _nextFailure = Mathf.Max(_nextFailure, Time.time + RestoreGrace);
        }

        private void ApplyState(int index)
        {
            PowerSector sector = Sectors[index];
            foreach (LightFlicker lamp in sector.Lamps)
            {
                if (lamp != null) lamp.SetPower(sector.State);
            }

            foreach (AudioSource hum in sector.Hums)
            {
                if (hum == null) continue;
                hum.mute = sector.State == PowerState.Blackout;
                hum.pitch = sector.State == PowerState.Emergency ? 0.85f : 1.0f;
            }
        }

        private void StartAlarm(PowerSector sector)
        {
            sector.AlarmUntil = Time.time + AlarmDuration;
            sector.NextAlarmNoise = Time.time;
            if (sector.Alarm != null && !sector.Alarm.isPlaying) sector.Alarm.Play();
            Subtitles.Show($"[alarme de falha de energia — {sector.Name}]", sector.Center, 4.0f);
        }

        // a mission's reward: the grid holds for a while
        public void SuppressFailures(float seconds)
        {
            _nextFailure = Mathf.Max(_nextFailure, Time.time + seconds);
        }

        // e.g. the environmental events: a short alarm without a failure
        public void AlarmBurst(int index, float duration)
        {
            if (index < 0 || index >= Sectors.Length) return;
            PowerSector sector = Sectors[index];
            sector.AlarmUntil = Mathf.Max(sector.AlarmUntil, Time.time + duration);
            sector.NextAlarmNoise = Time.time;
            if (sector.Alarm != null && !sector.Alarm.isPlaying) sector.Alarm.Play();
            Subtitles.Show("[alarme disparou]", sector.Center, duration);
        }

        // e.g. a mission shutting the alarm panels down
        public void StopAlarmAt(int index)
        {
            if (index >= 0 && index < Sectors.Length) StopAlarm(Sectors[index]);
        }

        private static void StopAlarm(PowerSector sector)
        {
            sector.AlarmUntil = 0.0f;
            if (sector.Alarm != null) sector.Alarm.Stop();
        }

        // ---------------------------------------------------------------- update

        private void Update()
        {
            bool playing = GameFlow.Instance == null || GameFlow.Instance.Current == GameFlow.State.Playing;

            for (int i = 0; i < Sectors.Length; i++)
            {
                PowerSector sector = Sectors[i];
                if (sector.State == PowerState.Emergency && sector.BlackoutAt > 0.0f && Time.time >= sector.BlackoutAt) SetState(i, PowerState.Blackout);

                // ringing alarms draw the infected
                if (sector.AlarmUntil > 0.0f)
                {
                    if (Time.time >= sector.AlarmUntil)
                    {
                        StopAlarm(sector);
                    }
                    else if (Time.time >= sector.NextAlarmNoise)
                    {
                        sector.NextAlarmNoise = Time.time + 2.0f;
                        NoiseSystem.Emit(sector.Center, NoiseSystem.DefaultRadius(NoiseCategory.Alarm), NoiseCategory.Alarm, Factions.Environment, gameObject);
                    }
                }

                UpdateEmergencyLights(sector);
            }

            // the clock only runs during a game: the menu doesn't count towards the first failure
            if (!playing) _nextFailure = Mathf.Max(_nextFailure, Time.time + FirstFailureDelay);
            if (playing && RandomFailures && AllowFailures && Time.time >= _nextFailure && Sectors.Length > 0)
            {
                _nextFailure = Time.time + UnityEngine.Random.Range(FailureInterval.x, FailureInterval.y);
                // never more than half of the base down at once
                int down = 0;
                foreach (PowerSector s in Sectors) if (s.State != PowerState.Normal) down++;
                if (down < Mathf.CeilToInt(Sectors.Length * 0.5f)) TriggerFailure(UnityEngine.Random.Range(0, Sectors.Length));
            }

            UpdateAmbient();
        }

        private void UpdateEmergencyLights(PowerSector sector)
        {
            float level = sector.State == PowerState.Emergency ? 1.0f : sector.State == PowerState.Blackout ? BatteryLevel : 0.0f;
            // a slow rotating-beacon pulse
            float pulse = 0.55f + 0.45f * Mathf.Sin(Time.time * 4.0f);
            foreach (Light light in sector.EmergencyLights)
            {
                if (light == null) continue;
                light.enabled = level > 0.0f;
                // the generator beacon stays steady and bright: it's where the player has to go
                bool beacon = light.name == "GeneratorBeacon";
                light.intensity = beacon ? EmergencyIntensity * 0.8f * (0.75f + 0.25f * pulse)
                    : EmergencyIntensity * level * (sector.State == PowerState.Emergency ? pulse : 1.0f);
            }
        }

        private void UpdateAmbient()
        {
            bool dark = _player != null && StateAt(_player.position) == PowerState.Blackout;
            _ambientBlend = Mathf.MoveTowards(_ambientBlend, dark ? 1.0f : 0.0f, Time.deltaTime * 0.5f);
            float boost = Mathf.Lerp(1.0f, BlackoutAmbientBoost, _ambientBlend);
            RenderSettings.ambientSkyColor = _ambientSky * boost;
            RenderSettings.ambientEquatorColor = _ambientEquator * boost;
            RenderSettings.ambientGroundColor = _ambientGround * boost;
        }

        private void OnDrawGizmosSelected()
        {
            if (Sectors == null) return;
            foreach (PowerSector sector in Sectors)
            {
                Gizmos.color = new Color(1.0f, 0.8f, 0.2f, 0.4f);
                Gizmos.DrawWireCube(sector.Center, sector.Size);
            }
        }
    }
}
