using System;
using UnityEngine;

namespace Combat
{
    /* What a bullet does when it hits each surface: sparks, dust, flying debris, a splash, a hole
     * and the sound. The surface comes from Surfaces.Of (the same lookup as the footsteps), the
     * particles from CombatEffects' pooled systems. Lives in Resources; without it the built-in
     * defaults below are used, so impacts never break.
     */
    [CreateAssetMenu(menuName = "Combat/Surface Impact Library", fileName = "SurfaceImpactLibrary")]
    public class SurfaceImpactLibrary : ScriptableObject
    {
        public const string ResourcePath = "SurfaceImpactLibrary";

        [Serializable]
        public class Impact
        {
            public SurfaceType Surface;
            [Tooltip("Hot sparks (min, max)")]
            public Vector2Int Sparks;
            public Color SparkColor = new Color(1.0f, 0.75f, 0.35f);
            public float SparkSpeed = 4.0f;
            [Tooltip("Puffs of dust or mist (min, max)")]
            public Vector2Int Dust;
            public Color DustColor = new Color(0.75f, 0.72f, 0.68f, 0.55f);
            public float DustSize = 0.25f;
            [Tooltip("Chunks thrown out that fall back down: splinters, clods, chips, droplets (min, max)")]
            public Vector2Int Debris;
            public Color DebrisColor = new Color(0.3f, 0.3f, 0.3f, 1.0f);
            public float DebrisSize = 0.03f;
            public float DebrisSpeed = 3.0f;
            [Tooltip("Debris goes up instead of along the normal (water)")]
            public bool Splash;
            public bool Decal = true;
            public float DecalScale = 1.0f;
            public AudioClip[] Sounds;
            public float Volume = 0.75f;
        }

        public Impact[] Impacts = new Impact[0];

        private static SurfaceImpactLibrary _instance;
        private static bool _loaded;
        private static Impact[] _defaults;

        public static Impact Get(SurfaceType surface)
        {
            if (!_loaded)
            {
                _loaded = true;
                _instance = Resources.Load<SurfaceImpactLibrary>(ResourcePath);
            }

            if (_instance != null)
            {
                foreach (Impact impact in _instance.Impacts)
                {
                    if (impact != null && impact.Surface == surface) return impact;
                }
            }

            if (_defaults == null) _defaults = Defaults();
            foreach (Impact impact in _defaults)
            {
                if (impact.Surface == surface) return impact;
            }

            return _defaults[0];
        }

        // tuned by eye in the night lighting; the setup menu writes the same values into the asset
        public static Impact[] Defaults()
        {
            return new[]
            {
                new Impact
                {
                    Surface = SurfaceType.Concrete, Sparks = new Vector2Int(2, 5), SparkColor = new Color(1.0f, 0.9f, 0.7f), SparkSpeed = 4.0f,
                    Dust = new Vector2Int(4, 6), DustColor = new Color(0.72f, 0.7f, 0.66f, 0.6f), DustSize = 0.28f,
                    Debris = new Vector2Int(4, 7), DebrisColor = new Color(0.55f, 0.53f, 0.5f, 1.0f), DebrisSize = 0.025f, DebrisSpeed = 3.5f,
                    Decal = true, DecalScale = 1.0f, Volume = 0.7f
                },
                new Impact
                {
                    Surface = SurfaceType.Metal, Sparks = new Vector2Int(12, 18), SparkColor = new Color(1.0f, 0.72f, 0.3f), SparkSpeed = 7.5f,
                    Dust = new Vector2Int(1, 2), DustColor = new Color(0.3f, 0.3f, 0.32f, 0.45f), DustSize = 0.18f,
                    Debris = new Vector2Int(0, 0), Decal = true, DecalScale = 0.7f, Volume = 0.8f
                },
                new Impact
                {
                    Surface = SurfaceType.Wood, Sparks = new Vector2Int(0, 0),
                    Dust = new Vector2Int(2, 3), DustColor = new Color(0.55f, 0.45f, 0.33f, 0.45f), DustSize = 0.2f,
                    Debris = new Vector2Int(6, 10), DebrisColor = new Color(0.5f, 0.36f, 0.22f, 1.0f), DebrisSize = 0.04f, DebrisSpeed = 4.0f,
                    Decal = true, DecalScale = 0.9f, Volume = 0.75f
                },
                new Impact
                {
                    Surface = SurfaceType.Dirt, Sparks = new Vector2Int(0, 0),
                    Dust = new Vector2Int(4, 6), DustColor = new Color(0.4f, 0.32f, 0.24f, 0.6f), DustSize = 0.32f,
                    Debris = new Vector2Int(8, 12), DebrisColor = new Color(0.28f, 0.21f, 0.15f, 1.0f), DebrisSize = 0.035f, DebrisSpeed = 3.0f,
                    Decal = false, Volume = 0.7f
                },
                new Impact
                {
                    Surface = SurfaceType.Grass, Sparks = new Vector2Int(0, 0),
                    Dust = new Vector2Int(2, 3), DustColor = new Color(0.35f, 0.33f, 0.24f, 0.45f), DustSize = 0.24f,
                    Debris = new Vector2Int(6, 9), DebrisColor = new Color(0.22f, 0.3f, 0.14f, 1.0f), DebrisSize = 0.03f, DebrisSpeed = 2.5f,
                    Decal = false, Volume = 0.6f
                },
                new Impact
                {
                    Surface = SurfaceType.Water, Sparks = new Vector2Int(0, 0),
                    Dust = new Vector2Int(2, 3), DustColor = new Color(0.75f, 0.8f, 0.85f, 0.35f), DustSize = 0.3f,
                    Debris = new Vector2Int(12, 18), DebrisColor = new Color(0.75f, 0.82f, 0.9f, 0.8f), DebrisSize = 0.03f, DebrisSpeed = 3.2f,
                    Splash = true, Decal = false, Volume = 0.75f
                }
            };
        }
    }
}
