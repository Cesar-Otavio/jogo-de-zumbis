using System;
using UnityEngine;

namespace Combat
{
    /* What the survivor knows of the base: a coarse grid over the map, revealed around the player
     * as they walk and in larger patches by documents, terminals or missions (Reveal). Kept in
     * PlayerPrefs, since the base doesn't change between runs; a new campaign forgets it.
     * The fog texture is what the map screens draw on top of the map.
     */
    public static class MapDiscovery
    {
        public const int Size = 64;
        private const string PrefsKey = "Combat.MapFog";
        private static readonly Color32 Unknown = new Color32(4, 5, 7, 235);
        private static readonly Color32 Known = new Color32(0, 0, 0, 0);

        private static bool[] _cells;
        private static Texture2D _fog;
        private static bool _textureDirty;
        private static bool _saveDirty;
        private static float _nextSave;

        public static event Action Changed;

        public static Texture2D Fog
        {
            get
            {
                Ensure();
                if (_textureDirty) ApplyTexture();
                return _fog;
            }
        }

        public static float RevealedFraction
        {
            get
            {
                Ensure();
                int known = 0;
                foreach (bool cell in _cells) if (cell) known++;
                return known / (float)_cells.Length;
            }
        }

        public static bool IsRevealed(Vector3 world)
        {
            Ensure();
            MapDefinition map = MapDefinition.Instance;
            if (map == null) return true;
            Vector2 uv = map.ToMap(world);
            int x = Mathf.FloorToInt(uv.x * Size), y = Mathf.FloorToInt(uv.y * Size);
            if (x < 0 || y < 0 || x >= Size || y >= Size) return true;
            return _cells[y * Size + x];
        }

        // returns true if anything new was revealed
        public static bool Reveal(Vector3 world, float radius)
        {
            Ensure();
            MapDefinition map = MapDefinition.Instance;
            if (map == null) return false;
            Vector2 uv = map.ToMap(world);
            float cellsX = radius / map.Size.x * Size, cellsY = radius / map.Size.y * Size;
            int cx = Mathf.FloorToInt(uv.x * Size), cy = Mathf.FloorToInt(uv.y * Size);
            bool changed = false;
            for (int y = Mathf.Max(0, cy - Mathf.CeilToInt(cellsY)); y <= Mathf.Min(Size - 1, cy + Mathf.CeilToInt(cellsY)); y++)
            {
                for (int x = Mathf.Max(0, cx - Mathf.CeilToInt(cellsX)); x <= Mathf.Min(Size - 1, cx + Mathf.CeilToInt(cellsX)); x++)
                {
                    float dx = (x + 0.5f - uv.x * Size) / Mathf.Max(cellsX, 0.01f);
                    float dy = (y + 0.5f - uv.y * Size) / Mathf.Max(cellsY, 0.01f);
                    if (dx * dx + dy * dy > 1.0f || _cells[y * Size + x]) continue;
                    _cells[y * Size + x] = true;
                    changed = true;
                }
            }

            if (!changed) return false;
            _textureDirty = true;
            _saveDirty = true;
            Changed?.Invoke();
            return true;
        }

        public static void RevealAll()
        {
            Ensure();
            for (int i = 0; i < _cells.Length; i++) _cells[i] = true;
            _textureDirty = true;
            _saveDirty = true;
            Changed?.Invoke();
        }

        // a new campaign: the base is unknown again
        public static void Forget()
        {
            Ensure();
            Array.Clear(_cells, 0, _cells.Length);
            PlayerPrefs.DeleteKey(PrefsKey);
            _textureDirty = true;
            Changed?.Invoke();
        }

        // called regularly by the minimap; writes at most every few seconds
        public static void SaveIfNeeded(bool force = false)
        {
            if (!_saveDirty || (!force && Time.unscaledTime < _nextSave)) return;
            _saveDirty = false;
            _nextSave = Time.unscaledTime + 5.0f;
            var bytes = new byte[_cells.Length / 8];
            for (int i = 0; i < _cells.Length; i++) if (_cells[i]) bytes[i >> 3] |= (byte)(1 << (i & 7));
            PlayerPrefs.SetString(PrefsKey, Convert.ToBase64String(bytes));
            PlayerPrefs.Save();
        }

        private static void Ensure()
        {
            if (_cells != null) return;
            _cells = new bool[Size * Size];
            string saved = PlayerPrefs.GetString(PrefsKey, "");
            if (!string.IsNullOrEmpty(saved))
            {
                try
                {
                    byte[] bytes = Convert.FromBase64String(saved);
                    for (int i = 0; i < _cells.Length && (i >> 3) < bytes.Length; i++) _cells[i] = (bytes[i >> 3] & (1 << (i & 7))) != 0;
                }
                catch (FormatException)
                {
                    // a damaged save just means an unexplored map
                }
            }

            _fog = new Texture2D(Size, Size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, name = "MapFog" };
            _textureDirty = true;
        }

        private static void ApplyTexture()
        {
            var pixels = new Color32[_cells.Length];
            for (int i = 0; i < _cells.Length; i++) pixels[i] = _cells[i] ? Known : Unknown;
            _fog.SetPixels32(pixels);
            _fog.Apply(false);
            _textureDirty = false;
        }
    }
}
