using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using Random = UnityEngine.Random;

namespace Combat
{
    public enum HorrorEvent
    {
        DoorSlam,
        DistantFootsteps,
        ContainerThuds,
        BuildingMoans,
        RadioInterference,
        AlarmBurst,
        LampFlicker,
        FallingObject,
        Silhouette,
        CorridorMovement,
        SpotlightReveal,
        MetalScrape
    }

    /* Unsettling things happening around the player: slams, footsteps, moans in empty buildings,
     * radio static, flickering lamps, a figure glimpsed in the dark... Each event has a weight,
     * a cooldown and conditions (valid spots at the right distance, out of or in view as fits).
     * Most are only sounds; a few make a noise the infected may come to check, none spawns enemies.
     * The tension director scales how often they happen.
     */
    public class EnvironmentalEventDirector : MonoBehaviour
    {
        [Serializable]
        public class EventSettings
        {
            public HorrorEvent Kind;
            public float Weight = 1.0f;
            public float Cooldown = 60.0f;
            [Tooltip("Chance that the event also makes a noise the infected may investigate")]
            [Range(0, 1)] public float NoiseChance;
            [NonSerialized] public float ReadyAt;
        }

        public EventSettings[] Events =
        {
            new EventSettings { Kind = HorrorEvent.DoorSlam, Weight = 1.0f, Cooldown = 50.0f, NoiseChance = 0.4f },
            new EventSettings { Kind = HorrorEvent.DistantFootsteps, Weight = 1.2f, Cooldown = 45.0f },
            new EventSettings { Kind = HorrorEvent.ContainerThuds, Weight = 1.0f, Cooldown = 60.0f, NoiseChance = 0.3f },
            new EventSettings { Kind = HorrorEvent.BuildingMoans, Weight = 1.2f, Cooldown = 40.0f },
            new EventSettings { Kind = HorrorEvent.RadioInterference, Weight = 0.7f, Cooldown = 90.0f },
            new EventSettings { Kind = HorrorEvent.AlarmBurst, Weight = 0.4f, Cooldown = 150.0f, NoiseChance = 1.0f },
            new EventSettings { Kind = HorrorEvent.LampFlicker, Weight = 1.0f, Cooldown = 30.0f },
            new EventSettings { Kind = HorrorEvent.FallingObject, Weight = 0.9f, Cooldown = 50.0f, NoiseChance = 0.5f },
            new EventSettings { Kind = HorrorEvent.Silhouette, Weight = 0.5f, Cooldown = 120.0f },
            new EventSettings { Kind = HorrorEvent.CorridorMovement, Weight = 1.0f, Cooldown = 45.0f },
            new EventSettings { Kind = HorrorEvent.SpotlightReveal, Weight = 0.6f, Cooldown = 100.0f },
            new EventSettings { Kind = HorrorEvent.MetalScrape, Weight = 1.0f, Cooldown = 45.0f }
        };

        [Header("Pacing")]
        [Tooltip("Seconds between events, before the tension director's scaling")]
        public Vector2 Interval = new Vector2(16.0f, 38.0f);
        public float FirstEventDelay = 20.0f;
        [Tooltip("Set by the tension director: more than 1 means more frequent events")]
        public float RateScale = 1.0f;

        [Header("Distances")]
        public float MinDistance = 14.0f;
        public float MaxDistance = 45.0f;

        [Header("Audio")]
        public AudioClip[] DoorSlams;
        public AudioClip[] Footsteps;
        public AudioClip[] Thuds;
        public AudioClip[] Moans;
        public AudioClip[] RadioStatic;
        public AudioClip[] FallingObjects;
        public AudioClip[] Scrapes;
        public AudioClip[] Shuffles;
        public AudioClip SpotlightOn;

        [Header("Silhouette")]
        [Tooltip("A model used as the figure in the dark (its AI is stripped); the zombie prefab works")]
        public GameObject SilhouettePrefab;

        public HorrorEvent LastEvent { get; private set; }
        public float LastEventTime { get; private set; } = -100.0f;
        public event Action<HorrorEvent, Vector3> EventPlayed;

