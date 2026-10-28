using System;
using System.Collections.Generic;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using TMPro;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Combat
{
    /* MULTIJOGADOR: create a room (host), join one by IP and port, and the lobby where everyone gets
     * ready before the host starts. Shows the real connection state and the reason when something fails.
     * The host's own IPv4 addresses (LAN, Radmin VPN, Hamachi) are listed so they can be shared.
     */
    public class NetMenuView : MonoBehaviour
    {
        public static NetMenuView Instance { get; private set; }

        // a message to show after the scene reloads (the session ended during a match)
        public static string PendingMessage;

        private enum Page { Home, Create, Join, Lobby }

        private GameObject _root;
        private readonly Dictionary<Page, GameObject> _pages = new Dictionary<Page, GameObject>();
        private Page _page;

        private TMP_InputField _name;
        private TMP_InputField _room;
        private TMP_InputField _hostPort;
        private TMP_InputField _hostPassword;
        private TMP_InputField _address;
        private TMP_InputField _joinPort;
        private TMP_InputField _joinPassword;
        private TextMeshProUGUI _players;
        private TextMeshProUGUI _difficulty;
        private TextMeshProUGUI _joinStatus;
        private TextMeshProUGUI _lobbyTitle;
        private TextMeshProUGUI _lobbySettings;
        private TextMeshProUGUI _lobbyAddresses;
        private TextMeshProUGUI _lobbyLog;
        private TextMeshProUGUI _startHint;
        private TextMeshProUGUI _lobbyDifficulty;
        private readonly List<(TextMeshProUGUI name, TextMeshProUGUI state, Button kick)> _slots = new List<(TextMeshProUGUI, TextMeshProUGUI, Button)>();
        private Button _start;
        private Button _ready;
        private Button _diffLeft;
        private Button _diffRight;
        private readonly List<string> _log = new List<string>();

        private int _maxPlayers = 4;
        private int _difficultyChoice;

        private const string PrefsName = "Combat.Net.Name";
        private const string PrefsAddress = "Combat.Net.Address";
        private const string PrefsPort = "Combat.Net.Port";

        private void Awake()
        {
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (NetSession.Instance != null) NetSession.Instance.StatusChanged -= OnStatus;
        }

        private void Start()
        {
            Build();
            _root.SetActive(false);
            if (NetSession.Instance != null) NetSession.Instance.StatusChanged += OnStatus;
            if (!string.IsNullOrEmpty(PendingMessage))
            {
                Show();
                Log(PendingMessage);
                SetJoinStatus(PendingMessage, true);
                PendingMessage = null;
            }
        }

        // ---------------------------------------------------------------- building

        private void Build()
        {
            RectTransform canvas = UiKit.Canvas("Multiplayer", transform, 110);
            _root = UiKit.Panel("MultiplayerPanel", canvas, Vector2.zero, Vector2.one, new Color(0.02f, 0.025f, 0.03f, 1.0f)).gameObject;
            var root = (RectTransform)_root.transform;
            root.GetComponent<Image>().raycastTarget = true;
            UiKit.Text(root, "MULTIJOGADOR", 52, new Vector2(0.06f, 0.86f), new Vector2(0.6f, 0.95f), TextAlignmentOptions.Left, UiKit.Accent, true);
            UiKit.Text(root, "Cooperativo para até 4 jogadores · LAN, Radmin VPN ou Hamachi · o computador que cria a sala é o Host",
                22, new Vector2(0.06f, 0.82f), new Vector2(0.94f, 0.86f), TextAlignmentOptions.Left, UiKit.Muted);

            _pages[Page.Home] = BuildHome(root);
            _pages[Page.Create] = BuildCreate(root);
            _pages[Page.Join] = BuildJoin(root);
            _pages[Page.Lobby] = BuildLobby(root);
        }

        private RectTransform NewPage(RectTransform root, string name)
        {
            return UiKit.Panel(name, root, new Vector2(0.06f, 0.06f), new Vector2(0.94f, 0.8f), new Color(0, 0, 0, 0)).rectTransform;
        }

        private GameObject BuildHome(RectTransform root)
        {
            RectTransform page = NewPage(root, "Home");
            Label(page, "SEU NOME", 0.86f);
            _name = Field(page, "Nome do jogador", new Vector2(0.0f, 0.76f), new Vector2(0.4f, 0.85f), PlayerPrefs.GetString(PrefsName, "Sobrevivente"), TMP_InputField.ContentType.Standard, 20);
            UiKit.Button(page, "CRIAR PARTIDA", new Vector2(0.0f, 0.56f), new Vector2(0.4f, 0.68f), () => Go(Page.Create), true, 30);
            UiKit.Button(page, "ENTRAR EM PARTIDA", new Vector2(0.0f, 0.41f), new Vector2(0.4f, 0.53f), () => Go(Page.Join), false, 30);
            UiKit.Button(page, "VOLTAR", new Vector2(0.0f, 0.0f), new Vector2(0.2f, 0.08f), Hide, false, 24);
            TextMeshProUGUI help = UiKit.Text(page,
                "<color=#FFC74D><b>Como jogar com amigos</b></color>\n" +
                "1. Todos usam a <b>mesma versão</b> do jogo.\n" +
                "2. Na mesma casa: só a mesma rede (LAN).\n" +
                "   Pela internet: todos na mesma rede do <b>Radmin VPN</b> ou <b>Hamachi</b>.\n" +
                "3. Um jogador cria a partida e passa o IP que aparece no lobby.\n" +
                "4. Os outros entram com esse IP e a porta (padrão 7777, UDP).\n\n" +
                "Se não conectar: confira se o firewall do Host permite o jogo na rede privada.",
                22, new Vector2(0.48f, 0.1f), new Vector2(1.0f, 0.86f), TextAlignmentOptions.TopLeft, Color.white);
            help.textWrappingMode = TextWrappingModes.Normal;
            return page.gameObject;
        }

        private GameObject BuildCreate(RectTransform root)
        {
            RectTransform page = NewPage(root, "Create");
            Label(page, "NOME DA SALA", 0.86f);
            _room = Field(page, "Sobreviventes Aurora", new Vector2(0.0f, 0.76f), new Vector2(0.45f, 0.85f), "Sobreviventes Aurora", TMP_InputField.ContentType.Standard, 30);
            Label(page, "MODO", 0.7f);
            UiKit.Text(page, "MODO INFINITO  <color=#FFFFFF80>· campanha cooperativa: próxima etapa</color>", 24, new Vector2(0.0f, 0.6f), new Vector2(0.6f, 0.69f), TextAlignmentOptions.Left, Color.white, true);

            Label(page, "DIFICULDADE", 0.54f);
            _difficultyChoice = (int)GameDifficulty.Current;
            Cycler(page, 0.44f, () => GameDifficulty.Get((Difficulty)_difficultyChoice).Name, step => _difficultyChoice = (_difficultyChoice + step + 4) % 4, out _difficulty);
            Label(page, "JOGADORES", 0.38f);
            Cycler(page, 0.28f, () => _maxPlayers.ToString(), step => _maxPlayers = Mathf.Clamp(_maxPlayers + step, 2, 4), out _players);

            Label(page, "PORTA UDP", 0.86f, 0.55f);
            _hostPort = Field(page, "7777", new Vector2(0.55f, 0.76f), new Vector2(0.75f, 0.85f), PlayerPrefs.GetInt(PrefsPort, NetSession.DefaultPort).ToString(), TMP_InputField.ContentType.IntegerNumber, 5);
            Label(page, "SENHA (OPCIONAL)", 0.7f, 0.55f);
            _hostPassword = Field(page, "sem senha", new Vector2(0.55f, 0.6f), new Vector2(0.9f, 0.69f), "", TMP_InputField.ContentType.Password, 20);

            UiKit.Button(page, "CRIAR", new Vector2(0.55f, 0.1f), new Vector2(0.9f, 0.22f), CreateRoom, true, 30);
            UiKit.Button(page, "VOLTAR", new Vector2(0.0f, 0.0f), new Vector2(0.2f, 0.08f), () => Go(Page.Home), false, 24);
            return page.gameObject;
        }

        private GameObject BuildJoin(RectTransform root)
        {
            RectTransform page = NewPage(root, "Join");
            Label(page, "IP DO HOST", 0.86f);
            _address = Field(page, "ex.: 26.12.34.56 ou 192.168.0.10", new Vector2(0.0f, 0.76f), new Vector2(0.45f, 0.85f), PlayerPrefs.GetString(PrefsAddress, "127.0.0.1"), TMP_InputField.ContentType.Standard, 15);
            Label(page, "PORTA UDP", 0.7f);
            _joinPort = Field(page, "7777", new Vector2(0.0f, 0.6f), new Vector2(0.2f, 0.69f), PlayerPrefs.GetInt(PrefsPort, NetSession.DefaultPort).ToString(), TMP_InputField.ContentType.IntegerNumber, 5);
            Label(page, "SENHA", 0.54f);
            _joinPassword = Field(page, "se a sala tiver", new Vector2(0.0f, 0.44f), new Vector2(0.35f, 0.53f), "", TMP_InputField.ContentType.Password, 20);
            UiKit.Button(page, "CONECTAR", new Vector2(0.0f, 0.24f), new Vector2(0.35f, 0.36f), JoinRoom, true, 30);
            UiKit.Button(page, "CANCELAR", new Vector2(0.37f, 0.24f), new Vector2(0.55f, 0.36f), () => { if (NetSession.Instance != null) NetSession.Instance.Leave("Cancelado"); }, false, 24);
            _joinStatus = UiKit.Text(page, "", 24, new Vector2(0.0f, 0.1f), new Vector2(1.0f, 0.22f), TextAlignmentOptions.TopLeft, Color.white);
            _joinStatus.textWrappingMode = TextWrappingModes.Normal;
            UiKit.Button(page, "VOLTAR", new Vector2(0.0f, 0.0f), new Vector2(0.2f, 0.08f), () => { if (NetSession.Instance != null) NetSession.Instance.Leave(); Go(Page.Home); }, false, 24);
            return page.gameObject;
        }

        private GameObject BuildLobby(RectTransform root)
        {
            RectTransform page = NewPage(root, "Lobby");
            _lobbyTitle = UiKit.Text(page, "", 34, new Vector2(0.0f, 0.9f), new Vector2(0.6f, 1.0f), TextAlignmentOptions.Left, Color.white, true);
            _lobbySettings = UiKit.Text(page, "", 22, new Vector2(0.0f, 0.84f), new Vector2(0.6f, 0.9f), TextAlignmentOptions.Left, UiKit.Muted);
            for (int i = 0; i < 4; i++)
            {
                float top = 0.8f - i * 0.15f;
                Image slot = UiKit.Panel("Slot", page, new Vector2(0.0f, top - 0.13f), new Vector2(0.6f, top), new Color(1, 1, 1, 0.06f));
                TextMeshProUGUI name = UiKit.Text(slot.rectTransform, "", 26, new Vector2(0.03f, 0.45f), new Vector2(0.75f, 0.95f), TextAlignmentOptions.Left, Color.white, true);
                TextMeshProUGUI state = UiKit.Text(slot.rectTransform, "", 20, new Vector2(0.03f, 0.05f), new Vector2(0.75f, 0.5f), TextAlignmentOptions.Left, UiKit.Muted);
                int index = i;
                Button kick = UiKit.Button(slot.rectTransform, "REMOVER", new Vector2(0.78f, 0.25f), new Vector2(0.97f, 0.75f), () => Kick(index), false, 18);
                _slots.Add((name, state, kick));
            }

            _diffLeft = UiKit.Button(page, "<", new Vector2(0.64f, 0.84f), new Vector2(0.69f, 0.92f), () => ChangeLobbyDifficulty(-1), false, 24);
            _diffRight = UiKit.Button(page, ">", new Vector2(0.94f, 0.84f), new Vector2(0.99f, 0.92f), () => ChangeLobbyDifficulty(1), false, 24);
            _lobbyDifficulty = UiKit.Text(page, "", 24, new Vector2(0.7f, 0.84f), new Vector2(0.93f, 0.92f), TextAlignmentOptions.Center, Color.white, true);
            _lobbyAddresses = UiKit.Text(page, "", 21, new Vector2(0.64f, 0.42f), new Vector2(1.0f, 0.82f), TextAlignmentOptions.TopLeft, Color.white);
            _lobbyAddresses.textWrappingMode = TextWrappingModes.Normal;
            _lobbyLog = UiKit.Text(page, "", 19, new Vector2(0.64f, 0.16f), new Vector2(1.0f, 0.4f), TextAlignmentOptions.BottomLeft, UiKit.Muted);
            _lobbyLog.textWrappingMode = TextWrappingModes.Normal;
            _start = UiKit.Button(page, "INICIAR PARTIDA", new Vector2(0.64f, 0.0f), new Vector2(1.0f, 0.12f), () => { if (NetGame.Instance != null) NetGame.Instance.StartMatch(); }, true, 28);
            _ready = UiKit.Button(page, "PRONTO", new Vector2(0.64f, 0.0f), new Vector2(1.0f, 0.12f), ToggleReady, true, 28);
            _startHint = UiKit.Text(page, "", 19, new Vector2(0.0f, 0.12f), new Vector2(0.6f, 0.17f), TextAlignmentOptions.Left, new Color(1.0f, 0.75f, 0.4f));
            UiKit.Button(page, "SAIR DA SALA", new Vector2(0.0f, 0.0f), new Vector2(0.25f, 0.09f), () => { if (NetSession.Instance != null) NetSession.Instance.Leave("Você saiu da sala"); Go(Page.Home); }, false, 22);
            return page.gameObject;
        }

        private static void Label(RectTransform page, string text, float top, float x = 0.0f)
        {
            UiKit.Text(page, text, 20, new Vector2(x, top), new Vector2(x + 0.45f, top + 0.05f), TextAlignmentOptions.BottomLeft, UiKit.Muted, true);
        }

        private void Cycler(RectTransform page, float bottom, Func<string> value, Action<int> change, out TextMeshProUGUI label)
        {
            TextMeshProUGUI shown = UiKit.Text(page, value(), 26, new Vector2(0.07f, bottom), new Vector2(0.33f, bottom + 0.09f), TextAlignmentOptions.Center, Color.white, true);
            UiKit.Button(page, "<", new Vector2(0.0f, bottom), new Vector2(0.06f, bottom + 0.09f), () => { change(-1); shown.text = value(); }, false, 26);
            UiKit.Button(page, ">", new Vector2(0.34f, bottom), new Vector2(0.4f, bottom + 0.09f), () => { change(1); shown.text = value(); }, false, 26);
            label = shown;
        }

        // a TextMeshPro input field built in code, in the menus' style
        private static TMP_InputField Field(RectTransform parent, string placeholder, Vector2 min, Vector2 max, string value, TMP_InputField.ContentType type, int limit)
        {
            Image background = UiKit.Panel("Field", parent, min, max, new Color(1, 1, 1, 0.1f));
            background.raycastTarget = true;
            GameObject go = background.gameObject;
            go.SetActive(false);
            var area = new GameObject("TextArea", typeof(RectTransform), typeof(RectMask2D)).GetComponent<RectTransform>();
            area.SetParent(background.rectTransform, false);
            area.anchorMin = Vector2.zero;
            area.anchorMax = Vector2.one;
            area.offsetMin = new Vector2(14.0f, 4.0f);
            area.offsetMax = new Vector2(-14.0f, -4.0f);
            TextMeshProUGUI hint = UiKit.Text(area, placeholder, 24, Vector2.zero, Vector2.one, TextAlignmentOptions.Left, new Color(1, 1, 1, 0.3f));
            TextMeshProUGUI text = UiKit.Text(area, "", 24, Vector2.zero, Vector2.one, TextAlignmentOptions.Left, Color.white);
            hint.textWrappingMode = TextWrappingModes.NoWrap;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            var field = go.AddComponent<TMP_InputField>();
            field.textViewport = area;
            field.textComponent = text;
            field.placeholder = hint;
            field.contentType = type;
            field.characterLimit = limit;
            field.caretColor = UiKit.Accent;
            field.selectionColor = new Color(1.0f, 0.78f, 0.3f, 0.35f);
            go.SetActive(true);
            field.text = value;
            return field;
        }

        // ---------------------------------------------------------------- navigation

        public void Show()
        {
            _root.SetActive(true);
            Go(Net.Listening ? Page.Lobby : Page.Home);
        }

        public void Hide()
        {
            _root.SetActive(false);
        }

        private void Go(Page page)
        {
            _page = page;
            foreach (var pair in _pages) pair.Value.SetActive(pair.Key == page);
            if (page == Page.Lobby) RefreshAddresses();
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
        }

        // ---------------------------------------------------------------- actions

        private void CreateRoom()
        {
            if (!TryPort(_hostPort.text, out ushort port)) return;
            SaveName();
            PlayerPrefs.SetInt(PrefsPort, port);
            GameDifficulty.Current = (Difficulty)_difficultyChoice;
            if (NetSession.Instance != null && NetSession.Instance.Host(_room.text, _name.text, port, _maxPlayers, _hostPassword.text)) Go(Page.Lobby);
            else SetJoinStatus(NetSession.Instance != null ? NetSession.Instance.Message : "Rede indisponível", true);
        }

        private void JoinRoom()
        {
            if (!TryPort(_joinPort.text, out ushort port)) return;
            SaveName();
            PlayerPrefs.SetString(PrefsAddress, _address.text.Trim());
            PlayerPrefs.SetInt(PrefsPort, port);
            if (NetSession.Instance != null) NetSession.Instance.Join(_address.text, port, _name.text, _joinPassword.text);
        }

        private bool TryPort(string text, out ushort port)
        {
            if (ushort.TryParse(text, out port) && port >= 1024) return true;
            SetJoinStatus("Porta inválida: use um número entre 1024 e 65535 (padrão 7777).", true);
            Log("Porta inválida");
            return false;
        }

        private void SaveName()
        {
            PlayerPrefs.SetString(PrefsName, NetSession.CleanName(_name.text));
            PlayerPrefs.Save();
        }

        private void ToggleReady()
        {
            if (NetPlayer.Local != null) NetPlayer.Local.Ready.Value = !NetPlayer.Local.Ready.Value;
        }

        private void ChangeLobbyDifficulty(int step)
        {
            if (NetGame.Instance == null || !NetGame.Instance.IsServer) return;
            NetGame.Instance.DifficultyLevel.Value = (NetGame.Instance.DifficultyLevel.Value + step + 4) % 4;
        }

        private void Kick(int slot)
        {
            NetworkManager manager = NetworkManager.Singleton;
            if (manager == null || !manager.IsServer || slot >= NetPlayer.All.Count) return;
            NetPlayer player = NetPlayer.All[slot];
            if (player.IsOwnedByServer) return;
            manager.DisconnectClient(player.OwnerClientId, "Você foi removido da sala pelo Host.");
        }

        // ---------------------------------------------------------------- state

        private void OnStatus(Combat.NetSession.Status status)
        {
            string message = NetSession.Instance.Message;
            switch (status)
            {
                case NetSession.Status.Connecting:
                    SetJoinStatus(message, false);
                    break;
                case NetSession.Status.Connected:
                    SetJoinStatus(message, false);
                    Go(Page.Lobby);
                    break;
                case NetSession.Status.Failed:
                    Log(message);
                    // the session ended mid-match: back to the menu, and say why there
                    if (GameFlow.Instance != null && GameFlow.Instance.Current != GameFlow.State.Menu)
                    {
                        PendingMessage = message;
                        GameFlow.Instance.ReturnToMenu();
                        return;
                    }

                    SetJoinStatus(message, true);
                    if (_root.activeSelf && _page == Page.Lobby) Go(Page.Join);
                    break;
                case NetSession.Status.Offline:
                    if (!string.IsNullOrEmpty(message)) Log(message);
                    break;
            }
        }

        private void SetJoinStatus(string text, bool error)
        {
            if (_joinStatus == null) return;
            _joinStatus.text = text;
            _joinStatus.color = error ? new Color(1.0f, 0.5f, 0.42f) : new Color(0.6f, 1.0f, 0.7f);
        }

        public void Log(string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            _log.Add($"<color=#FFFFFF60>{DateTime.Now:HH:mm}</color>  {text}");
            if (_log.Count > 6) _log.RemoveAt(0);
            if (_lobbyLog != null) _lobbyLog.text = string.Join("\n", _log);
        }

        // time spent here, for the performance probe (Diagnostics/PerfProbe)
        private static readonly Unity.Profiling.ProfilerMarker UpdateMarker = new Unity.Profiling.ProfilerMarker("Combat.NetMenuView.Update");

        private void Update()
        {
            using (UpdateMarker.Auto()) UpdateMarked();
        }

        private void UpdateMarked()
        {
            if (!_root.activeSelf || _page != Page.Lobby) return;
            NetworkManager manager = NetworkManager.Singleton;
            NetGame game = NetGame.Instance;
            bool host = manager != null && manager.IsServer;

            _lobbyTitle.text = game != null ? game.Room.Value.ToString() : "Conectando…";
            _lobbySettings.text = game != null
                ? $"Modo Infinito  ·  {GameDifficulty.Get((Difficulty)game.DifficultyLevel.Value).Name}  ·  {NetPlayer.All.Count}/{game.MaxPlayers.Value} jogadores"
                : "";
            _diffLeft.gameObject.SetActive(host);
            _diffRight.gameObject.SetActive(host);
            _lobbyDifficulty.text = host && game != null ? GameDifficulty.Get((Difficulty)game.DifficultyLevel.Value).Name : "";

            var transport = manager != null ? manager.GetComponent<UnityTransport>() : null;
            for (int i = 0; i < _slots.Count; i++)
            {
                var slot = _slots[i];
                if (i >= NetPlayer.All.Count)
                {
                    slot.name.text = "<color=#FFFFFF40>Vago</color>";
                    slot.state.text = "";
                    slot.kick.gameObject.SetActive(false);
                    continue;
                }

                NetPlayer player = NetPlayer.All[i];
                string you = player.IsOwner ? "  <color=#FFC74D>(você)</color>" : "";
                string hostTag = player.IsOwnedByServer ? "  <color=#66CCFF>HOST</color>" : "";
                slot.name.text = player.DisplayName + hostTag + you;
                string ready = player.IsOwnedByServer || player.Ready.Value ? "<color=#66FF88>Pronto</color>" : "<color=#FFB060>Aguardando</color>";
                string ping = "";
                if (transport != null && host && !player.IsOwnedByServer) ping = $"  ·  {transport.GetCurrentRtt(player.OwnerClientId)} ms";
                if (transport != null && !host && player.IsOwnedByServer) ping = $"  ·  {transport.GetCurrentRtt(NetworkManager.ServerClientId)} ms";
                slot.state.text = $"{ready}  ·  {OutfitText(player.Outfit.Value)}{ping}";
                slot.kick.gameObject.SetActive(host && !player.IsOwnedByServer);
            }

            _start.gameObject.SetActive(host);
            _ready.gameObject.SetActive(!host);
            if (host && game != null)
            {
                bool can = game.CanStart(out string reason);
                _start.interactable = can;
                _startHint.text = can ? (NetPlayer.All.Count < 2 ? "Você pode começar sozinho ou esperar os amigos." : "") : reason;
            }
            else
            {
                _startHint.text = "Aguardando o Host iniciar a partida.";
                if (NetPlayer.Local != null) UiKit.SetButtonText(_ready, NetPlayer.Local.Ready.Value ? "PRONTO ✓" : "PRONTO");
            }
        }

        private static string OutfitText(int packed)
        {
            int[] choices = NetPlayer.UnpackOutfit(packed);
            return $"traje {choices[0] + 1}-{choices[1] + 1}-{choices[4]}";
        }

        private void RefreshAddresses()
        {
            NetworkManager manager = NetworkManager.Singleton;
            if (manager == null || !manager.IsServer)
            {
                _lobbyAddresses.text = "";
                return;
            }

            var lines = new List<string> { "<color=#FFC74D><b>PASSE ESTE IP AOS AMIGOS</b></color>" };
            try
            {
                foreach (NetworkInterface adapter in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (adapter.OperationalStatus != OperationalStatus.Up || adapter.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
                    foreach (UnicastIPAddressInformation address in adapter.GetIPProperties().UnicastAddresses)
                    {
                        if (address.Address.AddressFamily != AddressFamily.InterNetwork) continue;
                        string kind = adapter.Description.IndexOf("Radmin", StringComparison.OrdinalIgnoreCase) >= 0 || adapter.Name.IndexOf("Radmin", StringComparison.OrdinalIgnoreCase) >= 0 ? "Radmin VPN"
                            : adapter.Description.IndexOf("Hamachi", StringComparison.OrdinalIgnoreCase) >= 0 ? "Hamachi" : "rede local";
                        lines.Add($"<b>{address.Address}</b>  <color=#FFFFFF80>{kind} ({adapter.Name})</color>");
                    }
                }
            }
            catch (NetworkInformationException)
            {
                lines.Add("Não foi possível listar os endereços desta máquina.");
            }

            var transport = manager.GetComponent<UnityTransport>();
            lines.Add($"Porta UDP: <b>{transport.ConnectionData.Port}</b>");
            _lobbyAddresses.text = string.Join("\n", lines);
        }
    }
}
