using System.Collections.Generic;
using UnityEngine;

namespace Combat
{
    /* Street lamps cast shadows only where they're worth it: the few working lamps nearest the camera
     * get soft shadows, every other lamp lights without them. Measured in a build (Docs/FASE7/01), dozens
     * of shadowed spot lights cost more CPU (shadow culling and drawing) than all the gameplay scripts,
     * while the shadows of far lamps can't be told apart.
     *
     * The budget follows the graphics options: none when the quality level's pipeline has no additional
     * light shadows, otherwise one to four lamps by the shadow distance option. Lamps already shadowed keep their place unless another one is
     * clearly closer, so shadows don't pop on and off while walking between two lamps.
     */
    public class LampShadowBudget : MonoBehaviour
    {
        private static readonly int[] BudgetByShadowDistance = { 1, 2, 3, 4 };

        [Tooltip("Seconds between re-ranking the lamps")]
        public float Interval = 0.4f;
        [Tooltip("A shadowed lamp counts as this much closer when ranking (keeps shadows from flipping)")]
        [Range(0.5f, 1.0f)] public float Stickiness = 0.8f;
        [Range(0.0f, 1.0f)] public float ShadowStrength = 0.85f;

        // a pipeline without additional light shadows (the low quality level) gets none
        public static int Budget
        {
            get
            {
                var pipeline = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline as UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset;
                if (pipeline != null && !pipeline.supportsAdditionalLightShadows) return 0;
                return BudgetByShadowDistance[GameSettings.ShadowDistance];
            }
        }

        private readonly List<(LightFlicker lamp, float score)> _ranked = new List<(LightFlicker, float)>();
        private readonly HashSet<LightFlicker> _shadowed = new HashSet<LightFlicker>();
        private float _next;

        private void Update()
        {
            if (Time.unscaledTime < _next) return;
            _next = Time.unscaledTime + Interval;

            Camera camera = Camera.main;
            if (camera == null) return;
            Vector3 eye = camera.transform.position;
            int budget = Budget;

            _ranked.Clear();
            foreach (LightFlicker lamp in LightFlicker.All)
            {
                if (lamp == null || lamp.Light == null) continue;
                if (!lamp.IsOn)
                {
                    Shadow(lamp, false);
                    continue;
                }

                float distance = Vector3.Distance(eye, lamp.transform.position);
                if (_shadowed.Contains(lamp)) distance *= Stickiness;
                _ranked.Add((lamp, distance));
            }

            _ranked.Sort((a, b) => a.score.CompareTo(b.score));
            for (int i = 0; i < _ranked.Count; i++) Shadow(_ranked[i].lamp, i < budget);
        }

        private void Shadow(LightFlicker lamp, bool on)
        {
            Light light = lamp.Light;
            if (light == null) return;
            LightShadows wanted = on ? LightShadows.Soft : LightShadows.None;
            if (light.shadows != wanted)
            {
                light.shadows = wanted;
                light.shadowStrength = ShadowStrength;
            }

            if (on) _shadowed.Add(lamp);
            else _shadowed.Remove(lamp);
        }
    }
}
