using System.Collections.Generic;
using UnityEngine;

namespace Combat
{
    public enum SurfaceType
    {
        Concrete,
        Metal,
        Dirt,
        Grass,
        Water,
        Wood
    }

    /* Works out what a collider is made of: a SurfaceMaterial component wins, otherwise
     * the object's name is matched against keywords (the base's models are named after what they are).
     * Results are cached per collider.
     */
    public static class Surfaces
    {
        private static readonly Dictionary<int, SurfaceType> Cache = new Dictionary<int, SurfaceType>();

        private static readonly (string keyword, SurfaceType surface)[] Keywords =
        {
            ("container", SurfaceType.Metal), ("hangar", SurfaceType.Metal), ("garage", SurfaceType.Metal),
            ("roof", SurfaceType.Metal), ("barrel", SurfaceType.Metal), ("ladder", SurfaceType.Metal),
            ("tower", SurfaceType.Metal), ("fence", SurfaceType.Metal), ("metal", SurfaceType.Metal),
            ("generator", SurfaceType.Metal), ("door", SurfaceType.Metal),
            ("lamp", SurfaceType.Metal), ("pole", SurfaceType.Metal), ("antenna", SurfaceType.Metal),
            ("wood", SurfaceType.Wood), ("crate", SurfaceType.Wood), ("chest", SurfaceType.Wood),
            ("pallet", SurfaceType.Wood), ("plank", SurfaceType.Wood), ("toilet", SurfaceType.Wood),
            ("grass", SurfaceType.Grass), ("bush", SurfaceType.Grass),
            ("water", SurfaceType.Water), ("puddle", SurfaceType.Water),
            ("terrain", SurfaceType.Dirt), ("dirt", SurfaceType.Dirt), ("sand", SurfaceType.Dirt), ("mud", SurfaceType.Dirt),
            ("tile", SurfaceType.Concrete), ("block", SurfaceType.Concrete), ("concrete", SurfaceType.Concrete),
            ("shed", SurfaceType.Concrete), ("floor", SurfaceType.Concrete)
        };

        public static SurfaceType Of(Collider collider)
        {
            if (collider == null) return SurfaceType.Dirt;

            int id = collider.GetHashCode();
            if (Cache.TryGetValue(id, out SurfaceType cached)) return cached;

            SurfaceType surface = Resolve(collider);
            Cache[id] = surface;
            return surface;
        }

        // what is under a point, looking a little up and down from it
        public static SurfaceType Below(Vector3 position, int mask)
        {
            if (Physics.Raycast(position + Vector3.up * 0.3f, Vector3.down, out RaycastHit hit, 1.0f, mask, QueryTriggerInteraction.Ignore))
            {
                return Of(hit.collider);
            }

            return SurfaceType.Dirt;
        }

        private static SurfaceType Resolve(Collider collider)
        {
            var explicitSurface = collider.GetComponentInParent<SurfaceMaterial>();
            if (explicitSurface != null) return explicitSurface.Surface;

            if (collider is TerrainCollider) return SurfaceType.Dirt;

            // the object and its parents, e.g. a "Roof" mesh inside a "Hangar"
            Transform t = collider.transform;
            for (int depth = 0; t != null && depth < 3; depth++, t = t.parent)
            {
                string name = t.name.ToLowerInvariant();
                foreach (var (keyword, surface) in Keywords)
                {
                    if (name.Contains(keyword)) return surface;
                }
            }

            return SurfaceType.Concrete;
        }
    }
}
