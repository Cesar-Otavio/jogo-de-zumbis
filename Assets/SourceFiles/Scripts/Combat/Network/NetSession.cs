using System;
using System.Text;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

namespace Combat
{
    /* The co-op session: host or join by IP and UDP port (LAN, Radmin VPN, Hamachi), with a version,
     * room-size and password check before anyone is let in. Wraps the scene's NetworkManager; with no
     * session running every other system behaves exactly as in single player (Net.Active is false).
     *
     * Topology: host-client, the host is authoritative for enemies, waves and damage. Each player keeps
     * driving the character in their own scene (input, camera, HUD stay local) and sends its state; the
     * others see it as a puppet (NetPuppet).
     */
    [RequireComponent(typeof(NetworkManager))]
    public class NetSession : MonoBehaviour
    {
        // bump when the messages or synced state change, so mismatched builds refuse each other cleanly
        public const ushort Protocol = 1;
        public const ushort DefaultPort = 7777;
        public const float ConnectTimeout = 10.0f;
        public const int DisconnectTimeoutMs = 10000;

        public static NetSession Instance { get; private set; }

        public enum Status
        {
            Offline,
            Hosting,
            Connecting,
            Connected,
            Failed
        }

        [Serializable]
        private class JoinRequest
        {
            public ushort Protocol;
            public string Version;
            public string Name;
            public string Password;
        }

        public Status Current { get; private set; } = Status.Offline;
        // the reason the last attempt failed or the session ended, for the menu
        public string Message { get; private set; } = "";
        public string RoomName { get; private set; } = "";
        public int MaxPlayers { get; private set; } = 4;
        public string LocalName { get; private set; } = "Sobrevivente";

        public event Action<Status> StatusChanged;

        private NetworkManager _manager;
        private UnityTransport _transport;
        private string _password = "";
        private float _connectStarted;

        private void Awake()
        {
            Instance = this;
            _manager = GetComponent<NetworkManager>();
            _transport = GetComponent<UnityTransport>();
            // a player whose game stopped answering leaves after 10 s instead of the transport's 30;
            // heartbeats every half second keep a quiet but healthy link (VPN included) from timing out
            _transport.DisconnectTimeoutMS = DisconnectTimeoutMs;
            _transport.HeartbeatTimeoutMS = 500;
            _manager.NetworkConfig.ProtocolVersion = Protocol;
            _manager.NetworkConfig.ConnectionApproval = true;
            _manager.ConnectionApprovalCallback = Approve;
            _manager.OnClientDisconnectCallback += OnDisconnected;
            _manager.OnClientConnectedCallback += OnConnected;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (_manager != null && _manager.IsListening) _manager.Shutdown();
        }

        // ---------------------------------------------------------------- host

        // after a scene reload the previous manager may have cleared the static reference on its way out
        private void EnsureSingleton()
        {
            if (NetworkManager.Singleton != _manager) _manager.SetSingleton();
        }

        public bool Host(string room, string playerName, ushort port, int maxPlayers, string password)
        {
            EnsureSingleton();
            if (_manager.IsListening) return false;
            RoomName = string.IsNullOrWhiteSpace(room) ? "Sobreviventes Aurora" : room.Trim();
            LocalName = CleanName(playerName);
            MaxPlayers = Mathf.Clamp(maxPlayers, 2, 4);
            _password = password ?? "";
            // listen on every interface, so LAN and VPN adapters (Radmin, Hamachi) all reach it
            _transport.SetConnectionData("127.0.0.1", port, "0.0.0.0");
            _manager.NetworkConfig.ConnectionData = Payload(LocalName, _password);
            if (!_manager.StartHost())
            {
                Fail($"Não foi possível abrir a porta UDP {port}. Ela pode estar em uso por outro programa.");
                return false;
            }

            SetStatus(Status.Hosting, $"Sala aberta na porta UDP {port}");
            return true;
        }

        // ---------------------------------------------------------------- join

        public bool Join(string address, ushort port, string playerName, string password)
        {
            EnsureSingleton();
            if (_manager.IsListening) return false;
            address = (address ?? "").Trim();
            if (!System.Net.IPAddress.TryParse(address, out System.Net.IPAddress ip) || ip.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork)
            {
                Fail("Endereço IPv4 inválido. Use o IP do Host, por exemplo 26.12.34.56 (Radmin) ou 192.168.0.10 (LAN).");
                return false;
            }

            LocalName = CleanName(playerName);
            _transport.SetConnectionData(address, port);
            _manager.NetworkConfig.ConnectionData = Payload(LocalName, password ?? "");
            _connectStarted = Time.unscaledTime;
            if (!_manager.StartClient())
            {
                Fail("Não foi possível iniciar a conexão.");
                return false;
            }

            SetStatus(Status.Connecting, $"Conectando a {address}:{port}…");
            return true;
        }

