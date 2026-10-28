using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Combat
{
    /* Applies the display options from GameSettings: quality level, resolution and window mode,
     * v-sync and frame limit, and the shadow distance of the render pipeline. The field of view,
     * effects quality and post intensity are read where they're used (PlayerCombat, CombatEffects,
     * GameFlow). In the editor, the pipeline asset is put back as it was when play stops.
     */
    public static class DisplayOptions
    {
        public static readonly int[] FrameLimits = { 0, 30, 60, 120, 144 };
        public static readonly float[] ShadowDistances = { 30.0f, 50.0f, 70.0f, 90.0f };

        private static List<Vector2Int> _resolutions;
        private static UniversalRenderPipelineAsset _pipeline;
        private static float _originalShadowDistance = -1.0f;

        public static IReadOnlyList<Vector2Int> Resolutions
        {
            get
            {
                if (_resolutions != null) return _resolutions;
                _resolutions = new List<Vector2Int>();
                foreach (Resolution r in Screen.resolutions)
                {
                    var size = new Vector2Int(r.width, r.height);
                    if (!_resolutions.Contains(size)) _resolutions.Add(size);
                }

                return _resolutions;
            }
        }

        public static string ResolutionName(int index)
        {
            if (index < 0 || index >= Resolutions.Count) return "NATIVA";
            return $"{Resolutions[index].x}×{Resolutions[index].y}";
        }

        public static string WindowModeName(int mode) => mode == 1 ? "TELA CHEIA" : mode == 2 ? "JANELA" : "S/ BORDA";
        public static string FrameLimitName(int index) => FrameLimits[index] == 0 ? "SEM LIMITE" : FrameLimits[index] + " FPS";
        public static string QualityName(int level) => level >= 0 && level < QualitySettings.names.Length ? QualitySettings.names[level].ToUpperInvariant() : "?";

        public static void Apply()
        {
            int quality = Mathf.Clamp(GameSettings.Quality, 0, QualitySettings.names.Length - 1);
            if (QualitySettings.GetQualityLevel() != quality) QualitySettings.SetQualityLevel(quality, true);

            QualitySettings.vSyncCount = GameSettings.VSync ? 1 : 0;
            int limit = FrameLimits[GameSettings.FrameLimit];
            Application.targetFrameRate = limit > 0 ? limit : -1;

            FullScreenMode mode = GameSettings.WindowMode == 1 ? FullScreenMode.ExclusiveFullScreen
                : GameSettings.WindowMode == 2 ? FullScreenMode.Windowed : FullScreenMode.FullScreenWindow;
            int index = GameSettings.Resolution;
            Vector2Int size = index >= 0 && index < Resolutions.Count ? Resolutions[index] : new Vector2Int(Display.main.systemWidth, Display.main.systemHeight);
            // the editor's game view ignores this; it applies in a build
            if (!Application.isEditor && (Screen.width != size.x || Screen.height != size.y || Screen.fullScreenMode != mode)) Screen.SetResolution(size.x, size.y, mode);

            // the quality level may have switched the pipeline asset
            var pipeline = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (pipeline != null)
            {
                if (pipeline != _pipeline)
                {
                    Restore();
                    _pipeline = pipeline;
                    _originalShadowDistance = pipeline.shadowDistance;
                    if (Application.isEditor) Application.quitting += Restore;
                }

                pipeline.shadowDistance = ShadowDistances[GameSettings.ShadowDistance];
            }
        }

        // the editor saves changes to assets: leave the pipeline the way it was found
        private static void Restore()
        {
            if (_pipeline != null && _originalShadowDistance >= 0.0f) _pipeline.shadowDistance = _originalShadowDistance;
        }
    }
}
