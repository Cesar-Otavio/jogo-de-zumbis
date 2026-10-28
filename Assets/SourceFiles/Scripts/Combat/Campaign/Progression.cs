using System;
using System.Collections.Generic;
using UnityEngine;

namespace Combat
{
    /* Permanent progress between runs: experience and level, achievements, documents found,
     * totals and what they unlock (outfits, weapons in the shop). Saved as JSON in PlayerPrefs
     * next to the existing records and outfit, which stay where they are.
     */
    public static class Progression
    {
        [Serializable]
        private class SaveData
        {
            public int Experience;
            public int TotalKills;
            public int TotalWaves;
            public int MissionsCompleted;
            public int BestEndlessWave;
            public bool CampaignCompleted;
            public int CampaignChapter;
            public List<string> Achievements = new List<string>();
            public List<string> Documents = new List<string>();
        }

        public struct Achievement
        {
            public string Id;
            public string Title;
            public string Description;
        }

        public static readonly Achievement[] AllAchievements =
        {
            new Achievement { Id = "survive_5", Title = "Ainda de pé", Description = "Sobreviva a cinco ondas numa partida." },
            new Achievement { Id = "kill_100", Title = "Cem a menos", Description = "Elimine cem zumbis (no total)." },
            new Achievement { Id = "mission_no_damage", Title = "Intocável", Description = "Conclua uma missão sem receber dano." },
            new Achievement { Id = "boss_e07", Title = "Fim do Experimento", Description = "Derrote o Experimento 07." },
            new Achievement { Id = "campaign", Title = "Protocolo Aurora", Description = "Termine a campanha." },
            new Achievement { Id = "basic_wave", Title = "Só o básico", Description = "Sobreviva a uma onda usando apenas a pistola inicial, sem melhorias." },
            new Achievement { Id = "boss_aurora", Title = "Amanhecer", Description = "Derrote Aurora." },
            new Achievement { Id = "archivist", Title = "Arquivista", Description = "Encontre todos os documentos." }
        };

        public const int MaxLevel = 30;
        private const string Key = "Combat.Progression";

        public static event Action<int, string> ExperienceGained;
        public static event Action<int> LevelUp;
        public static event Action<Achievement> AchievementUnlocked;

        private static SaveData _data;

        private static SaveData Data
        {
            get
            {
                if (_data != null) return _data;
                string json = PlayerPrefs.GetString(Key, "");
                _data = string.IsNullOrEmpty(json) ? new SaveData() : JsonUtility.FromJson<SaveData>(json) ?? new SaveData();
                return _data;
            }
        }

        private static void Save()
        {
            PlayerPrefs.SetString(Key, JsonUtility.ToJson(Data));
            PlayerPrefs.Save();
        }

        // ---------------------------------------------------------------- experience

        public static int Experience => Data.Experience;
        public static int TotalKills => Data.TotalKills;
        public static int MissionsCompleted => Data.MissionsCompleted;
        public static int BestEndlessWave => Data.BestEndlessWave;
        public static bool CampaignCompleted => Data.CampaignCompleted;
        public static int DocumentsFound => Data.Documents.Count;

        // each level asks a bit more than the last
        public static int ExperienceFor(int level) => level <= 1 ? 0 : 250 * (level - 1) + 50 * (level - 1) * (level - 2) / 2;

        public static int Level
        {
            get
            {
                int level = 1;
                while (level < MaxLevel && Data.Experience >= ExperienceFor(level + 1)) level++;
                return level;
            }
        }

        public static float LevelProgress
        {
            get
            {
                int level = Level;
                if (level >= MaxLevel) return 1.0f;
                int from = ExperienceFor(level);
                return Mathf.Clamp01((Data.Experience - from) / (float)(ExperienceFor(level + 1) - from));
            }
        }

        // save: false for the small, frequent gains (kills), saved later in one go
        public static void AddExperience(int amount, string reason, bool save = true)
        {
            if (amount <= 0) return;
            int before = Level;
            Data.Experience += amount;
            if (save) Save();
            ExperienceGained?.Invoke(amount, reason);
            int after = Level;
            if (after > before) LevelUp?.Invoke(after);
        }

        // ---------------------------------------------------------------- records

        public static void RecordKill(bool zombie)
        {
            Data.TotalKills++;
            if (zombie && Data.TotalKills >= 100) Unlock("kill_100");
            // saved in batches: every kill would hammer the disk
            if (Data.TotalKills % 10 == 0) Save();
        }

        public static void Flush()
        {
            if (_data != null) Save();
        }

        public static void RecordWave(int waveInRun)
        {
            Data.TotalWaves++;
            if (waveInRun >= 5) Unlock("survive_5");
            Save();
        }

        public static void RecordEndlessWave(int wave)
        {
            if (wave <= Data.BestEndlessWave) return;
            Data.BestEndlessWave = wave;
            Save();
        }

        public static void RecordMission()
        {
            Data.MissionsCompleted++;
            Save();
        }

        public static int CampaignChapter
        {
            get => Data.CampaignChapter;
            set
            {
                Data.CampaignChapter = Mathf.Clamp(value, 0, 9);
                Save();
            }
        }

        // the next campaign starts from the first wave again (achievements and level stay)
        public static void RestartCampaign()
        {
            Data.CampaignChapter = 0;
            Save();
            // a new campaign starts with the base unexplored
            MapDiscovery.Forget();
        }

        public static void CompleteCampaign()
        {
            Data.CampaignCompleted = true;
            // the next campaign is a fresh run from the first wave
            Data.CampaignChapter = 0;
            Unlock("campaign");
            Save();
        }

        public static void FoundDocument(string id)
        {
            if (Data.Documents.Contains(id)) return;
            Data.Documents.Add(id);
            if (Data.Documents.Count >= Story.Documents.Count) Unlock("archivist");
            Save();
        }

        public static bool HasDocument(string id) => Data.Documents.Contains(id);

        // ---------------------------------------------------------------- achievements

        public static bool IsUnlocked(string id) => Data.Achievements.Contains(id);

        public static void Unlock(string id)
        {
            if (Data.Achievements.Contains(id)) return;
            foreach (Achievement achievement in AllAchievements)
            {
                if (achievement.Id != id) continue;
                Data.Achievements.Add(id);
                Save();
                AchievementUnlocked?.Invoke(achievement);
                AddExperience(150, "conquista");
                return;
            }
        }

        // ---------------------------------------------------------------- unlocks

        // wardrobe: some looks are earned (category index, option label prefix, level, or the campaign)
        public static bool OutfitUnlocked(string category, string option, out string requirement)
        {
            requirement = null;
            int level = 1;
            bool campaign = false;
            if (category == "COLETE" && (option == "Tático 2" || option == "Tático 3")) level = 3;
            else if (category == "MOCHILA" && option.StartsWith("Cargueira")) level = 5;
            else if (category == "LUVAS" && option == "Luvas 2") level = 2;
            else if (category == "BONÉ" && option == "Boné 4") campaign = true;
            else if (category == "CAMISA" && option == "Estilo 3") level = 4;

            if (campaign && !Data.CampaignCompleted)
            {
                requirement = "termine a campanha";
                return false;
            }

            if (Level < level)
            {
                requirement = "nível " + level;
                return false;
            }

            return true;
        }

        // for testing and the "reset progress" button
        public static void ResetAll()
        {
            _data = new SaveData();
            Save();
        }
    }
}
