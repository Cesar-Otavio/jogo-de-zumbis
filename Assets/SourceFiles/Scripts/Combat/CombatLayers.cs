using System;
using UnityEngine;

namespace Combat
{
    public static class CombatLayers
    {
        public const int Player = 8;
        public const int Enemy = 9;
        public const int Hitbox = 10;
        public const int Debris = 11;

        // what bullets and the aim ray can hit; the shooter's own layer is removed separately
        public static int Shootable => ~((1 << Debris) | (1 << 2) | (1 << 5));

        // what blocks line of sight
        public static int Sight => ~((1 << Debris) | (1 << 2) | (1 << 5) | (1 << Enemy) | (1 << Hitbox));
    }

    public static class CombatEvents
    {
        // position and team of a gunshot, so nearby enemies can hear it
        public static event Action<Vector3, int> Gunshot;

        public static void ReportGunshot(Vector3 position, int team, bool suppressed = false, GameObject shooter = null)
        {
            Gunshot?.Invoke(position, team);
            NoiseCategory category = suppressed ? NoiseCategory.SilencedShot : NoiseCategory.Gunshot;
            NoiseSystem.Emit(position, NoiseSystem.DefaultRadius(category), category, team, shooter);
        }
    }
}
