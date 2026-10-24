using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Combat
{
    public enum MissionState
    {
        Active,
        Completed,
        Failed,
        Abandoned
    }

    // one mission being played: which objective is current and how far each one is
    public class MissionRun
    {
        public MissionDefinition Definition;
        public MissionState State;
        // the same mission can be the story in the campaign and a side job in endless mode
        public bool Optional;
        public int Current;
        public float[] Progress;
        public bool[] Done;
        public bool[] Skipped;
        public float StartedAt;
        public float ObjectiveStartedAt;
        public float AwayTime;
        public float DamageAtStart;
        public int SequenceStep;
        public bool WaveLaunched;

        public ObjectiveDefinition CurrentObjective =>
            Current >= 0 && Current < Definition.Objectives.Length ? Definition.Objectives[Current] : null;

        public float TimeLeft => Definition.TimeLimit > 0.0f ? Mathf.Max(0.0f, Definition.TimeLimit - (Time.time - StartedAt)) : -1.0f;
    }

    /* Runs every mission: starts them, keeps objective progress up to date from game events and
     * from polling the world, completes or fails them, pays the rewards exactly once and applies
     * their effects on the base. Optional missions can be abandoned; choices start one mission of
     * a pair and drop the other. Objectives whose targets are missing, or that drag on far too
     * long, are waived so the run never gets stuck.
     */
    public class MissionManager : MonoBehaviour
    {
        public static MissionManager Instance { get; private set; }

        [Tooltip("Required objectives tied to a place are waived after this many seconds")]
        public float DefaultSkipAfter = 300.0f;
        [Tooltip("Seconds the player may leave a zone being defended before the mission fails")]
        public float DefendGrace = 20.0f;
        [Tooltip("Weapons missions can hand out by name")]
        public WeaponData[] Weapons;

        public readonly List<MissionRun> Active = new List<MissionRun>();
        public (MissionDefinition a, MissionDefinition b) PendingChoice { get; private set; }

        public event Action<MissionRun> MissionStarted;
        public event Action<MissionRun> MissionCompleted;
        public event Action<MissionRun> MissionFailed;
        public event Action<MissionRun, int> ObjectiveCompleted;
        // a mission objective wants a wave of enemies now
        public event Action<MissionRun> WaveRequested;

        private readonly Dictionary<string, MissionState> _history = new Dictionary<string, MissionState>();
        private readonly HashSet<string> _rewarded = new HashSet<string>();
        private PlayerCombat _player;
        private EnemySpawner _spawner;

        private void Awake()
        {
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            Health.AnyDied -= OnAnyDied;
            MissionDevice.Activated -= OnDeviceActivated;
            if (_spawner != null) _spawner.WaveCleared -= OnWaveCleared;
        }

        private void Start()
        {
            _player = FindAnyObjectByType<PlayerCombat>();
            _spawner = FindAnyObjectByType<EnemySpawner>();
            Health.AnyDied += OnAnyDied;
            MissionDevice.Activated += OnDeviceActivated;
            if (_spawner != null) _spawner.WaveCleared += OnWaveCleared;
        }

        // ---------------------------------------------------------------- starting and stopping

        public bool IsActive(MissionDefinition definition) => Find(definition) != null;

        public MissionRun Find(MissionDefinition definition)
        {
            foreach (MissionRun run in Active) if (run.Definition == definition) return run;
            return null;
        }

        public bool WasCompleted(MissionDefinition definition)
        {
            return definition != null && _history.TryGetValue(definition.Id, out MissionState state) && state == MissionState.Completed;
        }

        // only missions whose places exist and whose prerequisites are met are ever offered
        public bool CanOffer(MissionDefinition definition)
        {
            if (definition == null || IsActive(definition)) return false;
            if (_history.TryGetValue(definition.Id, out MissionState state))
            {
                if (state == MissionState.Completed) return false;
                if (state == MissionState.Failed && !definition.Retryable) return false;
            }

            if (!string.IsNullOrEmpty(definition.RequiredFlag) && !RunState.HasFlag(definition.RequiredFlag)) return false;
            if (definition.RequiredIds != null)
            {
                foreach (string id in definition.RequiredIds)
                {
                    if (!MissionAnchor.Exists(id)) return false;
                }
            }

            return true;
        }

        public MissionRun StartMission(MissionDefinition definition)
        {
            return StartMission(definition, definition != null && definition.Optional);
        }

        public MissionRun StartMission(MissionDefinition definition, bool optional)
        {
            if (!CanOffer(definition) || definition.Objectives == null || definition.Objectives.Length == 0) return null;

            // a choice: the other missions of the group are off the table
            if (!string.IsNullOrEmpty(definition.ChoiceGroup))
            {
                RunState.SetFlag("choice_" + definition.ChoiceGroup + "_" + definition.Id);
                for (int i = Active.Count - 1; i >= 0; i--)
                {
                    if (Active[i].Definition.ChoiceGroup == definition.ChoiceGroup) Abandon(Active[i], true);
                }
            }

            int count = definition.Objectives.Length;
            var run = new MissionRun
            {
                Definition = definition,
                State = MissionState.Active,
                Optional = optional,
                Current = -1,
                Progress = new float[count],
                Done = new bool[count],
                Skipped = new bool[count],
                StartedAt = Time.time,
                DamageAtStart = _player != null ? _player.Health.DamageTaken : 0.0f
            };

            Active.Add(run);
            _history[definition.Id] = MissionState.Active;
            Apply(definition.OnStart);
            MissionStarted?.Invoke(run);
            AdvanceObjective(run);
            return run;
        }

        public void OfferChoice(MissionDefinition a, MissionDefinition b)
        {
            bool canA = CanOffer(a);
            bool canB = CanOffer(b);
            if (canA && canB) PendingChoice = (a, b);
            // never offer an option that can't be done: with only one left, it just starts
            else if (canA) StartMission(a);
            else if (canB) StartMission(b);
        }

        public void Choose(int option)
        {
            var (a, b) = PendingChoice;
            if (a == null) return;
            PendingChoice = (null, null);
            StartMission(option == 0 ? a : b);
        }

        public void Abandon(MissionRun run, bool silent = false)
        {
            if (run == null || run.State != MissionState.Active) return;
            if (!run.Optional && !silent) return;
            run.State = MissionState.Abandoned;
            Active.Remove(run);
            _history[run.Definition.Id] = MissionState.Abandoned;
            ResetDevices(run);
            if (!silent && _player != null) _player.ShowNotice($"Missão abandonada: {run.Definition.Title}");
        }

        // drop everything, e.g. a new run
        public void Clear()
        {
            foreach (MissionRun run in Active) ResetDevices(run);
            Active.Clear();
            _history.Clear();
            _rewarded.Clear();
            PendingChoice = (null, null);
        }

        // is some mission still after this item or device (pickups and devices hide otherwise)
        public bool IsNeeded(string id)
        {
            if (string.IsNullOrEmpty(id)) return false;
            foreach (MissionRun run in Active)
            {
                ObjectiveDefinition[] objectives = run.Definition.Objectives;
                for (int i = 0; i < objectives.Length; i++)
                {
                    if (run.Done[i]) continue;
                    ObjectiveDefinition o = objectives[i];
                    if (o.Target == id) return true;
                    if ((o.Type == ObjectiveType.Sequence || (o.Type == ObjectiveType.Interact && o.Count > 1)) && id.StartsWith(o.Target + "_")) return true;
                    for (int n = 1; n <= o.Count && o.Count > 1; n++)
                    {
                        MissionDevice part = MissionDevice.Find(o.Target + "_" + n);
                        if (part != null && part.RequiredItem == id) return true;
                    }
                    // the item a device of this mission will ask for
                    MissionDevice device = MissionDevice.Find(o.Target);
                    if (device != null && device.RequiredItem == id) return true;
                }
            }

            return false;
        }

        // ---------------------------------------------------------------- objectives

        private void AdvanceObjective(MissionRun run)
        {
            ObjectiveDefinition[] objectives = run.Definition.Objectives;
            int next = run.Current + 1;
            while (next < objectives.Length && (objectives[next].Optional || run.Done[next])) next++;
            run.Current = next;
            run.ObjectiveStartedAt = Time.time;
            run.AwayTime = 0.0f;
            run.SequenceStep = 0;
            run.WaveLaunched = false;

            if (next >= objectives.Length)
            {
                CompleteMission(run);
                return;
            }

            BeginObjective(run, objectives[next]);
            // optional objectives run alongside from the start
            if (next == FirstRequired(run))
            {
                for (int i = 0; i < objectives.Length; i++)
                {
                    if (objectives[i].Optional) BeginObjective(run, objectives[i]);
                }
            }
        }

        private static int FirstRequired(MissionRun run)
        {
            for (int i = 0; i < run.Definition.Objectives.Length; i++)
            {
                if (!run.Definition.Objectives[i].Optional) return i;
            }

            return 0;
        }

        private void BeginObjective(MissionRun run, ObjectiveDefinition objective)
        {
            if (!string.IsNullOrEmpty(objective.Radio)) Story.PlayRadio(objective.Radio);

            if (objective.PressureEnemies > 0 && _spawner != null)
            {
                Vector3 where = TryLocate(objective, out Vector3 at) ? at : (_player != null ? _player.transform.position : Vector3.zero);
                _spawner.SpawnPressure(objective.PressureEnemies, where);
            }

            if (objective.Type == ObjectiveType.Sequence)
            {
                for (int i = 1; i <= objective.Count; i++) MissionDevice.Find(objective.Target + "_" + i)?.ResetDevice();
            }
            else if (objective.Type == ObjectiveType.Interact)
            {
                MissionDevice device = MissionDevice.Find(objective.Target);
                if (device != null && device.Reusable) device.ResetDevice();
            }
        }

        private void Update()
        {
            UpdateChoiceInput();

            for (int r = Active.Count - 1; r >= 0; r--)
            {
                MissionRun run = Active[r];
                if (run.State != MissionState.Active) continue;

                if (run.Definition.TimeLimit > 0.0f && Time.time - run.StartedAt > run.Definition.TimeLimit)
                {
                    FailMission(run, "tempo esgotado");
                    continue;
                }

                ObjectiveDefinition[] objectives = run.Definition.Objectives;
                for (int i = 0; i < objectives.Length; i++)
                {
                    if (run.Done[i] || run.State != MissionState.Active) continue;
                    if (!objectives[i].Optional && i != run.Current) continue;
                    Evaluate(run, i);
                }
            }
        }

        private void Evaluate(MissionRun run, int index)
        {
            ObjectiveDefinition o = run.Definition.Objectives[index];
            float dt = Time.deltaTime;
            bool playerAlive = _player != null && !_player.Health.IsDead;
            bool located = TryLocate(o, out Vector3 target);

            switch (o.Type)
            {
                case ObjectiveType.Collect:
                case ObjectiveType.Retrieve:
                    run.Progress[index] = Mathf.Min(o.Count, RunState.ItemCount(o.Target));
                    if (RunState.ItemCount(o.Target) >= o.Count) CompleteObjective(run, index, false);
                    break;

                case ObjectiveType.Interact:
                    if (o.Count > 1)
                    {
                        // several devices in any order: Target_1 .. Target_Count
                        int on = 0;
                        for (int i = 1; i <= o.Count; i++) if (MissionDevice.Find(o.Target + "_" + i)?.IsActivated == true) on++;
                        run.Progress[index] = on;
                        if (on >= o.Count) CompleteObjective(run, index, false);
                        break;
                    }

                    MissionDevice device = MissionDevice.Find(o.Target);
                    if (device != null && device.IsActivated) CompleteObjective(run, index, false);
                    break;

                case ObjectiveType.Fortify:
                    int built = 0;
                    foreach (IBreakable breakable in Breakables.All)
                    {
                        if (breakable is BarricadePoint point && point.Built && point.Id.StartsWith(o.Target)) built++;
                    }

                    run.Progress[index] = built;
                    if (built >= o.Count) CompleteObjective(run, index, false);
                    break;

                case ObjectiveType.Armed:
                    if (_player != null && (_player.Current != null || _player.Other != null)) CompleteObjective(run, index, false);
                    break;

                case ObjectiveType.Defend:
                    if (!playerAlive) break;
                    bool inside = located && Vector3.Distance(_player.transform.position, target) <= o.Radius;
                    if (inside)
                    {
                        run.Progress[index] += dt;
                        run.AwayTime = Mathf.Max(0.0f, run.AwayTime - dt * 0.5f);
                    }
                    else
                    {
                        run.AwayTime += dt;
                        if (run.AwayTime > DefendGrace)
                        {
                            FailMission(run, "posição abandonada");
                            return;
                        }
                    }

                    if (run.Progress[index] >= o.Duration) CompleteObjective(run, index, false);
                    break;

                case ObjectiveType.Survive:
                    if (playerAlive) run.Progress[index] += dt;
                    if (run.Progress[index] >= o.Duration) CompleteObjective(run, index, false);
                    break;

                case ObjectiveType.RestorePower:
                    if (located && PowerGridManager.Instance != null && PowerGridManager.Instance.StateAt(target) == PowerState.Normal) CompleteObjective(run, index, false);
                    if (PowerGridManager.Instance == null) CompleteObjective(run, index, true);
                    break;

                case ObjectiveType.Door:
                    InteractiveDoor door = located ? DoorNear(target) : null;
                    if (door != null && (door.IsOpen || door.IsBroken) == (o.Count != 0)) CompleteObjective(run, index, false);
                    break;

                case ObjectiveType.Investigate:
                case ObjectiveType.Extract:
                    if (playerAlive && located && Vector3.Distance(_player.transform.position, target) <= o.Radius) CompleteObjective(run, index, false);
                    break;

                case ObjectiveType.SurviveWave:
                    if (!run.WaveLaunched)
                    {
                        run.WaveLaunched = true;
                        WaveRequested?.Invoke(run);
                    }

                    break;
            }

            if (run.State != MissionState.Active || run.Done[index] || o.Optional) return;

            // safety net: a target that doesn't exist, or a place-bound objective that drags on forever
            bool needsPlace = o.Type != ObjectiveType.Kill && o.Type != ObjectiveType.Survive && o.Type != ObjectiveType.SurviveWave &&
                              o.Type != ObjectiveType.Collect && o.Type != ObjectiveType.Retrieve && o.Type != ObjectiveType.Armed &&
                              o.Type != ObjectiveType.Fortify;
            if (needsPlace && !located)
            {
                Debug.LogWarning($"Missão {run.Definition.Id}: alvo '{o.Target}' não existe na cena, objetivo ignorado");
                CompleteObjective(run, index, true);
                return;
            }

            float skipAfter = o.SkipAfter > 0.0f ? o.SkipAfter : DefaultSkipAfter;
            if (needsPlace && o.Type != ObjectiveType.Defend && Time.time - run.ObjectiveStartedAt > skipAfter) CompleteObjective(run, index, true);
        }

        private bool TryLocate(ObjectiveDefinition o, out Vector3 position)
        {
            if (o.Type == ObjectiveType.Sequence)
            {
                MissionRun run = null;
                foreach (MissionRun r in Active) if (Array.IndexOf(r.Definition.Objectives, o) >= 0) run = r;
                int step = run != null ? run.SequenceStep + 1 : 1;
                MissionDevice device = MissionDevice.Find(o.Target + "_" + step);
                position = device != null ? device.transform.position : Vector3.zero;
                return device != null;
            }

            if (o.Type == ObjectiveType.Kill || o.Type == ObjectiveType.Survive || o.Type == ObjectiveType.SurviveWave || o.Type == ObjectiveType.Armed)
            {
                position = Vector3.zero;
                return false;
            }

            if (o.Type == ObjectiveType.Interact && o.Count > 1)
            {
                // the nearest device of the set that still needs doing
                position = Vector3.zero;
                float best = float.MaxValue;
                for (int i = 1; i <= o.Count; i++)
                {
                    MissionDevice device = MissionDevice.Find(o.Target + "_" + i);
                    if (device == null || device.IsActivated) continue;
                    float d = _player != null ? Vector3.Distance(device.transform.position, _player.transform.position) : 0.0f;
                    if (d < best)
                    {
                        best = d;
                        position = device.transform.position;
                    }
                }

                return best < float.MaxValue;
            }

            if (o.Type == ObjectiveType.Fortify)
            {
                position = Vector3.zero;
                float best = float.MaxValue;
                foreach (IBreakable breakable in Breakables.All)
                {
                    if (!(breakable is BarricadePoint point) || point.Built || !point.Id.StartsWith(o.Target)) continue;
                    float d = _player != null ? Vector3.Distance(point.Position, _player.transform.position) : 0.0f;
                    if (d < best)
                    {
                        best = d;
                        position = point.Position;
                    }
                }

                return best < float.MaxValue;
            }

            return MissionAnchor.TryLocate(o.Target, out position);
        }

        private static InteractiveDoor DoorNear(Vector3 point)
        {
            InteractiveDoor best = null;
            float bestDistance = 5.0f;
            foreach (InteractiveDoor door in InteractiveDoor.All)
            {
                float distance = Vector3.Distance(door.transform.position, point);
                if (distance < bestDistance)
                {
                    best = door;
                    bestDistance = distance;
                }
            }

            return best;
        }

        private void CompleteObjective(MissionRun run, int index, bool skipped)
        {
            if (run.Done[index]) return;
            ObjectiveDefinition o = run.Definition.Objectives[index];
            run.Done[index] = true;
            run.Skipped[index] = skipped;
            if (o.Type == ObjectiveType.Survive || o.Type == ObjectiveType.Defend) run.Progress[index] = o.Duration;
            else run.Progress[index] = o.Count;

            if (_player != null)
            {
                _player.ShowNotice(skipped ? "<color=#AAAAAA>Objetivo indisponível — seguindo em frente</color>" : $"<color=#9BE39B>OK</color>  {o.Text}");
            }

            if (o.Optional && !skipped && o.BonusCredits > 0) RunState.AddCredits(o.BonusCredits, "objetivo opcional");
            ObjectiveCompleted?.Invoke(run, index);
            if (!o.Optional && index == run.Current) AdvanceObjective(run);
        }

        // ---------------------------------------------------------------- events

        private void OnDeviceActivated(MissionDevice device)
        {
            foreach (MissionRun run in Active.ToArray())
            {
                ObjectiveDefinition o = run.CurrentObjective;
                if (o == null || o.Type != ObjectiveType.Sequence || !device.Id.StartsWith(o.Target + "_")) continue;

                int expected = run.SequenceStep + 1;
                if (device.Id == o.Target + "_" + expected)
                {
                    run.SequenceStep = expected;
                    run.Progress[run.Current] = expected;
                    if (expected >= o.Count) CompleteObjective(run, run.Current, false);
                }
                else
                {
                    // out of order: everything goes back and the player hears about it
                    for (int i = 1; i <= o.Count; i++) MissionDevice.Find(o.Target + "_" + i)?.ResetDevice();
                    run.SequenceStep = 0;
                    run.Progress[run.Current] = 0;
                    if (_player != null) _player.ShowNotice("<color=#FF8866>Sequência errada — recomece</color>");
                    if (PowerGridManager.Instance != null) PowerGridManager.Instance.AlarmBurst(PowerGridManager.Instance.SectorAt(device.transform.position), 3.0f);
                }
            }
        }

        private void OnAnyDied(Health victim, DamageInfo info)
        {
            bool byPlayer = _player != null && info.Instigator == _player.gameObject;
            if (!byPlayer) return;

            foreach (MissionRun run in Active.ToArray())
            {
                ObjectiveDefinition[] objectives = run.Definition.Objectives;
                for (int i = 0; i < objectives.Length; i++)
                {
                    ObjectiveDefinition o = objectives[i];
                    if (run.Done[i] || o.Type != ObjectiveType.Kill || (!o.Optional && i != run.Current)) continue;
                    if (!KindMatches(victim, o.Target)) continue;
                    run.Progress[i] += 1;
                    if (run.Progress[i] >= o.Count) CompleteObjective(run, i, false);
                }
            }
        }

        public static bool KindMatches(Health victim, string kind)
        {
            if (string.IsNullOrEmpty(kind) || kind == "any") return true;
            var zombie = victim.GetComponent<ZombieAI>();
            var boss = victim.GetComponent<BossAI>();
            switch (kind)
            {
                case "zombie": return zombie != null;
                case "soldier": return victim.GetComponent<EnemyShooterAI>() != null;
                case "special": return zombie != null && zombie.Kind != ZombieKind.Common;
                case "boss": return boss != null;
                default:
                    if (boss != null && boss.Definition != null && boss.Definition.Id == kind) return true;
                    return zombie != null && zombie.Kind.ToString() == kind;
            }
        }

        private void OnWaveCleared(int wave)
        {
            foreach (MissionRun run in Active.ToArray())
            {
                ObjectiveDefinition o = run.CurrentObjective;
                if (o != null && o.Type == ObjectiveType.SurviveWave && run.WaveLaunched) CompleteObjective(run, run.Current, false);
            }
        }

        // ---------------------------------------------------------------- results

        private void CompleteMission(MissionRun run)
        {
            run.State = MissionState.Completed;
            Active.Remove(run);
            _history[run.Definition.Id] = MissionState.Completed;
            Apply(run.Definition.OnComplete);

            // rewards are paid once per mission per run, whatever happens
            if (_rewarded.Add(run.Definition.Id))
            {
                MissionDefinition d = run.Definition;
                RunState.AddCredits(d.Credits, d.Title);
                RunState.MissionsCompleted++;
                Progression.AddExperience(d.Experience, "missão");
                if (GameFlow.Instance != null) GameFlow.Instance.AddBonusScore(d.Score, "MISSÃO CONCLUÍDA");
                bool untouched = _player != null && _player.Health.DamageTaken - run.DamageAtStart < 0.5f;
                if (untouched) Progression.Unlock("mission_no_damage");
                Progression.RecordMission();
            }

            if (_player != null) _player.ShowNotice($"<color=#FFC74D>MISSÃO CONCLUÍDA</color>  {run.Definition.Title}");
            MissionCompleted?.Invoke(run);
        }

        private void FailMission(MissionRun run, string reason)
        {
            run.State = MissionState.Failed;
            Active.Remove(run);
            _history[run.Definition.Id] = MissionState.Failed;
            ResetDevices(run);
            Apply(run.Definition.OnFail);
            if (_player != null) _player.ShowNotice($"<color=#FF5A4A>MISSÃO FALHOU</color>  {run.Definition.Title} ({reason})");
            MissionFailed?.Invoke(run);
        }

        private static void ResetDevices(MissionRun run)
        {
            foreach (ObjectiveDefinition o in run.Definition.Objectives)
            {
                if (o.Type == ObjectiveType.Sequence)
                {
                    for (int i = 1; i <= o.Count; i++) MissionDevice.Find(o.Target + "_" + i)?.ResetDevice();
                }
            }

            // items of a failed mission go back where they were
            foreach (ObjectiveDefinition o in run.Definition.Objectives)
            {
                if (o.Type != ObjectiveType.Collect && o.Type != ObjectiveType.Retrieve) continue;
                foreach (MissionPickup pickup in Resources.FindObjectsOfTypeAll<MissionPickup>())
                {
                    if (pickup.ItemId == o.Target && pickup.gameObject.scene.IsValid() && RunState.ItemCount(o.Target) == 0) pickup.Restore();
                }
            }
        }

        // ---------------------------------------------------------------- effects

        public void Apply(MissionEffect[] effects)
        {
            if (effects == null) return;
            foreach (MissionEffect effect in effects) Apply(effect);
        }

        private void Apply(MissionEffect e)
        {
            PowerGridManager grid = PowerGridManager.Instance;
            bool located = MissionAnchor.TryLocate(e.Target, out Vector3 at);
            switch (e.Type)
            {
                case MissionEffectType.RestorePower:
                    if (grid == null) break;
                    if (located) grid.Restore(grid.SectorAt(at));
                    else for (int i = 0; i < grid.Sectors.Length; i++) grid.Restore(i);
                    break;
                case MissionEffectType.CutPower:
                    if (grid != null && located) grid.SetState(grid.SectorAt(at), e.Value > 0.5f ? PowerState.Blackout : PowerState.Emergency);
                    break;
                case MissionEffectType.UnlockDoor:
                    if (located) DoorNear(at)?.Unlock();
                    break;
                case MissionEffectType.LockDoor:
                    if (located) DoorNear(at)?.Lock();
                    break;
                case MissionEffectType.BlockSpawns:
                    if (located && _spawner != null) _spawner.BlockSpawns(at, 40.0f, e.Value);
                    break;
                case MissionEffectType.Floodlights:
                    MissionAnchor anchor = MissionAnchor.Find(e.Target);
                    if (anchor != null) foreach (Light light in anchor.GetComponentsInChildren<Light>(true)) light.enabled = e.Value >= 0.0f;
                    break;
                case MissionEffectType.StartAlarm:
                    if (grid != null && located) grid.AlarmBurst(grid.SectorAt(at), e.Value > 0.0f ? e.Value : 9999.0f);
                    break;
                case MissionEffectType.StopAlarm:
                    if (grid != null && located) grid.StopAlarmAt(grid.SectorAt(at));
                    break;
                case MissionEffectType.SetFlag:
                    RunState.SetFlag(e.Target);
                    break;
                case MissionEffectType.GiveItem:
                    if (_player != null && Enum.TryParse(e.Target, out QuickItem item))
                    {
                        _player.GetComponent<InventoryManager>()?.Add(item, Mathf.Max(1, Mathf.RoundToInt(e.Value)), true);
                    }

                    break;
                case MissionEffectType.GiveAmmo:
                    if (_player != null)
                    {
                        foreach (WeaponInstance weapon in _player.Slots)
                        {
                            if (weapon != null) weapon.Reserve += weapon.Data.MagazineSize * Mathf.Max(1, Mathf.RoundToInt(e.Value));
                        }
                    }

                    break;
                case MissionEffectType.SpawnWeapon:
                    WeaponData data = WeaponNamed(e.Target2);
                    if (data != null && located)
                    {
                        var go = new GameObject("MissionWeapon_" + data.DisplayName);
                        go.transform.position = at + Vector3.up * 0.4f;
                        var pickup = go.AddComponent<WeaponPickup>();
                        pickup.Data = data;
                        pickup.Floating = true;
                    }

                    break;
                case MissionEffectType.Radio:
                    Story.PlayRadio(e.Target);
                    break;
                case MissionEffectType.Document:
                    Story.Open(e.Target);
                    break;
                case MissionEffectType.SpawnEnemies:
                    if (_spawner != null) _spawner.SpawnPressure(Mathf.RoundToInt(e.Value), located ? at : _player.transform.position);
                    break;
                case MissionEffectType.ShopDiscount:
                    ShopManager.Discount = Mathf.Clamp01(e.Value);
                    break;
                case MissionEffectType.SpawnSupplies:
                    if (!located) break;
                    float supplies = GameFlow.Instance != null ? GameFlow.Instance.Difficulty.Supplies : 1.0f;
                    int count = Mathf.Max(1, Mathf.RoundToInt(Mathf.Max(1.0f, e.Value) * supplies));
                    for (int i = 0; i < count; i++)
                    {
                        Vector3 offset = Quaternion.Euler(0.0f, i * 360.0f / count, 0.0f) * Vector3.forward * 1.3f;
                        ConsumablePickup.Spawn(i % 2 == 0 ? ConsumablePickup.Kind.Ammo : ConsumablePickup.Kind.Health, at + offset, 0.0f);
                    }

                    ItemPickup.Spawn(QuickItem.Grenade, Mathf.Max(1, count / 2), at + Vector3.right * 0.6f, 0.0f);
                    ItemPickup.Spawn(QuickItem.Molotov, 1, at - Vector3.right * 0.6f, 0.0f);
                    break;
                case MissionEffectType.StablePower:
                    if (grid != null) grid.SuppressFailures(e.Value);
                    break;
                case MissionEffectType.Reveal:
                    MissionAnchor revealed = MissionAnchor.Find(e.Target);
                    if (revealed == null) break;
                    foreach (Transform child in revealed.GetComponentsInChildren<Transform>(true))
                    {
                        if (child.name == "Reveal") child.gameObject.SetActive(e.Value >= 0.0f);
                    }

                    break;
            }
        }

        private WeaponData WeaponNamed(string name)
        {
            if (Weapons == null) return null;
            foreach (WeaponData weapon in Weapons)
            {
                if (weapon != null && (weapon.DisplayName == name || weapon.name == name)) return weapon;
            }

            return null;
        }

        // ---------------------------------------------------------------- choice and markers

        private void UpdateChoiceInput()
        {
            if (PendingChoice.a == null || Keyboard.current == null || ShopManager.IsOpen) return;
            if (Keyboard.current.digit1Key.wasPressedThisFrame) Choose(0);
            else if (Keyboard.current.digit2Key.wasPressedThisFrame) Choose(1);
        }

        // where the current objective of the most important mission is, for the HUD marker
        public bool TryGetMarker(out Vector3 position, out string label)
        {
            position = Vector3.zero;
            label = null;
            MissionRun best = null;
            foreach (MissionRun run in Active)
            {
                if (run.CurrentObjective == null || !run.CurrentObjective.ShowMarker) continue;
                if (best == null || (best.Optional && !run.Optional)) best = run;
            }

            if (best == null) return false;
            ObjectiveDefinition o = best.CurrentObjective;

            // items: point at the nearest one still lying around
            if ((o.Type == ObjectiveType.Collect || o.Type == ObjectiveType.Retrieve) && _player != null)
            {
                float nearest = float.MaxValue;
                bool any = false;
                foreach (MissionPickup pickup in MissionPickup.All)
                {
                    if (pickup.ItemId != o.Target) continue;
                    float d = Vector3.Distance(pickup.transform.position, _player.transform.position);
                    if (d < nearest)
                    {
                        nearest = d;
                        position = pickup.transform.position;
                        any = true;
                    }
                }

                label = MissionDevice.ItemName(o.Target);
                return any;
            }

            if (!TryLocate(o, out position)) return false;
            MissionAnchor anchor = MissionAnchor.Find(o.Target);
            label = anchor != null && !string.IsNullOrEmpty(anchor.Label) ? anchor.Label : o.Text;
            return true;
        }

        public static string ProgressText(MissionRun run, int index)
        {
            ObjectiveDefinition o = run.Definition.Objectives[index];
            if (run.Done[index]) return "";
            switch (o.Type)
            {
                case ObjectiveType.Collect:
                case ObjectiveType.Retrieve:
                case ObjectiveType.Kill:
                case ObjectiveType.Sequence:
                    return o.Count > 1 ? $" ({Mathf.Min((int)run.Progress[index], o.Count)}/{o.Count})" : "";
                case ObjectiveType.Defend:
                case ObjectiveType.Survive:
                    return $" ({Mathf.CeilToInt(Mathf.Max(0.0f, o.Duration - run.Progress[index]))}s)";
                default:
                    return "";
            }
        }
    }
}
