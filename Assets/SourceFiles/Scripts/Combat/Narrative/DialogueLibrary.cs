using System;
using System.Collections.Generic;
using UnityEngine;

namespace Combat
{
    public enum DialogueSpeaker
    {
        Narrator,
        Duarte,
        Pilot
    }

    // higher plays first and may cut a lower one off
    public enum DialoguePriority
    {
        Low,
        Medium,
        High,
        Critical
    }

    public enum DialogueTrigger
    {
        Manual,
        MissionStarted,
        ObjectiveCompleted,
        MissionCompleted,
        MissionFailed,
        ItemCollected,
        Blackout,
        BossSpotted,
        BossDefeated,
        LowAmmo,
        CriticalHealth,
        ChapterStarted,
        WaveCleared
    }

    public enum DialogueInterrupt
    {
        // waits its turn in the queue
        Queue,
        // cuts off whatever lower-priority line is playing
        InterruptLower,
        // only plays if nothing else is being said; otherwise it's dropped
        DropIfBusy
    }

    [Serializable]
    public class SubtitleSegment
    {
        [Tooltip("Seconds from the start of the line")]
        public float Time;
        [TextArea] public string Text;
    }

    /* One spoken line: who says it, what (Portuguese text + localisation key), the voice clip if recorded,
     * when it plays (trigger and its condition) and how it behaves in the queue. */
    [Serializable]
    public class DialogueLine
    {
        public string Id;
        public DialogueSpeaker Speaker = DialogueSpeaker.Duarte;
        [Tooltip("Key in the localisation table; the Portuguese text below is the source")]
        public string LocalizationKey;
        [TextArea(2, 5)] public string Text;
        [Tooltip("Recorded voice. Missing clips are fine: the line shows as a subtitle with radio sounds")]
        public AudioClip Clip;
        [Tooltip("0 = clip length, or a reading time from the text")]
        public float Duration;
        public DialoguePriority Priority = DialoguePriority.Medium;
        public DialogueInterrupt Interrupt = DialogueInterrupt.Queue;

        [Header("When")]
        public DialogueTrigger Trigger = DialogueTrigger.Manual;
        [Tooltip("Mission id for mission triggers (empty = any); boss id for boss triggers")]
        public string MissionId;
        [Tooltip("Objective index for ObjectiveCompleted; also: the objective that must still be current when the line plays (-1 = don't care)")]
        public int ObjectiveIndex = -1;
        [Tooltip("Item id for ItemCollected; chapter index (as text) for ChapterStarted")]
        public string ItemId;
        [Tooltip("ItemCollected: plays when the player holds exactly this many (0 = any)")]
        public int ItemCount;
        public bool Once = true;
        public float Cooldown;
        [Tooltip("A queued line older than this is dropped instead of describing something stale")]
        public float MaxQueueTime = 12.0f;
        [Tooltip("Drop the line if its mission isn't active any more by the time it would play")]
        public bool RequireMissionActive;
        [Tooltip("Skip it if this other line has already been heard (e.g. the prologue already said it)")]
        public string SkipIfPlayed;
        [Tooltip("Lines played together (e.g. the prologue) can be cancelled as a group")]
        public string Group;

        [Header("Subtitles")]
        [Tooltip("Optional timed parts; without them the whole text shows for the whole line")]
        public SubtitleSegment[] Segments = new SubtitleSegment[0];
    }

    [CreateAssetMenu(menuName = "Combat/Dialogue Library")]
    public class DialogueLibrary : ScriptableObject
    {
        public const string ResourcePath = "DialogueLibrary";

        public DialogueLine[] Lines = new DialogueLine[0];

        private Dictionary<string, DialogueLine> _byId;
        private static DialogueLibrary _instance;
        private static bool _loaded;

        public static DialogueLibrary Instance
        {
            get
            {
                if (!_loaded)
                {
                    _loaded = true;
                    _instance = Resources.Load<DialogueLibrary>(ResourcePath);
                }

                return _instance;
            }
        }

        public DialogueLine Find(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            if (_byId == null)
            {
                _byId = new Dictionary<string, DialogueLine>();
                foreach (DialogueLine line in Lines)
                {
                    if (line != null && !string.IsNullOrEmpty(line.Id)) _byId[line.Id] = line;
                }
            }

            return _byId.TryGetValue(id, out DialogueLine found) ? found : null;
        }

        public static string SpeakerName(DialogueSpeaker speaker)
        {
            switch (speaker)
            {
                case DialogueSpeaker.Narrator: return "NARRADOR";
                case DialogueSpeaker.Pilot: return "PILOTO — ÁGUIA 2";
                default: return "TENENTE DUARTE — CONTROLE AURORA";
            }
        }

        public static Color SpeakerColor(DialogueSpeaker speaker)
        {
            switch (speaker)
            {
                case DialogueSpeaker.Narrator: return new Color(0.85f, 0.85f, 0.88f);
                case DialogueSpeaker.Pilot: return new Color(0.55f, 0.85f, 1.0f);
                default: return new Color(0.6f, 1.0f, 0.7f);
            }
        }

        // everyone but the narrator is heard over the radio
        public static bool IsRadio(DialogueSpeaker speaker) => speaker != DialogueSpeaker.Narrator;
    }
}
