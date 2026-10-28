using UnityEngine;

namespace Combat
{
    /* The base seen from above: a texture baked in the editor from the level's colliders
     * (Tools > Combat > Phase 5 > 1 - Bake Map) and the square of the world it covers.
     * Map coordinates are 0..1 across that square, x = world x, y = world z (north up).
     */
    [CreateAssetMenu(menuName = "Combat/Map Definition", fileName = "BaseMap")]
    public class MapDefinition : ScriptableObject
    {
        public const string ResourcePath = "BaseMap";

        public Texture2D Map;
        public Vector2 Min;
        public Vector2 Max = new Vector2(100.0f, 100.0f);

        private static MapDefinition _instance;
        private static bool _loaded;

        public static MapDefinition Instance
        {
            get
            {
                if (!_loaded)
                {
                    _loaded = true;
                    _instance = Resources.Load<MapDefinition>(ResourcePath);
                }

                return _instance;
            }
        }

        public Vector2 Size => Max - Min;

        public Vector2 ToMap(Vector3 world)
        {
            Vector2 size = Size;
            return new Vector2((world.x - Min.x) / size.x, (world.z - Min.y) / size.y);
        }

        public Vector3 ToWorld(Vector2 map)
        {
            return new Vector3(Min.x + map.x * Size.x, 0.0f, Min.y + map.y * Size.y);
        }
    }
}
