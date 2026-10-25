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

    /* One connected player (NGO player object). The owner fills in its name, ready flag, outfit and,
     * during a match, the state of the character it is driving; everyone else turns that into a puppet.
     * The host also uses the puppet as a real target for the infected, and sends the hits on it back to
     * the owner, who applies them to their own health (the owner stays in charge of their character).
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

        [Tooltip("State updates per second sent by the owner")]
        public float SendRate = 20.0f;

        public string DisplayName => PlayerName.Value.Length > 0 ? PlayerName.Value.ToString() : $"Jogador {OwnerClientId + 1}";
        public NetPuppet Puppet { get; private set; }

        private PlayerCombat _local;
        private float _nextSend;
        private byte _rolls;
        private byte _climbUps;
        private bool _wasRolling;
        private bool _wasClimbingUp;
        private float _lastPositionTime;
        private Vector3 _lastPosition;

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

        private void Update()
        {
            if (!IsSpawned) return;
            bool inMatch = NetGame.Instance != null && NetGame.Instance.Started;
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
            if (_local.Health.IsDead) flags |= 128;
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
