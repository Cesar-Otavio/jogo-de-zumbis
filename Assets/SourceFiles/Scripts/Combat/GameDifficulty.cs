using UnityEngine;

namespace Combat
{
    public enum Difficulty
    {
        Easy,
        Normal,
        Hard,
        Nightmare
    }

    // Everything the difficulty changes, as multipliers on the base values
    public struct DifficultySettings
    {
        public string Name;
        public string Description;
        public int Lives;
        public float EnemyDamage;
        public float EnemyHealth;
        public float EnemyAimError;
        public float EnemyReaction;
        public float EnemyCount;
        public float PlayerRegen;
        public float ScoreMultiplier;
        // challenge beyond health and damage: how much is found lying around, how sharp the
        // enemies' senses are and how often special infected show up
        public float Supplies;
        public float Perception;
        public float SpecialChance;
    }

    public static class GameDifficulty
    {
        private const string PrefsKey = "Combat.Difficulty";

        public static Difficulty Current
        {
            get => (Difficulty)Mathf.Clamp(PlayerPrefs.GetInt(PrefsKey, (int)Difficulty.Normal), 0, 3);
            set
            {
                PlayerPrefs.SetInt(PrefsKey, (int)value);
                PlayerPrefs.Save();
            }
        }

        public static DifficultySettings Settings => Get(Current);

        public static DifficultySettings Get(Difficulty difficulty)
        {
            switch (difficulty)
            {
                case Difficulty.Easy:
                    return new DifficultySettings
                    {
                        Name = "FÁCIL", Description = "Inimigos erram mais e causam menos dano. 5 vidas.",
                        Lives = 5, EnemyDamage = 0.6f, EnemyHealth = 0.8f, EnemyAimError = 1.5f, EnemyReaction = 1.4f,
                        EnemyCount = 0.75f, PlayerRegen = 1.5f, ScoreMultiplier = 0.75f,
                        Supplies = 1.3f, Perception = 0.85f, SpecialChance = 0.7f
                    };
                case Difficulty.Hard:
                    return new DifficultySettings
                    {
                        Name = "DIFÍCIL", Description = "Inimigos precisos, rápidos e resistentes. 2 vidas.",
                        Lives = 2, EnemyDamage = 1.4f, EnemyHealth = 1.25f, EnemyAimError = 0.75f, EnemyReaction = 0.75f,
                        EnemyCount = 1.3f, PlayerRegen = 0.75f, ScoreMultiplier = 1.5f,
                        Supplies = 0.85f, Perception = 1.15f, SpecialChance = 1.25f
                    };
                case Difficulty.Nightmare:
                    return new DifficultySettings
                    {
                        Name = "PESADELO", Description = "Hordas enormes, quase sem regeneração. 1 vida.",
                        Lives = 1, EnemyDamage = 1.8f, EnemyHealth = 1.5f, EnemyAimError = 0.6f, EnemyReaction = 0.6f,
                        EnemyCount = 1.6f, PlayerRegen = 0.4f, ScoreMultiplier = 2.0f,
                        Supplies = 0.7f, Perception = 1.3f, SpecialChance = 1.5f
                    };
                default:
                    return new DifficultySettings
                    {
                        Name = "NORMAL", Description = "O equilíbrio pensado para o jogo. 3 vidas.",
                        Lives = 3, EnemyDamage = 1.0f, EnemyHealth = 1.0f, EnemyAimError = 1.0f, EnemyReaction = 1.0f,
                        EnemyCount = 1.0f, PlayerRegen = 1.0f, ScoreMultiplier = 1.0f,
                        Supplies = 1.0f, Perception = 1.0f, SpecialChance = 1.0f
                    };
            }
        }
    }
}
