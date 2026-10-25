using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace Combat
{
    public static class EnemyKind
    {
        public const byte Zombie = 0;
        public const byte Soldier = 1;
        public const byte Boss = 2;
    }

    // one enemy as seen by everyone: what the clients need to draw it, nothing they could use to decide anything
    public struct EnemyState : INetworkSerializable
    {
        public ushort Id;
        public byte Kind;
        // zombie / soldier prefab in the spawner's lists, or the boss in the boss list
        public byte Prefab;
        // zombie: variant (255 = common); soldier: gun in its loadout; boss: its current move
        public byte Variant;
        public Vector3 Position;
        public float Yaw;
        public float Speed;
        // bit 0 dead; zombie: bits 1-7 attack counter (a change plays the attack);
        // soldier: bit 1 aiming, bit 2 gun lowered; boss: bit 1 vulnerable, bit 2 lit, bits 3-7 phase
        public byte Flags;
        public byte Health;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Id);
            serializer.SerializeValue(ref Kind);
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
     *   enemies  - the host's infected, soldiers and bosses streamed ten times a second; clients show
     *              replicas (EnemyReplica); what they do once (a scream, a shot, a boss's warning) comes as an event
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
        private readonly Dictionary<Health, (Animator animator, bool hasSpeed)> _animators = new Dictionary<Health, (Animator, bool)>();
        private List<BossDefinition> _bosses;

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
                if (_spawner != null) _spawner.WaveCleared += OnWaveClearedOnHost;
                ScreamerBehaviour.ChargeStarted += OnScreamCharge;
                ScreamerBehaviour.Screamed += OnScream;
                BossAI.Warning += OnBossWarning;
                BossAI.Defeated += OnBossDefeated;
                Room.Value = NetSession.Instance != null ? NetSession.Instance.RoomName : "Sala";
                MaxPlayers.Value = NetSession.Instance != null ? NetSession.Instance.MaxPlayers : 4;
                DifficultyLevel.Value = (int)GameDifficulty.Current;
            }
        }

        public override void OnNetworkDespawn()
        {
            Waves.OnValueChanged -= MirrorWaves;
            if (_spawner != null) _spawner.WaveCleared -= OnWaveClearedOnHost;
            ScreamerBehaviour.ChargeStarted -= OnScreamCharge;
            ScreamerBehaviour.Screamed -= OnScream;
            BossAI.Warning -= OnBossWarning;
            BossAI.Defeated -= OnBossDefeated;
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
                if (health == null || health.Team == Factions.Player) continue;
                if (!Describe(health, out EnemyState state)) continue;
                state.Id = IdOf(health);
                _snapshot.Add(state);
            }

            // forget the ones that are gone (despawned corpses)
            var gone = new List<Health>();
            foreach (var pair in _ids) if (pair.Key == null) gone.Add(pair.Key);
            foreach (Health dead in gone)
            {
                _byId.Remove(_ids[dead]);
                _ids.Remove(dead);
                _animators.Remove(dead);
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

        // what the clients need of one enemy; false for anything they can't draw
        private bool Describe(Health health, out EnemyState state)
        {
            state = new EnemyState
            {
                Position = health.transform.position,
                Yaw = health.transform.eulerAngles.y,
                Health = (byte)Mathf.CeilToInt(Mathf.Clamp01(health.Normalized) * 255.0f)
            };
            int dead = health.IsDead ? 1 : 0;

            // a boss is built on an infected body (its ZombieAI switched off), so it's checked first
            BossAI boss = health.GetComponent<BossAI>();
            if (boss != null)
            {
                int index = Bosses.IndexOf(boss.Definition);
                if (index < 0) return false;
                state.Kind = EnemyKind.Boss;
                state.Prefab = (byte)index;
                state.Variant = (byte)boss.State;
                state.Speed = AnimatorSpeed(health);
                state.Flags = (byte)(dead | (boss.Vulnerable ? 2 : 0) | (boss.Lit ? 4 : 0) | ((Mathf.Min(boss.Phase, 31)) << 3));
                return true;
            }

            ZombieAI zombie = health.GetComponent<ZombieAI>();
            if (zombie != null)
            {
                state.Kind = EnemyKind.Zombie;
                state.Prefab = (byte)PrefabIndex(_spawner != null ? _spawner.ZombiePrefabs : null, zombie.name);
                state.Variant = (byte)VariantIndex(zombie.AppliedVariant);
                state.Speed = zombie.AnimationSpeed;
                state.Flags = (byte)(dead | ((zombie.AttackCount & 0x7F) << 1));
                return true;
            }

            EnemyShooterAI soldier = health.GetComponent<EnemyShooterAI>();
            if (soldier != null)
            {
                WeaponHolder holder = soldier.GetComponent<WeaponHolder>();
                state.Kind = EnemyKind.Soldier;
                state.Prefab = (byte)PrefabIndex(_spawner != null ? _spawner.SoldierPrefabs : null, soldier.name);
                state.Variant = (byte)LoadoutIndex(soldier, holder);
                state.Speed = AnimatorSpeed(health);
                state.Flags = (byte)(dead | (holder != null && holder.Aiming ? 2 : 0) | (holder != null && holder.Lowered ? 4 : 0));
                return true;
            }

            return false;
        }

        private ushort IdOf(Health health)
        {
            if (_ids.TryGetValue(health, out ushort id)) return id;
            id = _nextId++;
            _ids[health] = id;
            _byId[id] = health;

            // a soldier's shots are drawn on every client
            WeaponHolder holder = health.GetComponent<EnemyShooterAI>() != null ? health.GetComponent<WeaponHolder>() : null;
            if (holder != null)
            {
                ushort shooter = id;
                holder.Fired += _ =>
                {
                    if (IsSpawned) EnemyShotRpc(shooter, holder.LastShotFrom, holder.LastShotTo);
                };
            }

            return id;
        }

        private float AnimatorSpeed(Health health)
        {
            if (!_animators.TryGetValue(health, out var entry))
            {
                Animator animator = health.GetComponentInChildren<Animator>();
                bool has = false;
                if (animator != null)
                    foreach (AnimatorControllerParameter parameter in animator.parameters)
                        if (parameter.name == "Speed" && parameter.type == AnimatorControllerParameterType.Float) has = true;
                entry = (animator, has);
                _animators[health] = entry;
            }

            return entry.animator != null && entry.hasSpeed ? entry.animator.GetFloat("Speed") : 0.0f;
        }

        private static int PrefabIndex(GameObject[] prefabs, string instanceName)
        {
            if (prefabs == null) return 0;
            string name = instanceName.Replace("(Clone)", "").Trim();
            for (int i = 0; i < prefabs.Length; i++)
            {
                if (prefabs[i] != null && prefabs[i].name == name) return i;
            }

            return 0;
        }

        private static int LoadoutIndex(EnemyShooterAI soldier, WeaponHolder holder)
        {
            if (holder == null || holder.Current == null || soldier.Loadout == null) return 255;
            return Mathf.Max(0, System.Array.IndexOf(soldier.Loadout, holder.Current.Data));
        }

        // every boss either side can field, in the same order on every machine
        public List<BossDefinition> Bosses
        {
            get
            {
                if (_bosses != null) return _bosses;
                _bosses = new List<BossDefinition>();
                if (_spawner != null)
                {
                    if (_spawner.MidBoss != null) _bosses.Add(_spawner.MidBoss);
                    if (_spawner.FinalBoss != null && !_bosses.Contains(_spawner.FinalBoss)) _bosses.Add(_spawner.FinalBoss);
                }

                CampaignManager campaign = CampaignManager.Instance;
                if (campaign != null && campaign.Campaign != null && campaign.Campaign.Chapters != null)
                    foreach (var chapter in campaign.Campaign.Chapters)
                        if (chapter.Boss != null && !_bosses.Contains(chapter.Boss)) _bosses.Add(chapter.Boss);
                return _bosses;
            }
        }

        // ---------------------------------------------------------------- one-off enemy events (host -> clients)

        public const byte EventScreamCharge = 1;
        public const byte EventScream = 2;

        private void OnScreamCharge(ScreamerBehaviour screamer) => SendEnemyEvent(screamer, EventScreamCharge);
        private void OnScream(ScreamerBehaviour screamer) => SendEnemyEvent(screamer, EventScream);

        private void SendEnemyEvent(Component enemy, byte kind)
        {
            if (!IsSpawned || enemy == null) return;
            Health health = enemy.GetComponent<Health>();
            if (health != null) EnemyEventRpc(IdOf(health), kind);
        }

        [Rpc(SendTo.NotServer)]
        private void EnemyEventRpc(ushort id, byte kind)
        {
            if (_replicas.TryGetValue(id, out EnemyReplica replica) && replica != null) replica.PlayEvent(kind);
        }

        [Rpc(SendTo.NotServer, Delivery = RpcDelivery.Unreliable)]
        private void EnemyShotRpc(ushort id, Vector3 from, Vector3 to)
        {
            if (_replicas.TryGetValue(id, out EnemyReplica replica) && replica != null) replica.PlayShot(from, to);
        }

        private void OnBossWarning(BossAI boss, string text)
        {
            if (boss != null && IsSpawned) BossWarningRpc(text);
        }

        [Rpc(SendTo.NotServer)]
        private void BossWarningRpc(FixedString128Bytes text)
        {
            BossAI.ShowRemoteWarning(text.ToString());
        }

        // the wave's pay for every client player, as GameFlow pays this machine's own
        private void OnWaveClearedOnHost(int wave)
        {
            if (!Started) return;
            foreach (NetPlayer player in NetPlayer.All) player.GrantCredits(40 + 10 * wave);
        }

        // a boss down pays everyone in the team, not only whoever landed the last hit
        private void OnBossDefeated(BossAI boss)
        {
            if (boss == null || boss.Definition == null) return;
            foreach (NetPlayer player in NetPlayer.All)
                if (!player.IsOwnedByServer) player.GrantCredits(boss.Definition.Credits);
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
                    replica = CreateReplica(state);
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

        private EnemyReplica CreateReplica(EnemyState state)
        {
            switch (state.Kind)
            {
                case EnemyKind.Soldier:
                    return EnemyReplica.CreateSoldier(state, SoldierPrefab(state.Prefab));
                case EnemyKind.Boss:
                    return state.Prefab < Bosses.Count ? EnemyReplica.CreateBoss(state, Bosses[state.Prefab], ZombiePrefab(0)) : null;
                default:
                    return EnemyReplica.Create(state, ZombiePrefab(state.Prefab), ZombieVariantAt(state.Variant));
            }
        }

        private GameObject SoldierPrefab(byte index)
        {
            if (_spawner == null || _spawner.SoldierPrefabs == null || _spawner.SoldierPrefabs.Length == 0) return null;
            return _spawner.SoldierPrefabs[Mathf.Clamp(index, 0, _spawner.SoldierPrefabs.Length - 1)];
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

            if (target.IsDead && GameFlow.Instance != null)
            {
                // what the kill is worth is the host's call; the credits go into the shooter's account
                int points = GameFlow.Instance.KillPoints(target, headshot);
                int credits = GameFlow.Instance.KillCredits(target);
                shooter.GrantCredits(credits);
                KillConfirmedRpc(points, IsSpecial(target), RpcTarget.Single(sender, RpcTargetUse.Temp));
            }
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

        private static bool IsSpecial(Health target)
        {
            ZombieAI zombie = target.GetComponent<ZombieAI>();
            return zombie != null && zombie.Kind != ZombieKind.Common;
        }

        [Rpc(SendTo.SpecifiedInParams)]
        private void KillConfirmedRpc(int points, bool special, RpcParams rpc)
        {
            NetMatch.KillConfirmed(points, special);
        }
    }
}
