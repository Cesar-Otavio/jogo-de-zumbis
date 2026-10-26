using System.Collections.Generic;
using TMPro;
using Unity.Cinemachine;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Combat
{
    /* The co-op layer of the HUD, only while a match is on:
     *   team     - each teammate's name, health, distance, ping and condition (down with the clock, dead)
     *   link     - this machine's own round trip to the host
     *   down     - "you're down" with the bleed-out clock and the progress of whoever is reviving you
     *   revive   - holding the interact key next to a downed teammate fills a bar; letting go or walking
     *              off stops it (the host checks the result: NetPlayer)
     *   spectate - dead until the next wave: the camera follows a living teammate, a click moves to the
     *              next one; nothing can be done from there
     */
    public class TeamHUD : MonoBehaviour
    {
        public static TeamHUD Instance { get; private set; }

        private const int MaxRows = 3;

        private RectTransform _root;
        private readonly List<(Image background, TextMeshProUGUI name, Image healthFill, TextMeshProUGUI info)> _rows = new List<(Image, TextMeshProUGUI, Image, TextMeshProUGUI)>();
        private TextMeshProUGUI _link;
        private TextMeshProUGUI _status;
        private Image _barBack;
        private Image _barFill;

        private PlayerCombat _player;
        private NetPlayer _reviving;
        private float _reviveProgress;
        private float _reviveStarted;
        private InputAction _interact;

        private NetPuppet _watching;
        private CinemachineCamera _camera;
        private Transform _ownTarget;
        private readonly List<NetPuppet> _living = new List<NetPuppet>();

        public static void Ensure()
        {
            if (Instance == null) new GameObject("TeamHUD").AddComponent<TeamHUD>();
        }

        private void Awake()
        {
            Instance = this;
            Build();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Build()
        {
            // under the menus (110) and above the game HUD
            _root = UiKit.Canvas("TeamHUD", transform, 60);

            for (int i = 0; i < MaxRows; i++)
            {
                Image background = UiKit.Box("Teammate" + i, _root, new Vector2(0.0f, 0.5f), new Vector2(170.0f, 120.0f - i * 62.0f), new Vector2(300.0f, 54.0f), new Color(0.05f, 0.06f, 0.08f, 0.72f));
                TextMeshProUGUI name = UiKit.Text(background.rectTransform, "", 20, new Vector2(0.04f, 0.45f), new Vector2(0.96f, 1.0f), TextAlignmentOptions.MidlineLeft, Color.white, true);
                Image healthBack = UiKit.Panel("HealthBack", background.rectTransform, new Vector2(0.04f, 0.14f), new Vector2(0.55f, 0.36f), new Color(1, 1, 1, 0.15f));
                Image healthFill = UiKit.Panel("Health", healthBack.rectTransform, Vector2.zero, Vector2.one, new Color(0.4f, 0.85f, 0.45f));
                TextMeshProUGUI info = UiKit.Text(background.rectTransform, "", 16, new Vector2(0.58f, 0.0f), new Vector2(0.97f, 0.5f), TextAlignmentOptions.MidlineRight, UiKit.Muted);
                _rows.Add((background, name, healthFill, info));
            }

            _link = UiKit.Text(_root, "", 16, new Vector2(0.01f, 0.955f), new Vector2(0.3f, 0.995f), TextAlignmentOptions.TopLeft, UiKit.Muted);
            // above the middle of the screen, clear of the name over a followed teammate
            _status = UiKit.Text(_root, "", 30, new Vector2(0.25f, 0.74f), new Vector2(0.75f, 0.86f), TextAlignmentOptions.Center, Color.white, true);
            _barBack = UiKit.Box("ReviveBar", _root, new Vector2(0.5f, 0.5f), new Vector2(0.0f, 225.0f), new Vector2(420.0f, 14.0f), new Color(1, 1, 1, 0.18f));
            _barFill = UiKit.Panel("Fill", _barBack.rectTransform, Vector2.zero, Vector2.one, UiKit.Accent);
        }

        // time spent here, for the performance probe (Diagnostics/PerfProbe)
        private static readonly Unity.Profiling.ProfilerMarker UpdateMarker = new Unity.Profiling.ProfilerMarker("Combat.TeamHUD.Update");

        private void Update()
        {
            using (UpdateMarker.Auto()) UpdateMarked();
        }

        private void UpdateMarked()
        {
            bool on = Net.Active && GameFlow.Instance != null && GameFlow.Instance.Current != GameFlow.State.Menu && GameFlow.Instance.Current != GameFlow.State.GameOver;
            if (_root.gameObject.activeSelf != on) _root.gameObject.SetActive(on);
            if (!on)
            {
                StopSpectating();
                return;
            }

            if (_player == null) _player = FindAnyObjectByType<PlayerCombat>();
            PlayerCombat player = _player;
            NetPlayer me = NetPlayer.Local;
            if (player == null || me == null) return;

            UpdateTeam(player);
            UpdateLink(me);

            float bar = -1.0f;
            string status = null;
            if (me.Condition.Value == CoopCondition.Downed)
            {
                status = $"<color=#FF6A5A>VOCÊ CAIU</color>\n<size=70%>Um aliado pode te reanimar · {Mathf.CeilToInt(me.BleedOutLeft)} s</size>";
                if (me.Reviver.Value != NetPlayer.NoReviver)
                {
                    NetPlayer helper = NetPlayer.For(me.Reviver.Value);
                    status = $"<color=#7CE08A>{(helper != null ? helper.DisplayName : "Aliado")} está te reanimando</color>";
                    bar = me.ReviveProgress;
                }
            }

            UpdateRevive(player, me, ref status, ref bar);
            UpdateSpectating(player, me, ref status);

            _status.gameObject.SetActive(status != null);
            if (status != null) _status.text = status;
            _barBack.gameObject.SetActive(bar >= 0.0f);
            if (bar >= 0.0f) _barFill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(bar), 1.0f);
        }

        // ---------------------------------------------------------------- team list

        private void UpdateTeam(PlayerCombat player)
        {
            int row = 0;
            foreach (NetPlayer mate in NetPlayer.All)
            {
                if (mate.IsOwner || row >= MaxRows) continue;
                var (background, name, fill, info) = _rows[row++];
                background.gameObject.SetActive(true);
                PlayerState state = mate.State.Value;
                float health = state.MaxHealth > 0.0f ? Mathf.Clamp01(state.Health / state.MaxHealth) : 0.0f;
                float distance = Vector3.Distance(player.transform.position, mate.Position);
                name.text = mate.DisplayName;
                fill.rectTransform.anchorMax = new Vector2(health, 1.0f);
                switch (mate.Condition.Value)
                {
                    case CoopCondition.Downed:
                        fill.color = new Color(1.0f, 0.42f, 0.35f);
                        info.text = $"<color=#FF6A5A>CAÍDO {Mathf.CeilToInt(mate.BleedOutLeft)} s</color> · {distance:F0} m";
                        break;
                    case CoopCondition.Dead:
                        fill.rectTransform.anchorMax = new Vector2(0.0f, 1.0f);
                        info.text = "<color=#FF6A5A>MORTO</color>";
                        break;
                    default:
                        fill.color = health < 0.3f ? new Color(1.0f, 0.65f, 0.3f) : new Color(0.4f, 0.85f, 0.45f);
                        info.text = $"{distance:F0} m · {mate.Ping.Value} ms";
                        break;
                }
            }

            for (; row < MaxRows; row++) _rows[row].background.gameObject.SetActive(false);
        }

        private void UpdateLink(NetPlayer me)
        {
            if (NetworkManager.Singleton.IsServer)
            {
                _link.text = $"HOST · {NetPlayer.All.Count} jogador{(NetPlayer.All.Count == 1 ? "" : "es")}";
                return;
            }

            int ping = me.Ping.Value;
            string color = ping < 80 ? "7CE08A" : ping < 160 ? "FFC74D" : "FF6A5A";
            _link.text = $"Conexão <color=#{color}>{ping} ms</color>";
        }

        // ---------------------------------------------------------------- reviving a teammate

        // the interact key was pressed on a downed teammate (NetPuppet); holding it does the rest
        public void StartRevive(NetPlayer target)
        {
            NetPlayer me = NetPlayer.Local;
            if (me == null || target == null || !me.IsUp || target.Condition.Value != CoopCondition.Downed) return;
            if (target.Reviver.Value != NetPlayer.NoReviver && target.Reviver.Value != me.OwnerClientId) return;
            _reviving = target;
            _reviveProgress = 0.0f;
            _reviveStarted = Time.unscaledTime;
            me.BeginRevive(target);
        }

        private void UpdateRevive(PlayerCombat player, NetPlayer me, ref string status, ref float bar)
        {
            if (_reviving == null) return;

            if (_interact == null)
            {
                var input = player.GetComponent<PlayerInput>();
                _interact = input != null ? input.actions.FindAction("Interact") : null;
            }

            NetPuppet puppet = _reviving.Puppet;
            bool held = _interact != null && _interact.IsPressed();
            bool near = puppet != null && Vector3.Distance(player.transform.position, puppet.InteractionPoint) <= NetPlayer.ReviveRange + 0.3f;
            bool stillDown = _reviving.Condition.Value == CoopCondition.Downed;
            // the host gives the revive to one teammate at a time
            bool ours = _reviving.Reviver.Value == me.OwnerClientId || Time.unscaledTime - _reviveStarted < 0.6f;
            if (!held || !near || !stillDown || !me.IsUp || player.Health.IsDead || !ours)
            {
                if (stillDown) me.CancelRevive(_reviving);
                _reviving = null;
                return;
            }

            _reviveProgress += Time.unscaledDeltaTime / NetPlayer.ReviveTime;
            status = $"Reanimando {_reviving.DisplayName}…";
            bar = _reviveProgress;
            if (_reviveProgress >= 1.0f)
            {
                me.FinishRevive(_reviving);
                _reviving = null;
            }
        }

        // ---------------------------------------------------------------- watching while dead

        private void UpdateSpectating(PlayerCombat player, NetPlayer me, ref string status)
        {
            bool spectate = me.Condition.Value == CoopCondition.Dead && player.Health.IsDead;
            if (!spectate)
            {
                StopSpectating();
                return;
            }

            _living.Clear();
            foreach (NetPlayer mate in NetPlayer.All)
                if (!mate.IsOwner && mate.IsUp && mate.Puppet != null) _living.Add(mate.Puppet);

            bool next = (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame) ||
                        (Gamepad.current != null && Gamepad.current.rightTrigger.wasPressedThisFrame);
            if (_watching == null || !_living.Contains(_watching) || next)
            {
                int index = _watching != null ? _living.IndexOf(_watching) : -1;
                _watching = _living.Count > 0 ? _living[(index + 1) % _living.Count] : null;
            }

            Watch(_watching != null ? _watching.CameraRoot : null);
            status = _watching != null
                ? $"<size=80%>ESPECTADOR · seguindo <color=#FFC74D>{_watching.Owner.DisplayName}</color></size>\n<size=55%>[clique] próximo aliado · você volta na próxima onda</size>"
                : "<size=80%>Ninguém de pé para acompanhar</size>";
        }

        private void Watch(Transform target)
        {
            if (_camera == null) _camera = FindAnyObjectByType<CinemachineCamera>();
            if (_camera == null) return;
            if (_ownTarget == null) _ownTarget = _camera.Follow;
            Transform wanted = target != null ? target : _ownTarget;
            if (_camera.Follow != wanted) _camera.Follow = wanted;
        }

        private void StopSpectating()
        {
            _watching = null;
            if (_camera != null && _ownTarget != null && _camera.Follow != _ownTarget) _camera.Follow = _ownTarget;
            _ownTarget = null;
        }
    }
}