        private Transform _player;
        private Camera _camera;
        private float _nextEvent;
        private readonly List<Transform> _containers = new List<Transform>();
        private readonly List<Transform> _buildings = new List<Transform>();
        private GameObject _silhouette;
        private float _silhouetteUntil;
        private Light _spotlight;
        private float _spotlightStart;

        private void Start()
        {
            var player = FindAnyObjectByType<PlayerCombat>();
            if (player != null) _player = player.transform;
            _camera = Camera.main;
            _nextEvent = Time.time + FirstEventDelay;

            // valid spots: what the base is made of, found by name once
            foreach (Collider col in FindObjectsByType<Collider>())
            {
                if (col.isTrigger) continue;
                string name = col.name.ToLowerInvariant();
                if (name.Contains("container")) _containers.Add(col.transform);
                else if (name.Contains("hangar") || name.Contains("garage") || name.Contains("toilet") || name.Contains("tower") || name.Contains("shed")) _buildings.Add(col.transform);
            }
        }

        private void Update()
        {
            UpdateSilhouette();
            UpdateSpotlight();

            bool playing = GameFlow.Instance == null || GameFlow.Instance.Current == GameFlow.State.Playing;
            if (!playing || _player == null || Time.time < _nextEvent) return;

            var health = _player.GetComponent<Health>();
            if (health != null && health.IsDead) return;

            float scale = Mathf.Max(RateScale, 0.05f);
            _nextEvent = Time.time + Random.Range(Interval.x, Interval.y) / scale;
            TryPlayRandom();
        }

        // ---------------------------------------------------------------- choosing

        private void TryPlayRandom()
        {
            float total = 0.0f;
            foreach (EventSettings e in Events)
            {
                if (Time.time >= e.ReadyAt && e.Kind != LastEvent) total += e.Weight;
            }

            // a few tries, since an event can fail its conditions (no valid spot right now)
            for (int attempt = 0; attempt < 4 && total > 0.0f; attempt++)
            {
                float roll = Random.value * total;
                foreach (EventSettings e in Events)
                {
                    if (Time.time < e.ReadyAt || e.Kind == LastEvent) continue;
                    roll -= e.Weight;
                    if (roll > 0.0f) continue;

                    if (Play(e.Kind, out Vector3 where))
                    {
                        e.ReadyAt = Time.time + e.Cooldown;
                        LastEvent = e.Kind;
                        LastEventTime = Time.time;
                        if (Random.value < e.NoiseChance)
                        {
                            NoiseSystem.Emit(where, NoiseSystem.DefaultRadius(NoiseCategory.Ambient), NoiseCategory.Ambient, Factions.Environment, gameObject);
                        }

                        EventPlayed?.Invoke(e.Kind, where);
                        return;
                    }

                    break;
                }
            }
        }

