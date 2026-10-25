using System.Collections.Generic;
using UnityEngine;

namespace Combat
{
    /* Plays the story's spoken lines (narrator, Lt. Duarte on the radio, the pilot) in response to what
     * actually happens in the game, one at a time, through a priority queue.
     *
     *   - the lines and their triggers live in the DialogueLibrary asset (Resources), never in scripts
     *   - a higher-priority line may cut off a lower one (radio "tchk"); others wait their turn
     *   - a line that waited too long, whose mission ended or whose objective moved on is dropped,
     *     so the radio never describes a goal that no longer exists
     *   - "once" lines and cooldowns stop repetition (low ammo, critical health...)
     *   - radio voices get a band-limited, slightly crunched sound with squelch clicks and a little static;
     *     the music ducks while anyone speaks
     *   - lines without a recorded clip still work: subtitle plus radio sounds, timed by reading speed
     *   - everything is local: no network calls at play time
     */
    public class DialogueDirector : MonoBehaviour
    {
        public static DialogueDirector Instance { get; private set; }

        [Tooltip("Music volume multiplier while someone speaks")]
        public float MusicDuckLevel = 0.35f;
        public float VoiceVolume = 1.0f;
        public float RadioStaticVolume = 0.08f;
        [Tooltip("Reading speed for lines without a clip, characters per second")]
        public float ReadingSpeed = 15.0f;

        // read by GameFlow when it sets the music volume
        public static float MusicDuck { get; private set; } = 1.0f;

        public DialogueLine Current { get; private set; }
        public float CurrentElapsed => Current != null ? Time.unscaledTime - _started : 0.0f;
        public float CurrentDuration { get; private set; }
        public bool Speaking => Current != null;

        private struct Pending
        {
            public DialogueLine Line;
            public float Queued;
            public int Order;
        }

        private readonly List<Pending> _queue = new List<Pending>();
        private readonly HashSet<string> _played = new HashSet<string>();
        private readonly Dictionary<string, float> _lastPlayed = new Dictionary<string, float>();
        private DialogueLibrary _library;
        private AudioSource _voice;
        private AudioSource _static;
        private AudioHighPassFilter _highPass;
        private AudioLowPassFilter _lowPass;
        private AudioDistortionFilter _distortion;
        private AudioClip _squelchOn;
        private AudioClip _squelchOff;
        private AudioClip _staticLoop;
        private float _started;
        private float _voiceStartsAt;
        private bool _voicePending;
        private int _order;
        private float _nextAmmoCheck;
        private PlayerCombat _player;

        private void Awake()
        {
            Instance = this;
            _library = DialogueLibrary.Instance;

            var voiceGo = new GameObject("DialogueVoice");
            voiceGo.transform.SetParent(transform, false);
            _voice = voiceGo.AddComponent<AudioSource>();
            _voice.spatialBlend = 0.0f;
            _voice.playOnAwake = false;
            _voice.priority = 0;
            _voice.ignoreListenerPause = true;
            // a military handset: no lows, no highs, a little grit
            _highPass = voiceGo.AddComponent<AudioHighPassFilter>();
            _highPass.cutoffFrequency = 380.0f;
            _lowPass = voiceGo.AddComponent<AudioLowPassFilter>();
            _lowPass.cutoffFrequency = 3400.0f;
            _distortion = voiceGo.AddComponent<AudioDistortionFilter>();
            _distortion.distortionLevel = 0.18f;

            _static = gameObject.AddComponent<AudioSource>();
            AudioRouting.Route(_static, AudioRouting.Channel.Radio);
            _static.spatialBlend = 0.0f;
            _static.loop = true;
            _static.playOnAwake = false;
            _static.ignoreListenerPause = true;

            _squelchOn = RadioSounds.Squelch(true);
            _squelchOff = RadioSounds.Squelch(false);
            _staticLoop = RadioSounds.Static();
            _static.clip = _staticLoop;
        }

