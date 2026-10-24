using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Combat
{
    // Builds and drives the combat UI in code, so it needs no prefab
    [DefaultExecutionOrder(200)]
    public class CombatHUD : MonoBehaviour
    {
        public PlayerCombat Player;

        private static readonly Color Accent = new Color(1.0f, 0.78f, 0.3f);
        private static readonly Color Danger = new Color(1.0f, 0.25f, 0.2f);

        private Camera _camera;
        private RectTransform _root;

        private RectTransform[] _crossBars;
        private Image[] _crossImages;
        private Image _crossDot;
        private float _crossGap;

        private RectTransform _hitMarker;
        private Image[] _hitImages;
        private float _hitTime = -10.0f;
        private Color _hitColor;
        private float _hitScale;

        private static readonly Color CardColor = new Color(0.055f, 0.06f, 0.07f, 0.58f);
        private GameObject _weaponCard;
        private Image _weaponIcon;
        private Image _otherIcon;
        private Image _healthIcon;
        private TextMeshProUGUI _weaponName;
        private TextMeshProUGUI _ammo;
        private TextMeshProUGUI _reserve;
        private TextMeshProUGUI _otherWeapon;
        private Image _reloadFill;
        private GameObject _reloadBar;

        private Image _healthFill;
        private Image _healthLag;
        private TextMeshProUGUI _healthText;
        private float _lagValue = 1.0f;

        private TextMeshProUGUI _prompt;
        private TextMeshProUGUI _kills;
        private TextMeshProUGUI _notice;
        private float _noticeTime = -10.0f;
        private TextMeshProUGUI _controls;

        private Image _vignette;
        private float _damageFlash;
        private readonly List<(RectTransform arrow, Image image, Vector3 from, float time)> _arrows = new List<(RectTransform, Image, Vector3, float)>();

        private GameObject _deathScreen;
        private TextMeshProUGUI _deathTimer;
        private GameObject _scope;

        private GameObject _canvasObject;
        private bool _visible = true;

        private Image _staminaFill;
        private CanvasGroup _staminaGroup;
        private TextMeshProUGUI _lives;
        private TextMeshProUGUI _waveTitle;
        private TextMeshProUGUI _waveRemaining;
        private TextMeshProUGUI _waveBanner;
        private float _bannerTime = -10.0f;
        private EnemySpawner _spawner;

        private TextMeshProUGUI _score;
        private TextMeshProUGUI _scorePopup;
        private float _scorePopupTime = -10.0f;

        // arrows around the crosshair pointing at enemies that are out of view
        private const int ThreatArrows = 12;
        private const float ThreatRange = 45.0f;
        private RectTransform[] _threatPivots;
        private Image[] _threatImages;
        private readonly List<(Vector3 position, bool zombie, float distance, bool aware, bool special)> _threats = new List<(Vector3, bool, float, bool, bool)>();

        // horror additions: captions, stealth meter, power status, warnings, injury state
        private TextMeshProUGUI _subtitles;
        private Image _stealthBack;
        private Image _stealthFill;
        private TextMeshProUGUI _stealthLabel;
        private CanvasGroup _stealthGroup;
        private TextMeshProUGUI _powerStatus;
        private TextMeshProUGUI _warning;
        private float _warningTime = -10.0f;
        private TextMeshProUGUI _injury;
        private PlayerInjuryFeedback _injuryFeedback;

        private void Start()
        {
            _camera = Camera.main;
            if (Player == null) Player = FindAnyObjectByType<PlayerCombat>();
            Build();

            Player.LandedHit += OnLandedHit;
            Player.TookDamage += OnTookDamage;
            Player.Notify += ShowNotice;

            _spawner = FindAnyObjectByType<EnemySpawner>();
            if (_spawner != null) _spawner.WaveStarted += OnWaveStarted;
            _injuryFeedback = Player.GetComponent<PlayerInjuryFeedback>();
            ZombieAI.SpottedPlayer += OnZombieSpotted;
            ScreamerBehaviour.ChargeStarted += OnScreamerCharging;
            if (PowerGridManager.Instance != null) PowerGridManager.Instance.SectorChanged += OnSectorChanged;
            if (GameFlow.Instance != null) GameFlow.Instance.ScoreAdded += OnScoreAdded;
            _canvasObject.SetActive(_visible);
        }

        private void OnDestroy()
        {
            ZombieAI.SpottedPlayer -= OnZombieSpotted;
            ScreamerBehaviour.ChargeStarted -= OnScreamerCharging;
            if (PowerGridManager.Instance != null) PowerGridManager.Instance.SectorChanged -= OnSectorChanged;
        }

        private void OnScoreAdded(int points, string reason)
        {
            _scorePopup.text = string.IsNullOrEmpty(reason) ? $"+{points}" : $"+{points}  <size=60%>{reason}</size>";
            _scorePopupTime = Time.time;
        }

        // other HUD pieces (the minimap) follow this
        public bool Visible => _visible;

        // the main menu hides the HUD
        public void SetVisible(bool visible)
        {
            _visible = visible;
            if (_canvasObject != null) _canvasObject.SetActive(visible);
        }

        private void OnWaveStarted(int wave)
        {
            _waveBanner.text = $"ONDA {wave}";
            _bannerTime = Time.time;
        }

        // ---------------------------------------------------------------- building

        private void Build()
        {
            var canvasGo = new GameObject("CombatHUD", typeof(Canvas), typeof(CanvasScaler));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 50;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920.0f, 1080.0f);
            scaler.matchWidthOrHeight = 0.5f;
            _root = (RectTransform)canvasGo.transform;
            _canvasObject = canvasGo;

            // full screen overlays first so they draw under everything else
            _vignette = Panel("DamageVignette", _root, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Color(0.7f, 0.0f, 0.0f, 0.0f));
            _vignette.sprite = RadialSprite(true);
            _vignette.raycastTarget = false;

            _scope = BuildScope();
            BuildCrosshair();
            BuildHitMarker();
            BuildAmmo();
            BuildHealth();

            _prompt = Label("Prompt", _root, new Vector2(0.5f, 0.0f), new Vector2(0.0f, 230.0f), 30, TextAlignmentOptions.Center);
            Card("ScoreCard", new Vector2(1.0f, 1.0f), new Vector2(-220.0f, -92.0f), new Vector2(360.0f, 140.0f), false);
            _kills = Label("Kills", _root, new Vector2(1.0f, 1.0f), new Vector2(-60.0f, -50.0f), 28, TextAlignmentOptions.Right);
            _notice = Label("Notice", _root, new Vector2(0.5f, 0.5f), new Vector2(0.0f, 150.0f), 28, TextAlignmentOptions.Center);
            _notice.fontStyle = FontStyles.Bold;
            _controls = Label("Controls", _root, new Vector2(0.0f, 0.0f), new Vector2(60.0f, 170.0f), 20, TextAlignmentOptions.Left);
            _controls.text = "<b>LMB</b> atirar   <b>RMB</b> mirar   <b>R</b> recarregar   <b>E</b> usar   <b>Q</b> trocar   <b>C</b> agachar   <b>Ctrl</b> rolar   <b>V</b> golpe   <b>B</b> empurrar   <b>F</b> lanterna";

            // waves: title and enemies left at the top, a big banner when a wave starts
            _waveTitle = Label("Wave", _root, new Vector2(0.5f, 1.0f), new Vector2(0.0f, -50.0f), 40, TextAlignmentOptions.Center);
            _waveTitle.fontStyle = FontStyles.Bold;
            _waveRemaining = Label("WaveRemaining", _root, new Vector2(0.5f, 1.0f), new Vector2(0.0f, -92.0f), 24, TextAlignmentOptions.Center);
            _waveBanner = Label("WaveBanner", _root, new Vector2(0.5f, 0.5f), new Vector2(0.0f, 220.0f), 110, TextAlignmentOptions.Center);
            _waveBanner.fontStyle = FontStyles.Bold;
            _waveBanner.color = Accent;
            _waveBanner.alpha = 0.0f;

            _score = Label("Score", _root, new Vector2(1.0f, 1.0f), new Vector2(-60.0f, -90.0f), 26, TextAlignmentOptions.Right);
            _scorePopup = Label("ScorePopup", _root, new Vector2(0.5f, 0.5f), new Vector2(150.0f, 40.0f), 34, TextAlignmentOptions.Left);
            _scorePopup.fontStyle = FontStyles.Bold;
            _scorePopup.color = Accent;
            _scorePopup.alpha = 0.0f;

            BuildThreatArrows();
            _controls.color = new Color(1.0f, 1.0f, 1.0f, 0.75f);
            BuildHorrorHud();

            _deathScreen = BuildDeathScreen();
        }

        private void BuildThreatArrows()
        {
            Sprite arrow = TriangleSprite();
            _threatPivots = new RectTransform[ThreatArrows];
            _threatImages = new Image[ThreatArrows];
            for (int i = 0; i < ThreatArrows; i++)
            {
                var pivot = new GameObject("Threat", typeof(RectTransform)).GetComponent<RectTransform>();
                pivot.SetParent(_root, false);
                Image image = Panel("Arrow", pivot, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.0f, 210.0f), new Vector2(34.0f, 26.0f), Color.white);
                image.sprite = arrow;
                _threatPivots[i] = pivot;
                _threatImages[i] = image;
                pivot.gameObject.SetActive(false);
            }
        }

        private void BuildHorrorHud()
        {
            // above the interaction prompt
            _subtitles = Label("Subtitles", _root, new Vector2(0.5f, 0.0f), new Vector2(0.0f, 360.0f), 26, TextAlignmentOptions.Bottom);
            _subtitles.rectTransform.sizeDelta = new Vector2(1200.0f, 160.0f);
            _subtitles.color = new Color(1.0f, 1.0f, 1.0f, 0.9f);

            // stealth: a thin meter under the crosshair, how close the nearest enemy is to spotting you
            _stealthBack = Panel("Stealth", _root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.0f, -70.0f), new Vector2(90.0f, 5.0f), new Color(0, 0, 0, 0.5f));
            _stealthFill = Panel("Fill", _stealthBack.rectTransform, Vector2.zero, new Vector2(0.0f, 1.0f), Vector2.zero, Vector2.zero, Color.white);
            _stealthFill.rectTransform.pivot = new Vector2(0.0f, 0.5f);
            _stealthLabel = Label("StealthLabel", _stealthBack.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.0f, -16.0f), 18, TextAlignmentOptions.Center);
            _stealthGroup = _stealthBack.gameObject.AddComponent<CanvasGroup>();
            _stealthGroup.alpha = 0.0f;

            _powerStatus = Label("Power", _root, new Vector2(0.0f, 1.0f), new Vector2(60.0f, -50.0f), 24, TextAlignmentOptions.Left);
            _warning = Label("Warning", _root, new Vector2(0.5f, 1.0f), new Vector2(0.0f, -150.0f), 34, TextAlignmentOptions.Center);
            _warning.fontStyle = FontStyles.Bold;
            _warning.color = Danger;
            _warning.alpha = 0.0f;
            _injury = Label("Injury", _root, new Vector2(0.0f, 0.0f), new Vector2(60.0f, 30.0f), 20, TextAlignmentOptions.Left);
        }

        private void UpdateHorrorHud(bool dead)
        {
            // captions, newest at the bottom, with the side the sound came from
            Subtitles.Prune();
            if (Subtitles.Active.Count == 0 || dead)
            {
                _subtitles.text = "";
            }
            else
            {
                var builder = new System.Text.StringBuilder();
                foreach (Subtitles.Line line in Subtitles.Active)
                {
                    if (builder.Length > 0) builder.Append('\n');
                    builder.Append(line.Text);
                    if (line.Positional) builder.Append("  <size=80%><color=#FFC74D>").Append(Side(line.Position)).Append("</color></size>");
                }

                _subtitles.text = builder.ToString();
            }

            // stealth meter: shown while crouching or while someone is noticing you
            float awareness = 0.0f;
            bool seen = false;
            foreach (EnemyPerception enemy in EnemyPerception.All)
            {
                if (enemy == null) continue;
                awareness = Mathf.Max(awareness, enemy.PlayerAwareness);
                if (enemy.Target == Player.Health && (enemy.State == AwarenessState.Chase || enemy.State == AwarenessState.Attack)) seen = true;
            }

            var controller = Player.Controller;
            bool crouching = controller != null && controller.IsCrouching;
            var stealth = Player.GetComponent<PerceptionTarget>();
            float target = !dead && (crouching || awareness > 0.05f || seen) ? 1.0f : 0.0f;
            _stealthGroup.alpha = Mathf.MoveTowards(_stealthGroup.alpha, target, Time.deltaTime * 3.0f);
            _stealthFill.rectTransform.anchorMax = new Vector2(seen ? 1.0f : Mathf.Clamp01(awareness), 1.0f);
            _stealthFill.color = seen ? Danger : Color.Lerp(new Color(0.7f, 0.85f, 1.0f), Accent, awareness);
            _stealthLabel.text = seen ? "<color=#FF4033>DETECTADO</color>"
                : awareness > 0.4f ? "<color=#FFC74D>SUSPEITANDO</color>"
                : stealth != null && !stealth.InLight && !Player.FlashlightOn ? "OCULTO" : crouching ? "AGACHADO" : "";

            // the sector the player stands in, when its power is down
            PowerGridManager grid = PowerGridManager.Instance;
            int sector = grid != null ? grid.SectorAt(Player.transform.position) : -1;
            PowerState power = grid != null ? grid.GetState(sector) : PowerState.Normal;
            _powerStatus.gameObject.SetActive(power != PowerState.Normal && !dead);
            if (power != PowerState.Normal)
            {
                _powerStatus.text = power == PowerState.Emergency
                    ? $"<color=#FFB040>ENERGIA DE EMERGÊNCIA</color>  <size=80%>{grid.SectorName(sector)}</size>"
                    : $"<color=#FF5A4A>APAGÃO</color>  <size=80%>{grid.SectorName(sector)} · religue o gerador</size>";
            }

            float warningAge = Time.time - _warningTime;
            _warning.alpha = warningAge < 2.0f ? 1.0f : Mathf.Clamp01(1.0f - (warningAge - 2.0f) * 2.0f);

            InjuryState injury = _injuryFeedback != null ? _injuryFeedback.State : InjuryState.Healthy;
            _injury.gameObject.SetActive(!dead && injury != InjuryState.Healthy);
            _injury.text = injury == InjuryState.Critical ? "<color=#FF4033>ESTADO CRÍTICO</color>" : "<color=#FFB040>FERIDO</color>";
        }

        private string Side(Vector3 position)
        {
            Transform cam = _camera.transform;
            Vector3 offset = Vector3.ProjectOnPlane(position - cam.position, Vector3.up);
            if (offset.magnitude < 3.0f) return "perto";
            float angle = Vector3.SignedAngle(Vector3.ProjectOnPlane(cam.forward, Vector3.up), offset, Vector3.up);
            if (Mathf.Abs(angle) < 35.0f) return "à frente";
            if (Mathf.Abs(angle) > 135.0f) return "atrás";
            return angle > 0.0f ? "à direita" : "à esquerda";
        }

        private void ShowWarning(string text)
        {
            _warning.text = text;
            _warningTime = Time.time;
        }

        private void OnZombieSpotted(ZombieAI zombie)
        {
            switch (zombie.Kind)
            {
                case ZombieKind.Tank:
                    ShowWarning("TANQUE TE VIU");
                    break;
                case ZombieKind.Runner:
                    ShowWarning("CORREDOR!");
                    break;
                case ZombieKind.Crawler:
                    ShowWarning("RASTEJANTE!");
                    break;
            }
        }

        private void OnScreamerCharging(ScreamerBehaviour screamer)
        {
            ShowWarning("GRITADOR — INTERROMPA O GRITO!");
        }

        private void OnSectorChanged(int sector, PowerState previous, PowerState state)
        {
            string name = PowerGridManager.Instance.SectorName(sector);
            if (state == PowerState.Emergency) ShowNotice($"<color=#FFB040>FALHA DE ENERGIA</color>  {name}");
            else if (state == PowerState.Blackout) ShowNotice($"<color=#FF5A4A>APAGÃO</color>  {name}");
            else ShowNotice($"<color=#9BE39B>ENERGIA RESTAURADA</color>  {name}");
        }

        private static Sprite TriangleSprite()
        {
            const int size = 64;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    // pointing up, softly anti-aliased
                    float u = (x + 0.5f) / size, v = (y + 0.5f) / size;
                    float halfWidth = (1.0f - v) * 0.5f;
                    float edge = Mathf.Clamp01((halfWidth - Mathf.Abs(u - 0.5f)) * size * 0.5f);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(edge * 255));
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        }

        private void UpdateThreats()
        {
            _threats.Clear();
            Vector3 playerPosition = Player.transform.position;
            Difficulty difficulty = GameFlow.Instance != null ? GameDifficulty.Current : Difficulty.Normal;
            ThreatArrowMode mode = GameSettings.EffectiveThreatArrows(difficulty);
            foreach (Health health in Health.Living)
            {
                if (mode == ThreatArrowMode.Off) break;
                if (health.Team == Player.Health.Team || health.IsDead) continue;
                float distance = Vector3.Distance(health.transform.position, playerPosition);
                if (distance > ThreatRange) continue;

                // in the strict mode only those hunting or searching for the player show up
                var perception = health.GetComponent<EnemyPerception>();
                bool aware = perception != null && perception.Target == Player.Health &&
                             (perception.State == AwarenessState.Chase || perception.State == AwarenessState.Attack || perception.State == AwarenessState.Search);
                if (mode == ThreatArrowMode.AwareOnly && !aware) continue;

                // only the ones out of view need an arrow
                Vector3 view = _camera.WorldToViewportPoint(health.transform.position + Vector3.up * 1.2f);
                if (view.z > 0.0f && view.x > 0.05f && view.x < 0.95f && view.y > 0.05f && view.y < 0.95f) continue;
                var zombie = health.GetComponent<ZombieAI>();
                _threats.Add((health.transform.position, zombie != null, distance, aware, zombie != null && zombie.Kind != ZombieKind.Common));
            }

            _threats.Sort((a, b) => a.distance.CompareTo(b.distance));
            Transform cam = _camera.transform;
            Vector3 forward = Vector3.ProjectOnPlane(cam.forward, Vector3.up);
            for (int i = 0; i < ThreatArrows; i++)
            {
                bool show = i < _threats.Count;
                _threatPivots[i].gameObject.SetActive(show);
                if (!show) continue;

                var (position, zombie, distance, aware, special) = _threats[i];
                float angle = Vector3.SignedAngle(forward, Vector3.ProjectOnPlane(position - cam.position, Vector3.up), Vector3.up);
                _threatPivots[i].localRotation = Quaternion.Euler(0.0f, 0.0f, -angle);
                float closeness = 1.0f - distance / ThreatRange;
                Color color = zombie ? new Color(1.0f, 0.22f, 0.18f) : new Color(1.0f, 0.75f, 0.3f);
                if (special) color = new Color(1.0f, 0.3f, 0.85f);
                // the unaware are faint, the hunters pulse
                color.a = Mathf.Clamp01(0.25f + closeness * 0.75f) * (aware ? 0.8f + 0.2f * Mathf.Sin(Time.time * 8.0f) : 0.55f);
                _threatImages[i].color = color;
                _threatImages[i].rectTransform.localScale = Vector3.one * Mathf.Lerp(0.7f, 1.3f, closeness) * (special ? 1.25f : 1.0f);
            }
        }

        private void BuildCrosshair()
        {
            var holder = new GameObject("Crosshair", typeof(RectTransform)).GetComponent<RectTransform>();
            holder.SetParent(_root, false);
            _crossBars = new RectTransform[4];
            _crossImages = new Image[4];
            for (int i = 0; i < 4; i++)
            {
                Image bar = Panel("Bar", holder, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, i % 2 == 0 ? new Vector2(3, 14) : new Vector2(14, 3), Color.white);
                bar.gameObject.AddComponent<Shadow>().effectDistance = new Vector2(1, -1);
                _crossBars[i] = bar.rectTransform;
                _crossImages[i] = bar;
            }

            _crossDot = Panel("Dot", holder, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(4, 4), Color.white);
            _crossDot.gameObject.AddComponent<Shadow>().effectDistance = new Vector2(1, -1);
        }

        private void BuildHitMarker()
        {
            _hitMarker = new GameObject("HitMarker", typeof(RectTransform)).GetComponent<RectTransform>();
            _hitMarker.SetParent(_root, false);
            _hitImages = new Image[4];
            for (int i = 0; i < 4; i++)
            {
                float angle = 45.0f + i * 90.0f;
                Vector2 dir = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));
                Image bar = Panel("Mark", _hitMarker, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), dir * 16.0f, new Vector2(14, 3), Color.white);
                bar.rectTransform.localRotation = Quaternion.Euler(0, 0, angle);
                _hitImages[i] = bar;
            }

            _hitMarker.gameObject.SetActive(false);
        }

        // translucent graphite card with a thin accent edge: the base of every HUD block
        private Image Card(string name, Vector2 anchor, Vector2 center, Vector2 size, bool accentLeft)
        {
            Image card = Panel(name, _root, anchor, anchor, center, size, CardColor);
            Image edge = Panel("Edge", card.rectTransform, accentLeft ? Vector2.zero : new Vector2(1.0f, 0.0f), accentLeft ? new Vector2(0.0f, 1.0f) : Vector2.one,
                Vector2.zero, new Vector2(3.0f, 0.0f), new Color(1.0f, 1.0f, 1.0f, 0.22f));
            edge.rectTransform.pivot = new Vector2(accentLeft ? 0.0f : 1.0f, 0.5f);
            return card;
        }

        private void BuildAmmo()
        {
            _weaponCard = Card("WeaponCard", new Vector2(1.0f, 0.0f), new Vector2(-260.0f, 128.0f), new Vector2(440.0f, 208.0f), false).gameObject;
            _weaponIcon = Panel("WeaponIcon", _root, new Vector2(1.0f, 0.0f), new Vector2(1.0f, 0.0f), new Vector2(-60.0f, 196.0f), new Vector2(150.0f, 62.0f), Color.white);
            _weaponIcon.rectTransform.pivot = new Vector2(1.0f, 0.5f);
            _weaponIcon.preserveAspect = true;
            _otherIcon = Panel("OtherIcon", _root, new Vector2(1.0f, 0.0f), new Vector2(1.0f, 0.0f), new Vector2(-60.0f, 258.0f), new Vector2(64.0f, 28.0f), new Color(1.0f, 1.0f, 1.0f, 0.5f));
            _otherIcon.rectTransform.pivot = new Vector2(1.0f, 0.5f);
            _otherIcon.preserveAspect = true;
            _weaponName = Label("WeaponName", _root, new Vector2(1.0f, 0.0f), new Vector2(-224.0f, 196.0f), 22, TextAlignmentOptions.Right);
            _weaponName.color = Accent;
            _ammo = Label("Ammo", _root, new Vector2(1.0f, 0.0f), new Vector2(-170.0f, 90.0f), 72, TextAlignmentOptions.Right);
            _ammo.fontStyle = FontStyles.Bold;
            _reserve = Label("Reserve", _root, new Vector2(1.0f, 0.0f), new Vector2(-60.0f, 80.0f), 34, TextAlignmentOptions.Right);
            _reserve.color = new Color(1.0f, 1.0f, 1.0f, 0.7f);
            _otherWeapon = Label("OtherWeapon", _root, new Vector2(1.0f, 0.0f), new Vector2(-136.0f, 258.0f), 18, TextAlignmentOptions.Right);
            _otherWeapon.color = new Color(1.0f, 1.0f, 1.0f, 0.55f);

            Image back = Panel("ReloadBar", _root, new Vector2(1.0f, 0.0f), new Vector2(1.0f, 0.0f), new Vector2(-160.0f, 45.0f), new Vector2(200.0f, 6.0f), new Color(0, 0, 0, 0.5f));
            _reloadFill = Panel("Fill", back.rectTransform, Vector2.zero, new Vector2(0.0f, 1.0f), Vector2.zero, Vector2.zero, Accent);
            _reloadFill.rectTransform.pivot = new Vector2(0.0f, 0.5f);
            _reloadBar = back.gameObject;
        }

        private void BuildHealth()
        {
            Card("VitalsCard", Vector2.zero, new Vector2(250.0f, 82.0f), new Vector2(430.0f, 112.0f), true);
            ItemCatalog catalog = ItemCatalog.Instance;
            Image healthIcon = Panel("HealthIcon", _root, Vector2.zero, Vector2.zero, new Vector2(74.0f, 100.0f), new Vector2(24.0f, 24.0f), new Color(0.35f, 0.95f, 0.55f));
            healthIcon.sprite = catalog != null ? catalog.HealthIcon : null;
            healthIcon.preserveAspect = true;
            _healthIcon = healthIcon;
            Image back = Panel("Health", _root, new Vector2(0.0f, 0.0f), new Vector2(0.0f, 0.0f), new Vector2(240.0f, 70.0f), new Vector2(360.0f, 16.0f), new Color(0, 0, 0, 0.55f));
            _healthLag = Panel("Lag", back.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Color(1.0f, 0.9f, 0.8f, 0.8f));
            _healthLag.rectTransform.pivot = new Vector2(0.0f, 0.5f);
            _healthFill = Panel("Fill", back.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Color(0.35f, 0.95f, 0.55f));
            _healthFill.rectTransform.pivot = new Vector2(0.0f, 0.5f);
            _healthText = Label("HealthText", _root, new Vector2(0.0f, 0.0f), new Vector2(94.0f, 100.0f), 30, TextAlignmentOptions.Left);
            _healthText.fontStyle = FontStyles.Bold;
            _lives = Label("Lives", _root, new Vector2(0.0f, 0.0f), new Vector2(420.0f, 100.0f), 24, TextAlignmentOptions.Right);

            // stamina, just under the health bar; fades out while full
            Image staminaBack = Panel("Stamina", _root, new Vector2(0.0f, 0.0f), new Vector2(0.0f, 0.0f), new Vector2(240.0f, 52.0f), new Vector2(360.0f, 7.0f), new Color(0, 0, 0, 0.55f));
            _staminaFill = Panel("Fill", staminaBack.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Color(0.95f, 0.85f, 0.35f));
            _staminaFill.rectTransform.pivot = new Vector2(0.0f, 0.5f);
            _staminaGroup = staminaBack.gameObject.AddComponent<CanvasGroup>();
        }

        private GameObject BuildScope()
        {
            Image scope = Panel("Scope", _root, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, Color.black);
            scope.sprite = RadialSprite(false);
            scope.preserveAspect = false;
            var fitter = scope.gameObject.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fitter.aspectRatio = 1.0f;
            // reticle: thick posts from the edge, thin lines and mil dots in the middle, red center dot
            Color ink = new Color(0.0f, 0.0f, 0.0f, 0.9f);
            Panel("H", scope.rectTransform, new Vector2(0.2f, 0.5f), new Vector2(0.8f, 0.5f), Vector2.zero, new Vector2(0.0f, 1.5f), ink);
            Panel("V", scope.rectTransform, new Vector2(0.5f, 0.2f), new Vector2(0.5f, 0.8f), Vector2.zero, new Vector2(1.5f, 0.0f), ink);
            Panel("PostL", scope.rectTransform, new Vector2(0.2f, 0.5f), new Vector2(0.4f, 0.5f), Vector2.zero, new Vector2(0.0f, 7.0f), ink);
            Panel("PostR", scope.rectTransform, new Vector2(0.6f, 0.5f), new Vector2(0.8f, 0.5f), Vector2.zero, new Vector2(0.0f, 7.0f), ink);
            Panel("PostB", scope.rectTransform, new Vector2(0.5f, 0.2f), new Vector2(0.5f, 0.4f), Vector2.zero, new Vector2(7.0f, 0.0f), ink);
            for (int i = 1; i <= 4; i++)
            {
                float offset = i * 0.02f;
                Panel("DotH", scope.rectTransform, new Vector2(0.5f + offset, 0.5f), new Vector2(0.5f + offset, 0.5f), Vector2.zero, new Vector2(5.0f, 5.0f), ink);
                Panel("DotH", scope.rectTransform, new Vector2(0.5f - offset, 0.5f), new Vector2(0.5f - offset, 0.5f), Vector2.zero, new Vector2(5.0f, 5.0f), ink);
                Panel("DotV", scope.rectTransform, new Vector2(0.5f, 0.5f - offset), new Vector2(0.5f, 0.5f - offset), Vector2.zero, new Vector2(5.0f, 5.0f), ink);
            }

            Panel("Center", scope.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(4.0f, 4.0f), Danger);
            scope.gameObject.SetActive(false);
            return scope.gameObject;
        }

        private GameObject BuildDeathScreen()
        {
            Image dim = Panel("DeathScreen", _root, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Color(0.1f, 0.0f, 0.0f, 0.55f));
            TextMeshProUGUI title = Label("Title", dim.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.0f, 40.0f), 72, TextAlignmentOptions.Center);
            title.text = "VOCÊ FOI ABATIDO";
            title.fontStyle = FontStyles.Bold;
            title.color = Danger;
            _deathTimer = Label("Timer", dim.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.0f, -40.0f), 30, TextAlignmentOptions.Center);
            dim.gameObject.SetActive(false);
            return dim.gameObject;
        }

        private static Image Panel(string name, RectTransform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 position, Vector2 size, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            var image = go.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static TextMeshProUGUI Label(string name, RectTransform parent, Vector2 anchor, Vector2 position, float size, TextAlignmentOptions alignment)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = new Vector2(alignment == TextAlignmentOptions.Right ? 1.0f : alignment == TextAlignmentOptions.Left ? 0.0f : 0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(900.0f, size * 1.4f);
            var text = go.GetComponent<TextMeshProUGUI>();
            text.fontSize = size;
            text.alignment = alignment;
            text.raycastTarget = false;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.outlineWidth = 0.15f;
            text.outlineColor = new Color32(0, 0, 0, 160);
            return text;
        }

        // transparent in the middle for the scope, transparent at the edges for the damage vignette
        private static Sprite RadialSprite(bool vignette)
        {
            const int size = 256;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f) / size * 2.0f - 1.0f;
                    float dy = (y + 0.5f) / size * 2.0f - 1.0f;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = vignette ? Mathf.SmoothStep(0.0f, 1.0f, Mathf.InverseLerp(0.45f, 1.25f, r))
                                       : Mathf.SmoothStep(0.0f, 1.0f, Mathf.InverseLerp(0.58f, 0.62f, r));
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255));
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        }

        // ---------------------------------------------------------------- update

        private void Update()
        {
            if (Player == null || !_visible) return;

            float dt = Time.deltaTime;
            WeaponHolder holder = Player.Holder;
            WeaponInstance weapon = Player.Current;
            bool dead = Player.Health.IsDead;

            // scope; the gun also vanishes mid-roll
            bool scoped = weapon != null && weapon.Data.HasScope && holder.AimBlend > 0.9f && !dead;
            _scope.SetActive(scoped);
            holder.HideVisual = scoped || Player.IsRolling;

            UpdateStaminaAndLives(dt);
            UpdateWaves();
            UpdateThreats();

            GameFlow flow = GameFlow.Instance;
            _score.gameObject.SetActive(flow != null);
            if (flow != null) _score.text = $"PONTOS  <b>{flow.Score:N0}</b>".Replace(',', '.');
            float popupAge = Time.time - _scorePopupTime;
            _scorePopup.alpha = Mathf.Clamp01(1.0f - (popupAge - 0.6f) * 2.0f);
            _scorePopup.rectTransform.anchoredPosition = new Vector2(150.0f, 40.0f + popupAge * 40.0f);

            // looking down the sights replaces the crosshair
            bool sights = Player.FirstPersonAim && weapon != null && holder.AimBlend > 0.5f;
            UpdateCrosshair(holder, weapon, scoped || dead || sights, dt);
            UpdateHitMarker();
            UpdateAmmo(holder, weapon, dead);
            UpdateHealth(dt);
            UpdateDamage(dt);

            WeaponPickup pickup = Player.NearbyPickup;
            int interaction = dead ? 0 : Player.ClosestInteraction();
            _prompt.gameObject.SetActive(interaction != 0);
            if (interaction == 1)
            {
                _prompt.text = "<color=#FFC74D>[E]</color> Abrir baú";
            }
            else if (interaction == 2)
            {
                bool owned = WeaponUpgrades.SameWeapon(Player.Slots[0]?.Data, pickup.Data) || WeaponUpgrades.SameWeapon(Player.Slots[1]?.Data, pickup.Data);
                _prompt.text = owned ? $"<color=#FFC74D>[E]</color> Pegar munição de {pickup.Data.DisplayName}"
                    : $"<color=#FFC74D>[E]</color> Pegar {pickup.Data.DisplayName}";
            }
            else if (interaction == 3)
            {
                _prompt.text = Player.NearbyInteractable.Prompt;
            }

            UpdateHorrorHud(dead);

            _kills.text = $"ABATES  <b>{Player.Kills}</b>";

            float noticeAge = Time.time - _noticeTime;
            _notice.alpha = noticeAge < 1.2f ? 1.0f : Mathf.Clamp01(1.0f - (noticeAge - 1.2f) / 0.5f);
            _notice.rectTransform.localScale = Vector3.one * (1.0f + Mathf.Max(0.0f, 0.3f - noticeAge) * 1.2f);

            _controls.alpha = Mathf.Clamp01(1.0f - (Time.timeSinceLevelLoad - 15.0f) / 3.0f) * 0.75f;

            _deathScreen.SetActive(dead);
            if (dead)
            {
                _deathTimer.text = Player.AllowRespawn
                    ? $"Renascendo em {Mathf.CeilToInt(Mathf.Max(0.0f, Player.RespawnTime - Time.time))}..."
                    : "Sem vidas restantes";
            }
        }

        private void UpdateStaminaAndLives(float dt)
        {
            var controller = Player.Controller;
            if (controller != null && controller.MaxStamina > 0.0f)
            {
                float stamina = controller.Stamina / controller.MaxStamina;
                _staminaFill.rectTransform.anchorMax = new Vector2(stamina, 1.0f);
                // orange while exhausted: no sprinting until it recovers
                _staminaFill.color = controller.Exhausted ? new Color(1.0f, 0.45f, 0.2f) : new Color(0.95f, 0.85f, 0.35f);
                float target = stamina < 0.999f || controller.IsSprinting ? 1.0f : 0.0f;
                _staminaGroup.alpha = Mathf.MoveTowards(_staminaGroup.alpha, target, dt * (target > 0.5f ? 6.0f : 1.5f));
            }

            GameFlow flow = GameFlow.Instance;
            _lives.gameObject.SetActive(flow != null && flow.Current != GameFlow.State.Menu);
            if (flow != null) _lives.text = $"VIDAS <b>{Mathf.Max(0, flow.LivesLeft)}</b>";
        }

        private void UpdateWaves()
        {
            bool waves = _spawner != null && _spawner.Running;
            _waveTitle.gameObject.SetActive(waves && _spawner.Wave > 0);
            // nothing to count before a wave is on (the campaign launches them from missions)
            _waveRemaining.gameObject.SetActive(waves && (_spawner.WaveInProgress || (_spawner.InIntermission && !_spawner.ManualWaves)));
            if (waves)
            {
                _waveTitle.text = $"ONDA {_spawner.Wave}";
                _waveRemaining.text = _spawner.InIntermission
                    ? $"<color=#FFC74D>Próxima onda em {Mathf.CeilToInt(_spawner.IntermissionLeft)}</color>"
                    : $"{_spawner.RemainingInWave} inimigos restantes";
            }

            float age = Time.time - _bannerTime;
            _waveBanner.alpha = age < 2.0f ? Mathf.Clamp01(age * 4.0f) : Mathf.Clamp01(1.0f - (age - 2.0f) * 2.0f);
            _waveBanner.rectTransform.localScale = Vector3.one * (1.0f + Mathf.Max(0.0f, 0.4f - age) * 0.8f);
        }

        private void UpdateCrosshair(WeaponHolder holder, WeaponInstance weapon, bool hidden, float dt)
        {
            bool armed = weapon != null;
            // spread in degrees to pixels on the 1080p reference canvas
            float pixelsPerDegree = 1080.0f / Mathf.Max(_camera.fieldOfView, 1.0f);
            float targetGap = armed ? 5.0f + holder.CurrentSpread * pixelsPerDegree * 0.5f : 0.0f;
            _crossGap = Mathf.Lerp(_crossGap, targetGap, 1.0f - Mathf.Exp(-18.0f * dt));

            // red when the crosshair sits on an enemy
            bool onEnemy = false;
            if (armed && Physics.Raycast(_camera.transform.position, _camera.transform.forward, out RaycastHit hit, 200.0f,
                    CombatLayers.Shootable & ~(1 << CombatLayers.Player), QueryTriggerInteraction.Ignore))
            {
                Hitbox hitbox = hit.collider.GetComponent<Hitbox>();
                onEnemy = hitbox != null && hitbox.Owner != null && hitbox.Owner.Team != Player.Health.Team && !hitbox.Owner.IsDead;
            }

            Color color = onEnemy ? Danger : Color.white;
            float alpha = hidden ? 0.0f : 1.0f - holder.AimBlend * 0.35f;
            color.a = armed ? alpha : 0.0f;

            Vector2[] directions = { Vector2.up, Vector2.right, Vector2.down, Vector2.left };
            for (int i = 0; i < 4; i++)
            {
                _crossBars[i].anchoredPosition = directions[i] * (_crossGap + 7.0f);
                _crossImages[i].color = color;
            }

            Color dot = onEnemy ? Danger : Color.white;
            dot.a = hidden ? 0.0f : armed ? 0.9f : 0.5f;
            _crossDot.color = dot;
        }

        private void UpdateHitMarker()
        {
            float age = Time.time - _hitTime;
            bool visible = age < 0.25f;
            _hitMarker.gameObject.SetActive(visible);
            if (!visible) return;

            float t = age / 0.25f;
            _hitMarker.localScale = Vector3.one * (_hitScale * (1.0f + (1.0f - t) * 0.4f));
            Color color = _hitColor;
            color.a = 1.0f - t * t;
            foreach (Image image in _hitImages) image.color = color;
        }

        private void UpdateAmmo(WeaponHolder holder, WeaponInstance weapon, bool dead)
        {
            bool armed = weapon != null && !dead;
            _weaponName.gameObject.SetActive(armed);
            _ammo.gameObject.SetActive(armed);
            _reserve.gameObject.SetActive(armed);
            _reloadBar.SetActive(armed && holder.IsReloading);
            _weaponCard.SetActive(armed);
            _weaponIcon.gameObject.SetActive(armed && weapon.Data.Icon != null);
            if (armed) _weaponIcon.sprite = weapon.Data.Icon;
            // while the hands are busy putting a gun away or drawing one, the readout dims
            _weaponIcon.color = new Color(1.0f, 1.0f, 1.0f, armed && holder.Busy ? 0.45f : 0.92f);

            WeaponInstance other = Player.Other;
            _otherWeapon.gameObject.SetActive(other != null && !dead);
            _otherIcon.gameObject.SetActive(other != null && !dead && other.Data.Icon != null);
            if (other != null)
            {
                _otherWeapon.text = $"[Q] {other.Data.DisplayName}";
                _otherIcon.sprite = other.Data.Icon;
            }

            if (!armed) return;

            _weaponName.text = weapon.Data.DisplayName.ToUpperInvariant();
            _ammo.text = weapon.Magazine.ToString();
            bool low = weapon.Magazine <= Mathf.Max(1, weapon.Data.MagazineSize / 4);
            _ammo.color = weapon.Magazine == 0 ? Danger : low ? Accent : Color.white;
            _reserve.text = "/ " + weapon.Reserve;

            if (holder.IsReloading)
            {
                _reloadFill.rectTransform.anchorMax = new Vector2(holder.ReloadProgress, 1.0f);
            }
        }

        private void UpdateHealth(float dt)
        {
            float health = Player.Health.Normalized;
            _lagValue = health > _lagValue ? health : Mathf.MoveTowards(_lagValue, health, dt * 0.35f);
            _healthFill.rectTransform.anchorMax = new Vector2(health, 1.0f);
            _healthLag.rectTransform.anchorMax = new Vector2(_lagValue, 1.0f);
            _healthFill.color = Color.Lerp(Danger, new Color(0.35f, 0.95f, 0.55f), Mathf.InverseLerp(0.2f, 0.6f, health));
            if (_healthIcon != null) _healthIcon.color = _healthFill.color;
            _healthText.text = Mathf.CeilToInt(Player.Health.Current).ToString();
        }

        private void UpdateDamage(float dt)
        {
            _damageFlash = Mathf.MoveTowards(_damageFlash, 0.0f, dt * 1.5f);
            float health = Player.Health.Normalized;
            float lowHealth = health < 0.35f ? (0.35f - health) / 0.35f * (0.6f + 0.4f * Mathf.Sin(Time.time * 6.0f)) : 0.0f;
            Color color = _vignette.color;
            color.a = Mathf.Clamp01(_damageFlash * 0.8f + lowHealth * 0.6f) * Mathf.Lerp(0.3f, 1.0f, GameSettings.InjuryEffects);
            _vignette.color = color;

            // arrows around the crosshair point towards whoever hit us
            Transform cam = _camera.transform;
            for (int i = _arrows.Count - 1; i >= 0; i--)
            {
                var (arrow, image, from, time) = _arrows[i];
                float age = Time.time - time;
                if (age > 1.5f)
                {
                    Destroy(arrow.gameObject);
                    _arrows.RemoveAt(i);
                    continue;
                }

                Vector3 toAttacker = from - cam.position;
                float angle = Vector3.SignedAngle(Vector3.ProjectOnPlane(cam.forward, Vector3.up), Vector3.ProjectOnPlane(toAttacker, Vector3.up), Vector3.up);
                arrow.localRotation = Quaternion.Euler(0.0f, 0.0f, -angle);
                Color c = image.color;
                c.a = 1.0f - age / 1.5f;
                image.color = c;
            }
        }

        // ---------------------------------------------------------------- events

        private void OnLandedHit(HitReport report)
        {
            _hitTime = Time.time;
            _hitColor = report.Killed ? Danger : report.Headshot ? Accent : Color.white;
            _hitScale = report.Killed ? 1.5f : report.Headshot ? 1.25f : 1.0f;

            if (report.Killed) ShowNotice(report.Headshot ? "<color=#FFC74D>HEADSHOT</color>  +1 ABATE" : "+1 ABATE");
        }

        private void OnTookDamage(DamageInfo info)
        {
            _damageFlash = Mathf.Min(1.0f, _damageFlash + 0.25f + info.Amount / 60.0f);
            if (info.Instigator == null) return;

            var pivot = new GameObject("DamageArrow", typeof(RectTransform)).GetComponent<RectTransform>();
            pivot.SetParent(_root, false);
            Image arrow = Panel("Arrow", pivot, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.0f, 140.0f), new Vector2(46.0f, 10.0f), Danger);
            _arrows.Add((pivot, arrow, info.Instigator.transform.position, Time.time));
        }

        private void ShowNotice(string text)
        {
            _notice.text = text;
            _noticeTime = Time.time;
        }
    }
}
