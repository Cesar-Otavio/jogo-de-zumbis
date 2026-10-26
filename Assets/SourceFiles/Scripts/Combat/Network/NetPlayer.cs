using System.Collections.Generic;
using StarterAssets;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace Combat
{
    // what the others need to draw a player: where, how fast, what pose, what gun, how hurt
    public struct PlayerState : INetworkSerializable, System.IEquatable<PlayerState>
    {
        public Vector3 Position;
        public float Yaw;
        public float Speed;
        // bits: 0 grounded, 1 jump, 2 free fall, 3 hanging, 4 crouched, 5 aiming, 6 flashlight, 7 dead
        public byte Flags;
        // a change plays the one-shot animation
        public byte Rolls;
        public byte ClimbUps;
        // index in the network weapon list, 255 = unarmed
        public byte Weapon;
        public float Health;
        public float MaxHealth;

        public bool Has(int bit) => (Flags & (1 << bit)) != 0;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Position);
            serializer.SerializeValue(ref Yaw);
            serializer.SerializeValue(ref Speed);
            serializer.SerializeValue(ref Flags);
            serializer.SerializeValue(ref Rolls);
            serializer.SerializeValue(ref ClimbUps);
            serializer.SerializeValue(ref Weapon);
            serializer.SerializeValue(ref Health);
            serializer.SerializeValue(ref MaxHealth);
        }

        public bool Equals(PlayerState other)
        {
            return Position == other.Position && Mathf.Approximately(Yaw, other.Yaw) && Mathf.Approximately(Speed, other.Speed) && Flags == other.Flags &&
                   Rolls == other.Rolls && ClimbUps == other.ClimbUps && Weapon == other.Weapon && Mathf.Approximately(Health, other.Health) &&
                   Mathf.Approximately(MaxHealth, other.MaxHealth);
        }
    }

    public enum CoopCondition : byte
    {
        Up,
        // on the ground, can be revived until the bleed-out time
        Downed,
        // out until the host brings them back (next wave); watches the others meanwhile
        Dead
    }

    /* One connected player (NGO player object). The owner fills in its name, ready flag, outfit and,
     * during a match, the state of the character it is driving; everyone else turns that into a puppet.
     * The host also uses the puppet as a real target for the infected, and sends the hits on it back to
     * the owner, who applies them to their own health (the owner stays in charge of their character).
     *
     * Down and revive: a blow that would kill an owner (while they have downs left this wave) puts them
     * on the ground instead; the host records it and starts a bleed-out clock. A teammate holding the
     * interact key next to them for ReviveTime asks the host, which checks who, how close and how long
     * before standing them up. Bleeding out or a down too many is death; the host brings the dead back
     * at the next wave, and calls game over when nobody is left standing.
     */
    public class NetPlayer : NetworkBehaviour
    {
        public static readonly List<NetPlayer> All = new List<NetPlayer>();
        public static NetPlayer Local { get; private set; }

        public readonly NetworkVariable<FixedString32Bytes> PlayerName = new NetworkVariable<FixedString32Bytes>(writePerm: NetworkVariableWritePermission.Owner);
        public readonly NetworkVariable<bool> Ready = new NetworkVariable<bool>(writePerm: NetworkVariableWritePermission.Owner);
        // seven outfit choices, four bits each
        public readonly NetworkVariable<int> Outfit = new NetworkVariable<int>(writePerm: NetworkVariableWritePermission.Owner);
        public readonly NetworkVariable<PlayerState> State = new NetworkVariable<PlayerState>(writePerm: NetworkVariableWritePermission.Owner);
        public readonly NetworkVariable<int> SpawnIndex = new NetworkVariable<int>(-1);

        // ---- co-op life, written by the host only
        public readonly NetworkVariable<CoopCondition> Condition = new NetworkVariable<CoopCondition>();
        public readonly NetworkVariable<double> BleedOutAt = new NetworkVariable<double>();
        public readonly NetworkVariable<byte> Downs = new NetworkVariable<byte>();
        public readonly NetworkVariable<ulong> Reviver = new NetworkVariable<ulong>(NoReviver);
        public readonly NetworkVariable<double> ReviveStartedAt = new NetworkVariable<double>();
        // round trip to the host in ms, measured by the host
        public readonly NetworkVariable<ushort> Ping = new NetworkVariable<ushort>();
        // a client player's credits, kept by the host (the host's own are in its RunState); the client mirrors it
        public readonly NetworkVariable<int> Credits = new NetworkVariable<int>();

        public const ulong NoReviver = ulong.MaxValue;
        public const float ReviveTime = 5.0f;
        public const float ReviveRange = 2.5f;
        public const float ReviveHealth = 0.3f;
        // later downs in the same wave bleed out faster, and the blow after the last one is final
        public static readonly float[] BleedOutTimes = { 30.0f, 20.0f };
        public static int MaxDownsPerWave => BleedOutTimes.Length;

        [Tooltip("State updates per second sent by the owner")]
        public float SendRate = 20.0f;

        public string DisplayName => PlayerName.Value.Length > 0 ? PlayerName.Value.ToString() : $"Jogador {OwnerClientId + 1}";
        public bool IsUp => Condition.Value == CoopCondition.Up;
        public float BleedOutLeft => Mathf.Max(0.0f, (float)(BleedOutAt.Value - ServerNow));
        public float ReviveProgress => Reviver.Value == NoReviver ? 0.0f : Mathf.Clamp01((float)(ServerNow - ReviveStartedAt.Value) / ReviveTime);
        public Vector3 Position => State.Value.Position;
        private static double ServerNow => NetworkManager.Singleton != null ? NetworkManager.Singleton.ServerTime.Time : 0.0;
        public NetPuppet Puppet { get; private set; }

        private PlayerCombat _local;
        private float _nextSend;
        private byte _rolls;
        private byte _climbUps;
        private bool _wasRolling;
        private bool _wasClimbingUp;
        private float _lastPositionTime;
        private Vector3 _lastPosition;
        private float _nextPing;

        public static NetPlayer For(ulong clientId)
        {
            foreach (NetPlayer player in All) if (player.OwnerClientId == clientId) return player;
            return null;
        }

        public override void OnNetworkSpawn()
        {
            All.Add(this);
            All.Sort((a, b) => a.OwnerClientId.CompareTo(b.OwnerClientId));
            if (IsOwner)
            {
                Local = this;
                PlayerName.Value = NetSession.Instance != null ? NetSession.Instance.LocalName : "Sobrevivente";
                Outfit.Value = PackOutfit(FindAnyObjectByType<PlayerOutfit>());
                // the host is always ready; it starts the match itself
                Ready.Value = IsServer;
            }

            Outfit.OnValueChanged += (_, value) => { if (Puppet != null) Puppet.SetOutfit(UnpackOutfit(value)); };
            Condition.OnValueChanged += OnConditionChanged;
            if (IsOwner && !IsServer) Credits.OnValueChanged += (before, now) => RunState.MirrorCredits(now, now - before);
            // the name may arrive a moment after the player object: announce whoever it is once it does
            if (!IsOwner)
            {
                if (PlayerName.Value.Length > 0) NetMatch.Notice($"{DisplayName} entrou na sessão");
                else PlayerName.OnValueChanged += AnnounceOnce;
            }
        }

        private void AnnounceOnce(FixedString32Bytes before, FixedString32Bytes now)
        {
            PlayerName.OnValueChanged -= AnnounceOnce;
            NetMatch.Notice($"{DisplayName} entrou na sessão");
        }

        public override void OnNetworkDespawn()
        {
            All.Remove(this);
            if (Local == this) Local = null;
            if (Puppet != null) Destroy(Puppet.gameObject);
            if (!IsOwner && NetGame.Instance != null) NetMatch.Notice($"{DisplayName} saiu da sessão");
        }

        // time spent here, for the performance probe (Diagnostics/PerfProbe)
        private static readonly Unity.Profiling.ProfilerMarker UpdateMarker = new Unity.Profiling.ProfilerMarker("Combat.NetPlayer.Update");

        private void Update()
        {
            using (UpdateMarker.Auto()) UpdateMarked();
        }

        private void UpdateMarked()
        {
            if (!IsSpawned) return;
            bool inMatch = NetGame.Instance != null && NetGame.Instance.Started;
            if (IsServer) ServerUpdate(inMatch);
            if (IsOwner)
            {
                if (inMatch) Publish();
                return;
            }

            // everyone else: a puppet that follows the state
            if (inMatch && Puppet == null) Puppet = NetPuppet.Create(this, UnpackOutfit(Outfit.Value));
            if (Puppet != null) Puppet.Follow(State.Value);
        }

        // ---------------------------------------------------------------- owner

        private void Publish()
        {
            if (_local == null)
            {
                _local = FindAnyObjectByType<PlayerCombat>();
                if (_local == null) return;
                // every shot of the local gun is drawn for the others
                if (_local.Holder != null) _local.Holder.Fired += _ => ReportShot(_local.Holder.LastShotFrom, _local.Holder.LastShotTo);
                // co-op: a killing blow can be a down instead, and a real death is the host's to know
                _local.Health.PreventDeath = PreventDeath;
                _local.Health.Died += _ => ReportDeathRpc();
            }

            // one-shot moves are counted as they start, so a late packet still plays them
            ThirdPersonController controller = _local.Controller;
            bool rolling = controller != null && controller.IsRolling;
            if (rolling && !_wasRolling) _rolls++;
            _wasRolling = rolling;
            ClimbingSystem climbing = _local.GetComponent<ClimbingSystem>();
            bool climbingUp = climbing != null && climbing.IsClimbing;
            if (climbingUp && !_wasClimbingUp) _climbUps++;
            _wasClimbingUp = climbingUp;

            if (Time.unscaledTime < _nextSend) return;
            _nextSend = Time.unscaledTime + 1.0f / SendRate;

            Animator animator = _local.GetComponent<Animator>();
            var state = new PlayerState
            {
                Position = _local.transform.position,
                Yaw = _local.transform.eulerAngles.y,
                Speed = animator != null ? animator.GetFloat("Speed") : 0.0f,
                Rolls = _rolls,
                ClimbUps = _climbUps,
                Weapon = (byte)NetMatch.WeaponIndex(_local.Current != null ? _local.Current.Data : null),
                Health = _local.Health.Current,
                MaxHealth = _local.Health.MaxHealth
            };

            int flags = 0;
            if (animator != null)
            {
                if (animator.GetBool("Grounded")) flags |= 1;
                if (animator.GetBool("Jump")) flags |= 2;
                if (animator.GetBool("FreeFall")) flags |= 4;
                if (animator.GetBool("Hanging")) flags |= 8;
            }

            if (controller != null && controller.IsCrouching) flags |= 16;
            if (_local.Holder != null && _local.Holder.Aiming) flags |= 32;
            if (_local.FlashlightOn) flags |= 64;
            // the others see a downed teammate on the ground the way they see a dead one
            if (_local.Health.IsDead || _local.IsDowned) flags |= 128;
            state.Flags = (byte)flags;
            State.Value = state;
        }

        // the owner's gun fired: the others draw the shot (the hit itself is handled where it lands)
        public void ReportShot(Vector3 from, Vector3 to)
        {
            if (IsOwner && IsSpawned) ShotRpc(from, to);
        }

        [Rpc(SendTo.NotMe, Delivery = RpcDelivery.Unreliable)]
        private void ShotRpc(Vector3 from, Vector3 to)
        {
            if (Puppet != null) Puppet.PlayShot(from, to);
        }

        // ---------------------------------------------------------------- damage on the puppet (host -> owner)

        public void SendDamage(DamageInfo info)
        {
            if (IsServer) DamageRpc(info.Amount, info.Point, info.Direction, info.Force);
        }

        [Rpc(SendTo.Owner)]
        private void DamageRpc(float amount, Vector3 point, Vector3 direction, float force)
        {
            if (_local == null) _local = FindAnyObjectByType<PlayerCombat>();
            if (_local == null) return;
            _local.Health.TakeDamage(new DamageInfo { Amount = amount, Point = point, Direction = direction, Force = force });
        }

        // ---------------------------------------------------------------- down, revive, death

        private bool PreventDeath(DamageInfo info)
        {
            if (!IsOwner || !Net.Active || _local == null) return false;
            if (Condition.Value != CoopCondition.Up || Downs.Value >= MaxDownsPerWave) return false;
            // shown at once here; the host confirms (or overrules with death) a moment later
            _local.EnterDowned();
            ReportDownRpc();
            return true;
        }

        [Rpc(SendTo.Server)]
        private void ReportDownRpc()
        {
            if (Condition.Value != CoopCondition.Up) return;
            if (Downs.Value >= MaxDownsPerWave)
            {
                SetDead("uma queda a mais nesta onda");
                return;
            }

            BleedOutAt.Value = ServerNow + BleedOutTimes[Downs.Value];
            Downs.Value++;
            Reviver.Value = NoReviver;
            Condition.Value = CoopCondition.Downed;
            if (NetGame.Instance != null) NetGame.Instance.Announce($"{DisplayName} caiu! Ajude antes que seja tarde");
        }

        [Rpc(SendTo.Server)]
        private void ReportDeathRpc()
        {
            if (Condition.Value != CoopCondition.Dead) SetDead("morte no jogo do dono");
        }

        private void SetDead(string reason)
        {
            Debug.Log($"[Net] {DisplayName} morto: {reason}");
            Reviver.Value = NoReviver;
            Condition.Value = CoopCondition.Dead;
            if (NetGame.Instance != null) NetGame.Instance.Announce($"{DisplayName} morreu (volta na próxima onda)");
        }

        // the host brings everyone who died back for a new wave, and the downs start over
        public void ServerNewWave()
        {
            if (!IsServer) return;
            Downs.Value = 0;
            if (Condition.Value == CoopCondition.Dead) Condition.Value = CoopCondition.Up;
        }

        // this machine's player starts reviving a downed teammate (the host decides if it counts)
        public void BeginRevive(NetPlayer target)
        {
            if (IsOwner && target != null) BeginReviveRpc(target.OwnerClientId);
        }

        public void CancelRevive(NetPlayer target)
        {
            if (IsOwner && target != null) CancelReviveRpc(target.OwnerClientId);
        }

        public void FinishRevive(NetPlayer target)
        {
            if (IsOwner && target != null) FinishReviveRpc(target.OwnerClientId);
        }

        // these run on the reviver's own player object on the host; the network's sender id must match it
        [Rpc(SendTo.Server)]
        private void BeginReviveRpc(ulong targetClient, RpcParams rpc = default)
        {
            if (rpc.Receive.SenderClientId != OwnerClientId) return;
            NetPlayer target = For(targetClient);
            if (target == null || target == this || !IsUp || target.Condition.Value != CoopCondition.Downed) return;
            if (target.Reviver.Value != NoReviver && target.Reviver.Value != OwnerClientId) return;
            if (Vector3.Distance(Position, target.Position) > ReviveRange + 0.5f) return;
            target.ReviveStartedAt.Value = ServerNow;
            target.Reviver.Value = OwnerClientId;
        }

        [Rpc(SendTo.Server)]
        private void CancelReviveRpc(ulong targetClient, RpcParams rpc = default)
        {
            if (rpc.Receive.SenderClientId != OwnerClientId) return;
            NetPlayer target = For(targetClient);
            if (target != null && target.Reviver.Value == OwnerClientId) target.Reviver.Value = NoReviver;
        }

        [Rpc(SendTo.Server)]
        private void FinishReviveRpc(ulong targetClient, RpcParams rpc = default)
        {
            if (rpc.Receive.SenderClientId != OwnerClientId) return;
            NetPlayer target = For(targetClient);
            if (target == null || target.Reviver.Value != OwnerClientId) return;
            bool valid = IsUp && target.Condition.Value == CoopCondition.Downed &&
                         ServerNow - target.ReviveStartedAt.Value >= ReviveTime - 0.25 &&
                         Vector3.Distance(Position, target.Position) <= ReviveRange + 0.5f;
            target.Reviver.Value = NoReviver;
            if (!valid) return;
            target.Condition.Value = CoopCondition.Up;
            if (NetGame.Instance != null) NetGame.Instance.Announce($"{DisplayName} reanimou {target.DisplayName}");
        }

        private void ServerUpdate(bool inMatch)
        {
            if (Time.unscaledTime >= _nextPing)
            {
                _nextPing = Time.unscaledTime + 1.0f;
                var transport = NetworkManager.NetworkConfig.NetworkTransport;
                ushort rtt = IsOwnedByServer ? (ushort)0 : (ushort)Mathf.Min(transport.GetCurrentRtt(OwnerClientId), ushort.MaxValue);
                if (Ping.Value != rtt) Ping.Value = rtt;
            }

            if (!inMatch || Condition.Value != CoopCondition.Downed) return;
            if (ServerNow >= BleedOutAt.Value)
            {
                SetDead("sangrou até o fim");
                return;
            }

            // a reviver who walked off or went down stops counting
            if (Reviver.Value != NoReviver)
            {
                NetPlayer reviver = For(Reviver.Value);
                if (reviver == null || !reviver.IsUp || Vector3.Distance(reviver.Position, Position) > ReviveRange + 1.0f) Reviver.Value = NoReviver;
            }
        }

        // the owner follows what the host decided
        private void OnConditionChanged(CoopCondition before, CoopCondition now)
        {
            if (!IsOwner) return;
            if (_local == null) _local = FindAnyObjectByType<PlayerCombat>();
            if (_local == null) return;

            switch (now)
            {
                case CoopCondition.Up when before == CoopCondition.Downed:
                    _local.ExitDowned(ReviveHealth);
                    break;
                case CoopCondition.Up when before == CoopCondition.Dead:
                    _local.RespawnNow();
                    break;
                case CoopCondition.Dead:
                    _local.KillNow();
                    break;
                case CoopCondition.Downed:
                    _local.EnterDowned();
                    break;
            }
        }

        // ---------------------------------------------------------------- credits and the shop (host-checked)

        // the host paying a client player (kills, waves, chests, bosses)
        public void GrantCredits(int amount)
        {
            if (IsServer && amount > 0 && !IsOwnedByServer) Credits.Value += amount;
        }

        public void RequestPurchase(int entry, int price)
        {
            if (IsOwner) PurchaseRpc(entry, price);
        }

        // the host checks the entry exists, the price isn't below the shop's and the account covers it
        [Rpc(SendTo.Server)]
        private void PurchaseRpc(int entry, int price, RpcParams rpc = default)
        {
            if (rpc.Receive.SenderClientId != OwnerClientId) return;
            ShopManager shop = ShopManager.Instance;
            bool ok = shop != null && shop.Catalog != null && shop.Catalog.Entries != null && entry >= 0 && entry < shop.Catalog.Entries.Length
                      && price >= shop.Catalog.Entries[entry].Price && Credits.Value >= price;
            if (ok) Credits.Value -= price;
            PurchaseResultRpc(entry, ok);
        }

        [Rpc(SendTo.Owner)]
        private void PurchaseResultRpc(int entry, bool ok)
        {
            if (ShopManager.Instance != null) ShopManager.Instance.CompleteRemotePurchase(entry, ok);
        }

        // ---------------------------------------------------------------- outfit packing

        public static int PackOutfit(PlayerOutfit outfit)
        {
            if (outfit == null) return 0;
            int[] choices = outfit.Choices;
            int packed = 0;
            for (int i = 0; i < choices.Length && i < 7; i++) packed |= (choices[i] & 0xF) << (i * 4);
            return packed;
        }

        public static int[] UnpackOutfit(int packed)
        {
            var choices = new int[7];
            for (int i = 0; i < 7; i++) choices[i] = (packed >> (i * 4)) & 0xF;
            return choices;
        }

        // host check: an owner may not move faster than any move in the game allows
        public bool PlausibleMove(Vector3 position)
        {
            float now = Time.unscaledTime;
            float dt = Mathf.Max(0.02f, now - _lastPositionTime);
            bool ok = _lastPositionTime <= 0.0f || (position - _lastPosition).magnitude / dt < 25.0f;
            _lastPosition = position;
            _lastPositionTime = now;
            return ok;
        }
    }
}
