using System;
using System.Collections.Generic;
using UnityEngine;

namespace Combat
{
    public enum GameMode
    {
        Endless,
        Campaign
    }

    /* Everything that only lasts for one run: military credits, mission items, scenario flags,
     * weapon upgrades and run statistics. Reset when a run starts. Permanent progress (XP,
     * unlocks, achievements) lives in Progression; the score and records stay in GameFlow.
     */
    public static class RunState
    {
        public static GameMode Mode { get; set; } = GameMode.Endless;

        // ---------------------------------------------------------------- credits

        public static int Credits { get; private set; }
        public static int CreditsEarned { get; private set; }
        public static event Action<int, string> CreditsChanged;

        public static void AddCredits(int amount, string reason)
        {
            if (amount <= 0) return;
            Credits += amount;
            CreditsEarned += amount;
            CreditsChanged?.Invoke(amount, reason);
        }

        // the only way to spend: checks the balance and takes it in one step
        public static bool TrySpend(int amount)
        {
            if (amount < 0 || Credits < amount) return false;
            Credits -= amount;
            CreditsChanged?.Invoke(-amount, null);
            return true;
        }

        // ---------------------------------------------------------------- mission items and flags

        private static readonly Dictionary<string, int> Items = new Dictionary<string, int>();
        private static readonly HashSet<string> Flags = new HashSet<string>();
        public static event Action<string> ItemsChanged;
        public static event Action<string> FlagSet;

        public static int ItemCount(string id)
        {
            return id != null && Items.TryGetValue(id, out int count) ? count : 0;
        }

        public static void AddItem(string id, int count = 1)
        {
            if (string.IsNullOrEmpty(id)) return;
            Items[id] = ItemCount(id) + count;
            ItemsChanged?.Invoke(id);
        }

        public static bool TakeItem(string id, int count = 1)
        {
            if (ItemCount(id) < count) return false;
            Items[id] -= count;
            ItemsChanged?.Invoke(id);
            return true;
        }

        public static bool HasFlag(string flag) => Flags.Contains(flag);

        public static void SetFlag(string flag)
        {
            if (string.IsNullOrEmpty(flag) || !Flags.Add(flag)) return;
            FlagSet?.Invoke(flag);
        }

        // ---------------------------------------------------------------- statistics

        public static int Kills;
        public static int SpecialKills;
        public static int WavesSurvived;
        public static int MissionsCompleted;
        // nothing but the starting pistol, no upgrades and no quick items used this wave
        public static bool BasicOnly = true;
        public static float StartedAt;

        public static void Reset(GameMode mode)
        {
            Mode = mode;
            Credits = 0;
            CreditsEarned = 0;
            Items.Clear();
            Flags.Clear();
            Kills = 0;
            SpecialKills = 0;
            WavesSurvived = 0;
            MissionsCompleted = 0;
            StartedAt = Time.time;
            WeaponUpgrades.Reset();
            CreditsChanged?.Invoke(0, null);
        }
    }
}
