using UnityEngine;

namespace Combat
{
    // a light that flares up and fades out, e.g. an explosion
    public class TimedLight : MonoBehaviour
    {
        private Light _light;
        private float _peak;
        private float _life;
        private float _born;

        public static void Flash(Vector3 position, Color color, float intensity, float range, float life)
        {
            var go = new GameObject("Flash");
            go.transform.position = position;
            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = color;
            light.range = range;
            light.intensity = intensity;
            light.shadows = LightShadows.None;
            var timed = go.AddComponent<TimedLight>();
            timed._light = light;
            timed._peak = intensity;
            timed._life = life;
            timed._born = Time.time;
        }

        private void Update()
        {
            float t = (Time.time - _born) / Mathf.Max(_life, 0.01f);
            if (t >= 1.0f)
            {
                Destroy(gameObject);
                return;
            }

            _light.intensity = _peak * (1.0f - t) * (1.0f - t);
        }
    }
}
