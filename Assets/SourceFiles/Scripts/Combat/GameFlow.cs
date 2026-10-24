using System;
using StarterAssets;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Combat
{
    /* Main menu, pause, lives and game over around the wave mode.
     * The scene opens on the menu with a slow flyover of the level; Play starts the waves.
     */
    [DefaultExecutionOrder(-100)]
    public class GameFlow : MonoBehaviour
    {
        public enum State
        {
            Menu,
            Playing,
            Paused,
            GameOver
        }

        [Header("Menu Flyover")]
        [Tooltip("Point the menu camera circles around, the spawner by default")]
        public Transform FlyoverCenter;
        public float FlyoverRadius = 48.0f;
        public float FlyoverHeight = 20.0f;
        public float FlyoverSpeed = 4.0f;

        [Header("Audio")]
        public AudioClip MenuMusic;
        public AudioClip GameMusic;
        [Range(0, 1)] public float MusicVolume = 0.35f;
        public AudioClip ClickSound;
        public AudioClip WaveStartSound;
        public AudioClip WaveClearedSound;
        [Tooltip("Played now and then far from the player while playing: distant groans, metal, wind")]
        public AudioClip[] AmbientSounds;

        [Header("Score")]
        public int ZombiePoints = 100;
        public int SoldierPoints = 150;
        public int HeadshotBonus = 50;
        public int WaveBonus = 300;
        [Tooltip("Kills within this many seconds of each other build a combo")]
        public float ComboWindow = 3.0f;

        public static GameFlow Instance { get; private set; }

        public State Current { get; private set; } = State.Menu;
        public int LivesLeft { get; private set; }
        public DifficultySettings Difficulty { get; private set; }
        public EnemySpawner Spawner => _spawner;

        public event Action<int> WaveAnnounced;
        // points and a short reason ("HEADSHOT", "COMBO x2") for the HUD popup
        public event Action<int, string> ScoreAdded;

        public int Score { get; private set; }

        private const string BestWaveKey = "Combat.BestWave";
        private const string BestScoreKey = "Combat.BestScore";
        private int _combo;
        private float _comboUntil;
        private float _nextAmbient;
        // set before reloading the scene to skip the menu ("play again")
        private static bool _startImmediately;
        private static GameMode _restartMode = GameMode.Endless;

        [Header("Economy")]
        public int ZombieCredits = 10;
        public int SoldierCredits = 15;
        public int SpecialCredits = 10;
        [Tooltip("Chance of a zombie killed by the player dropping a little ammo (scaled by the difficulty's supplies)")]
        [Range(0, 1)] public float AmmoDropChance = 0.12f;
        [Tooltip("Between waves health is brought back up to at least this fraction (medkits do the rest)")]
        [Range(0, 1)] public float WaveHealFloor = 0.6f;

        private GameObject _progressPanel;
        private TextMeshProUGUI _progressText;
        private GameObject _completePanel;
        private TextMeshProUGUI _completeStats;
        private TextMeshProUGUI _campaignButtonLabel;
        private float _baseStamina;
        private AudioClip _gameMusic;

        private PlayerCombat _player;
        private StarterAssetsInputs _input;
        private EnemySpawner _spawner;
        private CombatHUD _hud;
        private Camera _gameCamera;
        private int _gameCameraMask;
        private Camera _menuCamera;
        private AudioSource _music;
        private float _musicLevel;
        private float _baseRegen;

        private GameObject _menuPanel;
        private GameObject _controlsPanel;
        private GameObject _pausePanel;
        private GameObject _gameOverPanel;
        private TextMeshProUGUI _difficultyLabel;
        private TextMeshProUGUI _difficultyDescription;
        private TextMeshProUGUI _bestLabel;
        private TextMeshProUGUI _gameOverStats;

        private GameObject _optionsPanel;
        private readonly System.Collections.Generic.List<Action> _optionRefreshers = new System.Collections.Generic.List<Action>();
        private UnityEngine.Rendering.Universal.ColorAdjustments _gradeColor;
        private UnityEngine.Rendering.Universal.FilmGrain _gradeGrain;
        private float _basePostExposure;
        private float _baseGrain;

        private GameObject _menuBand;
        private GameObject _wardrobePanel;
        private PlayerOutfit _outfit;
        private TextMeshProUGUI[] _wardrobeValues;
        private Quaternion _playerRotation;

        private void Awake()
        {
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            Time.timeScale = 1.0f;
            GameSettings.Changed -= ApplySettings;
            Health.AnyDied -= OnAnyDied;
            Progression.Flush();
        }

        // the night grade on the global volume: brightness and grain follow the settings
        private void FindGrade()
        {
            foreach (UnityEngine.Rendering.Volume volume in FindObjectsByType<UnityEngine.Rendering.Volume>())
            {
                // the night grade, not the injury overlay that lives on the player
                if (!volume.isGlobal || volume.sharedProfile == null || volume.GetComponentInParent<PlayerInjuryFeedback>() != null) continue;
                // a runtime copy, so the settings never change the saved asset
                UnityEngine.Rendering.VolumeProfile profile = volume.profile;
                if (profile.TryGet(out _gradeColor)) _basePostExposure = _gradeColor.postExposure.value;
                if (profile.TryGet(out _gradeGrain)) _baseGrain = _gradeGrain.intensity.value;
                if (_gradeColor != null) break;
            }
        }

        private void ApplySettings()
        {
            if (_gradeColor != null) _gradeColor.postExposure.Override(_basePostExposure + GameSettings.Brightness * 0.9f);
            if (_gradeGrain != null) _gradeGrain.intensity.Override(_baseGrain * GameSettings.Grain);
        }

        private void Start()
        {
            AudioListener.volume = GameSettings.MasterVolume;
            _player = FindAnyObjectByType<PlayerCombat>();
            _input = _player != null ? _player.GetComponent<StarterAssetsInputs>() : null;
            _spawner = FindAnyObjectByType<EnemySpawner>();
            _hud = FindAnyObjectByType<CombatHUD>();
            _gameCamera = Camera.main;
            if (_gameCamera != null) _gameCameraMask = _gameCamera.cullingMask;
            if (_player != null)
            {
                _baseRegen = _player.Health.RegenRate;
                _baseStamina = _player.Controller.MaxStamina;
            }

            _gameMusic = GameMusic;
            Health.AnyDied += OnAnyDied;
            if (CampaignManager.Instance != null) CampaignManager.Instance.CampaignCompleted += ShowCampaignComplete;
            if (FlyoverCenter == null && _spawner != null) FlyoverCenter = _spawner.transform;

            if (_spawner != null)
            {
                _spawner.UseWaves = true;
                _spawner.StopWaves();
                _spawner.WaveStarted += OnWaveStarted;
                _spawner.WaveCleared += OnWaveCleared;
            }

            if (_player != null)
            {
                _player.PlayerDied += OnPlayerDied;
                _player.LandedHit += OnLandedHit;
                _outfit = _player.GetComponent<PlayerOutfit>();
                _playerRotation = _player.transform.rotation;
            }

            _music = gameObject.AddComponent<AudioSource>();
            _music.loop = true;
            _music.spatialBlend = 0.0f;
            _music.volume = MusicVolume * GameSettings.MusicVolume;
            _musicLevel = MusicVolume;

            EnsureEventSystem();
            FindGrade();
            GameSettings.Changed += ApplySettings;
            ApplySettings();
            BuildMenus();

            if (_startImmediately)
            {
                _startImmediately = false;
                StartGame(_restartMode);
            }
            else
            {
                EnterMenu();
            }
        }

        private void Update()
        {
            if (Current == State.Menu)
            {
                if (_wardrobePanel != null && _wardrobePanel.activeSelf) UpdateWardrobeCamera();
                else UpdateFlyover();
                return;
            }

            if (Current == State.Playing)
            {
                UpdateAmbience();
                // the music swells in a fight and drops back in quiet moments
                // a boss fight has its own music
                AudioClip bossMusic = CampaignAssets.Clip(a => a.BossMusic);
                bool boss = BossAI.AnyAlive && bossMusic != null;
                PlayMusic(boss ? bossMusic : _gameMusic);
                if (_music != null)
                {
                    float level = boss ? MusicVolume : MusicVolume * TensionDirector.MusicScale;
                    _musicLevel = Mathf.MoveTowards(_musicLevel, level, Time.deltaTime * (boss ? 1.0f : 0.15f));
                    // dialogue ducks it; the player's music volume scales it
                    _music.volume = _musicLevel * DialogueDirector.MusicDuck * GameSettings.MusicVolume;
                }
            }

            if (_input != null && _input.pause)
            {
                _input.pause = false;
                // Esc first closes whatever is in front: the shop, a document, a cinematic
                if (ShopManager.IsOpen) ShopManager.Instance.Close();
                else if (CampaignHUD.ReaderOpen || CinematicPlayer.Playing) { }
                else if (Current == State.Playing) Pause();
                else if (Current == State.Paused) Resume();
            }
        }

        // ---------------------------------------------------------------- states

        private void EnterMenu()
        {
            Current = State.Menu;
            Time.timeScale = 1.0f;
            SetGameplayActive(false);
            ShowOnly(_menuPanel);
            RefreshMenu();
            PlayMusic(MenuMusic);

            if (_menuCamera == null)
            {
                var cameraObject = new GameObject("MenuCamera");
                _menuCamera = cameraObject.AddComponent<Camera>();
                // same look as the game camera: post processing for the night grade, vignette and grain
                var cameraData = cameraObject.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
                cameraData.renderPostProcessing = true;
                _menuCamera.fieldOfView = 50.0f;
                _menuCamera.farClipPlane = 600.0f;
                // draw over the game camera, which is told to draw nothing meanwhile
                _menuCamera.depth = (_gameCamera != null ? _gameCamera.depth : 0.0f) + 10.0f;
            }

            _menuCamera.gameObject.SetActive(true);
            if (_gameCamera != null) _gameCamera.cullingMask = 0;
        }

        public void StartGame()
        {
            StartGame(GameMode.Endless);
        }

        public void StartGame(GameMode mode)
        {
            if (_wardrobePanel != null && _wardrobePanel.activeSelf) CloseWardrobe();
            _restartMode = mode;
            RunState.Reset(mode);
            if (MissionManager.Instance != null) MissionManager.Instance.Clear();
            if (ShopManager.Instance != null) ShopManager.Instance.ResetForRun();
            if (DialogueDirector.Instance != null) DialogueDirector.Instance.ResetAll();
            PickupFeed.Clear();
            _player.Controller.MaxStamina = _baseStamina;
            _player.Health.Armor = 0.0f;
            _player.GetComponent<InventoryManager>()?.ResetForRun();
            // the campaign starts with nothing in hand: the first objective is finding a gun
            _player.StartUnarmed = mode == GameMode.Campaign && Progression.CampaignChapter == 0;
            Difficulty = GameDifficulty.Settings;
            LivesLeft = Difficulty.Lives;
            Score = 0;
            _combo = 0;
            _nextAmbient = Time.time + 6.0f;
            Current = State.Playing;
            Time.timeScale = 1.0f;

            if (_menuCamera != null) _menuCamera.gameObject.SetActive(false);
            if (_gameCamera != null) _gameCamera.cullingMask = _gameCameraMask;

            SetGameplayActive(true);
            ShowOnly(null);
            _player.Health.RegenRate = _baseRegen * Difficulty.PlayerRegen;
            _player.ResetForNewGame();
            if (mode == GameMode.Campaign && CampaignManager.Instance != null)
            {
                CampaignManager.Instance.Begin(Progression.CampaignChapter);
            }
            else if (_spawner != null)
            {
                _spawner.BeginWaves(Difficulty);
            }

            PlayMusic(GameMusic);
        }

        private void Pause()
        {
            Current = State.Paused;
            Time.timeScale = 0.0f;
            ShowOnly(_pausePanel);
            SetCursor(false);
            AudioListener.pause = true;
        }

        private void Resume()
        {
            Current = State.Playing;
            Time.timeScale = 1.0f;
            ShowOnly(null);
            SetCursor(true);
            AudioListener.pause = false;
        }

        private void GameOver()
        {
            Current = State.GameOver;
            int wave = _spawner != null ? _spawner.Wave : 0;
            int bestWave = PlayerPrefs.GetInt(BestWaveKey, 0);
            int bestScore = PlayerPrefs.GetInt(BestScoreKey, 0);
            bool record = Score > bestScore;
            if (wave > bestWave) PlayerPrefs.SetInt(BestWaveKey, wave);
            if (record) PlayerPrefs.SetInt(BestScoreKey, Score);
            PlayerPrefs.Save();

            _gameOverStats.text = $"<size=130%><color=#FFC74D>{FormatPoints(Score)} PONTOS</color></size>\n" +
                                  $"Onda {wave}  ·  {_player.Kills} {(_player.Kills == 1 ? "abate" : "abates")}  ·  {Difficulty.Name}\n" +
                                  (record ? "<color=#FFC74D>NOVO RECORDE!</color>" : $"Recorde: {FormatPoints(Mathf.Max(bestScore, Score))} pontos");
            if (RunState.Mode == GameMode.Endless) Progression.RecordEndlessWave(Mathf.Max(0, wave - 1));
            if (CampaignManager.Instance != null) CampaignManager.Instance.Stop();
            if (ShopManager.IsOpen) ShopManager.Instance.Close();
            _gameOverStats.text += $"\n<size=80%>{RunState.CreditsEarned} créditos ganhos  ·  {RunState.MissionsCompleted} {(RunState.MissionsCompleted == 1 ? "missão" : "missões")}  ·  nível {Progression.Level}</size>";
            Progression.Flush();
            ShowOnly(_gameOverPanel);
            SetCursor(false);
            // the HUD's own death screen would show through the summary
            if (_hud != null) _hud.SetVisible(false);
            if (_spawner != null) _spawner.StopWaves();
        }

        private void Restart()
        {
            _startImmediately = true;
            Reload();
        }

        private void BackToMenu()
        {
            _startImmediately = false;
            Reload();
        }

        private void Reload()
        {
            Time.timeScale = 1.0f;
            AudioListener.pause = false;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex >= 0
                ? SceneManager.GetActiveScene().buildIndex
                : 0);
        }

        private void SetGameplayActive(bool active)
        {
            if (_player != null)
            {
                _player.enabled = active;
                var controller = _player.GetComponent<ThirdPersonController>();
                if (controller != null) controller.enabled = active;
                var climbing = _player.GetComponent<ClimbingSystem>();
                if (climbing != null) climbing.enabled = active;
            }

            if (_hud != null) _hud.SetVisible(active);
            SetCursor(active);
        }

        private void SetCursor(bool locked)
        {
            if (_input != null) _input.SetCursorLocked(locked);
            else
            {
                Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
                Cursor.visible = !locked;
            }
        }

        private void UpdateFlyover()
        {
            if (_menuCamera == null || FlyoverCenter == null) return;

            float angle = Time.unscaledTime * FlyoverSpeed * Mathf.Deg2Rad;
            Vector3 center = FlyoverCenter.position;
            Vector3 offset = new Vector3(Mathf.Cos(angle), 0.0f, Mathf.Sin(angle)) * FlyoverRadius + Vector3.up * FlyoverHeight;
            _menuCamera.transform.position = center + offset;
            _menuCamera.transform.LookAt(center + Vector3.up * 2.0f);
        }

        private void PlayMusic(AudioClip clip)
        {
            if (_music == null || clip == null || _music.clip == clip) return;
            _music.clip = clip;
            _music.Play();
        }

        // ---------------------------------------------------------------- events

        private void OnPlayerDied(DamageInfo info)
        {
            if (Current != State.Playing) return;

            LivesLeft--;
            if (LivesLeft <= 0)
            {
                _player.AllowRespawn = false;
                // let the ragdoll fall for a moment before the screen comes up
                Invoke(nameof(GameOver), 2.0f);
            }
        }

        private void OnLandedHit(HitReport report)
        {
            if (!report.Killed || report.Target == null || Current != State.Playing) return;

            var zombie = report.Target.GetComponent<ZombieAI>();
            var boss = report.Target.GetComponent<BossAI>();
            int points = boss != null ? boss.Definition.Score : zombie != null ? ZombiePoints : SoldierPoints;
            // special infected are worth more
            if (zombie != null && zombie.AppliedVariant != null) points += zombie.AppliedVariant.BonusPoints;
            string reason = "";
            if (report.Headshot)
            {
                points += HeadshotBonus;
                reason = "HEADSHOT";
            }

            // quick successive kills chain into a combo, up to double points
            _combo = Time.time < _comboUntil ? _combo + 1 : 0;
            _comboUntil = Time.time + ComboWindow;
            float multiplier = 1.0f + Mathf.Min(_combo, 4) * 0.25f;
            if (_combo > 0) reason = (reason.Length > 0 ? reason + "  " : "") + $"COMBO x{multiplier:0.##}";

            AddScore(Mathf.RoundToInt(points * multiplier * Difficulty.ScoreMultiplier), reason);
        }

        // missions and other bonuses go through the difficulty multiplier like everything else
        public void AddBonusScore(int points, string reason)
        {
            if (Current == State.Playing) AddScore(Mathf.RoundToInt(points * Difficulty.ScoreMultiplier), reason);
        }

        // credits, experience, kill records and the odd ammo drop: kills by the player in any way (gun, melee, grenade, fire)
        private void OnAnyDied(Health victim, DamageInfo info)
        {
            if (Current != State.Playing || _player == null || victim == _player.Health) return;
            if (info.Instigator != _player.gameObject) return;

            var zombie = victim.GetComponent<ZombieAI>();
            bool special = zombie != null && zombie.Kind != ZombieKind.Common;
            if (victim.GetComponent<BossAI>() == null)
            {
                int credits = zombie != null ? ZombieCredits : SoldierCredits;
                if (special) credits += SpecialCredits;
                RunState.AddCredits(credits, null);
            }

            RunState.Kills++;
            if (special) RunState.SpecialKills++;
            Progression.RecordKill(zombie != null);
            Progression.AddExperience(special ? 15 : 5, "abate", false);

            if (zombie != null && UnityEngine.Random.value < AmmoDropChance * Difficulty.Supplies)
            {
                ConsumablePickup.Spawn(ConsumablePickup.Kind.Ammo, victim.transform.position + Vector3.up * 0.2f, 30.0f);
            }
        }

        private void AddScore(int points, string reason)
        {
            Score += points;
            ScoreAdded?.Invoke(points, reason);
        }

        public static string FormatPoints(int points)
        {
            return points.ToString("N0").Replace(',', '.');
        }

        private void UpdateAmbience()
        {
            if (AmbientSounds == null || AmbientSounds.Length == 0 || _player == null || Time.time < _nextAmbient) return;

            _nextAmbient = Time.time + UnityEngine.Random.Range(7.0f, 16.0f);
            Vector2 direction = UnityEngine.Random.insideUnitCircle.normalized;
            Vector3 position = _player.transform.position + new Vector3(direction.x, 0.5f, direction.y) * UnityEngine.Random.Range(25.0f, 45.0f);
            if (CombatEffects.Instance != null)
            {
                AudioClip clip = AmbientSounds[UnityEngine.Random.Range(0, AmbientSounds.Length)];
                // heavy rain covers the distant sounds
                CombatEffects.Instance.PlaySound(clip, position, 0.55f * (1.0f - WeatherController.AmbientMasking), 0.12f, 1.0f, 8.0f, 90.0f);
            }
        }

        private void OnWaveStarted(int wave)
        {
            WaveAnnounced?.Invoke(wave);
            // chests shut and refill for every wave
            foreach (LootCrate crate in LootCrate.All) crate.Restock();
            if (CombatEffects.Instance != null && WaveStartSound != null) CombatEffects.Instance.PlayUI(WaveStartSound, 0.8f);
        }

        private void OnWaveCleared(int wave)
        {
            if (Current == State.Playing)
            {
                AddScore(Mathf.RoundToInt(WaveBonus * wave * Difficulty.ScoreMultiplier), $"ONDA {wave} VENCIDA");
                RunState.AddCredits(40 + 10 * wave, $"onda {wave}");
                RunState.WavesSurvived++;
                Progression.AddExperience(50, "onda");
                Progression.RecordWave(RunState.WavesSurvived);
                if (RunState.BasicOnly) Progression.Unlock("basic_wave");
            }

            RunState.BasicOnly = true;

            // a breather: patched up to a decent level and stamina back; medkits still matter for the rest
            if (_player != null && !_player.Health.IsDead)
            {
                float floor = _player.Health.MaxHealth * WaveHealFloor;
                if (_player.Health.Current < floor) _player.Health.Heal(floor - _player.Health.Current);
                _player.Controller.RefillStamina();
            }

            if (CombatEffects.Instance != null && WaveClearedSound != null) CombatEffects.Instance.PlayUI(WaveClearedSound, 0.8f);
        }

        // ---------------------------------------------------------------- menus

        private static readonly Color Accent = new Color(1.0f, 0.78f, 0.3f);
        private static readonly Color PanelColor = new Color(0.05f, 0.06f, 0.08f, 0.82f);

        private void EnsureEventSystem()
        {
            if (FindAnyObjectByType<EventSystem>() != null) return;
            var eventSystem = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            eventSystem.transform.SetParent(transform);
        }

        private void BuildMenus()
        {
            var canvasObject = new GameObject("GameMenus", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920.0f, 1080.0f);
            scaler.matchWidthOrHeight = 0.5f;
            var root = (RectTransform)canvasObject.transform;

            // main menu: title on the left over a dark band, the level flying by behind
            _menuPanel = Panel("MainMenu", root, Vector2.zero, Vector2.one, new Color(0, 0, 0, 0)).gameObject;
            Image band = Panel("Band", (RectTransform)_menuPanel.transform, new Vector2(0.0f, 0.0f), new Vector2(0.42f, 1.0f), new Color(0.01f, 0.01f, 0.02f, 0.82f));
            _menuBand = band.gameObject;
            RectTransform menu = band.rectTransform;
            Label(menu, "BASE MILITAR", 96, new Vector2(0.1f, 0.78f), new Vector2(0.95f, 0.92f), TextAlignmentOptions.BottomLeft, FontStyles.Bold, Color.white);
            Label(menu, "SOBREVIVA À NOITE", 34, new Vector2(0.1f, 0.72f), new Vector2(0.95f, 0.78f), TextAlignmentOptions.TopLeft, FontStyles.Normal, new Color(0.85f, 0.2f, 0.18f));
            _bestLabel = Label(menu, "", 24, new Vector2(0.1f, 0.67f), new Vector2(0.95f, 0.72f), TextAlignmentOptions.TopLeft, FontStyles.Normal, new Color(1, 1, 1, 0.6f));

            Button(menu, "CAMPANHA", new Vector2(0.1f, 0.585f), () => StartGame(GameMode.Campaign), true);
            _campaignButtonLabel = menu.Find("Button_CAMPANHA").GetComponentInChildren<TextMeshProUGUI>();
            Button(menu, "MODO INFINITO", new Vector2(0.1f, 0.505f), () => StartGame(GameMode.Endless), false);
            Label(menu, "DIFICULDADE", 22, new Vector2(0.1f, 0.455f), new Vector2(0.9f, 0.485f), TextAlignmentOptions.BottomLeft, FontStyles.Normal, new Color(1, 1, 1, 0.6f));
            SmallButton(menu, "<", new Vector2(0.1f, 0.385f), () => ChangeDifficulty(-1));
            _difficultyLabel = Label(menu, "NORMAL", 40, new Vector2(0.17f, 0.38f), new Vector2(0.6f, 0.45f), TextAlignmentOptions.Center, FontStyles.Bold, Color.white);
            SmallButton(menu, ">", new Vector2(0.63f, 0.385f), () => ChangeDifficulty(1));
            _difficultyDescription = Label(menu, "", 22, new Vector2(0.1f, 0.33f), new Vector2(0.9f, 0.378f), TextAlignmentOptions.TopLeft, FontStyles.Normal, new Color(1, 1, 1, 0.75f));
            CompactButton(menu, "PERSONAGEM", new Vector2(0.1f, 0.265f), OpenWardrobe);
            CompactButton(menu, "PROGRESSO", new Vector2(0.1f, 0.205f), () => ToggleSidePanel(_progressPanel));
            CompactButton(menu, "CONTROLES", new Vector2(0.1f, 0.145f), () => ToggleSidePanel(_controlsPanel));
            CompactButton(menu, "OPÇÕES", new Vector2(0.1f, 0.085f), () => ToggleSidePanel(_optionsPanel));
            CompactButton(menu, "SAIR", new Vector2(0.1f, 0.025f), Quit);

            BuildWardrobe((RectTransform)_menuPanel.transform);

            Image controls = Panel("Controls", (RectTransform)_menuPanel.transform, new Vector2(0.5f, 0.18f), new Vector2(0.92f, 0.82f), PanelColor);
            Label(controls.rectTransform, "CONTROLES", 40, new Vector2(0.06f, 0.84f), new Vector2(0.94f, 0.96f), TextAlignmentOptions.Left, FontStyles.Bold, Accent);
            Label(controls.rectTransform,
                "<b>WASD</b>  andar        <b>Shift</b>  correr (gasta estamina)\n" +
                "<b>Espaço</b>  pular, pulo duplo no ar      <b>C</b>  agachar (silencioso)\n" +
                "<b>Ctrl</b>  rolar (invulnerável no começo)\n" +
                "<b>Botão esquerdo</b>  atirar      <b>Botão direito</b>  mirar\n" +
                "<b>V</b>  coronhada / faca      <b>B</b>  empurrar      <b>T</b>  trocar ombro\n" +
                "<b>R</b>  recarregar      <b>E</b>  usar: armas, baús, portas, geradores, painéis\n" +
                "<b>Q</b>  trocar de arma      <b>G</b>  largar arma      <b>F</b>  lanterna\n" +
                "<b>1-6</b> / <b>Z</b>  escolher item      <b>X</b>  usar item (segure para mirar o arremesso)\n" +
                "<b>L</b>  loja (no intervalo)      <b>N</b>  próxima onda      <b>K</b>  abandonar missão opcional\n" +
                "Barulho atrai os infectados: correr no metal e atirar são ouvidos de longe",
                24, new Vector2(0.06f, 0.06f), new Vector2(0.94f, 0.82f), TextAlignmentOptions.TopLeft, FontStyles.Normal, Color.white);
            _controlsPanel = controls.gameObject;
            _controlsPanel.SetActive(false);

            BuildOptions((RectTransform)_menuPanel.transform);
            BuildProgress((RectTransform)_menuPanel.transform);
            BuildCampaignComplete(root);

            // pause
            Image pause = Panel("Pause", root, Vector2.zero, Vector2.one, new Color(0, 0, 0, 0.6f));
            Label(pause.rectTransform, "PAUSADO", 84, new Vector2(0.3f, 0.6f), new Vector2(0.7f, 0.75f), TextAlignmentOptions.Center, FontStyles.Bold, Color.white);
            Button(pause.rectTransform, "CONTINUAR", new Vector2(0.4f, 0.47f), Resume, true);
            Button(pause.rectTransform, "REINICIAR", new Vector2(0.4f, 0.37f), Restart, false);
            Button(pause.rectTransform, "MENU PRINCIPAL", new Vector2(0.4f, 0.27f), BackToMenu, false);
            _pausePanel = pause.gameObject;

            // game over
            Image over = Panel("GameOver", root, Vector2.zero, Vector2.one, new Color(0.12f, 0.0f, 0.0f, 0.7f));
            Label(over.rectTransform, "FIM DE JOGO", 96, new Vector2(0.2f, 0.64f), new Vector2(0.8f, 0.8f), TextAlignmentOptions.Center, FontStyles.Bold, new Color(1.0f, 0.3f, 0.25f));
            _gameOverStats = Label(over.rectTransform, "", 36, new Vector2(0.2f, 0.46f), new Vector2(0.8f, 0.64f), TextAlignmentOptions.Center, FontStyles.Normal, Color.white);
            Button(over.rectTransform, "JOGAR DE NOVO", new Vector2(0.4f, 0.32f), Restart, true);
            Button(over.rectTransform, "MENU PRINCIPAL", new Vector2(0.4f, 0.22f), BackToMenu, false);
            _gameOverPanel = over.gameObject;
        }

        private void ShowOnly(GameObject panel)
        {
            _menuPanel.SetActive(panel == _menuPanel);
            _pausePanel.SetActive(panel == _pausePanel);
            _gameOverPanel.SetActive(panel == _gameOverPanel);
            _completePanel.SetActive(panel == _completePanel);
            if (panel != _menuPanel)
            {
                _controlsPanel.SetActive(false);
                _optionsPanel.SetActive(false);
                _progressPanel.SetActive(false);
                _wardrobePanel.SetActive(false);
                _menuBand.SetActive(true);
            }
        }

        private void ToggleSidePanel(GameObject panel)
        {
            bool show = !panel.activeSelf;
            _controlsPanel.SetActive(false);
            _optionsPanel.SetActive(false);
            _progressPanel.SetActive(false);
            panel.SetActive(show);
            if (show && panel == _optionsPanel) RefreshOptions();
            if (show && panel == _progressPanel) RefreshProgress();
        }

        // ---------------------------------------------------------------- options

        private void BuildOptions(RectTransform parent)
        {
            Image panel = Panel("Options", parent, new Vector2(0.5f, 0.08f), new Vector2(0.94f, 0.92f), PanelColor);
            _optionsPanel = panel.gameObject;
            RectTransform rect = panel.rectTransform;
            Label(rect, "OPÇÕES E ACESSIBILIDADE", 36, new Vector2(0.06f, 0.89f), new Vector2(0.94f, 0.97f), TextAlignmentOptions.Left, FontStyles.Bold, Accent);

            int row = 0;
            OptionRow(rect, row++, "Brilho", () => Percent(GameSettings.Brightness, -1.0f, 1.0f),
                step => GameSettings.Brightness = Mathf.Clamp(GameSettings.Brightness + step * 0.1f, -1.0f, 1.0f));
            OptionRow(rect, row++, "Granulação", () => Percent(GameSettings.Grain, 0.0f, 1.0f),
                step => GameSettings.Grain = Mathf.Clamp01(GameSettings.Grain + step * 0.1f));
            OptionRow(rect, row++, "Tremor de câmera", () => Percent(GameSettings.CameraShake, 0.0f, 1.0f),
                step => GameSettings.CameraShake = Mathf.Clamp01(GameSettings.CameraShake + step * 0.1f));
            OptionRow(rect, row++, "Flashes (raios, refletores)", () => Percent(GameSettings.Flashes, 0.0f, 1.0f),
                step => GameSettings.Flashes = Mathf.Clamp01(GameSettings.Flashes + step * 0.1f));
            OptionRow(rect, row++, "Efeitos de ferimento", () => Percent(GameSettings.InjuryEffects, 0.0f, 1.0f),
                step => GameSettings.InjuryEffects = Mathf.Clamp01(GameSettings.InjuryEffects + step * 0.1f));
            OptionRow(rect, row++, "Legendas de sons", () => GameSettings.Subtitles ? "LIGADAS" : "DESLIGADAS",
                step => GameSettings.Subtitles = !GameSettings.Subtitles);
            OptionRow(rect, row++, "Setas de inimigos", () => ArrowLabel(GameSettings.ThreatArrows),
                step => GameSettings.ThreatArrows = (ThreatArrowMode)(((int)GameSettings.ThreatArrows + step + 4) % 4));
            OptionRow(rect, row++, "Marcador de objetivo", () => GameSettings.ObjectiveHints ? "LIGADO" : "DESLIGADO",
                step => GameSettings.ObjectiveHints = !GameSettings.ObjectiveHints);

            // second column: dialogue and sound
            row = 0;
            OptionRow(rect, row++, "Legendas de diálogo", () => GameSettings.DialogueSubtitles ? "LIGADAS" : "DESLIGADAS",
                step => GameSettings.DialogueSubtitles = !GameSettings.DialogueSubtitles, 1);
            OptionRow(rect, row++, "Tamanho das legendas", () => Mathf.RoundToInt(GameSettings.SubtitleScale * 100.0f) + "%",
                step => GameSettings.SubtitleScale = GameSettings.SubtitleScale + step * 0.1f, 1);
            OptionRow(rect, row++, "Volume geral", () => Percent(GameSettings.MasterVolume, 0.0f, 1.0f),
                step => GameSettings.MasterVolume = GameSettings.MasterVolume + step * 0.1f, 1);
            OptionRow(rect, row++, "Música", () => Percent(GameSettings.MusicVolume, 0.0f, 1.0f),
                step => GameSettings.MusicVolume = GameSettings.MusicVolume + step * 0.1f, 1);
            OptionRow(rect, row++, "Efeitos", () => Percent(GameSettings.SfxVolume, 0.0f, 1.0f),
                step => GameSettings.SfxVolume = GameSettings.SfxVolume + step * 0.1f, 1);
            OptionRow(rect, row++, "Diálogos e rádio", () => Percent(GameSettings.DialogueVolume, 0.0f, 1.0f),
                step => GameSettings.DialogueVolume = GameSettings.DialogueVolume + step * 0.1f, 1);
            OptionRow(rect, row++, "Ambiente", () => Percent(GameSettings.AmbientVolume, 0.0f, 1.0f),
                step => GameSettings.AmbientVolume = GameSettings.AmbientVolume + step * 0.1f, 1);
            OptionRow(rect, row++, "Interface", () => Percent(GameSettings.UiVolume, 0.0f, 1.0f),
                step => GameSettings.UiVolume = GameSettings.UiVolume + step * 0.1f, 1);

            Label(rect, "Brilho e granulação mudam na hora. \"Automático\" mostra todas as setas no Fácil e Normal e só as dos inimigos que te viram no Difícil e Pesadelo.",
                18, new Vector2(0.06f, 0.015f), new Vector2(0.94f, 0.085f), TextAlignmentOptions.TopLeft, FontStyles.Normal, new Color(1, 1, 1, 0.6f)).textWrappingMode = TextWrappingModes.Normal;
            _optionsPanel.SetActive(false);
        }

        // two columns of rows: name, <, value, >
        private void OptionRow(RectTransform parent, int row, string name, Func<string> value, Action<int> change, int column = 0)
        {
            float y = 0.79f - row * 0.088f;
            float x = column == 0 ? 0.04f : 0.52f;
            Label(parent, name, 21, new Vector2(x, y), new Vector2(x + 0.2f, y + 0.07f), TextAlignmentOptions.Left, FontStyles.Normal, new Color(1, 1, 1, 0.75f));
            TextMeshProUGUI label = null;
            SmallButton(parent, "<", new Vector2(x + 0.205f, y), () =>
            {
                change(-1);
                label.text = value();
            }, 50.0f);
            label = Label(parent, value(), 22, new Vector2(x + 0.265f, y), new Vector2(x + 0.38f, y + 0.07f), TextAlignmentOptions.Center, FontStyles.Bold, Color.white);
            SmallButton(parent, ">", new Vector2(x + 0.385f, y), () =>
            {
                change(1);
                label.text = value();
            }, 50.0f);
            _optionRefreshers.Add(() => label.text = value());
        }

        private void RefreshOptions()
        {
            foreach (Action refresh in _optionRefreshers) refresh();
        }

        private static string Percent(float value, float min, float max)
        {
            return Mathf.RoundToInt(Mathf.InverseLerp(min, max, value) * 100.0f) + "%";
        }

        private static string ArrowLabel(ThreatArrowMode mode)
        {
            switch (mode)
            {
                case ThreatArrowMode.Off: return "DESLIGADAS";
                case ThreatArrowMode.AwareOnly: return "SÓ ALERTAS";
                case ThreatArrowMode.All: return "TODAS";
                default: return "AUTOMÁTICO";
            }
        }

        // ---------------------------------------------------------------- progress

        private void BuildProgress(RectTransform parent)
        {
            Image panel = Panel("Progress", parent, new Vector2(0.5f, 0.08f), new Vector2(0.94f, 0.92f), PanelColor);
            _progressPanel = panel.gameObject;
            RectTransform rect = panel.rectTransform;
            Label(rect, "PROGRESSO", 40, new Vector2(0.06f, 0.88f), new Vector2(0.94f, 0.97f), TextAlignmentOptions.Left, FontStyles.Bold, Accent);
            _progressText = Label(rect, "", 22, new Vector2(0.06f, 0.14f), new Vector2(0.94f, 0.87f), TextAlignmentOptions.TopLeft, FontStyles.Normal, Color.white);
            _progressText.textWrappingMode = TextWrappingModes.Normal;
            CompactButton(rect, "RECOMEÇAR CAMPANHA", new Vector2(0.06f, 0.03f), () =>
            {
                Progression.RestartCampaign();
                RefreshProgress();
                RefreshMenu();
            });
            _progressPanel.SetActive(false);
        }

        private void RefreshProgress()
        {
            var text = new System.Text.StringBuilder();
            int level = Progression.Level;
            int bar = Mathf.RoundToInt(Progression.LevelProgress * 20);
            text.Append($"<b>NÍVEL {level}</b>   <color=#FFC74D>{new string('|', bar)}</color><color=#555555>{new string('|', 20 - bar)}</color>   {Progression.Experience} XP\n");
            text.Append($"<size=85%>Abates: {Progression.TotalKills}   ·   Missões: {Progression.MissionsCompleted}   ·   Melhor onda no Modo Infinito: {Progression.BestEndlessWave}\n");
            text.Append($"Campanha: {(Progression.CampaignCompleted ? "<color=#9BE39B>concluída</color>" : $"onda {Mathf.Min(10, Progression.CampaignChapter + 1)} de 10")}   ·   Documentos: {Progression.DocumentsFound}/{Story.Documents.Count}</size>\n\n");
            text.Append("<b>CONQUISTAS</b>\n<size=85%>");
            foreach (Progression.Achievement a in Progression.AllAchievements)
            {
                bool done = Progression.IsUnlocked(a.Id);
                text.Append(done ? "<color=#9BE39B>[x]</color> " : "<color=#666666>[ ]</color> ");
                text.Append(done ? $"<b>{a.Title}</b>" : $"<color=#AAAAAA>{a.Title}</color>").Append($" — {a.Description}\n");
            }

            text.Append("</size>\n<size=80%><color=#AAAAAA>Desbloqueios: roupas no nível 2, 3, 4 e 5 e ao terminar a campanha; armas e melhorias na loja conforme o nível.</color></size>");
            _progressText.text = text.ToString();
        }

        // ---------------------------------------------------------------- campaign complete

        private void BuildCampaignComplete(RectTransform root)
        {
            Image done = Panel("CampaignComplete", root, Vector2.zero, Vector2.one, new Color(0.02f, 0.03f, 0.05f, 0.92f));
            Label(done.rectTransform, "CAMPANHA CONCLUÍDA", 84, new Vector2(0.1f, 0.68f), new Vector2(0.9f, 0.82f), TextAlignmentOptions.Center, FontStyles.Bold, Accent);
            Label(done.rectTransform, "Protocolo Aurora — você sobreviveu à noite", 32, new Vector2(0.1f, 0.62f), new Vector2(0.9f, 0.68f), TextAlignmentOptions.Center, FontStyles.Normal, Color.white);
            _completeStats = Label(done.rectTransform, "", 30, new Vector2(0.2f, 0.4f), new Vector2(0.8f, 0.6f), TextAlignmentOptions.Center, FontStyles.Normal, Color.white);
            RectTransform endless = Button(done.rectTransform, "MODO INFINITO", new Vector2(0.5f, 0.27f), () =>
            {
                _restartMode = GameMode.Endless;
                _startImmediately = true;
                Reload();
            }, true);
            RectTransform menu = Button(done.rectTransform, "MENU PRINCIPAL", new Vector2(0.5f, 0.17f), BackToMenu, false);
            // centred under the centred title, one above the other
            endless.pivot = menu.pivot = new Vector2(0.5f, 0.0f);
            menu.sizeDelta = new Vector2(endless.sizeDelta.x, menu.sizeDelta.y);
            _completePanel = done.gameObject;
            _completePanel.SetActive(false);
        }

        private void ShowCampaignComplete()
        {
            Current = State.GameOver;
            int bestScore = PlayerPrefs.GetInt(BestScoreKey, 0);
            if (Score > bestScore)
            {
                PlayerPrefs.SetInt(BestScoreKey, Score);
                PlayerPrefs.Save();
            }

            float minutes = (Time.time - RunState.StartedAt) / 60.0f;
            _completeStats.text = $"<color=#FFC74D>{FormatPoints(Score)} PONTOS</color>\n" +
                                  $"{RunState.Kills} {(RunState.Kills == 1 ? "abate" : "abates")}  ·  {RunState.MissionsCompleted} {(RunState.MissionsCompleted == 1 ? "missão" : "missões")}  ·  {Mathf.RoundToInt(minutes)} min  ·  {Difficulty.Name}\n" +
                                  $"<size=80%>Nível {Progression.Level}  ·  Documentos {Progression.DocumentsFound}/{Story.Documents.Count}</size>";
            if (_spawner != null) _spawner.StopWaves();
            if (_hud != null) _hud.SetVisible(false);
            ShowOnly(_completePanel);
            SetCursor(false);
            Progression.Flush();
        }

        private void CompactButton(RectTransform parent, string text, Vector2 anchor, Action onClick)
        {
            Image background = Panel("Button_" + text, parent, anchor, anchor, new Color(1, 1, 1, 0.08f));
            RectTransform rect = background.rectTransform;
            rect.pivot = new Vector2(0.0f, 0.0f);
            rect.sizeDelta = new Vector2(440.0f, 56.0f);
            var button = background.gameObject.AddComponent<Button>();
            ColorBlock colors = button.colors;
            colors.highlightedColor = new Color(1, 1, 1, 0.25f);
            colors.pressedColor = new Color(0.8f, 0.8f, 0.8f);
            button.colors = colors;
            button.onClick.AddListener(() =>
            {
                Click();
                onClick();
            });
            Label(rect, text, 28, Vector2.zero, Vector2.one, TextAlignmentOptions.Center, FontStyles.Bold, Color.white);
        }

        private void ChangeDifficulty(int step)
        {
            int next = ((int)GameDifficulty.Current + step + 4) % 4;
            GameDifficulty.Current = (Difficulty)next;
            RefreshMenu();
        }

        private void RefreshMenu()
        {
            DifficultySettings settings = GameDifficulty.Settings;
            _difficultyLabel.text = settings.Name;
            _difficultyDescription.text = settings.Description;
            int bestWave = PlayerPrefs.GetInt(BestWaveKey, 0);
            int bestScore = PlayerPrefs.GetInt(BestScoreKey, 0);
            _bestLabel.text = (bestScore > 0 ? $"Recorde: {FormatPoints(bestScore)} pontos  ·  onda {bestWave}  ·  " : "") + $"Nível {Progression.Level}";
            int chapter = Progression.CampaignChapter;
            if (_campaignButtonLabel != null) _campaignButtonLabel.text = chapter > 0 && chapter < 10 ? $"CAMPANHA — ONDA {chapter + 1}" : "CAMPANHA";
        }

        // ---------------------------------------------------------------- wardrobe

        private void BuildWardrobe(RectTransform parent)
        {
            Image panel = Panel("Wardrobe", parent, new Vector2(0.6f, 0.1f), new Vector2(0.96f, 0.9f), PanelColor);
            _wardrobePanel = panel.gameObject;
            RectTransform rect = panel.rectTransform;
            Label(rect, "PERSONAGEM", 44, new Vector2(0.07f, 0.88f), new Vector2(0.93f, 0.97f), TextAlignmentOptions.Left, FontStyles.Bold, Accent);

            if (_outfit != null)
            {
                int count = _outfit.Categories.Count;
                _wardrobeValues = new TextMeshProUGUI[count];
                for (int i = 0; i < count; i++)
                {
                    int category = i;
                    float y = 0.78f - i * 0.095f;
                    Label(rect, _outfit.Categories[i].Label, 24, new Vector2(0.07f, y), new Vector2(0.4f, y + 0.07f), TextAlignmentOptions.Left, FontStyles.Normal, new Color(1, 1, 1, 0.65f));
                    SmallButton(rect, "<", new Vector2(0.4f, y), () => CycleOutfit(category, -1));
                    _wardrobeValues[i] = Label(rect, "", 28, new Vector2(0.48f, y), new Vector2(0.82f, y + 0.07f), TextAlignmentOptions.Center, FontStyles.Bold, Color.white);
                    SmallButton(rect, ">", new Vector2(0.83f, y), () => CycleOutfit(category, 1));
                }
            }
            else
            {
                Label(rect, "Este personagem não tem roupas para trocar.", 26, new Vector2(0.07f, 0.5f), new Vector2(0.93f, 0.8f), TextAlignmentOptions.TopLeft, FontStyles.Normal, Color.white);
            }

            Button(rect, "VOLTAR", new Vector2(0.07f, 0.03f), CloseWardrobe, true);
            _wardrobePanel.SetActive(false);
        }

        private void OpenWardrobe()
        {
            _wardrobePanel.SetActive(true);
            _menuBand.SetActive(false);
            _controlsPanel.SetActive(false);
            _optionsPanel.SetActive(false);
            RefreshWardrobe();
        }

        private void CloseWardrobe()
        {
            _wardrobePanel.SetActive(false);
            _menuBand.SetActive(true);
            if (_player != null) _player.transform.rotation = _playerRotation;
        }

        private void CycleOutfit(int category, int step)
        {
            _outfit.Cycle(category, step);
            RefreshWardrobe();
        }

        private void RefreshWardrobe()
        {
            if (_outfit == null || _wardrobeValues == null) return;
            for (int i = 0; i < _wardrobeValues.Length; i++) _wardrobeValues[i].text = _outfit.CurrentLabel(i);
        }

        // the menu camera frames the character, slowly turning on the spot, with the panel to the right
        private void UpdateWardrobeCamera()
        {
            if (_menuCamera == null || _player == null) return;

            Transform player = _player.transform;
            player.Rotate(0.0f, 25.0f * Time.unscaledDeltaTime, 0.0f, Space.World);
            Vector3 center = player.position + Vector3.up * 1.0f;
            Vector3 front = _playerRotation * Vector3.forward;
            Vector3 right = Vector3.Cross(Vector3.up, front);
            Vector3 position = center + front * 3.0f + Vector3.up * 0.35f;
            Vector3 lookAt = center - right * 0.75f;
            float blend = 1.0f - Mathf.Exp(-4.0f * Time.unscaledDeltaTime);
            _menuCamera.transform.position = Vector3.Lerp(_menuCamera.transform.position, position, blend);
            _menuCamera.transform.rotation = Quaternion.Slerp(_menuCamera.transform.rotation,
                Quaternion.LookRotation(lookAt - _menuCamera.transform.position), blend);
        }

        private void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void Click()
        {
            if (CombatEffects.Instance != null && ClickSound != null) CombatEffects.Instance.PlayUI(ClickSound, 0.6f);
        }

        // ---------------------------------------------------------------- ui helpers

        private static Image Panel(string name, RectTransform parent, Vector2 anchorMin, Vector2 anchorMax, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            var image = go.GetComponent<Image>();
            image.color = color;
            return image;
        }

        private static TextMeshProUGUI Label(RectTransform parent, string text, float size, Vector2 anchorMin, Vector2 anchorMax,
            TextAlignmentOptions alignment, FontStyles style, Color color)
        {
            var go = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            var label = go.GetComponent<TextMeshProUGUI>();
            label.text = text;
            label.fontSize = size;
            label.alignment = alignment;
            label.fontStyle = style;
            label.color = color;
            label.raycastTarget = false;
            return label;
        }

        private RectTransform Button(RectTransform parent, string text, Vector2 anchor, Action onClick, bool primary)
        {
            Image background = Panel("Button_" + text, parent, anchor, anchor, primary ? Accent : new Color(1, 1, 1, 0.08f));
            RectTransform rect = background.rectTransform;
            rect.pivot = new Vector2(0.0f, 0.0f);
            rect.sizeDelta = new Vector2(primary ? 520.0f : 440.0f, primary ? 84.0f : 70.0f);
            rect.anchoredPosition = Vector2.zero;

            var button = background.gameObject.AddComponent<Button>();
            ColorBlock colors = button.colors;
            colors.highlightedColor = primary ? new Color(1.0f, 0.9f, 0.6f) : new Color(1, 1, 1, 0.25f);
            colors.pressedColor = new Color(0.8f, 0.8f, 0.8f);
            colors.normalColor = Color.white;
            colors.colorMultiplier = 1.0f;
            button.colors = colors;
            button.onClick.AddListener(() =>
            {
                Click();
                onClick();
            });

            Label(rect, text, primary ? 40 : 32, Vector2.zero, Vector2.one, TextAlignmentOptions.Center, FontStyles.Bold,
                primary ? new Color(0.1f, 0.08f, 0.04f) : Color.white);
            return rect;
        }

        private void SmallButton(RectTransform parent, string text, Vector2 anchor, Action onClick, float size = 70.0f)
        {
            Image background = Panel("Arrow_" + text, parent, anchor, anchor, new Color(1, 1, 1, 0.08f));
            RectTransform rect = background.rectTransform;
            rect.pivot = new Vector2(0.0f, 0.0f);
            rect.sizeDelta = new Vector2(size, size);
            var button = background.gameObject.AddComponent<Button>();
            button.onClick.AddListener(() =>
            {
                Click();
                onClick();
            });
            Label(rect, text, 40, Vector2.zero, Vector2.one, TextAlignmentOptions.Center, FontStyles.Bold, Color.white);
        }
    }
}
