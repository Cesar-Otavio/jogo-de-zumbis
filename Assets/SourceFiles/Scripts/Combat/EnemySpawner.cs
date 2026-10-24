using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace Combat
{
    /* Builds the navigation mesh for the level when the game starts and spawns enemies away
     * from the player's view: either in waves that grow each round, or keeping a fixed number alive.
     */
    public class EnemySpawner : MonoBehaviour
    {
        [Header("Waves")]
        [Tooltip("Spawn in waves (started by BeginWaves) instead of keeping a fixed number alive")]
        public bool UseWaves;
        public float IntermissionTime = 45.0f;
        [Tooltip("Campaign: waves are launched by missions, with no automatic breaks")]
        public bool ManualWaves;
        [Tooltip("Credits per second left in the break when the next wave is called early")]
        public float EarlyStartBonus = 0.5f;
        [Tooltip("Seconds between spawns inside a wave")]
        public float WaveSpawnInterval = 1.2f;

        public int Wave { get; private set; }
        public bool InIntermission { get; private set; }
        public float IntermissionLeft => Mathf.Max(0.0f, _intermissionEnds - Time.time);
        public int RemainingInWave => _soldiersToSpawn + _zombiesToSpawn + _soldiers.Count + _zombies.Count + _bosses.Count;
        public bool WaveInProgress => _wavesRunning && !InIntermission && _waveActive;
        // the break is over but the wave waits for the shop to close
        public bool WaitingForShop { get; private set; }
        public bool Running => UseWaves && _wavesRunning;

        public event System.Action<int> WaveStarted;
        public event System.Action<int> WaveCleared;

        [Header("Enemies")]
        [Tooltip("One is picked at random for each spawn")]
        public GameObject[] SoldierPrefabs;
        public int MaxSoldiers = 2;
        public GameObject[] ZombiePrefabs;
        public int MaxZombies = 4;
        public float RespawnDelay = 6.0f;
        public float FirstSpawnDelay = 6.0f;

        [System.Serializable]
        public class VariantSpawn
        {
            public ZombieVariant Variant;
            public float Weight = 1.0f;
            [Tooltip("First wave it can appear in")]
            public int MinWave = 1;
        }

        [Header("Special infected")]
        public VariantSpawn[] ZombieVariants;
        [Tooltip("Chance that a zombie is a special one: base plus a bit more every wave")]
        public float SpecialChance = 0.08f;
        public float SpecialChancePerWave = 0.05f;
        public float MaxSpecialChance = 0.5f;

        // set by the tension director: slower spawning during a breather, fewer specials, etc.
        [System.NonSerialized] public float SpawnIntervalScale = 1.0f;
        [System.NonSerialized] public float SpecialChanceScale = 1.0f;

        [Header("Bosses")]
        public BossDefinition MidBoss;
        public BossDefinition FinalBoss;
        [Tooltip("Endless mode: a boss every this many waves (0 = never)")]
        public int BossEvery = 5;

        [Header("Stuck enemies")]
        [Tooltip("An enemy that hasn't come near the player nor been seen for this long is quietly removed")]
        public float StuckTimeout = 80.0f;

        public event System.Action<GameObject> BossSpawned;

        [Header("Spawning")]
        [Tooltip("Spawn points, the children of this object are used when empty")]
        public Transform[] SpawnPoints;
        public float MinDistanceFromPlayer = 10.0f;

        [Tooltip("Big maps: spawn at a random spot on the navigation mesh around the player, out of view")]
        public bool SpawnAroundPlayer;
        public float MaxDistanceFromPlayer = 45.0f;

        [Header("Navigation")]
        [Tooltip("Area around this object that gets a navigation mesh")]
        public Vector3 NavMeshSize = new Vector3(40.0f, 20.0f, 40.0f);
        public LayerMask WalkableLayers = 1;

        private readonly List<GameObject> _soldiers = new List<GameObject>();
        private readonly List<GameObject> _zombies = new List<GameObject>();
        private float _nextSoldier;
        private float _nextZombie;
        private Transform _player;
        // a walkable point in the open middle of the base that every valid spawn must reach
        private bool _hubSampled;
        private bool _hasHub;
        private Vector3 _hub;
        private NavMeshPath _connectionPath;
        private NavMeshDataInstance _navMesh;

        private DifficultySettings _difficulty = GameDifficulty.Get(Difficulty.Normal);
        private bool _wavesRunning;
        private float _intermissionEnds;
        private int _soldiersToSpawn;
        private int _zombiesToSpawn;
        private int _maxAlive;
        private float _waveHealth = 1.0f;
        private float _nextWaveSpawn;
        private bool _waveActive;
        private float _waveStartedAt;
        private readonly List<GameObject> _bosses = new List<GameObject>();
        private readonly List<GameObject> _extras = new List<GameObject>();
        private readonly Dictionary<GameObject, float> _lastNear = new Dictionary<GameObject, float>();
        private readonly List<(Vector3 position, float radius, float until)> _blockers = new List<(Vector3, float, float)>();
        private float _nextStuckCheck;

        private void Awake()
        {
            BuildNavMesh();
            if (SpawnPoints == null || SpawnPoints.Length == 0)
            {
                SpawnPoints = new Transform[transform.childCount];
                for (int i = 0; i < transform.childCount; i++) SpawnPoints[i] = transform.GetChild(i);
            }
        }

        private void Start()
        {
            var player = FindAnyObjectByType<PlayerCombat>();
            if (player != null) _player = player.transform;
            _nextSoldier = Time.time + FirstSpawnDelay;
            _nextZombie = Time.time + FirstSpawnDelay + 1.0f;
        }

        private void OnDestroy()
        {
            _navMesh.Remove();
        }

        private void Update()
        {
            if (UseWaves)
            {
                UpdateWaves();
                return;
            }

            Maintain(_soldiers, SoldierPrefabs, MaxSoldiers, ref _nextSoldier);
            Maintain(_zombies, ZombiePrefabs, MaxZombies, ref _nextZombie);
        }

        // ---------------------------------------------------------------- control from missions

        // the next wave now, with a few credits for every second of break given up
        public void SkipIntermission()
        {
            if (!InIntermission || ManualWaves) return;
            int bonus = Mathf.RoundToInt(IntermissionLeft * EarlyStartBonus);
            if (bonus > 0) RunState.AddCredits(bonus, "onda antecipada");
            _intermissionEnds = Time.time;
        }

        // campaign: a wave of a given size, cleared like any other
        public void LaunchWave(int wave, int zombies, int soldiers, float health)
        {
            UseWaves = true;
            ManualWaves = true;
            _wavesRunning = true;
            InIntermission = false;
            WaitingForShop = false;
            Wave = wave;
            _zombiesToSpawn = zombies;
            _soldiersToSpawn = soldiers;
            _maxAlive = Mathf.Min(14, 5 + wave);
            _waveHealth = _difficulty.EnemyHealth * health;
            _nextWaveSpawn = Time.time;
            _waveActive = true;
            _waveStartedAt = Time.time;
            WaveStarted?.Invoke(Wave);
        }

        // closed gates: nothing comes in from around there for a while (0 = for good)
        public void BlockSpawns(Vector3 position, float radius, float duration)
        {
            _blockers.Add((position, radius, duration > 0.0f ? Time.time + duration : float.MaxValue));
        }

        // a few extra enemies come for a place (a noisy machine, a defence); not part of the wave count
        public void SpawnPressure(int count, Vector3 near, ZombieVariant variant = null)
        {
            if (ZombiePrefabs == null || ZombiePrefabs.Length == 0) return;
            for (int i = 0; i < count; i++)
            {
                if (!TryPickAround(near, 22.0f, 38.0f, out Vector3 position)) continue;
                GameObject enemy = Instantiate(ZombiePrefabs[Random.Range(0, ZombiePrefabs.Length)], position, Quaternion.Euler(0.0f, Random.Range(0.0f, 360.0f), 0.0f));
                if (variant != null) enemy.GetComponent<ZombieAI>()?.ApplyVariant(variant);
                ApplyDifficulty(enemy);
                var perception = enemy.GetComponent<EnemyPerception>();
                if (perception != null) perception.Investigate(near, true, 4.0f);
                _extras.Add(enemy);
            }
        }

        public GameObject SpawnBoss(BossDefinition definition, Vector3 near)
        {
            if (definition == null || ZombiePrefabs == null || ZombiePrefabs.Length == 0) return null;
            if (!TryPickAround(near, 25.0f, 40.0f, out Vector3 position)) position = near + Vector3.forward * 25.0f;
            GameObject boss = BossAI.Create(definition, ZombiePrefabs[0], position, _difficulty);
            _bosses.Add(boss);
            BossSpawned?.Invoke(boss);
            return boss;
        }

        private bool SpawnBlocked(Vector3 point)
        {
            foreach (var (position, radius, until) in _blockers)
            {
                if (Time.time < until && (point - position).sqrMagnitude < radius * radius) return true;
            }

            return false;
        }

        // enemies stuck somewhere unreachable would make a wave endless: they are removed unseen
        private void CleanUpStuck()
        {
            if (Time.time < _nextStuckCheck || _player == null) return;
            _nextStuckCheck = Time.time + 2.0f;
            Camera cam = Camera.main;
            foreach (List<GameObject> list in new[] { _soldiers, _zombies, _extras })
            {
                for (int i = list.Count - 1; i >= 0; i--)
                {
                    GameObject enemy = list[i];
                    if (enemy == null) continue;
                    bool near = (enemy.transform.position - _player.position).sqrMagnitude < 35.0f * 35.0f;
                    bool seen = false;
                    if (cam != null)
                    {
                        Vector3 view = cam.WorldToViewportPoint(enemy.transform.position + Vector3.up);
                        seen = view.z > 0.0f && view.z < 60.0f && view.x > 0.0f && view.x < 1.0f && view.y > 0.0f && view.y < 1.0f;
                    }

                    if (near || seen || !_lastNear.ContainsKey(enemy)) _lastNear[enemy] = Time.time;
                    if (Time.time - _lastNear[enemy] > StuckTimeout)
                    {
                        _lastNear.Remove(enemy);
                        list.RemoveAt(i);
                        Destroy(enemy);
                    }
                }
            }
        }

        private void Maintain(List<GameObject> alive, GameObject[] prefabs, int max, ref float nextSpawn)
        {
            if (prefabs == null || prefabs.Length == 0) return;

            // a dead enemy stops counting as soon as it dies, its body lingers on its own
            int before = alive.Count;
            RemoveDead(alive);
            if (alive.Count < before) nextSpawn = Mathf.Max(nextSpawn, Time.time + RespawnDelay);

            if (alive.Count >= max || Time.time < nextSpawn) return;

            GameObject enemy = Spawn(prefabs);
            if (enemy != null) alive.Add(enemy);
            // stagger spawns so they don't all appear at once
            nextSpawn = Time.time + 1.5f;
        }

        private static void RemoveDead(List<GameObject> alive)
        {
            alive.RemoveAll(e => e == null || e.GetComponent<Health>() == null || e.GetComponent<Health>().IsDead);
        }

        // ---------------------------------------------------------------- waves

        public void BeginWaves(DifficultySettings difficulty, float firstDelay = 5.0f)
        {
            _difficulty = difficulty;
            ManualWaves = false;
            UseWaves = true;
            _wavesRunning = true;
            Wave = 0;
            _soldiersToSpawn = 0;
            _zombiesToSpawn = 0;
            StartIntermission(firstDelay);
        }

        // campaign: difficulty without automatic waves
        public void Prepare(DifficultySettings difficulty)
        {
            _difficulty = difficulty;
            UseWaves = true;
            ManualWaves = true;
            _wavesRunning = true;
            InIntermission = false;
            _waveActive = false;
            Wave = 0;
            _zombiesToSpawn = 0;
            _soldiersToSpawn = 0;
        }

        public void StopWaves()
        {
            _wavesRunning = false;
            _waveActive = false;
            _soldiersToSpawn = 0;
            _zombiesToSpawn = 0;
        }

        private void StartIntermission(float duration)
        {
            InIntermission = true;
            _waveActive = false;
            _intermissionEnds = Time.time + duration;
        }

        private void StartWave()
        {
            Wave++;
            InIntermission = false;

            // zombies from the start, soldiers join from wave 2 and keep growing
            float count = _difficulty.EnemyCount;
            _zombiesToSpawn = Mathf.Max(1, Mathf.RoundToInt((3 + 2 * Wave) * count));
            _soldiersToSpawn = Mathf.RoundToInt(Mathf.Max(0, Wave - 1 + Wave / 3) * count);
            _maxAlive = Mathf.Min(14, 5 + Wave);
            _waveHealth = _difficulty.EnemyHealth * (1.0f + 0.08f * (Wave - 1));
            _nextWaveSpawn = Time.time;
            _waveActive = true;
            _waveStartedAt = Time.time;
            WaveStarted?.Invoke(Wave);

            // endless mode: a boss every few waves, the big one every second time
            if (BossEvery > 0 && Wave % BossEvery == 0 && _player != null)
            {
                bool final = Wave % (BossEvery * 2) == 0 && FinalBoss != null;
                SpawnBoss(final ? FinalBoss : MidBoss, _player.position);
            }
        }

        private void UpdateWaves()
        {
            RemoveDead(_soldiers);
            RemoveDead(_zombies);
            RemoveDead(_bosses);
            RemoveDead(_extras);
            CleanUpStuck();
            if (!_wavesRunning) return;

            if (InIntermission)
            {
                // never start a wave on someone stuck in the shop
                WaitingForShop = Time.time >= _intermissionEnds && ShopManager.IsOpen;
                if (Time.time >= _intermissionEnds && !WaitingForShop) StartWave();
                return;
            }

            if (!_waveActive) return;

            int alive = _soldiers.Count + _zombies.Count;
            if (_soldiersToSpawn + _zombiesToSpawn > 0 && alive < _maxAlive && Time.time >= _nextWaveSpawn)
            {
                // pick the kind still to come, at random, weighted by how many are left
                bool soldier = Random.Range(0, _soldiersToSpawn + _zombiesToSpawn) < _soldiersToSpawn;
                GameObject enemy = Spawn(soldier ? SoldierPrefabs : ZombiePrefabs);
                if (enemy != null)
                {
                    if (!soldier) ApplySpecial(enemy);
                    ApplyDifficulty(enemy);
                    if (soldier)
                    {
                        _soldiersToSpawn--;
                        _soldiers.Add(enemy);
                    }
                    else
                    {
                        _zombiesToSpawn--;
                        _zombies.Add(enemy);
                    }
                }

                _nextWaveSpawn = Time.time + WaveSpawnInterval * Mathf.Max(SpawnIntervalScale, 0.1f);
            }

            if (RemainingInWave == 0)
            {
                _waveActive = false;
                WaveCleared?.Invoke(Wave);
                if (!ManualWaves) StartIntermission(IntermissionTime);
            }
        }

        // some zombies become runners, tanks, screamers or crawlers, more often in later waves
        private void ApplySpecial(GameObject enemy)
        {
            if (ZombieVariants == null || ZombieVariants.Length == 0) return;
            var zombie = enemy.GetComponent<ZombieAI>();
            if (zombie == null || zombie.AppliedVariant != null) return;

            float chance = Mathf.Min(MaxSpecialChance, SpecialChance + SpecialChancePerWave * (Wave - 1)) * SpecialChanceScale * _difficulty.SpecialChance;
            if (Random.value >= chance) return;

            float total = 0.0f;
            foreach (VariantSpawn option in ZombieVariants)
            {
                if (option.Variant != null && Wave >= option.MinWave) total += option.Weight;
            }

            float roll = Random.value * total;
            foreach (VariantSpawn option in ZombieVariants)
            {
                if (option.Variant == null || Wave < option.MinWave) continue;
                roll -= option.Weight;
                if (roll > 0.0f) continue;
                zombie.ApplyVariant(option.Variant);
                return;
            }
        }

        private void ApplyDifficulty(GameObject enemy)
        {
            var health = enemy.GetComponent<Health>();
            if (health != null) health.SetMaxHealth(health.MaxHealth * _waveHealth);

            var shooter = enemy.GetComponent<EnemyShooterAI>();
            if (shooter != null)
            {
                shooter.DamageMultiplier *= _difficulty.EnemyDamage;
                shooter.AimErrorStart *= _difficulty.EnemyAimError;
                shooter.AimErrorEnd *= _difficulty.EnemyAimError;
                shooter.ReactionTime *= _difficulty.EnemyReaction;
            }

            var perception = enemy.GetComponent<EnemyPerception>();
            if (perception != null)
            {
                perception.SightRange *= _difficulty.Perception;
                perception.HearingMultiplier *= _difficulty.Perception;
            }

            var zombie = enemy.GetComponent<ZombieAI>();
            if (zombie != null)
            {
                zombie.AttackDamage *= _difficulty.EnemyDamage;
                // later waves shamble a little faster
                zombie.ChaseSpeed *= Mathf.Min(1.4f, 1.0f + 0.03f * (Wave - 1));
            }
        }

        private GameObject Spawn(GameObject[] prefabs)
        {
            if (prefabs == null || prefabs.Length == 0) return null;

            Vector3 position;
            if (!SpawnAroundPlayer || !TryPickAroundPlayer(out position))
            {
                Transform point = PickSpawnPoint();
                if (point == null) return null;
                position = point.position;
            }

            if (NavMesh.SamplePosition(position, out NavMeshHit hit, 3.0f, NavMesh.AllAreas)) position = hit.position;
            // a random heading, so new enemies patrol a little before they notice the player
            Quaternion facing = Quaternion.Euler(0.0f, Random.Range(0.0f, 360.0f), 0.0f);
            return Instantiate(prefabs[Random.Range(0, prefabs.Length)], position, facing);
        }

        private bool TryPickAroundPlayer(out Vector3 position)
        {
            position = Vector3.zero;
            if (_player == null) return false;
            return TryPickAround(_player.position, MinDistanceFromPlayer, MaxDistanceFromPlayer, out position);
        }

        // a walkable spot at a distance from a point, out of the player's view and not behind a closed gate
        private bool TryPickAround(Vector3 center, float minDistance, float maxDistance, out Vector3 position)
        {
            position = Vector3.zero;

            Camera cam = Camera.main;
            var bounds = new Bounds(transform.position, NavMeshSize);
            for (int i = 0; i < 20; i++)
            {
                Vector2 dir = Random.insideUnitCircle.normalized;
                float distance = Random.Range(minDistance, maxDistance);
                Vector3 candidate = center + new Vector3(dir.x, 0.0f, dir.y) * distance;
                if (!bounds.Contains(candidate)) continue;
                if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit, 4.0f, NavMesh.AllAreas)) continue;
                if (SpawnBlocked(hit.position)) continue;
                // not on a scrap of walkable ground cut off from the base (behind the perimeter, on a roof, in a sealed room)
                if (!ConnectedToBase(hit.position)) continue;
                // never right next to the player, whatever the center
                if (_player != null && (hit.position - _player.position).sqrMagnitude < MinDistanceFromPlayer * MinDistanceFromPlayer * 0.5f) continue;

                // never pop into view: off screen, or behind something solid
                if (cam != null)
                {
                    Vector3 view = cam.WorldToViewportPoint(hit.position + Vector3.up);
                    bool onScreen = view.z > 0.0f && view.x > -0.1f && view.x < 1.1f && view.y > -0.1f && view.y < 1.1f;
                    if (onScreen && !Physics.Linecast(cam.transform.position, hit.position + Vector3.up, CombatLayers.Sight, QueryTriggerInteraction.Ignore))
                    {
                        continue;
                    }
                }

                position = hit.position;
                return true;
            }

            return false;
        }

        private bool ConnectedToBase(Vector3 point)
        {
            if (!_hubSampled)
            {
                _hubSampled = true;
                _hasHub = NavMesh.SamplePosition(transform.position, out NavMeshHit hub, 12.0f, NavMesh.AllAreas);
                _hub = hub.position;
            }

            if (!_hasHub) return true;
            _connectionPath ??= new NavMeshPath();
            return NavMesh.CalculatePath(point, _hub, NavMesh.AllAreas, _connectionPath) && _connectionPath.status == NavMeshPathStatus.PathComplete;
        }

        private Transform PickSpawnPoint()
        {
            Transform best = null;
            float bestScore = float.MinValue;
            Camera cam = Camera.main;
            foreach (Transform point in SpawnPoints)
            {
                if (point == null) continue;

                float distance = _player != null ? Vector3.Distance(point.position, _player.position) : 100.0f;
                if (distance < MinDistanceFromPlayer) continue;

                // prefer points at a fighting distance that the player isn't looking at, with some randomness
                float score = -Mathf.Abs(distance - (MinDistanceFromPlayer + MaxDistanceFromPlayer) * 0.5f) * 0.2f + Random.value * 4.0f;
                if (cam != null)
                {
                    Vector3 view = cam.WorldToViewportPoint(point.position + Vector3.up);
                    bool onScreen = view.z > 0.0f && view.x > 0.0f && view.x < 1.0f && view.y > 0.0f && view.y < 1.0f;
                    if (onScreen) score -= 10.0f;
                }

                if (score > bestScore)
                {
                    bestScore = score;
                    best = point;
                }
            }

            return best;
        }

        private void BuildNavMesh()
        {
            var bounds = new Bounds(transform.position, NavMeshSize);
            var sources = new List<NavMeshBuildSource>();
            NavMeshBuilder.CollectSources(bounds, WalkableLayers, NavMeshCollectGeometry.PhysicsColliders, 0,
                new List<NavMeshBuildMarkup>(), sources);

            // skip trigger volumes such as collectibles
            sources.RemoveAll(s => s.component is Collider col && col.isTrigger);
            // doors open and close: they carve the mesh at runtime instead of being baked in
            sources.RemoveAll(s => s.component is Component c && c != null && c.GetComponentInParent<InteractiveDoor>() != null);

            NavMeshBuildSettings settings = NavMesh.GetSettingsByID(0);
            settings.agentRadius = 0.35f;
            settings.agentHeight = 1.8f;
            settings.agentClimb = 0.35f;
            settings.agentSlope = 45.0f;

            NavMeshData data = NavMeshBuilder.BuildNavMeshData(settings, sources, bounds, Vector3.zero, Quaternion.identity);
            if (data != null) _navMesh = NavMesh.AddNavMeshData(data);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.3f, 0.7f, 1.0f, 0.3f);
            Gizmos.DrawWireCube(transform.position, NavMeshSize);
        }
    }
}