        private void Start()
        {
            _player = FindAnyObjectByType<PlayerCombat>();
            MissionManager missions = MissionManager.Instance;
            if (missions != null)
            {
                missions.MissionStarted += run => Fire(DialogueTrigger.MissionStarted, run.Definition.Id, -1, null, 0);
                missions.ObjectiveCompleted += (run, index) => Fire(DialogueTrigger.ObjectiveCompleted, run.Definition.Id, index, null, 0);
                missions.MissionCompleted += run => Fire(DialogueTrigger.MissionCompleted, run.Definition.Id, -1, null, 0);
                missions.MissionFailed += run => Fire(DialogueTrigger.MissionFailed, run.Definition.Id, -1, null, 0);
            }

            RunState.ItemsChanged += id => Fire(DialogueTrigger.ItemCollected, null, -1, id, RunState.ItemCount(id));
            if (PowerGridManager.Instance != null)
            {
                PowerGridManager.Instance.SectorChanged += (sector, from, to) =>
                {
                    if (to == PowerState.Blackout) Fire(DialogueTrigger.Blackout, null, -1, null, 0);
                };
            }

            var spawner = FindAnyObjectByType<EnemySpawner>();
            if (spawner != null)
            {
                spawner.BossSpawned += boss =>
                {
                    var ai = boss != null ? boss.GetComponent<BossAI>() : null;
                    Fire(DialogueTrigger.BossSpotted, ai != null ? ai.Definition.Id : null, -1, null, 0);
                };
                spawner.WaveCleared += wave => Fire(DialogueTrigger.WaveCleared, null, -1, null, 0);
            }

            BossAI.Defeated += boss => Fire(DialogueTrigger.BossDefeated, boss.Definition.Id, -1, null, 0);
            if (CampaignManager.Instance != null)
            {
                CampaignManager.Instance.ChapterStarted += chapter => Fire(DialogueTrigger.ChapterStarted, null, -1, chapter.ToString(), 0);
            }

            if (_player != null) _player.Health.Damaged += OnPlayerDamaged;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            MusicDuck = 1.0f;
        }

        // ---------------------------------------------------------------- requests

        public bool Has(string id) => _library != null && _library.Find(id) != null;

        /* Plays a line by id (story radio calls, cinematics). Returns false if there's no such line. */
        public bool Play(string id)
        {
            DialogueLine line = _library != null ? _library.Find(id) : null;
            if (line == null) return false;
            Enqueue(line);
            return true;
        }

        public void PlaySequence(params string[] ids)
        {
            foreach (string id in ids) Play(id);
        }

        // drops queued lines of a group, and stops the current one if it belongs to it (a skipped prologue)
        public void CancelGroup(string group)
        {
            _queue.RemoveAll(p => p.Line.Group == group);
            if (Current != null && Current.Group == group) Stop(true);
        }

        // a new run: nothing carries over
        public void ResetAll()
        {
            _queue.Clear();
            _played.Clear();
            _lastPlayed.Clear();
            if (Current != null) Stop(false);
        }

        private void Fire(DialogueTrigger trigger, string missionId, int objective, string item, int count)
        {
            if (_library == null) return;
            foreach (DialogueLine line in _library.Lines)
            {
                if (line == null || line.Trigger != trigger) continue;
                if (!string.IsNullOrEmpty(line.MissionId) && line.MissionId != missionId) continue;
                if (trigger == DialogueTrigger.ObjectiveCompleted && line.ObjectiveIndex >= 0 && line.ObjectiveIndex != objective) continue;
                if (trigger == DialogueTrigger.ItemCollected && (line.ItemId != item || (line.ItemCount > 0 && line.ItemCount != count))) continue;
                if (trigger == DialogueTrigger.ChapterStarted && !string.IsNullOrEmpty(line.ItemId) && line.ItemId != item) continue;
                Enqueue(line);
            }
        }

        private void Enqueue(DialogueLine line)
        {
            if (!Allowed(line)) return;
            foreach (Pending p in _queue) if (p.Line == line) return;
            if (Current == line) return;

            if (Current != null)
            {
                if (line.Interrupt == DialogueInterrupt.DropIfBusy) return;
                if (line.Interrupt == DialogueInterrupt.InterruptLower && line.Priority > Current.Priority) Stop(true);
            }

            _queue.Add(new Pending { Line = line, Queued = Time.unscaledTime, Order = _order++ });
        }

        private bool Allowed(DialogueLine line)
        {
            if (line.Once && _played.Contains(line.Id)) return false;
            if (!string.IsNullOrEmpty(line.SkipIfPlayed) && _played.Contains(line.SkipIfPlayed)) return false;
            if (line.Cooldown > 0.0f && _lastPlayed.TryGetValue(line.Id, out float last) && Time.unscaledTime - last < line.Cooldown) return false;
            return true;
        }

        // still worth saying? (a line about an objective that's done, or a mission that's over, isn't)
        private bool StillValid(Pending pending)
        {
            DialogueLine line = pending.Line;
            if (Time.unscaledTime - pending.Queued > line.MaxQueueTime) return false;
            if (!Allowed(line)) return false;
            if (!line.RequireMissionActive || string.IsNullOrEmpty(line.MissionId)) return true;

            MissionManager missions = MissionManager.Instance;
            if (missions == null) return false;
            foreach (MissionRun run in missions.Active)
            {
                if (run.Definition.Id != line.MissionId || run.State != MissionState.Active) continue;
                return line.ObjectiveIndex < 0 || line.Trigger == DialogueTrigger.ObjectiveCompleted || run.Current == line.ObjectiveIndex;
            }

            return false;
        }

