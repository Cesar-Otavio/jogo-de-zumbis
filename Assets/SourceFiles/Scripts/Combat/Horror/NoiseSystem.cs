using System;
using UnityEngine;

namespace Combat
{
    public enum NoiseCategory
    {
        Footstep,
        Run,
        Land,
        Gunshot,
        SilencedShot,
        Explosion,
        Thrown,
        Door,
        Machine,
        Alarm,
        Scream,
        Melee,
        Ambient
    }

    public struct NoiseEvent
    {
        public Vector3 Position;
        // how far away a listener with normal hearing notices it
        public float Radius;
        public NoiseCategory Category;
        // team of whoever made it, or -1 for the environment (doors, alarms, machines)
        public int Team;
        public GameObject Source;
        public float Time;
    }

    /* One place for every sound the AI can react to. Emitters call Emit once per noise;
     * listeners (EnemyPerception) subscribe and only check the distance when something happens,
     * so nothing is polled per frame.
     */
    public static class NoiseSystem
    {
        public static event Action<NoiseEvent> Emitted;

        // the last noise the player made, e.g. for the stealth meter in the HUD
        public static NoiseEvent LastPlayerNoise { get; private set; }

        public static void Emit(Vector3 position, float radius, NoiseCategory category, int team, GameObject source = null)
        {
            if (radius <= 0.0f) return;

            var noise = new NoiseEvent
            {
                Position = position,
                Radius = radius,
                Category = category,
                Team = team,
                Source = source,
                Time = UnityEngine.Time.time
            };

            if (team == Factions.Player) LastPlayerNoise = noise;
            Emitted?.Invoke(noise);
        }

        // typical reach of each kind of noise, in meters
        public static float DefaultRadius(NoiseCategory category)
        {
            switch (category)
            {
                case NoiseCategory.Footstep: return 6.0f;
                case NoiseCategory.Run: return 14.0f;
                case NoiseCategory.Land: return 10.0f;
                case NoiseCategory.Gunshot: return 45.0f;
                case NoiseCategory.SilencedShot: return 9.0f;
                case NoiseCategory.Explosion: return 70.0f;
                case NoiseCategory.Thrown: return 12.0f;
                case NoiseCategory.Door: return 16.0f;
                case NoiseCategory.Machine: return 22.0f;
                case NoiseCategory.Alarm: return 45.0f;
                case NoiseCategory.Scream: return 55.0f;
                case NoiseCategory.Melee: return 8.0f;
                default: return 15.0f;
            }
        }
    }

    // who fights whom: every faction is hostile to every other one
    public static class Factions
    {
        public const int Environment = -1;
        public const int Player = 0;
        public const int Soldiers = 1;
        public const int Infected = 2;

        public static bool AreHostile(int a, int b)
        {
            return a != b && a >= 0 && b >= 0;
        }

        public static string Name(int faction)
        {
            switch (faction)
            {
                case Player: return "Sobrevivente";
                case Soldiers: return "Soldados";
                case Infected: return "Infectados";
                default: return "Ambiente";
            }
        }
    }
}
