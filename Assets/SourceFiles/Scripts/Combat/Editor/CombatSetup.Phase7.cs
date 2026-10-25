using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Combat.EditorTools
{
    /* Phase 7 (definitive edition) tools:
     *   1 - development build with the profiler markers (Builds/Win64Dev), for PerfProbe runs
     *   2 - release build (Builds/Win64)
     * Both turn on Frame Timing Stats, which PerfProbe needs for the CPU/GPU frame times.
     */
    public static partial class CombatSetup
    {
        [MenuItem("Tools/Combat/Phase 7/1 - Development Build (profiling)")]
        public static void BuildPhase7Development() => BuildWindows("Builds/Win64Dev/BaseMilitar.exe", BuildOptions.Development);

        [MenuItem("Tools/Combat/Phase 7/2 - Release Build")]
        public static void BuildPhase7Release() => BuildWindows("Builds/Win64/BaseMilitar.exe", BuildOptions.None);

        // The lamp posts of the base pack carry their own "Spot Light", marked Baked but never baked, so
        // it renders in real time with hard shadows, isn't fed by the power grid (stays on in a blackout)
        // and doubles the lamps' light count. The lamps' own LampLight (power grid, flicker) does the
        // lighting; the shadow budget gives the nearest of them real shadows.
        [MenuItem("Tools/Combat/Phase 7/3 - Lamp Lights Optimization")]
        public static void SetupPhase7LampLights()
        {
            int disabled = 0;
            foreach (Light light in Object.FindObjectsByType<Light>(FindObjectsInactive.Include))
            {
                if (light.name != "Spot Light" || light.GetComponent<LightFlicker>() != null) continue;
                if (light.transform.parent == null || !light.transform.parent.name.StartsWith("Lamp")) continue;
                if (!light.enabled) continue;
                Undo.RecordObject(light, "Disable pack lamp light");
                light.enabled = false;
                disabled++;
            }

            var budget = Object.FindAnyObjectByType<LampShadowBudget>();
            if (budget == null)
            {
                var go = new GameObject("LampShadowBudget");
                Undo.RegisterCreatedObjectUndo(go, "Lamp shadow budget");
                go.AddComponent<LampShadowBudget>();
            }

            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes();
            Debug.Log($"Lamp lights: {disabled} pack spot lights disabled, shadow budget in the scene");
        }

        private static void BuildWindows(string path, BuildOptions options)
        {
            PlayerSettings.enableFrameTimingStats = true;
            string[] scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            if (!scenes.Contains(BaseCombatScene)) scenes = new[] { BaseCombatScene }.Concat(scenes).ToArray();
            UnityEditor.Build.Reporting.BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = path,
                target = BuildTarget.StandaloneWindows64,
                options = options
            });
            var summary = report.summary;
            Debug.Log($"Build {summary.result}: {summary.outputPath}, {summary.totalSize / (1024 * 1024)} MB, {summary.totalTime.TotalSeconds:F0} s, {summary.totalErrors} erros");
        }
    }
}
