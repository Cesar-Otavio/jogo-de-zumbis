using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Combat
{
    /* Repeatable performance measurements in a build. Does nothing unless the game is started with
     * -perf-run. Puts the game into a fixed situation, waits for it to settle, records every frame for
     * a few seconds and writes a summary line (summary.csv) and a detail file next to it, then quits.
     *
     *   -perf-run label         name of this run in the results
     *   -perf-scene yard|wave   yard: Endless mode with no enemies; wave: N infected around the player
     *   -perf-enemies 30        how many infected for the wave scene
     *   -perf-profile name      low | medium | high | current (graphics options, see ApplyProfile)
     *   -perf-size 1280x720     window size (windowed)
     *   -perf-seconds 20        how long to record
     *   -perf-warmup 8          how long to wait before recording
     *   -perf-out C:\dir        where to write
     *   -perf-discover          also list every sampler that reported GPU time (slow, for exploring)
     *
     * Frame timings come from FrameTimingManager (needs "Frame Timing Stats" in the player settings);
     * the counters and markers only report in a development build.
     */
    public class PerfProbe : MonoBehaviour
    {
        private static readonly string[] Counters =
        {
            "Draw Calls Count", "SetPass Calls Count", "Triangles Count", "Shadow Casters Count", "Visible Skinned Meshes Count",
            "SRP Batcher Draw Calls Count", "Render Textures Count",
            "GC Allocated In Frame", "GC Allocation In Frame Count", "Total Used Memory", "GC Used Memory", "Gfx Used Memory"
        };

        // main-thread time spent in each part of the frame
        private static readonly string[] Markers =
        {
            "PlayerLoop", "Update.ScriptRunBehaviourUpdate", "PreLateUpdate.ScriptRunBehaviourLateUpdate",
            "FixedUpdate.ScriptRunBehaviourFixedUpdate", "FixedUpdate.PhysicsFixedUpdate", "Physics.Simulate",
            "Animators.Update", "PreLateUpdate.DirectorUpdateAnimationBegin", "NavMeshManager",
            "PostLateUpdate.PlayerUpdateCanvases", "UGUI.Rendering.UpdateBatches", "Camera.Render", "CullScriptable",
            "Gfx.WaitForPresentOnGfxThread", "Gfx.PresentFrame", "Audio.FMODSystemUpdate", "ParticleSystem.Update",
            "NetworkEarlyUpdate", "NetworkPreUpdate", "NetworkPostLateUpdate", "GC.Collect"
        };

        // render passes whose GPU time is worth knowing on its own
        private static readonly string[] GpuSamplers =
        {
            "MainLightShadow", "AdditionalLightsShadow", "DrawOpaqueObjects", "Draw Opaque Objects", "DrawTransparentObjects",
            "Draw Transparent Objects", "UberPostProcess", "Uber Post Process", "Bloom", "DepthPrepass", "Depth Prepass",
            "CopyDepth", "Copy Depth", "CopyColor", "Copy Color", "DrawSkybox", "Draw Skybox", "UI Rendering", "Render 2D Lights"
        };

        private string _label = "run";
        private string _scene = "yard";
        private int _enemies = 30;
        private string _profile = "current";
        private Vector2Int _size = new Vector2Int(1280, 720);
        private float _seconds = 20.0f;
        private float _warmup = 8.0f;
        private string _out = ".";
        private bool _discover;
        private readonly Dictionary<string, float> _savedSettings = new Dictionary<string, float>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-perf-run") < 0) return;
            if (FindAnyObjectByType<PerfProbe>() != null) return;
            var probe = new GameObject("PerfProbe").AddComponent<PerfProbe>();
            DontDestroyOnLoad(probe.gameObject);
        }

        private void Start()
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                string next = i + 1 < args.Length ? args[i + 1] : null;
                switch (args[i])
                {
                    case "-perf-run": _label = next; break;
                    case "-perf-scene": _scene = next; break;
                    case "-perf-enemies": int.TryParse(next, out _enemies); break;
                    case "-perf-profile": _profile = next; break;
                    case "-perf-size":
                        string[] parts = next != null ? next.Split('x') : new string[0];
                        if (parts.Length == 2 && int.TryParse(parts[0], out int w) && int.TryParse(parts[1], out int h)) _size = new Vector2Int(w, h);
                        break;
                    case "-perf-seconds": float.TryParse(next, NumberStyles.Float, CultureInfo.InvariantCulture, out _seconds); break;
                    case "-perf-warmup": float.TryParse(next, NumberStyles.Float, CultureInfo.InvariantCulture, out _warmup); break;
                    case "-perf-out": _out = next; break;
                    case "-perf-discover": _discover = true; break;
                }
            }

            StartCoroutine(Run());
        }

        private IEnumerator Run()
        {
            while (GameFlow.Instance == null || Time.unscaledTime < 3.0f) yield return null;

            ApplyProfile();
            yield return null;
            // a window can't be as big as the display: the full display size runs borderless
            bool full = _size.x >= Display.main.systemWidth && _size.y >= Display.main.systemHeight;
            Screen.SetResolution(_size.x, _size.y, full ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed);
            yield return new WaitForSecondsRealtime(1.0f);

            GameFlow.Instance.StartGame(GameMode.Endless);
            yield return new WaitForSecondsRealtime(1.5f);
            PrepareScene();

            yield return new WaitForSecondsRealtime(_warmup);
            yield return Record();

            // the screenshot is written at the end of a frame: give it two before quitting
            ScreenCapture.CaptureScreenshot(Path.Combine(_out, $"{_label}.png"));
            yield return null;
            yield return null;
            RestoreSettings();
            Application.Quit();
        }

        // ---------------------------------------------------------------- setup

        // low: the Baixo quality level with short shadows and reduced effects; medium: Médio with the default
        // shadows; high: Alto with long shadows; current: whatever the player has chosen
        private void ApplyProfile()
        {
            Save("Quality", GameSettings.Quality);
            Save("ShadowDistance", GameSettings.ShadowDistance);
            Save("Effects", GameSettings.EffectsQuality);
            Save("FrameLimit", GameSettings.FrameLimit);
            Save("VSync", GameSettings.VSync ? 1 : 0);

            int last = QualitySettings.names.Length - 1;
            switch (_profile)
            {
                case "low":
                    GameSettings.Quality = Level("Baixo", 0);
                    GameSettings.ShadowDistance = 0;
                    GameSettings.EffectsQuality = 0;
                    break;
                case "medium":
                    GameSettings.Quality = Level("Médio", last);
                    GameSettings.ShadowDistance = 1;
                    GameSettings.EffectsQuality = 1;
                    break;
                case "high":
                    GameSettings.Quality = Level("Alto", last);
                    GameSettings.ShadowDistance = 3;
                    GameSettings.EffectsQuality = 2;
                    break;
            }

            // measure what the hardware can do, not the frame cap
            GameSettings.FrameLimit = 0;
            GameSettings.VSync = false;
            DisplayOptions.Apply();
        }

        private void Save(string key, float value) => _savedSettings[key] = value;

        private static int Level(string name, int fallback)
        {
            int index = Array.IndexOf(QualitySettings.names, name);
            return index >= 0 ? index : fallback;
        }

        private void RestoreSettings()
        {
            if (_savedSettings.Count == 0) return;
            GameSettings.Quality = Mathf.RoundToInt(_savedSettings["Quality"]);
            GameSettings.ShadowDistance = Mathf.RoundToInt(_savedSettings["ShadowDistance"]);
            GameSettings.EffectsQuality = Mathf.RoundToInt(_savedSettings["Effects"]);
            GameSettings.FrameLimit = Mathf.RoundToInt(_savedSettings["FrameLimit"]);
            GameSettings.VSync = _savedSettings["VSync"] > 0.5f;
        }

        private void PrepareScene()
        {
            var spawner = FindAnyObjectByType<EnemySpawner>();
            if (spawner != null) spawner.StopWaves();
            foreach (ZombieAI zombie in FindObjectsByType<ZombieAI>()) Destroy(zombie.gameObject);
            foreach (EnemyShooterAI soldier in FindObjectsByType<EnemyShooterAI>()) Destroy(soldier.gameObject);
            if (spawner != null) spawner.enabled = false;

            PlayerCombat player = FindAnyObjectByType<PlayerCombat>();
            if (player != null) player.Health.InvulnerableUntil = float.MaxValue;

            if (_scene == "wave" && spawner != null && player != null)
                spawner.SpawnPressure(_enemies, player.transform.position);
        }

        // ---------------------------------------------------------------- recording

        private IEnumerator Record()
        {
            var counters = new Dictionary<string, ProfilerRecorder>();
            foreach (string name in Counters) counters[name] = ProfilerRecorder.StartNew(Category(name), name);
            var markers = new Dictionary<string, ProfilerRecorder>();
            foreach (string name in Markers) markers[name] = ProfilerRecorder.StartNew(ProfilerCategory.Internal, name);
            var gpu = new Dictionary<string, Recorder>();
            foreach (string name in GpuSamplers)
            {
                Recorder recorder = Recorder.Get(name);
                if (!recorder.isValid) continue;
                recorder.enabled = true;
                gpu[name] = recorder;
            }

            var discovered = new Dictionary<string, double>();
            var discoveredCpu = new Dictionary<string, double>();
            var discoverRecorders = new List<KeyValuePair<string, Recorder>>();
            if (_discover)
            {
                var names = new List<string>();
                Sampler.GetNames(names);
                foreach (string name in names)
                {
                    Recorder recorder = Recorder.Get(name);
                    if (!recorder.isValid) continue;
                    recorder.enabled = true;
                    discoverRecorders.Add(new KeyValuePair<string, Recorder>(name, recorder));
                }
            }

            var frame = new List<float>();
            var cpuMain = new List<float>();
            var cpuRender = new List<float>();
            var gpuFrame = new List<float>();
            var counterSums = new Dictionary<string, double>();
            var counterMax = new Dictionary<string, double>();
            var markerSums = new Dictionary<string, double>();
            var gpuSums = new Dictionary<string, double>();
            var timings = new FrameTiming[1];
            int lightsSum = 0, shadowLightsSum = 0, enemiesSum = 0;
            float end = Time.unscaledTime + _seconds;
            yield return null;

            while (Time.unscaledTime < end)
            {
                yield return null;
                frame.Add(Time.unscaledDeltaTime * 1000.0f);

                FrameTimingManager.CaptureFrameTimings();
                if (FrameTimingManager.GetLatestTimings(1, timings) > 0)
                {
                    cpuMain.Add((float)timings[0].cpuMainThreadFrameTime);
                    cpuRender.Add((float)timings[0].cpuRenderThreadFrameTime);
                    gpuFrame.Add((float)timings[0].gpuFrameTime);
                }

                foreach (var pair in counters)
                {
                    if (!pair.Value.Valid) continue;
                    double value = pair.Value.LastValue;
                    counterSums[pair.Key] = (counterSums.TryGetValue(pair.Key, out double s) ? s : 0.0) + value;
                    counterMax[pair.Key] = Math.Max(counterMax.TryGetValue(pair.Key, out double m) ? m : 0.0, value);
                }

                foreach (var pair in markers)
                    if (pair.Value.Valid) markerSums[pair.Key] = (markerSums.TryGetValue(pair.Key, out double s) ? s : 0.0) + pair.Value.LastValue / 1e6;

                foreach (var pair in gpu)
                    gpuSums[pair.Key] = (gpuSums.TryGetValue(pair.Key, out double s) ? s : 0.0) + pair.Value.gpuElapsedNanoseconds / 1e6;

                foreach (var pair in discoverRecorders)
                {
                    long ns = pair.Value.gpuElapsedNanoseconds;
                    if (ns > 0) discovered[pair.Key] = (discovered.TryGetValue(pair.Key, out double s) ? s : 0.0) + ns / 1e6;
                    long cpu = pair.Value.elapsedNanoseconds;
                    if (cpu > 0) discoveredCpu[pair.Key] = (discoveredCpu.TryGetValue(pair.Key, out double c) ? c : 0.0) + cpu / 1e6;
                }

                if (frame.Count % 30 == 0)
                {
                    CountLights(out int lights, out int shadowLights);
                    lightsSum += lights;
                    shadowLightsSum += shadowLights;
                    enemiesSum += Health.Living.Count;
                }
            }

            int samples = Mathf.Max(1, frame.Count / 30);
            foreach (var pair in counters) pair.Value.Dispose();
            foreach (var pair in markers) pair.Value.Dispose();
            foreach (var pair in gpu) pair.Value.enabled = false;
            foreach (var pair in discoverRecorders) pair.Value.enabled = false;

            Write(frame, cpuMain, cpuRender, gpuFrame, counterSums, counterMax, markerSums, gpuSums, discovered, discoveredCpu,
                lightsSum / (float)samples, shadowLightsSum / (float)samples, enemiesSum / (float)samples);
        }

        private static ProfilerCategory Category(string name)
        {
            return name.Contains("Memory") || name.StartsWith("GC ") ? ProfilerCategory.Memory : ProfilerCategory.Render;
        }

        // realtime lights the camera can see right now (and how many of them cast shadows)
        private static void CountLights(out int lights, out int shadowLights)
        {
            lights = 0;
            shadowLights = 0;
            Camera camera = Camera.main;
            if (camera == null) return;
            Plane[] planes = GeometryUtility.CalculateFrustumPlanes(camera);
            foreach (Light light in FindObjectsByType<Light>())
            {
                if (!light.enabled || light.intensity <= 0.0f || (light.bakingOutput.isBaked && light.bakingOutput.lightmapBakeType == LightmapBakeType.Baked)) continue;
                bool visible = light.type == LightType.Directional ||
                               GeometryUtility.TestPlanesAABB(planes, new Bounds(light.transform.position, Vector3.one * light.range * 2.0f));
                if (!visible) continue;
                lights++;
                if (light.shadows != LightShadows.None) shadowLights++;
            }
        }

        // ---------------------------------------------------------------- output

        private void Write(List<float> frame, List<float> cpuMain, List<float> cpuRender, List<float> gpuFrame,
            Dictionary<string, double> counterSums, Dictionary<string, double> counterMax, Dictionary<string, double> markerSums,
            Dictionary<string, double> gpuSums, Dictionary<string, double> discovered, Dictionary<string, double> discoveredCpu, float lights, float shadowLights, float enemies)
        {
            int n = Mathf.Max(1, frame.Count);
            var sorted = new List<float>(frame);
            sorted.Sort();
            float avg = Average(frame);
            var pipeline = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            string F(double v) => v.ToString("F2", CultureInfo.InvariantCulture);
            string C(string key) => counterSums.TryGetValue(key, out double s) ? (s / n).ToString("F0", CultureInfo.InvariantCulture) : "";

            Directory.CreateDirectory(_out);
            string summary = Path.Combine(_out, "summary.csv");
            bool header = !File.Exists(summary);
            var line = new StringBuilder();
            if (header)
                line.AppendLine("run;dev;scene;enemies_alive;profile;quality;size;shadow_dist;frames;avg_ms;fps;median_ms;p95_ms;p99_ms;max_ms;cpu_main_ms;cpu_render_ms;gpu_ms;draw_calls;setpass;triangles;shadow_casters;lights_visible;shadow_lights_visible;gc_alloc_bytes_frame;total_mem_mb;gpu_device");
            line.AppendLine(string.Join(";",
                _label, Debug.isDebugBuild ? "dev" : "release", _scene, F(enemies), _profile, QualitySettings.names[QualitySettings.GetQualityLevel()],
                $"{Screen.width}x{Screen.height}", pipeline != null ? F(pipeline.shadowDistance) : "", frame.Count,
                F(avg), F(1000.0 / Math.Max(0.001, avg)), F(Percentile(sorted, 0.5f)), F(Percentile(sorted, 0.95f)), F(Percentile(sorted, 0.99f)),
                F(sorted.Count > 0 ? sorted[sorted.Count - 1] : 0.0f), F(Average(cpuMain)), F(Average(cpuRender)), F(Average(gpuFrame)),
                C("Draw Calls Count"), C("SetPass Calls Count"), C("Triangles Count"), C("Shadow Casters Count"), F(lights), F(shadowLights),
                C("GC Allocated In Frame"), counterSums.TryGetValue("Total Used Memory", out double mem) ? F(mem / n / (1024 * 1024)) : "",
                SystemInfo.graphicsDeviceName.Replace(';', ',')));
            File.AppendAllText(summary, line.ToString());

            var detail = new StringBuilder();
            detail.AppendLine($"run {_label} ({(Debug.isDebugBuild ? "development" : "release")} build, {Application.version})");
            detail.AppendLine($"cpu {SystemInfo.processorType} ({SystemInfo.processorCount} threads), gpu {SystemInfo.graphicsDeviceName} ({SystemInfo.graphicsDeviceType}, {SystemInfo.graphicsMemorySize} MB), ram {SystemInfo.systemMemorySize} MB");
            detail.AppendLine($"screen {Screen.width}x{Screen.height} {Screen.fullScreenMode}, display {Display.main.systemWidth}x{Display.main.systemHeight}, quality {QualitySettings.names[QualitySettings.GetQualityLevel()]}, pipeline {(pipeline != null ? pipeline.name : "?")}, profile {_profile}");
            detail.AppendLine($"frame timing stats {FrameTimingManager.IsFeatureEnabled()}, gpu samples {gpuFrame.Count}/{frame.Count}");
            detail.AppendLine($"frames {frame.Count}: avg {F(avg)} ms, median {F(Percentile(sorted, 0.5f))}, p95 {F(Percentile(sorted, 0.95f))}, p99 {F(Percentile(sorted, 0.99f))}, max {F(sorted.Count > 0 ? sorted[sorted.Count - 1] : 0)}");
            detail.AppendLine($"cpu main {F(Average(cpuMain))} ms, render thread {F(Average(cpuRender))} ms, gpu {F(Average(gpuFrame))} ms (p95 gpu {F(Percentile(Sorted(gpuFrame), 0.95f))})");
            detail.AppendLine($"lights visible {F(lights)}, with shadows {F(shadowLights)}, living characters {F(enemies)}");
            detail.AppendLine("\ncounters (average / max per frame):");
            foreach (string key in Counters)
                if (counterSums.TryGetValue(key, out double s)) detail.AppendLine($"  {key}: {F(s / n)} / {F(counterMax[key])}");
            detail.AppendLine("\nmain thread markers (average ms per frame):");
            foreach (string key in Markers)
                if (markerSums.TryGetValue(key, out double s)) detail.AppendLine($"  {key}: {F(s / n)}");
            detail.AppendLine("\ngpu passes (average ms per frame):");
            foreach (var pair in gpuSums) detail.AppendLine($"  {pair.Key}: {F(pair.Value / n)}");
            if (discovered.Count > 0)
            {
                detail.AppendLine("\nall samplers with gpu time (average ms per frame):");
                var list = new List<KeyValuePair<string, double>>(discovered);
                list.Sort((a, b) => b.Value.CompareTo(a.Value));
                foreach (var pair in list) detail.AppendLine($"  {pair.Key}: {F(pair.Value / n)}");
            }

            if (discoveredCpu.Count > 0)
            {
                // every thread, nested samplers counted inside their parents too
                detail.AppendLine("\nsamplers with the most cpu time (average ms per frame, any thread, nested):");
                var list = new List<KeyValuePair<string, double>>(discoveredCpu);
                list.Sort((a, b) => b.Value.CompareTo(a.Value));
                for (int i = 0; i < list.Count && i < 120; i++) detail.AppendLine($"  {list[i].Key}: {F(list[i].Value / n)}");
            }

            File.WriteAllText(Path.Combine(_out, $"{_label}.txt"), detail.ToString());
        }

        private static List<float> Sorted(List<float> values)
        {
            var copy = new List<float>(values);
            copy.Sort();
            return copy;
        }

        private static float Average(List<float> values)
        {
            if (values.Count == 0) return 0.0f;
            double sum = 0.0;
            foreach (float v in values) sum += v;
            return (float)(sum / values.Count);
        }

        private static float Percentile(List<float> sorted, float p)
        {
            if (sorted.Count == 0) return 0.0f;
            int index = Mathf.Clamp(Mathf.CeilToInt(p * sorted.Count) - 1, 0, sorted.Count - 1);
            return sorted[index];
        }
    }
}
