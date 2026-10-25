using System;
using UnityEngine;

namespace Combat
{
    /* How a co-op wave grows with the team. Each extra survivor adds a little of each, not one big
     * multiplier on everything: more infected (but not as many more as players), a bit more of the
     * special ones, more of them at once, a boss that lasts longer, and a longer break to share the shop.
     * The difficulty's own multipliers still apply on top. Edited on the NetworkManager (NetSessionPrefabs).
     */
    [Serializable]
    public class CoopBalance
    {
        [Tooltip("Infected in a wave: +this fraction per extra player (1 player = the single-player count)")]
        public float CountPerExtraPlayer = 0.55f;
        [Tooltip("Chance of a special infected: + this per extra player")]
        public float SpecialChancePerExtraPlayer = 0.04f;
        [Tooltip("How many may be up at once: + this fraction per extra player")]
        public float MaxAlivePerExtraPlayer = 0.35f;
        [Tooltip("Boss health: + this fraction per extra player")]
        public float BossHealthPerExtraPlayer = 0.6f;
        [Tooltip("Seconds added to the break between waves per extra player")]
        public float BreakPerExtraPlayer = 5.0f;

        // the host sets the spawner up for the team that starts this wave
        public void Apply(EnemySpawner spawner, int players)
        {
            int extra = Mathf.Max(0, players - 1);
            spawner.CountScale = 1.0f + CountPerExtraPlayer * extra;
            spawner.SpecialChanceBonus = SpecialChancePerExtraPlayer * extra;
            spawner.MaxAliveScale = 1.0f + MaxAlivePerExtraPlayer * extra;
            spawner.BossHealthScale = 1.0f + BossHealthPerExtraPlayer * extra;
            spawner.IntermissionBonus = BreakPerExtraPlayer * extra;
        }

        public static void Reset(EnemySpawner spawner)
        {
            spawner.CountScale = 1.0f;
            spawner.SpecialChanceBonus = 0.0f;
            spawner.MaxAliveScale = 1.0f;
            spawner.BossHealthScale = 1.0f;
            spawner.IntermissionBonus = 0.0f;
        }
    }
}
