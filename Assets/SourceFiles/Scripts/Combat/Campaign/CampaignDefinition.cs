using System;
using UnityEngine;

namespace Combat
{
    /* The campaign as data: ten chapters, each a story beat with its missions, the wave the
     * missions ask for, an optional boss, side missions, a possible choice and the break before
     * the next one.
     */
    [CreateAssetMenu(menuName = "Combat/Campaign", fileName = "Campaign")]
    public class CampaignDefinition : ScriptableObject
    {
        [Serializable]
        public class Chapter
        {
            public string Title = "Onda";
            [TextArea] public string Subtitle;
            [Tooltip("Radio line at the start")]
            public string Radio;
            public MissionEffect[] OnStart;
            [Tooltip("Story missions, played in order; one of them launches the wave")]
            public MissionDefinition[] Missions;
            [Tooltip("Side missions offered during the chapter")]
            public MissionDefinition[] SideMissions;

            [Header("Wave")]
            public int Zombies = 6;
            public int Soldiers;
            public float EnemyHealth = 1.0f;
            [Tooltip("Comes with the wave")]
            public BossDefinition Boss;

            [Header("After the chapter")]
            [Tooltip("Seconds of break (shop, healing, preparing) before the next chapter")]
            public float Intermission = 60.0f;
            [Tooltip("Two incompatible missions offered in the break")]
            public MissionDefinition ChoiceA;
            public MissionDefinition ChoiceB;
        }

        public string Title = "Protocolo Aurora";
        [TextArea] public string Intro;
        public Chapter[] Chapters;
        [TextArea] public string Ending;
    }
}
