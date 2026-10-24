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
        // closed captions: descriptions of important sounds ([passos à esquerda])
        public static bool Subtitles { get => Get("Subtitles", 1.0f) > 0.5f; set => Set("Subtitles", value ? 1.0f : 0.0f); }
        // what the narrator and the radio say
        public static bool DialogueSubtitles { get => Get("DialogueSubtitles", 1.0f) > 0.5f; set => Set("DialogueSubtitles", value ? 1.0f : 0.0f); }
        // size of both kinds of subtitle, 0.8 .. 1.5
        public static float SubtitleScale { get => Mathf.Clamp(Get("SubtitleScale", 1.0f), 0.8f, 1.5f); set => Set("SubtitleScale", Mathf.Clamp(value, 0.8f, 1.5f)); }
        // the objective marker with distance on screen
        public static bool ObjectiveHints { get => Get("Hints", 1.0f) > 0.5f; set => Set("Hints", value ? 1.0f : 0.0f); }
        // volume groups, 0..1 (there's no AudioMixer asset yet: each system scales its own sources; see Docs/FASE3_ENTREGA.md)
        public static float MasterVolume { get => Get("VolMaster", 1.0f); set { Set("VolMaster", Mathf.Clamp01(value)); AudioListener.volume = Mathf.Clamp01(value); } }
        public static float MusicVolume { get => Get("VolMusic", 1.0f); set => Set("VolMusic", Mathf.Clamp01(value)); }
        public static float SfxVolume { get => Get("VolSfx", 1.0f); set => Set("VolSfx", Mathf.Clamp01(value)); }
        public static float DialogueVolume { get => Get("VolDialogue", 1.0f); set => Set("VolDialogue", Mathf.Clamp01(value)); }
        public static float AmbientVolume { get => Get("VolAmbient", 1.0f); set => Set("VolAmbient", Mathf.Clamp01(value)); }
        public static float UiVolume { get => Get("VolUi", 1.0f); set => Set("VolUi", Mathf.Clamp01(value)); }
        public static bool Minimap { get => Get("Minimap", 1.0f) > 0.5f; set => Set("Minimap", value ? 1.0f : 0.0f); }
        // the minimap turns with the camera (true) or keeps north up
        public static bool MinimapRotates { get => Get("MinimapRotate", 1.0f) > 0.5f; set => Set("MinimapRotate", value ? 1.0f : 0.0f); }
        // 0 close, 1 medium, 2 far
        public static int MinimapZoom { get => Mathf.Clamp(Mathf.RoundToInt(Get("MinimapZoom", 1.0f)), 0, 2); set => Set("MinimapZoom", Mathf.Clamp(value, 0, 2)); }
        // ---- display and graphics (applied by DisplayOptions)
        public static int Quality { get => Mathf.RoundToInt(Get("Quality", QualitySettings.GetQualityLevel())); set => Set("Quality", value); }
        // index into DisplayOptions.Resolutions, -1 = the screen's own
        public static int Resolution { get => Mathf.RoundToInt(Get("Resolution", -1.0f)); set => Set("Resolution", value); }
        // 0 borderless full screen, 1 exclusive full screen, 2 window
        public static int WindowMode { get => Mathf.Clamp(Mathf.RoundToInt(Get("WindowMode", 0.0f)), 0, 2); set => Set("WindowMode", Mathf.Clamp(value, 0, 2)); }
        // degrees added to the camera's field of view (hip fire; aiming keeps each gun's own zoom)
        public static float FieldOfViewOffset { get => Mathf.Clamp(Get("FovOffset", 0.0f), -10.0f, 20.0f); set => Set("FovOffset", Mathf.Clamp(value, -10.0f, 20.0f)); }
        // 0..3 = 30, 50, 70, 90 m
        public static int ShadowDistance { get => Mathf.Clamp(Mathf.RoundToInt(Get("ShadowDistance", 1.0f)), 0, 3); set => Set("ShadowDistance", Mathf.Clamp(value, 0, 3)); }
        // 0 low, 1 medium, 2 high: particles and bullet holes
        public static int EffectsQuality { get => Mathf.Clamp(Mathf.RoundToInt(Get("Effects", 2.0f)), 0, 2); set => Set("Effects", Mathf.Clamp(value, 0, 2)); }
        public static float EffectsScale => EffectsQuality == 0 ? 0.4f : EffectsQuality == 1 ? 0.7f : 1.0f;
        // index into DisplayOptions.FrameLimits
        public static int FrameLimit { get => Mathf.Clamp(Mathf.RoundToInt(Get("FrameLimit", 0.0f)), 0, 4); set => Set("FrameLimit", Mathf.Clamp(value, 0, 4)); }
        public static bool VSync { get => Get("VSync", 1.0f) > 0.5f; set => Set("VSync", value ? 1.0f : 0.0f); }
        // how strongly the night grade (vignette, grain, colour) is applied
        public static float PostIntensity { get => Mathf.Clamp(Get("Post", 1.0f), 0.4f, 1.0f); set => Set("Post", Mathf.Clamp(value, 0.4f, 1.0f)); }

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
