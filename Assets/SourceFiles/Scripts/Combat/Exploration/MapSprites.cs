using UnityEngine;

namespace Combat
{
    // small shapes for the map screens, drawn once in code (anti-aliased), so no art is needed for them
    public static class MapSprites
    {
        private static Sprite _circle;
        private static Sprite _ring;
        private static Sprite _arrow;
        private static Sprite _rim;

        public static Sprite Circle => _circle != null ? _circle : _circle = Make("MapCircle", (x, y) => 0.46f - Mathf.Sqrt(x * x + y * y));
        public static Sprite Ring => _ring != null ? _ring : _ring = Make("MapRing", (x, y) => 0.09f - Mathf.Abs(Mathf.Sqrt(x * x + y * y) - 0.38f));

        // a thin ring at the very edge, for framing a round clipped area
        public static Sprite Rim => _rim != null ? _rim : _rim = Make("MapRim", (x, y) => 0.016f - Mathf.Abs(Mathf.Sqrt(x * x + y * y) - 0.478f), 256);

        // pointing up: a chevron, so the facing reads even when tiny
        public static Sprite Arrow => _arrow != null ? _arrow : _arrow = Make("MapArrow", (x, y) =>
        {
            float outer = Triangle(x, y, new Vector2(0.0f, 0.46f), new Vector2(0.38f, -0.42f), new Vector2(-0.38f, -0.42f));
            float notch = Triangle(x, y, new Vector2(0.0f, -0.12f), new Vector2(0.42f, -0.6f), new Vector2(-0.42f, -0.6f));
            return Mathf.Min(outer, -notch);
        });

        // value > 0 inside, in units of the sprite's width
        private static Sprite Make(string name, System.Func<float, float, float> inside, int size = 64)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = name, filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = inside((x + 0.5f) / size - 0.5f, (y + 0.5f) / size - 0.5f);
                    byte a = (byte)(Mathf.Clamp01(d * size + 0.5f) * 255);
                    pixels[y * size + x] = new Color32(255, 255, 255, a);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        }

        // signed distance-ish: positive inside a triangle given clockwise (a at the top, then b, then c)
        private static float Triangle(float x, float y, Vector2 a, Vector2 b, Vector2 c)
        {
            var p = new Vector2(x, y);
            return Mathf.Min(Edge(p, a, b), Mathf.Min(Edge(p, b, c), Edge(p, c, a)));
        }

        private static float Edge(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 e = b - a;
            Vector2 n = new Vector2(e.y, -e.x).normalized;
            return Vector2.Dot(p - a, n);
        }
    }
}
