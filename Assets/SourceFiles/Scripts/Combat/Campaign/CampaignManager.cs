using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Combat
{
    /* Runs the campaign: the intro, each chapter (title, radio, story missions in order, side
     * missions, the wave its mission calls for, a boss now and then), the break between chapters
     * with the shop and an optional choice, and the ending. A failed story mission is simply
     * offered again; the run only ends through lives running out, as before.
     */
    public class CampaignManager : MonoBehaviour
    {
        public static CampaignManager Instance { get; private set; }

        public CampaignDefinition Campaign;
        [Tooltip("Where the extraction helicopter is heard and the ending camera looks")]
        public Transform EndingPoint;

        public bool IsRunning { get; private set; }
        public int Chapter { get; private set; } = -1;
        public bool InIntermission { get; private set; }
        public float IntermissionLeft => Mathf.Max(0.0f, _intermissionEnds - Time.time);
        public bool WaitingForShop { get; private set; }
        public CampaignDefinition.Chapter Current => Campaign != null && Chapter >= 0 && Chapter < Campaign.Chapters.Length ? Campaign.Chapters[Chapter] : null;

        public event Action<int> ChapterStarted;
        public event Action<int> ChapterCompleted;
        public event Action CampaignCompleted;

        private MissionManager _missions;
        private EnemySpawner _spawner;
        private PlayerCombat _player;
        private int _missionIndex;
        private float _intermissionEnds;
        private readonly List<MissionDefinition> _retry = new List<MissionDefinition>();
        private float _retryAt;
        private bool _ending;

        private void Awake()
        {
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Start()
        {
            _missions = MissionManager.Instance != null ? MissionManager.Instance : FindAnyObjectByType<MissionManager>();
            _spawner = FindAnyObjectByType<EnemySpawner>();
            _player = FindAnyObjectByType<PlayerCombat>();
            if (_missions != null)
            {
                _missions.MissionCompleted += OnMissionCompleted;
                _missions.MissionFailed += OnMissionFailed;
                _missions.WaveRequested += OnWaveRequested;
            }

            if (_spawner != null) _spawner.BossSpawned += OnBossSpawned;
        }

        // ---------------------------------------------------------------- flow

        public void Begin(int fromChapter)
        {
            if (Campaign == null || Campaign.Chapters == null || Campaign.Chapters.Length == 0) return;
            IsRunning = true;
            _ending = false;
            Chapter = -1;
            InIntermission = false;
            if (_spawner != null && GameFlow.Instance != null) _spawner.Prepare(GameFlow.Instance.Difficulty);

            int start = Mathf.Clamp(fromChapter, 0, Campaign.Chapters.Length - 1);
            if (start == 0) PlayIntro(() => StartChapter(0));
            else StartChapter(start);
        }

        public void Stop()
        {
            IsRunning = false;
            InIntermission = false;
        }

        /* The prologue (~45 s, skippable): the base from above, the hangars, the perimeter, then the player.
         * The narrator and Lt. Duarte's first transmission play over it from the DialogueLibrary
         * (group "prologue"); skipping cuts them and the chapter's own radio line takes over. */
        private void PlayIntro(Action then)
        {
            CinematicPlayer cinematic = CinematicPlayer.Instance;
            if (cinematic == null || _player == null)
            {
                then();
                return;
            }

            Vector3 p = _player.transform.position;
            Vector3 center = _spawner != null ? _spawner.transform.position : p;
            Vector3 helipad = EndingPoint != null ? EndingPoint.position : center + new Vector3(30.0f, 0.0f, 40.0f);
            var shots = new List<CinematicPlayer.Shot>
            {
                CinematicPlayer.Orbit(center, 85.0f, 42.0f, 10.0f, 55.0f, 10.0f, Campaign.Title.ToUpperInvariant(), ""),
                CinematicPlayer.Orbit(center, 40.0f, 14.0f, 140.0f, 185.0f, 10.0f, "", ""),
                new CinematicPlayer.Shot
                {
                    From = helipad + new Vector3(-25.0f, 9.0f, -20.0f), To = helipad + new Vector3(-8.0f, 5.0f, -30.0f),
                    LookFrom = helipad, LookTo = center, Duration = 9.0f, Title = "", Caption = ""
                },
                new CinematicPlayer.Shot
                {
                    From = p + new Vector3(7.0f, 4.0f, 7.0f), To = p + new Vector3(2.0f, 1.8f, 3.0f),
                    LookFrom = p + Vector3.up, LookTo = p + Vector3.up * 1.4f, Duration = 15.0f,
                    Title = "", Caption = ""
                }
            };

            DialogueDirector director = DialogueDirector.Instance;
            if (director != null)
            {
                director.PlaySequence("prologue_1", "prologue_2", "prologue_3", "prologue_4", "prologue_5", "prologue_6");
            }
            else
            {
                // no dialogue system: the old one-line caption
                CinematicPlayer.Shot last = shots[shots.Count - 1];
                last.Caption = Campaign.Intro;
                shots[shots.Count - 1] = last;
            }

            cinematic.Play(shots, () =>
            {
                if (DialogueDirector.Instance != null) DialogueDirector.Instance.CancelGroup("prologue");
                then();
            });
        }

        private void StartChapter(int index)
        {
            Chapter = index;
            InIntermission = false;
            WaitingForShop = false;
            _missionIndex = 0;
            Progression.CampaignChapter = index;
            CampaignDefinition.Chapter chapter = Current;

            if (_player != null) _player.ShowNotice($"<color=#FFC74D>{chapter.Title}</color>");
            if (!string.IsNullOrEmpty(chapter.Radio)) Story.PlayRadio(chapter.Radio);
            if (_missions != null)
            {
                _missions.Apply(chapter.OnStart);
                if (chapter.SideMissions != null)
                {
                    foreach (MissionDefinition side in chapter.SideMissions) _missions.StartMission(side, true);
                }
            }

            ChapterStarted?.Invoke(index);
            StartNextMission();
        }

        private void StartNextMission()
        {
            CampaignDefinition.Chapter chapter = Current;
            if (chapter == null) return;
            while (_missionIndex < chapter.Missions.Length)
            {
                MissionDefinition mission = chapter.Missions[_missionIndex];
                if (mission == null || _missions.WasCompleted(mission))
                {
                    _missionIndex++;
                    continue;
                }

                // a mission that can't run (its places don't exist) is skipped, never a dead end
                if (_missions.StartMission(mission, false) == null)
                {
                    Debug.LogWarning($"Campanha: missão {mission.Id} indisponível, seguindo");
                    _missionIndex++;
                    continue;
                }

                return;
            }

            CompleteChapter();
        }

        private void OnMissionCompleted(MissionRun run)
        {
            if (!IsRunning || Current == null || run.Optional) return;
            if (_missionIndex < Current.Missions.Length && Current.Missions[_missionIndex] == run.Definition)
            {
                _missionIndex++;
                StartNextMission();
            }
        }

        private void OnMissionFailed(MissionRun run)
        {
            if (!IsRunning || run.Optional) return;
            // story missions come back after a moment
            _retry.Add(run.Definition);
            _retryAt = Time.time + 6.0f;
            if (_player != null) _player.ShowNotice("A missão será reiniciada em instantes");
        }

        private void OnWaveRequested(MissionRun run)
        {
            if (!IsRunning || _spawner == null || Current == null) return;
            CampaignDefinition.Chapter chapter = Current;
            float count = GameFlow.Instance != null ? GameFlow.Instance.Difficulty.EnemyCount : 1.0f;
            _spawner.LaunchWave(Chapter + 1, Mathf.Max(1, Mathf.RoundToInt(chapter.Zombies * count)),
                Mathf.RoundToInt(chapter.Soldiers * count), chapter.EnemyHealth);
            RunState.BasicOnly = true;
            if (chapter.Boss != null && _player != null) _spawner.SpawnBoss(chapter.Boss, _player.transform.position);
        }

        private void OnBossSpawned(GameObject boss)
        {
            CinematicPlayer cinematic = CinematicPlayer.Instance;
            var ai = boss.GetComponent<BossAI>();
            if (cinematic == null || ai == null) return;
            // a short entrance: the camera circles the boss with its name
            cinematic.Play(new[]
            {
                CinematicPlayer.Orbit(boss.transform.position, 7.0f * ai.Definition.Scale * 0.6f, 2.5f, -40.0f, 20.0f, 3.2f,
                    ai.Definition.DisplayName.ToUpperInvariant(), ai.Definition.Title)
            }, null);
        }

        private void CompleteChapter()
        {
            ChapterCompleted?.Invoke(Chapter);
            Progression.CampaignChapter = Chapter + 1;
            if (Chapter >= Campaign.Chapters.Length - 1)
            {
                Finish();
                return;
            }

            CampaignDefinition.Chapter chapter = Current;
            InIntermission = true;
            _intermissionEnds = Time.time + chapter.Intermission;
            if (_player != null) _player.ShowNotice($"<color=#9BE39B>{chapter.Title} — CONCLUÍDA</color>");
            if (chapter.ChoiceA != null && chapter.ChoiceB != null) _missions.OfferChoice(chapter.ChoiceA, chapter.ChoiceB);
        }

        private void Update()
        {
            if (!IsRunning) return;

            if (_retry.Count > 0 && Time.time >= _retryAt)
            {
                foreach (MissionDefinition mission in _retry) _missions.StartMission(mission, false);
                _retry.Clear();
            }

            if (!InIntermission) return;

            // N calls the next chapter early
            if (Keyboard.current != null && Keyboard.current.nKey.wasPressedThisFrame && !ShopManager.IsOpen)
            {
                int bonus = Mathf.RoundToInt(IntermissionLeft * 0.5f);
                if (bonus > 0) RunState.AddCredits(bonus, "onda antecipada");
                _intermissionEnds = Time.time;
            }

            // the next chapter waits for the shop to close
            WaitingForShop = Time.time >= _intermissionEnds && ShopManager.IsOpen;
            if (Time.time >= _intermissionEnds && !WaitingForShop && !CinematicPlayer.Playing) StartChapter(Chapter + 1);
        }

        private void Finish()
        {
            if (_ending) return;
            _ending = true;
            IsRunning = false;
            InIntermission = false;
            Progression.CompleteCampaign();
            Progression.AddExperience(1000, "campanha concluída");
            CampaignAssets.Play(a => a.Helicopter, EndingPoint != null ? EndingPoint.position : _player.transform.position, 1.0f, 200.0f);

            Vector3 point = EndingPoint != null ? EndingPoint.position : _player.transform.position;
            CinematicPlayer cinematic = CinematicPlayer.Instance;
            var shots = new List<CinematicPlayer.Shot>
            {
                new CinematicPlayer.Shot
                {
                    From = point + new Vector3(8.0f, 2.0f, 8.0f), To = point + new Vector3(4.0f, 25.0f, 4.0f),
                    LookFrom = point + Vector3.up, LookTo = point, Duration = 6.0f,
                    Title = "AMANHECER", Caption = Campaign.Ending
                }
            };

            if (cinematic != null) cinematic.Play(shots, () => CampaignCompleted?.Invoke());
            else CampaignCompleted?.Invoke();
        }
    }
}
