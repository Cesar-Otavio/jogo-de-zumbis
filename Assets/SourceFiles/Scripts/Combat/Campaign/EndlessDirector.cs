using UnityEngine;

namespace Combat
{
    /* Endless mode extras on top of the original wave survival: a side mission offered in some
     * breaks, a choice once in a while, and the best wave reached kept as a record. Bosses come
     * from the spawner every few waves.
     */
    public class EndlessDirector : MonoBehaviour
    {
        public MissionDefinition[] SideMissions;
        public MissionDefinition ChoiceA;
        public MissionDefinition ChoiceB;
        [Tooltip("A side mission is offered after every this many waves")]
        public int SideMissionEvery = 2;
        [Tooltip("The choice is offered after this wave")]
        public int ChoiceAfterWave = 3;

        private EnemySpawner _spawner;

        private void Start()
        {
            _spawner = FindAnyObjectByType<EnemySpawner>();
            if (_spawner != null) _spawner.WaveCleared += OnWaveCleared;
        }

        private void OnDestroy()
        {
            if (_spawner != null) _spawner.WaveCleared -= OnWaveCleared;
        }

        private static bool Active => RunState.Mode == GameMode.Endless && GameFlow.Instance != null && GameFlow.Instance.Current == GameFlow.State.Playing;

        private void OnWaveCleared(int wave)
        {
            if (!Active) return;
            Progression.RecordEndlessWave(wave);
            // co-op: side missions belong to the host's game; a client only records its progress
            if (!Net.HasAuthority) return;
            MissionManager missions = MissionManager.Instance;
            if (missions == null) return;

            if (wave == ChoiceAfterWave && ChoiceA != null && ChoiceB != null)
            {
                missions.OfferChoice(ChoiceA, ChoiceB);
                return;
            }

            if (SideMissionEvery <= 0 || wave % SideMissionEvery != 0 || SideMissions == null || SideMissions.Length == 0) return;

            // one at a time: a new one only when nothing else is going on
            foreach (MissionRun run in missions.Active) if (run.Optional) return;

            int start = Random.Range(0, SideMissions.Length);
            for (int i = 0; i < SideMissions.Length; i++)
            {
                MissionDefinition mission = SideMissions[(start + i) % SideMissions.Length];
                if (!missions.CanOffer(mission)) continue;
                missions.StartMission(mission, true);
                return;
            }
        }
    }
}