        // plays one event now if its conditions are met; also usable by missions and debugging
        public bool Play(HorrorEvent kind, out Vector3 where)
        {
            where = _player != null ? _player.position : Vector3.zero;
            if (_player == null) return false;

            switch (kind)
            {
                case HorrorEvent.DoorSlam:
                    if (!PickDoorOrBuilding(out where)) return false;
                    return Sound(DoorSlams, where, 1.0f, "[porta metálica batendo]");

                case HorrorEvent.DistantFootsteps:
                    if (!PickGround(out where, 25.0f, MaxDistance, false)) return false;
                    return Sound(Footsteps, where, 0.7f, "[passos distantes]");

                case HorrorEvent.ContainerThuds:
                    if (!PickAnchor(_containers, out where, true)) return false;
                    where += Vector3.up * 2.8f;
                    return Sound(Thuds, where, 0.9f, "[algo se move sobre um contêiner]");

                case HorrorEvent.BuildingMoans:
                    if (!PickAnchor(_buildings, out where, false)) return false;
                    return Sound(Moans, where + Vector3.up * 1.5f, 0.8f, "[gemidos dentro do prédio]");

                case HorrorEvent.RadioInterference:
                    // the player's own radio: no position
                    if (RadioStatic == null || RadioStatic.Length == 0 || CombatEffects.Instance == null) return false;
                    CombatEffects.Instance.PlaySound(RadioStatic[Random.Range(0, RadioStatic.Length)], _player.position, 0.5f, 0.05f, 0.0f);
                    Subtitles.Show("[interferência no rádio]", 3.0f);
                    return true;

                case HorrorEvent.AlarmBurst:
                    PowerGridManager grid = PowerGridManager.Instance;
                    if (grid == null) return false;
                    int sector = grid.SectorAt(_player.position);
                    // the alarm of a neighbouring sector, not the one the player stands in
                    int other = grid.Sectors.Length > 1 ? (sector + 1 + Random.Range(0, grid.Sectors.Length - 1)) % grid.Sectors.Length : sector;
                    if (other < 0) return false;
                    grid.AlarmBurst(other, Random.Range(3.0f, 6.0f));
                    where = grid.Sectors[other].Center;
                    return true;

                case HorrorEvent.LampFlicker:
                    LightFlicker lamp = NearestLamp(8.0f, 30.0f);
                    if (lamp == null) return false;
                    lamp.ForceFlicker(Random.Range(2.0f, 4.0f));
                    where = lamp.transform.position;
                    return true;

                case HorrorEvent.FallingObject:
                    if (!PickGround(out where, MinDistance, 30.0f, false)) return false;
                    return Sound(FallingObjects, where, 0.9f, "[algo caiu]");

                case HorrorEvent.Silhouette:
                    return ShowSilhouette(out where);

                case HorrorEvent.CorridorMovement:
                    if (!PickGround(out where, 12.0f, 25.0f, true)) return false;
                    return Sound(Shuffles, where, 0.65f, "[movimento entre os contêineres]");

                case HorrorEvent.SpotlightReveal:
                    return RevealGroup(out where);

                case HorrorEvent.MetalScrape:
                    if (!PickAnchor(_containers, out where, false)) return false;
                    return Sound(Scrapes, where + Vector3.up, 0.8f, "[arranhões no metal]");
            }

            return false;
        }

        private bool Sound(AudioClip[] clips, Vector3 where, float volume, string caption)
        {
            if (clips == null || clips.Length == 0 || CombatEffects.Instance == null) return false;
            CombatEffects.Instance.PlaySound(clips[Random.Range(0, clips.Length)], where, volume, 0.1f, 1.0f, 6.0f, 90.0f);
            Subtitles.Show(caption, where, 3.0f);
            return true;
        }

        // ---------------------------------------------------------------- spots

        private bool InRange(Vector3 point, float min, float max)
        {
            float distance = Vector3.Distance(new Vector3(point.x, _player.position.y, point.z), _player.position);
            return distance >= min && distance <= max;
        }

        private bool InView(Vector3 point)
        {
            if (_camera == null) return false;
            Vector3 view = _camera.WorldToViewportPoint(point);
            return view.z > 0.0f && view.x > 0.0f && view.x < 1.0f && view.y > 0.0f && view.y < 1.0f;
        }

        private bool PickAnchor(List<Transform> anchors, out Vector3 where, bool outOfView)
        {
            where = Vector3.zero;
            for (int i = 0; i < 10 && anchors.Count > 0; i++)
            {
                Transform anchor = anchors[Random.Range(0, anchors.Count)];
                if (anchor == null || !InRange(anchor.position, MinDistance, MaxDistance)) continue;
                if (outOfView && InView(anchor.position)) continue;
                where = anchor.position;
                return true;
            }

            return false;
        }

        private bool PickDoorOrBuilding(out Vector3 where)
        {
            foreach (InteractiveDoor door in InteractiveDoor.All)
            {
                if (!door.IsBroken && InRange(door.transform.position, MinDistance, MaxDistance) && Random.value < 0.5f)
                {
                    where = door.InteractionPoint;
                    return true;
                }
            }

            return PickAnchor(_buildings, out where, false);
        }

