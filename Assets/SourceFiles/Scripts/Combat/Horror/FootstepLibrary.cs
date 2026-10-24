using System;
using UnityEngine;

namespace Combat
{
    // footstep clips and how loud each surface is, shared by the player and the enemies
    [CreateAssetMenu(menuName = "Combat/Footstep Library", fileName = "FootstepLibrary")]
    public class FootstepLibrary : ScriptableObject
    {
        [Serializable]
        public class SurfaceSounds
        {
            public SurfaceType Surface;
            public AudioClip[] Steps;
            public AudioClip[] Lands;
            [Tooltip("Multiplies how far the AI hears steps on this surface")]
            public float Loudness = 1.0f;
            public float Volume = 1.0f;
        }

        public SurfaceSounds[] Surfaces;
        [Tooltip("Played under the step when the ground is wet from the rain")]
        public AudioClip[] WetLayer;

        public SurfaceSounds Get(SurfaceType surface)
        {
            if (Surfaces == null) return null;
            foreach (SurfaceSounds sounds in Surfaces)
            {
                if (sounds.Surface == surface) return sounds;
            }

            return Surfaces.Length > 0 ? Surfaces[0] : null;
        }
    }
}