        // ---------------------------------------------------------------- playback

        // time spent here, for the performance probe (Diagnostics/PerfProbe)
        private static readonly Unity.Profiling.ProfilerMarker UpdateMarker = new Unity.Profiling.ProfilerMarker("Combat.DialogueDirector.Update");

        private void Update()
        {
            using (UpdateMarker.Auto()) UpdateMarked();
        }

        private void UpdateMarked()
        {
            float dt = Time.unscaledDeltaTime;
            MusicDuck = Mathf.MoveTowards(MusicDuck, Current != null ? MusicDuckLevel : 1.0f, dt * 1.5f);
            AudioRouting.UpdateDuck(Current != null, dt);
            CheckLowAmmo();

            if (Current != null)
            {
                float elapsed = Time.unscaledTime - _started;
                // the voice starts just after the squelch click
                if (_voicePending && Time.unscaledTime >= _voiceStartsAt)
                {
                    _voicePending = false;
                    _voice.Play();
                }

                if (elapsed >= CurrentDuration) Stop(false);
                return;
            }

            if (_queue.Count == 0) return;
            _queue.RemoveAll(p => !StillValid(p));
            if (_queue.Count == 0) return;

            // the most important first, then the oldest
            int best = 0;
            for (int i = 1; i < _queue.Count; i++)
            {
                Pending a = _queue[i], b = _queue[best];
                if (a.Line.Priority > b.Line.Priority || (a.Line.Priority == b.Line.Priority && a.Order < b.Order)) best = i;
            }

            DialogueLine next = _queue[best].Line;
            _queue.RemoveAt(best);
            Begin(next);
        }

        private void Begin(DialogueLine line)
        {
            Current = line;
            _started = Time.unscaledTime;
            _played.Add(line.Id);
            _lastPlayed[line.Id] = Time.unscaledTime;

            bool radio = DialogueLibrary.IsRadio(line.Speaker);
            float lead = radio ? 0.22f : 0.0f;
            _voiceStartsAt = _started + lead;
            float speech = line.Duration > 0.0f ? line.Duration
                : line.Clip != null ? line.Clip.length
                : Mathf.Max(2.2f, (line.Text ?? "").Length / Mathf.Max(1.0f, ReadingSpeed) + 0.8f);
            CurrentDuration = lead + speech + (radio ? 0.18f : 0.25f);

            _highPass.enabled = _lowPass.enabled = _distortion.enabled = radio;
            AudioRouting.Route(_voice, radio ? AudioRouting.Channel.Radio : AudioRouting.Channel.Dialogue);
            _voice.clip = line.Clip;
            _voice.volume = VoiceVolume * GameSettings.DialogueVolume;
            _voicePending = line.Clip != null;

            if (radio)
            {
                PlayOneShot(_squelchOn, 0.55f);
                _static.volume = RadioStaticVolume * GameSettings.DialogueVolume;
                _static.Play();
            }
        }

        private void Stop(bool cut)
        {
            if (Current == null) return;
            bool radio = DialogueLibrary.IsRadio(Current.Speaker);
            _voice.Stop();
            _static.Stop();
            _voicePending = false;
            if (radio) PlayOneShot(_squelchOff, cut ? 0.7f : 0.45f);
            Current = null;
        }

        private void PlayOneShot(AudioClip clip, float volume)
        {
            if (clip != null) _voice.PlayOneShot(clip, volume);
        }

        // ---------------------------------------------------------------- watchers

        private void OnPlayerDamaged(DamageInfo info)
        {
            if (_player == null) return;
            if (_player.Health.Current > 0.0f && _player.Health.Current < _player.Health.MaxHealth * 0.25f) Fire(DialogueTrigger.CriticalHealth, null, -1, null, 0);
        }

        private void CheckLowAmmo()
        {
            if (Time.unscaledTime < _nextAmmoCheck || _player == null) return;
            _nextAmmoCheck = Time.unscaledTime + 1.0f;
            WeaponInstance weapon = _player.Current;
            if (weapon == null || weapon.InfiniteReserve) return;
            if (weapon.Reserve == 0 && weapon.Magazine <= weapon.Data.MagazineSize / 3) Fire(DialogueTrigger.LowAmmo, null, -1, null, 0);
        }
    }
}
