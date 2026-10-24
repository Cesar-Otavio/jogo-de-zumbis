using System.Collections.Generic;
using UnityEngine;

namespace Combat
{
    // something in the way that the infected can batter: closed doors, barricades
    public interface IBreakable
    {
        Vector3 Position { get; }
        bool BlocksPath { get; }
        // heavy: tanks and bosses, who break what ordinary infected only dent
        void Hit(float damage, bool heavy, GameObject attacker);
    }

    public static class Breakables
    {
        public static readonly List<IBreakable> All = new List<IBreakable>();

        public static void Register(IBreakable breakable)
        {
            if (!All.Contains(breakable)) All.Add(breakable);
        }

        public static void Unregister(IBreakable breakable)
        {
            All.Remove(breakable);
        }

        // something blocking close to `from`, between it and `to`
        public static IBreakable Blocking(Vector3 from, Vector3 to, float radius)
        {
            Vector3 toTarget = to - from;
            toTarget.y = 0.0f;
            foreach (IBreakable breakable in All)
            {
                if (!breakable.BlocksPath) continue;
                Vector3 toIt = breakable.Position - from;
                toIt.y = 0.0f;
                if (toIt.magnitude > radius + 0.8f) continue;
                if (toTarget.sqrMagnitude > 0.01f && Vector3.Dot(toIt.normalized, toTarget.normalized) < 0.2f) continue;
                return breakable;
            }

            return null;
        }

        // the blocking door or barricade closest to `to` that lies on the way there from `from`
        public static IBreakable Gateway(Vector3 from, Vector3 to, float range)
        {
            IBreakable best = null;
            float bestCost = float.MaxValue;
            foreach (IBreakable breakable in All)
            {
                if (!breakable.BlocksPath) continue;
                float toTarget = Vector3.Distance(breakable.Position, to);
                if (toTarget > range) continue;
                float cost = Vector3.Distance(from, breakable.Position) + toTarget;
                if (cost < bestCost)
                {
                    bestCost = cost;
                    best = breakable;
                }
            }

            return best;
        }

        // everything blocking within a radius, e.g. a charging boss smashing through
        public static void HitAround(Vector3 center, float radius, float damage, GameObject attacker)
        {
            foreach (IBreakable breakable in All.ToArray())
            {
                if (breakable.BlocksPath && Vector3.Distance(breakable.Position, center) <= radius) breakable.Hit(damage, true, attacker);
            }
        }
    }
}
