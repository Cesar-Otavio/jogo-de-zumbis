using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace Combat
{
    // one infected as seen by everyone: what the clients need to draw it, nothing they could use to decide anything
    public struct EnemyState : INetworkSerializable
    {
        public ushort Id;
        public byte Prefab;
        // 255 = common zombie
        public byte Variant;
        public Vector3 Position;
        public float Yaw;
        public float Speed;
        // bit 0 dead, bits 1-7 attack counter (a change plays the attack)
        public byte Flags;
        public byte Health;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Id);
            serializer.SerializeValue(ref Prefab);
            serializer.SerializeValue(ref Variant);
            serializer.SerializeValue(ref Position);
            serializer.SerializeValue(ref Yaw);
            serializer.SerializeValue(ref Speed);
            serializer.SerializeValue(ref Flags);
            serializer.SerializeValue(ref Health);
        }
    }

    // the host's wave progress, for the clients' HUD (they run no waves of their own)
    public struct WaveState : INetworkSerializable, System.IEquatable<WaveState>
    {
        public ushort Wave;
        // bit 0 waves running, bit 1 in intermission, bit 2 waiting for the shop
        public byte Flags;
        public ushort Remaining;
        // whole seconds, so it only changes once a second
        public ushort IntermissionLeft;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Wave);
            serializer.SerializeValue(ref Flags);
            serializer.SerializeValue(ref Remaining);
            serializer.SerializeValue(ref IntermissionLeft);
        }

        public bool Equals(WaveState other) => Wave == other.Wave && Flags == other.Flags && Remaining == other.Remaining && IntermissionLeft == other.IntermissionLeft;
    }

    /* The shared side of a co-op match, spawned once by the host:
     *   lobby    - room name, mode, difficulty and who is ready (on each NetPlayer)
     *   start    - the host starts the match for everyone at once; nobody starts their own waves
     *   enemies  - the host's infected streamed ten times a second; clients show replicas (EnemyReplica)
     *   damage   - a client's hit on a replica is a request the host checks (range, rate, size) before applying
     *   waves    - the host's wave number, intermission and enemies left, mirrored on the clients' spawner
     */
    public class NetGame : NetworkBehaviour
    {
        public static NetGame Instance { get; private set; }

        public readonly NetworkVariable<FixedString64Bytes> Room = new NetworkVariable<FixedString64Bytes>();
        public readonly NetworkVariable<int> Mode = new NetworkVariable<int>((int)GameMode.Endless);
        public readonly NetworkVariable<int> DifficultyLevel = new NetworkVariable<int>((int)Difficulty.Normal);
        public readonly NetworkVariable<int> MaxPlayers = new NetworkVariable<int>(4);
        public readonly NetworkVariable<bool> MatchStarted = new NetworkVariable<bool>();
        public readonly NetworkVariable<WaveState> Waves = new NetworkVariable<WaveState>();

        public bool Started => IsSpawned && MatchStarted.Value;

        [Tooltip("Snapshots of the infected per second")]
        public float SendRate = 10.0f;

        // ---- host side
        private readonly Dictionary<Health, ushort> _ids = new Dictionary<Health, ushort>();
        private readonly Dictionary<ushort, Health> _byId = new Dictionary<ushort, Health>();
        private readonly List<EnemyState> _snapshot = new List<EnemyState>();
        private readonly Dictionary<ulong, (float windowStart, int hits)> _hitBudget = new Dictionary<ulong, (float, int)>();
        private ushort _nextId = 1;
        private float _nextSend;
        private EnemySpawner _spawner;
        private int _lastWave;
        private bool _gameOver;

        // ---- client side
        private readonly Dictionary<ushort, EnemyReplica> _replicas = new Dictionary<ushort, EnemyReplica>();
        private readonly Dictionary<ushort, float> _lastSeen = new Dictionary<ushort, float>();

        public static void SpawnFor(NetworkManager manager)
        {
            NetSessionPrefabs prefabs = manager.GetComponent<NetSessionPrefabs>();
            if (prefabs == null || prefabs.Game == null || Instance != null) return;
            manager.SpawnManager.InstantiateAndSpawn(prefabs.Game.GetComponent<NetworkObject>());
        }

        public override void OnNetworkSpawn()
        {
            Instance = this;
            _spawner = FindAnyObjectByType<EnemySpawner>();
            Waves.OnValueChanged += MirrorWaves;
            if (IsServer)
            {
                Room.Value = NetSession.Instance != null ? NetSession.Instance.RoomName : "Sala";
                MaxPlayers.Value = NetSession.Instance != null ? NetSession.Instance.MaxPlayers : 4;
                DifficultyLevel.Value = (int)GameDifficulty.Current;
            }
        }

        public override void OnNetworkDespawn()
        {
            Waves.OnValueChanged -= MirrorWaves;
            if (_spawner != null && !IsServer) _spawner.StopMirror();
            if (Instance == this) Instance = null;
            foreach (EnemyReplica replica in _replicas.Values) if (replica != null) Destroy(replica.gameObject);
            _replicas.Clear();
        }

        // ---------------------------------------------------------------- lobby

        public bool CanStart(out string reason)
        {
            reason = null;
            if (!IsServer) reason = "Só o Host inicia a partida";
            else if (Started) reason = "A partida já começou";
            else
            {
                foreach (NetPlayer player in NetPlayer.All)
                {
                    if (!player.IsOwnedByServer && !player.Ready.Value)
                    {
                        reason = $"{player.DisplayName} ainda não está pronto";
                        break;
                    }
                }
            }

            return reason == null;
        }

        public void StartMatch()
        {
            if (!CanStart(out string reason))
            {
                Debug.Log("[Net] " + reason);
                return;
            }

            // a spawn point per player, in the order they joined
            int index = 0;
            foreach (NetPlayer player in NetPlayer.All) player.SpawnIndex.Value = index++;
            MatchStarted.Value = true;
            StartMatchRpc(Mode.Value, DifficultyLevel.Value);
        }

        [Rpc(SendTo.ClientsAndHost)]
        private void StartMatchRpc(int mode, int difficulty)
        {
            GameDifficulty.Current = (Difficulty)difficulty;
            NetMatch.Begin((GameMode)mode);
        }

        // a line everyone sees in the notice area
        public void Announce(string text)
        {
            if (IsServer) AnnounceRpc(text);
        }

        [Rpc(SendTo.ClientsAndHost)]
        private void AnnounceRpc(FixedString128Bytes text)
        {
            NetMatch.Notice(text.ToString());
        }

        // ---------------------------------------------------------------- enemies: host

        private void Update()
        {
            if (!IsSpawned || !Started) return;
            if (IsServer)
            {
                StreamEnemies();
                PublishWaves();
                CheckTeamOut();
            }
            else ExpireReplicas();
        }

        // ---------------------------------------------------------------- waves

        private void PublishWaves()
        {
            if (_spawner == null) return;
            int flags = (_spawner.Running ? 1 : 0) | (_spawner.InIntermission ? 2 : 0) | (_spawner.WaitingForShop ? 4 : 0);
            var state = new WaveState
            {
                Wave = (ushort)Mathf.Clamp(_spawner.Wave, 0, ushort.MaxValue),
                Flags = (byte)flags,
                Remaining = (ushort)Mathf.Clamp(_spawner.RemainingInWave, 0, ushort.MaxValue),
                IntermissionLeft = (ushort)Mathf.CeilToInt(_spawner.IntermissionLeft)
            };
            // a network variable only goes out when it changes
            if (!state.Equals(Waves.Value)) Waves.Value = state;

            // a new wave brings back whoever died, and everyone's downs start over
            bool active = _spawner.WaveInProgress;
            if (active && _spawner.Wave > _lastWave)
            {
                _lastWave = _spawner.Wave;
                foreach (NetPlayer player in NetPlayer.All) player.ServerNewWave();
            }
        }

        // ---------------------------------------------------------------- the whole team out

        // nobody left standing means nobody can revive anyone: the run is over for everyone
        private void CheckTeamOut()
        {
            if (_gameOver || NetPlayer.All.Count == 0) return;
            foreach (NetPlayer player in NetPlayer.All)
                if (player.IsUp) return;
            _gameOver = true;
            TeamOutRpc();
        }

        [Rpc(SendTo.ClientsAndHost)]
        private void TeamOutRpc()
        {
            if (GameFlow.Instance != null) GameFlow.Instance.CoopGameOver();
        }

        private void MirrorWaves(WaveState before, WaveState now)
        {
            if (IsServer || _spawner == null) return;
            _spawner.Mirror(now.Wave, (now.Flags & 1) != 0, (now.Flags & 2) != 0, now.IntermissionLeft, now.Remaining, (now.Flags & 4) != 0);
        }

        private void StreamEnemies()
        {
            if (Time.unscaledTime < _nextSend) return;
            _nextSend = Time.unscaledTime + 1.0f / SendRate;

            _snapshot.Clear();
            foreach (Health health in Health.Living)
            {
                if (health == null || health.Team != Factions.Infected) continue;
                ZombieAI zombie = health.GetComponent<ZombieAI>();
                if (zombie == null) continue;
                if (!_ids.TryGetValue(health, out ushort id))
                {
                    id = _nextId++;
                    _ids[health] = id;
                    _byId[id] = health;
                }

                _snapshot.Add(new EnemyState
                {
                    Id = id,
                    Prefab = (byte)PrefabIndex(zombie),
                    Variant = (byte)VariantIndex(zombie.AppliedVariant),
                    Position = health.transform.position,
                    Yaw = health.transform.eulerAngles.y,
                    Speed = zombie.AnimationSpeed,
                    Flags = (byte)((health.IsDead ? 1 : 0) | ((zombie.AttackCount & 0x7F) << 1)),
                    Health = (byte)Mathf.CeilToInt(Mathf.Clamp01(health.Normalized) * 255.0f)
                });
            }

            // forget the ones that are gone (despawned corpses)
            var gone = new List<Health>();
            foreach (var pair in _ids) if (pair.Key == null) gone.Add(pair.Key);
            foreach (Health dead in gone)
            {
                _byId.Remove(_ids[dead]);
                _ids.Remove(dead);
            }

            // small chunks keep each message well under one packet
            const int chunk = 24;
            for (int start = 0; start < _snapshot.Count; start += chunk)
            {
                int count = Mathf.Min(chunk, _snapshot.Count - start);
                var states = new EnemyState[count];
                _snapshot.CopyTo(start, states, 0, count);
                EnemiesRpc(states);
            }
        }

        private int PrefabIndex(ZombieAI zombie)
        {
            if (_spawner == null || _spawner.ZombiePrefabs == null) return 0;
            string name = zombie.name.Replace("(Clone)", "").Trim();
            for (int i = 0; i < _spawner.ZombiePrefabs.Length; i++)
            {
                if (_spawner.ZombiePrefabs[i] != null && _spawner.ZombiePrefabs[i].name == name) return i;
            }

            return 0;
        }

        private int VariantIndex(ZombieVariant variant)
        {
            if (variant == null || _spawner == null || _spawner.ZombieVariants == null) return 255;
            for (int i = 0; i < _spawner.ZombieVariants.Length; i++)
            {
                if (_spawner.ZombieVariants[i].Variant == variant) return i;
            }

            return 255;
        }

        // ---------------------------------------------------------------- enemies: clients

        [Rpc(SendTo.NotServer, Delivery = RpcDelivery.Unreliable)]
        private void EnemiesRpc(EnemyState[] states)
        {
            foreach (EnemyState state in states)
            {
                _lastSeen[state.Id] = Time.unscaledTime;
                if (!_replicas.TryGetValue(state.Id, out EnemyReplica replica) || replica == null)
                {
                    // a corpse the client never saw alive isn't worth creating
                    if ((state.Flags & 1) != 0) continue;
                    replica = EnemyReplica.Create(state, ZombiePrefab(state.Prefab), ZombieVariantAt(state.Variant));
                    if (replica == null) continue;
                    _replicas[state.Id] = replica;
                }

                replica.Apply(state);
            }
        }

        private void ExpireReplicas()
        {
            var expired = new List<ushort>();
            foreach (var pair in _lastSeen)
            {
                // not in the stream for a while: despawned on the host
                if (Time.unscaledTime - pair.Value > 2.0f) expired.Add(pair.Key);
            }

            foreach (ushort id in expired)
            {
                _lastSeen.Remove(id);
                if (_replicas.TryGetValue(id, out EnemyReplica replica) && replica != null) Destroy(replica.gameObject);
                _replicas.Remove(id);
            }
        }

        private GameObject ZombiePrefab(byte index)
        {
            if (_spawner == null || _spawner.ZombiePrefabs == null || _spawner.ZombiePrefabs.Length == 0) return null;
            return _spawner.ZombiePrefabs[Mathf.Clamp(index, 0, _spawner.ZombiePrefabs.Length - 1)];
        }

        private ZombieVariant ZombieVariantAt(byte index)
        {
            if (index == 255 || _spawner == null || _spawner.ZombieVariants == null || index >= _spawner.ZombieVariants.Length) return null;
            return _spawner.ZombieVariants[index].Variant;
        }

        // ---------------------------------------------------------------- damage requests

        public void RequestDamage(ushort enemy, DamageInfo info)
        {
            DamageEnemyRpc(enemy, info.Amount, info.Headshot, info.Point, info.Direction, info.Force);
        }

        [Rpc(SendTo.Server)]
        private void DamageEnemyRpc(ushort enemy, float amount, bool headshot, Vector3 point, Vector3 direction, float force, RpcParams rpc = default)
        {
            ulong sender = rpc.Receive.SenderClientId;
            if (!_byId.TryGetValue(enemy, out Health target) || target == null || target.IsDead) return;
            NetPlayer shooter = NetPlayer.For(sender);
            if (shooter == null || shooter.Puppet == null || shooter.State.Value.Health <= 0) return;

            // checks against what a real shot can do: in range of the shooter, not too many, not too big
            float distance = Vector3.Distance(shooter.Puppet.transform.position, target.transform.position);
            if (distance > 320.0f) return;
            if (!SpendHit(sender)) return;
            amount = Mathf.Clamp(amount, 0.0f, headshot ? 400.0f : 200.0f);

            target.TakeDamage(new DamageInfo
            {
                Amount = amount,
                Headshot = headshot,
                Point = point,
                Direction = direction.sqrMagnitude > 0.0f ? direction.normalized : Vector3.forward,
                Force = Mathf.Clamp(force, 0.0f, 40.0f),
                Instigator = shooter.Puppet.gameObject
            });

            if (target.IsDead) KillConfirmedRpc(headshot, RpcTarget.Single(sender, RpcTargetUse.Temp));
        }

        // 40 hits a second covers a shotgun's pellets; more than that is not a gun
        private bool SpendHit(ulong sender)
        {
            float now = Time.unscaledTime;
            if (!_hitBudget.TryGetValue(sender, out var budget) || now - budget.windowStart > 1.0f) budget = (now, 0);
            budget.hits++;
            _hitBudget[sender] = budget;
            return budget.hits <= 40;
        }

        [Rpc(SendTo.SpecifiedInParams)]
        private void KillConfirmedRpc(bool headshot, RpcParams rpc)
        {
            NetMatch.KillConfirmed(headshot);
        }
    }
}
