using System;
using System.Collections.Generic;
using UnityEngine;

namespace Combat
{
    public enum ThreatArrowMode
    {
        Auto,
        Off,
        AwareOnly,
        All
    }

    /* Accessibility and comfort options, saved between sessions. Systems read them directly
     * and listen to Changed when they cache anything.
     */
    public static class GameSettings
    {
        public static event Action Changed;

        // -1 darker .. +1 brighter, added to the post exposure
        public static float Brightness { get => Get("Brightness", 0.0f); set => Set("Brightness", value); }
        // film grain, 0..1
        public static float Grain { get => Get("Grain", 1.0f); set => Set("Grain", value); }
        // camera shake and wobble, 0..1
        public static float CameraShake { get => Get("Shake", 1.0f); set => Set("Shake", value); }
        // lightning, muzzle and other flashes, 0..1
        public static float Flashes { get => Get("Flashes", 1.0f); set => Set("Flashes", value); }
        // red screen edges and pulses when hurt, 0..1
        public static float InjuryEffects { get => Get("Injury", 1.0f); set => Set("Injury", value); }
        public static bool Subtitles { get => Get("Subtitles", 1.0f) > 0.5f; set => Set("Subtitles", value ? 1.0f : 0.0f); }
        // the objective marker with distance on screen
        public static bool ObjectiveHints { get => Get("Hints", 1.0f) > 0.5f; set => Set("Hints", value ? 1.0f : 0.0f); }
        public static ThreatArrowMode ThreatArrows { get => (ThreatArrowMode)Mathf.RoundToInt(Get("Arrows", 0.0f)); set => Set("Arrows", (int)value); }

        private static readonly Dictionary<string, float> Cache = new Dictionary<string, float>();

        private static float Get(string key, float fallback)
        {
            if (Cache.TryGetValue(key, out float value)) return value;
            value = PlayerPrefs.GetFloat("Combat.Settings." + key, fallback);
            Cache[key] = value;
            return value;
        }

        private static void Set(string key, float value)
        {
            Cache[key] = value;
            PlayerPrefs.SetFloat("Combat.Settings." + key, value);
            PlayerPrefs.Save();
            Changed?.Invoke();
        }

        // the arrow mode actually in use: Auto shows every enemy on the easy settings, only the alerted ones on the hard ones
        public static ThreatArrowMode EffectiveThreatArrows(Difficulty difficulty)
        {
            ThreatArrowMode mode = ThreatArrows;
            if (mode != ThreatArrowMode.Auto) return mode;
            return difficulty == Difficulty.Easy || difficulty == Difficulty.Normal ? ThreatArrowMode.All : ThreatArrowMode.AwareOnly;
        }
    }

    // captions for sounds that matter, shown by the HUD when enabled in the settings
    public static class Subtitles
    {
        public struct Line
        {
            public string Text;
            public Vector3 Position;
            public bool Positional;
            public float Until;
        }

        public static readonly List<Line> Active = new List<Line>();

        public static void Show(string text, float duration = 2.5f)
        {
            Add(new Line { Text = text, Until = Time.time + duration });
        }

        // with a position, the HUD adds which side it came from
        public static void Show(string text, Vector3 position, float duration = 2.5f)
        {
            Add(new Line { Text = text, Position = position, Positional = true, Until = Time.time + duration });
        }

        private static void Add(Line line)
        {
            if (!GameSettings.Subtitles) return;
            // the same caption again just refreshes
            for (int i = 0; i < Active.Count; i++)
            {
                if (Active[i].Text == line.Text)
                {
                    Active[i] = line;
                    return;
                }
            }

            Active.Add(line);
            if (Active.Count > 4) Active.RemoveAt(0);
        }

        public static void Prune()
        {
            Active.RemoveAll(l => Time.time > l.Until);
        }
    }
}
