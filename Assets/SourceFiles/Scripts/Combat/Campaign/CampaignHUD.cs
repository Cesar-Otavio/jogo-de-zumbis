using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Combat
{
    /* The interface for missions and survival, next to the combat HUD and in its style:
     * the mission tracker, one objective marker with distance (can be turned off), credits,
     * the quick item bar, the break between waves, boss health and warnings, radio messages,
     * the document reader, the choice prompt and progression toasts.
     */
    [DefaultExecutionOrder(210)]
    public class CampaignHUD : MonoBehaviour
    {
        public static bool ReaderOpen { get; private set; }

        private RectTransform _root;
        private GameObject _canvas;
        private Camera _camera;
        private PlayerCombat _player;
        private InventoryManager _inventory;
        private EnemySpawner _spawner;

        private TextMeshProUGUI _tracker;
        private RectTransform _marker;
        private TextMeshProUGUI _markerText;
        private TextMeshProUGUI _credits;
        private TextMeshProUGUI _creditsPopup;
        private float _creditsPopupTime = -10.0f;
        private readonly Image[] _slots = new Image[6];
        private readonly TextMeshProUGUI[] _slotTexts = new TextMeshProUGUI[6];
        private readonly Image[] _slotIcons = new Image[6];
        private readonly TextMeshProUGUI[] _slotKeys = new TextMeshProUGUI[6];
        private TextMeshProUGUI _intermission;
        private GameObject _bossBar;
        private Image _bossFill;
        private TextMeshProUGUI _bossName;
        private TextMeshProUGUI _bossWarning;
        private float _bossWarningTime = -10.0f;
        private GameObject _radio;
        private TextMeshProUGUI _radioText;
        private string _radioFull;
        private float _radioStarted = -100.0f;
        private GameObject _reader;
        private TextMeshProUGUI _readerTitle;
        private TextMeshProUGUI _readerBody;
        private float _timeScaleBeforeReader = 1.0f;
        private GameObject _choice;
        private TextMeshProUGUI _choiceText;
        private TextMeshProUGUI _toast;
        private readonly Queue<string> _toasts = new Queue<string>();
        private float _toastStarted = -10.0f;

        private void Start()
        {
            _camera = Camera.main;
            _player = FindAnyObjectByType<PlayerCombat>();
            _inventory = _player != null ? _player.GetComponent<InventoryManager>() : null;
            _spawner = FindAnyObjectByType<EnemySpawner>();
            Build();

            RunState.CreditsChanged += OnCredits;
            Story.RadioReceived += OnRadio;
            Story.DocumentOpened += OnDocument;
            BossAI.Warning += OnBossWarning;
            Progression.AchievementUnlocked += a => Toast($"<color=#FFC74D>CONQUISTA</color>  {a.Title}");
            Progression.LevelUp += level => Toast($"<color=#7FE0FF>NÍVEL {level}</color>  novos itens podem ter sido desbloqueados");
        }

        private void OnDestroy()
        {
            RunState.CreditsChanged -= OnCredits;
            Story.RadioReceived -= OnRadio;
            Story.DocumentOpened -= OnDocument;
            BossAI.Warning -= OnBossWarning;
            if (ReaderOpen) Time.timeScale = 1.0f;
            ReaderOpen = false;
        }

        private void Build()
        {
            _root = UiKit.Canvas("CampaignHUD", transform, 60);
            _canvas = _root.gameObject;

            _tracker = UiKit.Text(_root, "", 22, new Vector2(0.02f, 0.5f), new Vector2(0.4f, 0.9f), TextAlignmentOptions.TopLeft, Color.white);
            _tracker.textWrappingMode = TextWrappingModes.Normal;

            _marker = UiKit.Box("Marker", _root, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(18.0f, 18.0f), UiKit.Accent).rectTransform;
            _marker.localRotation = Quaternion.Euler(0.0f, 0.0f, 45.0f);
            var markerLabel = new GameObject("MarkerLabel", typeof(RectTransform)).GetComponent<RectTransform>();
            markerLabel.SetParent(_root, false);
            _markerText = UiKit.Text(markerLabel, "", 18, Vector2.zero, Vector2.one, TextAlignmentOptions.Center, UiKit.Accent, true);
            markerLabel.sizeDelta = new Vector2(320.0f, 50.0f);
            markerLabel.anchorMin = markerLabel.anchorMax = new Vector2(0.5f, 0.5f);

            _credits = UiKit.Text(_root, "", 26, new Vector2(0.7f, 0.86f), new Vector2(0.969f, 0.9f), TextAlignmentOptions.Right, UiKit.Accent);
            _creditsPopup = UiKit.Text(_root, "", 22, new Vector2(0.7f, 0.82f), new Vector2(0.969f, 0.86f), TextAlignmentOptions.Right, UiKit.Accent);

            // quick bar, above the health bar: icon, key and count per slot
            for (int i = 0; i < 6; i++)
            {
                Image slot = UiKit.Box("Slot", _root, new Vector2(0.0f, 0.0f), new Vector2(92.0f + i * 66.0f, 215.0f), new Vector2(60.0f, 60.0f), new Color(0, 0, 0, 0.45f));
                _slots[i] = slot;
                var icon = new GameObject("Icon", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
                icon.rectTransform.SetParent(slot.rectTransform, false);
                icon.rectTransform.anchorMin = icon.rectTransform.anchorMax = new Vector2(0.5f, 0.56f);
                icon.rectTransform.sizeDelta = new Vector2(38.0f, 38.0f);
                icon.preserveAspect = true;
                icon.raycastTarget = false;
                icon.sprite = ItemCatalog.IconOf((QuickItem)i);
                _slotIcons[i] = icon;
                _slotKeys[i] = UiKit.Text(slot.rectTransform, (i + 1).ToString(), 13, new Vector2(0.06f, 0.62f), new Vector2(0.5f, 0.98f), TextAlignmentOptions.TopLeft, UiKit.Muted, true);
                _slotTexts[i] = UiKit.Text(slot.rectTransform, "", 16, new Vector2(0.4f, 0.0f), new Vector2(0.94f, 0.36f), TextAlignmentOptions.BottomRight, Color.white, true);
            }

            gameObject.AddComponent<PickupFeedView>().Build(_root);

            _intermission = UiKit.Text(_root, "", 24, new Vector2(0.25f, 0.8f), new Vector2(0.75f, 0.85f), TextAlignmentOptions.Center, UiKit.Accent, true);

            Image bossBack = UiKit.Box("BossBar", _root, new Vector2(0.5f, 1.0f), new Vector2(0.0f, -195.0f), new Vector2(640.0f, 14.0f), new Color(0, 0, 0, 0.6f));
            _bossBar = bossBack.gameObject;
            _bossFill = UiKit.Panel("Fill", bossBack.rectTransform, Vector2.zero, Vector2.one, new Color(0.85f, 0.15f, 0.12f));
            _bossFill.rectTransform.pivot = new Vector2(0.0f, 0.5f);
            _bossName = UiKit.Text(bossBack.rectTransform, "", 22, new Vector2(0.0f, 1.2f), new Vector2(1.0f, 3.2f), TextAlignmentOptions.Center, Color.white, true);
            _bossWarning = UiKit.Text(_root, "", 34, new Vector2(0.2f, 0.6f), new Vector2(0.8f, 0.68f), TextAlignmentOptions.Center, UiKit.Danger, true);
            _bossBar.SetActive(false);

            Image radio = UiKit.Panel("Radio", _root, new Vector2(0.3f, 0.66f), new Vector2(0.7f, 0.76f), new Color(0.02f, 0.05f, 0.03f, 0.75f));
            _radio = radio.gameObject;
            UiKit.Text(radio.rectTransform, "RÁDIO", 16, new Vector2(0.02f, 0.7f), new Vector2(0.3f, 0.98f), TextAlignmentOptions.TopLeft, new Color(0.5f, 1.0f, 0.6f), true);
            _radioText = UiKit.Text(radio.rectTransform, "", 21, new Vector2(0.02f, 0.05f), new Vector2(0.98f, 0.72f), TextAlignmentOptions.TopLeft, new Color(0.8f, 1.0f, 0.85f));
            _radioText.textWrappingMode = TextWrappingModes.Normal;
            _radio.SetActive(false);

            Image choice = UiKit.Panel("Choice", _root, new Vector2(0.2f, 0.3f), new Vector2(0.8f, 0.5f), UiKit.PanelColor);
            _choice = choice.gameObject;
            _choiceText = UiKit.Text(choice.rectTransform, "", 22, new Vector2(0.03f, 0.05f), new Vector2(0.97f, 0.95f), TextAlignmentOptions.TopLeft, Color.white);
            _choiceText.textWrappingMode = TextWrappingModes.Normal;
            _choice.SetActive(false);

            _toast = UiKit.Text(_root, "", 26, new Vector2(0.25f, 0.9f), new Vector2(0.75f, 0.95f), TextAlignmentOptions.Center, Color.white, true);

            // the document reader on its own canvas above everything, the game paused behind it
            RectTransform readerRoot = UiKit.Canvas("Reader", transform, 130);
            Image reader = UiKit.Panel("Document", readerRoot, new Vector2(0.25f, 0.12f), new Vector2(0.75f, 0.88f), new Color(0.86f, 0.82f, 0.72f, 0.97f));
            _readerTitle = UiKit.Text(reader.rectTransform, "", 34, new Vector2(0.06f, 0.84f), new Vector2(0.94f, 0.96f), TextAlignmentOptions.TopLeft, new Color(0.15f, 0.1f, 0.05f), true);
            _readerTitle.outlineWidth = 0.0f;
            _readerBody = UiKit.Text(reader.rectTransform, "", 24, new Vector2(0.06f, 0.1f), new Vector2(0.94f, 0.82f), TextAlignmentOptions.TopLeft, new Color(0.12f, 0.1f, 0.08f));
            _readerBody.outlineWidth = 0.0f;
            _readerBody.textWrappingMode = TextWrappingModes.Normal;
            TextMeshProUGUI close = UiKit.Text(reader.rectTransform, "[E] ou [Esc] fechar", 20, new Vector2(0.06f, 0.02f), new Vector2(0.94f, 0.08f), TextAlignmentOptions.Right, new Color(0.3f, 0.25f, 0.2f));
            close.outlineWidth = 0.0f;
            _reader = readerRoot.gameObject;
            _reader.SetActive(false);
        }

        // ---------------------------------------------------------------- events

        private void OnCredits(int amount, string reason)
        {
            if (amount <= 0) return;
            _creditsPopup.text = string.IsNullOrEmpty(reason) ? $"+{amount} CR" : $"+{amount} CR  <size=75%>{reason}</size>";
            _creditsPopupTime = Time.time;
        }

        private void OnRadio(string id, string text)
        {
            _radioFull = text;
            _radioStarted = Time.time;
            _radio.SetActive(true);
            CampaignAssets.PlayUI(a => a.RadioBlip, 0.6f);
        }

        private void OnDocument(string id, Story.Entry entry)
        {
            _readerTitle.text = entry.Title;
            _readerBody.text = $"<i>{entry.Author}</i>\n\n{entry.Text}";
            _reader.SetActive(true);
            ReaderOpen = true;
            _timeScaleBeforeReader = Time.timeScale > 0.0f ? Time.timeScale : 1.0f;
            Time.timeScale = 0.0f;
        }

        private void CloseReader()
        {
            _reader.SetActive(false);
            ReaderOpen = false;
            Time.timeScale = _timeScaleBeforeReader;
        }

        private void OnBossWarning(BossAI boss, string text)
        {
            _bossWarning.text = text;
            _bossWarningTime = Time.time;
        }

        public void Toast(string text)
        {
            _toasts.Enqueue(text);
        }

        // ---------------------------------------------------------------- update

        private void Update()
        {
            if (ReaderOpen && Keyboard.current != null && (Keyboard.current.eKey.wasPressedThisFrame || Keyboard.current.escapeKey.wasPressedThisFrame)) CloseReader();

            GameFlow flow = GameFlow.Instance;
            bool show = flow != null && flow.Current != GameFlow.State.Menu && flow.Current != GameFlow.State.GameOver && !CinematicPlayer.Playing && _player != null;
            _canvas.SetActive(show);
            if (!show) return;

            UpdateTracker();
            UpdateMarker();
            UpdateQuickBar();
            UpdateIntermission();
            UpdateBoss();
            UpdateRadio();
            UpdateChoice();
            UpdateToasts();

            _credits.text = $"CRÉDITOS  <b>{RunState.Credits}</b>";
            _creditsPopup.alpha = Mathf.Clamp01(1.0f - (Time.time - _creditsPopupTime - 1.5f));
        }

        private void UpdateTracker()
        {
            MissionManager missions = MissionManager.Instance;
            if (missions == null || missions.Active.Count == 0)
            {
                _tracker.text = "";
                return;
            }

            var text = new StringBuilder();
            foreach (MissionRun run in missions.Active)
            {
                text.Append(run.Optional ? "<color=#BBBBBB>OPCIONAL · </color>" : "<color=#FFC74D>MISSÃO · </color>");
                text.Append("<b>").Append(run.Definition.Title).Append("</b>");
                if (run.TimeLeft >= 0.0f) text.Append($"  <color=#FF8866>{Mathf.CeilToInt(run.TimeLeft)}s</color>");
                text.Append('\n');
                ObjectiveDefinition[] objectives = run.Definition.Objectives;
                for (int i = 0; i < objectives.Length; i++)
                {
                    ObjectiveDefinition o = objectives[i];
                    bool current = i == run.Current;
                    if (!current && !o.Optional && !run.Done[i]) continue;
                    if (run.Done[i] && !o.Optional) continue;
                    string mark = run.Done[i] ? "<color=#9BE39B>[x]</color>" : current ? "<color=#FFC74D>></color>" : "·";
                    string color = o.Optional ? "#AAAAAA" : "#FFFFFF";
                    text.Append($"  {mark} <color={color}>{o.Text}{MissionManager.ProgressText(run, i)}</color>");
                    if (o.Optional) text.Append(" <size=80%>(bônus)</size>");
                    text.Append('\n');
                }

                if (run.Optional) text.Append("  <size=75%><color=#888888>[K] abandonar</color></size>\n");
                text.Append('\n');
            }

            _tracker.text = text.ToString();

            // K abandons the side mission
            if (Keyboard.current != null && Keyboard.current.kKey.wasPressedThisFrame)
            {
                foreach (MissionRun run in missions.Active.ToArray())
                {
                    if (run.Optional)
                    {
                        missions.Abandon(run);
                        break;
                    }
                }
            }
        }

        private void UpdateMarker()
        {
            MissionManager missions = MissionManager.Instance;
            bool show = GameSettings.ObjectiveHints && missions != null && missions.TryGetMarker(out Vector3 target, out string label);
            _marker.gameObject.SetActive(show);
            _markerText.transform.parent.gameObject.SetActive(show);
            if (!show) return;

            missions.TryGetMarker(out target, out label);
            Vector3 aim = target + Vector3.up * 1.2f;
            Vector3 view = _camera.WorldToViewportPoint(aim);
            float distance = Vector3.Distance(_player.transform.position, target);
            // behind the camera: flip so the marker clamps to the right edge of the screen
            if (view.z < 0.0f)
            {
                view.x = 1.0f - view.x;
                view.y = 0.0f;
            }

            bool onScreen = view.z > 0.0f && view.x > 0.05f && view.x < 0.95f && view.y > 0.08f && view.y < 0.92f;
            Vector2 canvas = _root.sizeDelta;
            Vector2 position = new Vector2((view.x - 0.5f) * canvas.x, (view.y - 0.5f) * canvas.y);
            if (!onScreen)
            {
                // off screen: ride an ellipse around the crosshair, clear of the corner panels (tracker, quick bar, score)
                Vector2 dir = new Vector2(view.x - 0.5f, view.y - 0.5f);
                if (dir.sqrMagnitude < 0.0001f) dir = Vector2.down;
                dir.Normalize();
                position = new Vector2(dir.x * 0.36f * canvas.x, dir.y * 0.3f * canvas.y);
            }
            _marker.anchoredPosition = position;
            var labelRect = (RectTransform)_markerText.transform.parent;
            labelRect.anchoredPosition = position + new Vector2(0.0f, 30.0f);
            _markerText.text = onScreen ? $"{label}  {Mathf.RoundToInt(distance)} m" : $"{Mathf.RoundToInt(distance)} m";
            // closer and in view: smaller and quieter, to not clutter the screen
            float alpha = distance < 4.0f ? 0.3f : onScreen ? 0.85f : 1.0f;
            _marker.GetComponent<Image>().color = new Color(UiKit.Accent.r, UiKit.Accent.g, UiKit.Accent.b, alpha);
            _markerText.alpha = alpha;
        }

        private void UpdateQuickBar()
        {
            if (_inventory == null) return;
            for (int i = 0; i < 6; i++)
            {
                var item = (QuickItem)i;
                int count = _inventory.Count(item);
                bool selected = _inventory.Selected == item;
                _slots[i].color = selected ? new Color(1.0f, 0.78f, 0.3f, 0.32f) : new Color(0.05f, 0.06f, 0.07f, 0.55f);
                _slotTexts[i].text = count.ToString();
                _slotTexts[i].alpha = count > 0 ? 1.0f : 0.35f;
                // without the item the icon goes grey and faint, still showing what the slot is for
                _slotIcons[i].color = count > 0 ? (selected ? UiKit.Accent : Color.white) : new Color(1.0f, 1.0f, 1.0f, 0.22f);
                _slotKeys[i].color = selected ? UiKit.Accent : UiKit.Muted;
            }
        }

        private void UpdateIntermission()
        {
            CampaignManager campaign = CampaignManager.Instance;
            bool campaignBreak = campaign != null && campaign.IsRunning && campaign.InIntermission;
            bool endlessBreak = RunState.Mode == GameMode.Endless && _spawner != null && _spawner.Running && _spawner.InIntermission;
            if (!campaignBreak && !endlessBreak)
            {
                _intermission.text = "";
                return;
            }

            bool waiting = campaignBreak ? campaign.WaitingForShop : _spawner.WaitingForShop;
            float left = campaignBreak ? campaign.IntermissionLeft : _spawner.IntermissionLeft;
            _intermission.text = waiting
                ? "A próxima onda começa quando você sair da loja"
                : $"INTERVALO {Mathf.CeilToInt(left)}s   <size=80%><color=#FFFFFF>[L] loja   [N] próxima onda agora</color></size>";

            if (endlessBreak && Keyboard.current != null && Keyboard.current.nKey.wasPressedThisFrame && !ShopManager.IsOpen) _spawner.SkipIntermission();
        }

        private void UpdateBoss()
        {
            BossAI boss = null;
            foreach (BossAI active in BossAI.Active)
            {
                if (active != null && !active.Health.IsDead) boss = active;
            }

            _bossBar.SetActive(boss != null);
            if (boss != null)
            {
                _bossFill.rectTransform.anchorMax = new Vector2(boss.Health.Normalized, 1.0f);
                _bossFill.color = boss.Vulnerable ? new Color(1.0f, 0.75f, 0.2f) : boss.Lit ? new Color(1.0f, 0.95f, 0.6f) : new Color(0.85f, 0.15f, 0.12f);
                string state = boss.Vulnerable ? "  <color=#FFC74D>VULNERÁVEL</color>" : boss.Lit ? "  <color=#FFF2A0>EXPOSTA À LUZ</color>" : "";
                _bossName.text = boss.Definition.DisplayName.ToUpperInvariant() + state;
            }

            float age = Time.time - _bossWarningTime;
            _bossWarning.alpha = age < 1.6f ? 1.0f : Mathf.Clamp01(1.0f - (age - 1.6f) * 2.0f);
        }

        private void UpdateRadio()
        {
            if (!_radio.activeSelf) return;
            float age = Time.time - _radioStarted;
            int shown = Mathf.Min(_radioFull.Length, Mathf.FloorToInt(age * 40.0f));
            _radioText.text = _radioFull.Substring(0, shown);
            if (age > _radioFull.Length / 40.0f + 6.0f) _radio.SetActive(false);
        }

        private void UpdateChoice()
        {
            MissionManager missions = MissionManager.Instance;
            (MissionDefinition a, MissionDefinition b) = missions != null ? missions.PendingChoice : default;
            _choice.SetActive(a != null);
            if (a == null) return;
            _choiceText.text = "<color=#FFC74D><b>ESCOLHA</b></color>  <size=80%>(as duas não dá: a outra se perde)</size>\n\n" +
                               $"<b>[1] {a.Title}</b>\n<size=85%>{a.ChoiceSummary}</size>\n\n" +
                               $"<b>[2] {b.Title}</b>\n<size=85%>{b.ChoiceSummary}</size>";
        }

        private void UpdateToasts()
        {
            float age = Time.time - _toastStarted;
            if (age > 3.0f && _toasts.Count > 0)
            {
                _toast.text = _toasts.Dequeue();
                _toastStarted = Time.time;
                CampaignAssets.PlayUI(a => a.Achievement, 0.6f);
                age = 0.0f;
            }

            _toast.alpha = age < 2.4f ? 1.0f : Mathf.Clamp01(1.0f - (age - 2.4f) * 2.0f);
        }
    }
}