        // a walkable spot at the given distance, preferably behind or beside the player
        private bool PickGround(out Vector3 where, float min, float max, bool dark)
        {
            where = Vector3.zero;
            Vector3 back = _camera != null ? -Vector3.ProjectOnPlane(_camera.transform.forward, Vector3.up).normalized : -_player.forward;
            for (int i = 0; i < 12; i++)
            {
                Vector3 direction = Quaternion.Euler(0.0f, Random.Range(-110.0f, 110.0f), 0.0f) * back;
                Vector3 candidate = _player.position + direction * Random.Range(min, max);
                if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit, 4.0f, NavMesh.AllAreas)) continue;
                if (dark && LightFlicker.IsLit(hit.position + Vector3.up)) continue;
                where = hit.position + Vector3.up * 0.3f;
                return true;
            }

            return false;
        }

        private LightFlicker NearestLamp(float min, float max)
        {
            LightFlicker best = null;
            float bestDistance = float.MaxValue;
            foreach (LightFlicker lamp in LightFlicker.All)
            {
                if (lamp.Dead || !lamp.IsOn) continue;
                float distance = Vector3.Distance(lamp.transform.position, _player.position);
                if (distance < min || distance > max || distance > bestDistance) continue;
                best = lamp;
                bestDistance = distance;
            }

            return best;
        }

        // ---------------------------------------------------------------- silhouette

        // a still figure in the dark, at the edge of the view; gone when looked at or approached
        private bool ShowSilhouette(out Vector3 where)
        {
            where = Vector3.zero;
            if (SilhouettePrefab == null || _camera == null || _silhouette != null) return false;

            Vector3 forward = Vector3.ProjectOnPlane(_camera.transform.forward, Vector3.up).normalized;
            for (int i = 0; i < 12; i++)
            {
                // off to the side of where the player looks, but on screen
                float side = Random.value < 0.5f ? -1.0f : 1.0f;
                Vector3 direction = Quaternion.Euler(0.0f, side * Random.Range(14.0f, 26.0f), 0.0f) * forward;
                Vector3 candidate = _player.position + direction * Random.Range(22.0f, 34.0f);
                if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit, 3.0f, NavMesh.AllAreas)) continue;
                if (LightFlicker.IsLit(hit.position + Vector3.up)) continue;
                if (!InView(hit.position + Vector3.up)) continue;
                // must be visible, not behind a wall
                if (Physics.Linecast(_camera.transform.position, hit.position + Vector3.up * 1.4f, CombatLayers.Sight & ~(1 << CombatLayers.Player), QueryTriggerInteraction.Ignore)) continue;

                where = hit.position;
                _silhouette = BuildFigure(hit.position, Quaternion.LookRotation(Vector3.ProjectOnPlane(_player.position - hit.position, Vector3.up)));
                _silhouetteUntil = Time.time + Random.Range(2.0f, 3.5f);
                return _silhouette != null;
            }

            return false;
        }

        private GameObject BuildFigure(Vector3 position, Quaternion rotation)
        {
            // cloned under an inactive parent so nothing of the enemy (health lists, AI) ever wakes up,
            // then stripped down to the animated body
            var holder = new GameObject("SilhouetteHolder");
            holder.SetActive(false);
            GameObject figure = Instantiate(SilhouettePrefab, position, rotation, holder.transform);
            figure.name = "Silhouette";

            // dependents first, so RequireComponent never blocks a removal
            StripAll<ScreamerBehaviour>(figure);
            StripAll<ZombieAI>(figure);
            StripAll<EnemyShooterAI>(figure);
            StripAll<EnemyPerception>(figure);
            foreach (MonoBehaviour behaviour in figure.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (!(behaviour is Health)) DestroyImmediate(behaviour);
            }

            StripAll<Health>(figure);
            StripAll<NavMeshAgent>(figure);
            foreach (Collider col in figure.GetComponentsInChildren<Collider>(true)) DestroyImmediate(col);
            foreach (Joint joint in figure.GetComponentsInChildren<Joint>(true)) DestroyImmediate(joint);
            foreach (Rigidbody body in figure.GetComponentsInChildren<Rigidbody>(true)) DestroyImmediate(body);

            // a dark shape, not a lit character
            var block = new MaterialPropertyBlock();
            foreach (Renderer body in figure.GetComponentsInChildren<Renderer>(true))
            {
                for (int i = 0; i < body.sharedMaterials.Length; i++)
                {
                    body.GetPropertyBlock(block, i);
                    block.SetColor("_BaseColor", new Color(0.04f, 0.04f, 0.05f));
                    block.SetColor("_Color", new Color(0.04f, 0.04f, 0.05f));
                    body.SetPropertyBlock(block, i);
                }
            }

            figure.transform.SetParent(null, true);
            Destroy(holder);
            return figure;
        }

        private static void StripAll<T>(GameObject root) where T : Component
        {
            foreach (T component in root.GetComponentsInChildren<T>(true)) DestroyImmediate(component);
        }

        private void UpdateSilhouette()
        {
            if (_silhouette == null) return;

            Vector3 toFigure = _silhouette.transform.position + Vector3.up * 1.4f - (_camera != null ? _camera.transform.position : _player.position);
            bool lookedAt = _camera != null && Vector3.Angle(_camera.transform.forward, toFigure) < 6.0f;
            bool close = _player != null && Vector3.Distance(_player.position, _silhouette.transform.position) < 14.0f;
            if (Time.time >= _silhouetteUntil || lookedAt || close)
            {
                Destroy(_silhouette);
                _silhouette = null;
            }
        }

        // ---------------------------------------------------------------- spotlight

        // a floodlight snaps on over a group of the infected the player hasn't noticed yet
        private bool RevealGroup(out Vector3 where)
        {
            where = Vector3.zero;
            if (_spotlight != null) return false;

            Vector3 sum = Vector3.zero;
            int count = 0;
            foreach (EnemyPerception enemy in EnemyPerception.All)
            {
                if (enemy == null || enemy.GetComponent<ZombieAI>() == null) continue;
                float distance = Vector3.Distance(enemy.transform.position, _player.position);
                if (distance < 15.0f || distance > 40.0f) continue;
                if (count > 0 && Vector3.Distance(enemy.transform.position, sum / count) > 10.0f) continue;
                sum += enemy.transform.position;
                count++;
            }

            if (count < 2) return false;

            where = sum / count;
            var go = new GameObject("RevealSpotlight");
            go.transform.position = where + Vector3.up * 12.0f + Vector3.ProjectOnPlane(_player.position - where, Vector3.up).normalized * 4.0f;
            go.transform.LookAt(where);
            _spotlight = go.AddComponent<Light>();
            _spotlight.type = LightType.Spot;
            _spotlight.spotAngle = 40.0f;
            _spotlight.innerSpotAngle = 20.0f;
            _spotlight.range = 30.0f;
            _spotlight.color = new Color(0.9f, 0.95f, 1.0f);
            _spotlight.intensity = 0.0f;
            _spotlight.shadows = LightShadows.Soft;
            _spotlightStart = Time.time;
            if (CombatEffects.Instance != null && SpotlightOn != null) CombatEffects.Instance.PlaySound(SpotlightOn, go.transform.position, 1.0f, 0.0f, 1.0f, 8.0f, 90.0f);
            Subtitles.Show("[refletor acende]", where, 2.5f);
            return true;
        }

        private void UpdateSpotlight()
        {
            if (_spotlight == null) return;
            float t = Time.time - _spotlightStart;
            // snaps on, buzzes, then dies
            float level = t < 0.15f ? Random.value : t < 3.5f ? 1.0f - Random.value * 0.15f : Mathf.Clamp01(1.0f - (t - 3.5f) * 2.0f);
            _spotlight.intensity = 250.0f * level * Mathf.Lerp(0.6f, 1.0f, GameSettings.Flashes);
            if (t > 4.2f)
            {
                Destroy(_spotlight.gameObject);
                _spotlight = null;
            }
        }
    }
}