        public void Leave(string reason = "")
        {
            if (_manager != null && _manager.IsListening) _manager.Shutdown();
            SetStatus(Status.Offline, reason);
        }

        // time spent here, for the performance probe (Diagnostics/PerfProbe)
        private static readonly Unity.Profiling.ProfilerMarker UpdateMarker = new Unity.Profiling.ProfilerMarker("Combat.NetSession.Update");

        private void Update()
        {
            using (UpdateMarker.Auto()) UpdateMarked();
        }

        private void UpdateMarked()
        {
            // a host that never answers: give up instead of hanging on "connecting"
            if (Current == Status.Connecting && Time.unscaledTime - _connectStarted > ConnectTimeout)
            {
                _manager.Shutdown();
                Fail("Não foi possível conectar: o Host não respondeu. Confira o IP, a porta, a VPN e o firewall do Host.");
            }
        }

        // ---------------------------------------------------------------- approval (host)

        private void Approve(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
        {
            response.CreatePlayerObject = true;
            response.Pending = false;

            // the host itself
            if (request.ClientNetworkId == NetworkManager.ServerClientId)
            {
                response.Approved = true;
                return;
            }

            JoinRequest join = null;
            try
            {
                join = JsonUtility.FromJson<JoinRequest>(Encoding.UTF8.GetString(request.Payload ?? new byte[0]));
            }
            catch (ArgumentException)
            {
                // not ours: refused below
            }

            string refusal = null;
            if (join == null) refusal = "Pedido de conexão inválido.";
            else if (join.Protocol != Protocol || join.Version != Application.version) refusal = $"Versão incompatível: o Host usa {Application.version} (protocolo {Protocol}).";
            else if (_manager.ConnectedClientsIds.Count >= MaxPlayers) refusal = "Sala cheia.";
            else if (NetGame.Instance != null && NetGame.Instance.Started) refusal = "A partida já começou. Entre pelo lobby da próxima.";
            else if (!string.IsNullOrEmpty(_password) && join.Password != _password) refusal = "Senha incorreta.";

            response.Approved = refusal == null;
            response.Reason = refusal;
        }

        // ---------------------------------------------------------------- events

        private void OnConnected(ulong clientId)
        {
            if (_manager.IsServer)
            {
                // the shared game state lives on one object the host spawns once
                if (clientId == NetworkManager.ServerClientId) NetGame.SpawnFor(_manager);
                return;
            }

            if (clientId == _manager.LocalClientId) SetStatus(Status.Connected, "Conectado. Aguardando o Host.");
        }

        private void OnDisconnected(ulong clientId)
        {
            if (_manager.IsServer)
            {
                if (clientId != NetworkManager.ServerClientId && NetGame.Instance != null) NetGame.Instance.Announce($"Um jogador saiu da sessão");
                return;
            }

            // the local client was refused or dropped
            string reason = _manager.DisconnectReason;
            if (Current == Status.Connecting) Fail(string.IsNullOrEmpty(reason) ? "Não foi possível conectar." : reason);
            else Fail(string.IsNullOrEmpty(reason) ? "Conexão perdida com o Host." : reason);
        }

        // ---------------------------------------------------------------- helpers

        private static byte[] Payload(string name, string password)
        {
            var join = new JoinRequest { Protocol = Protocol, Version = Application.version, Name = name, Password = password };
            return Encoding.UTF8.GetBytes(JsonUtility.ToJson(join));
        }

        public static string CleanName(string name)
        {
            name = (name ?? "").Trim();
            if (name.Length == 0) name = "Sobrevivente";
            return name.Length > 20 ? name.Substring(0, 20) : name;
        }

        private void Fail(string message)
        {
            SetStatus(Status.Failed, message);
        }

        private void SetStatus(Status status, string message)
        {
            Current = status;
            Message = message ?? "";
            Debug.Log($"[Net] {status}: {Message}");
            StatusChanged?.Invoke(status);
        }
    }
}
