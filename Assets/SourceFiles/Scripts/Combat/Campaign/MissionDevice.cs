using System;
using System.Collections.Generic;
using UnityEngine;

namespace Combat
{
    /* Something a mission has the player operate: a fuse box, a terminal, an antenna, a valve,
     * a gate lever. It can consume an item (fuses, a module, a keycard), take a while to work
     * (the player has to stay close), make noise that draws the infected, and belong to a
     * sequence (Id_1, Id_2...). It reports to the MissionManager through Activated.
     */
    public class MissionDevice : MonoBehaviour, IInteractable
    {
        public string Id = "device";
        public string DisplayName = "painel";
        [Tooltip("Verb shown on the prompt, e.g. Ativar, Instalar, Fechar")]
        public string Verb = "Ativar";

        [Header("Requirements")]
        public string RequiredItem;
        public int RequiredCount = 1;
        [Tooltip("Needs its power sector running (electronic devices)")]
        public bool RequiresPower;

        [Header("Operation")]
        [Tooltip("Seconds of work after pressing, staying close by")]
        public float WorkTime;
        public float WorkRange = 3.0f;
        [Tooltip("Noise radius made while working, draws enemies (0 = silent)")]
        public float Noise;
        [Tooltip("Can be used again after a mission resets it")]
        public bool Reusable = true;
        [Tooltip("Only usable while a mission asks for it")]
        public bool OnlyWhileNeeded = true;

        [Header("Effects")]
        [Tooltip("Applied to the level when it finishes working (unlock a door, close a gate...)")]
        public MissionEffect[] OnActivated;

        [Header("Visuals")]
        public Light StatusLight;
        public Renderer StatusLamp;
        public AudioClip UseSound;
        public AudioClip WorkLoop;
        public AudioClip DoneSound;

        public bool IsActivated { get; private set; }
        public bool IsWorking { get; private set; }
        public float Progress { get; private set; }

        public static event Action<MissionDevice> Activated;

        private static readonly Dictionary<string, MissionDevice> Registry = new Dictionary<string, MissionDevice>();
        private float _nextNoise;
        private AudioSource _loop;
        private PlayerCombat _worker;
        private MaterialPropertyBlock _block;

        public Vector3 InteractionPoint => transform.position + Vector3.up * 1.0f;
        public float InteractionRange => 2.2f;

        public string Prompt
        {
            get
            {
                if (OnlyWhileNeeded && MissionManager.Instance != null && !MissionManager.Instance.IsNeeded(Id)) return "";
                if (IsWorking) return $"{Capitalized} — {Mathf.RoundToInt(Progress * 100)}% (fique por perto)";
                if (IsActivated && !Reusable) return $"<color=#9BE39B>{Capitalized} ativo</color>";
                if (RequiresPower && !HasPower) return $"<color=#FF8866>{Capitalized} sem energia</color>";
                if (!string.IsNullOrEmpty(RequiredItem) && RunState.ItemCount(RequiredItem) < RequiredCount)
                {
                    return $"<color=#FF8866>{Capitalized}</color>: precisa de {RequiredCount}× {ItemName(RequiredItem)} ({RunState.ItemCount(RequiredItem)}/{RequiredCount})";
                }

                return Interactables.Key + $"{Verb} {DisplayName}";
            }
        }

        private string Capitalized => DisplayName.Length > 0 ? char.ToUpperInvariant(DisplayName[0]) + DisplayName.Substring(1) : DisplayName;
        private bool HasPower => PowerGridManager.Instance == null || PowerGridManager.Instance.StateAt(transform.position) == PowerState.Normal;

        public static string ItemName(string id)
        {
            ItemDefinition item = ItemCatalog.Find(id);
            if (item != null) return item.DisplayName.ToLowerInvariant();
            switch (id)
            {
                case "fuse": return "fusível";
                case "keycard": return "cartão de acesso";
                case "radio_module": return "módulo de rádio";
                case "valve_key": return "chave de válvula";
                default: return id;
            }
        }

        private void OnEnable()
        {
            if (!string.IsNullOrEmpty(Id)) Registry[Id] = this;
            Interactables.Register(this);
        }

        private void OnDisable()
        {
            if (!string.IsNullOrEmpty(Id) && Registry.TryGetValue(Id, out MissionDevice device) && device == this) Registry.Remove(Id);
            Interactables.Unregister(this);
        }

        public static MissionDevice Find(string id)
        {
            return id != null && Registry.TryGetValue(id, out MissionDevice device) ? device : null;
        }

        private void Awake()
        {
            _block = new MaterialPropertyBlock();
            _loop = gameObject.AddComponent<AudioSource>();
            AudioRouting.Route(_loop, AudioRouting.Channel.Sfx);
            _loop.loop = true;
            _loop.spatialBlend = 1.0f;
            _loop.minDistance = 2.0f;
            _loop.maxDistance = 30.0f;
            _loop.volume = 0.6f;
            _loop.playOnAwake = false;
            SetStatus(false);
        }

        public void Interact(PlayerCombat player)
        {
            if (IsWorking || (IsActivated && !Reusable)) return;
            if (OnlyWhileNeeded && MissionManager.Instance != null && !MissionManager.Instance.IsNeeded(Id)) return;
            if (RequiresPower && !HasPower)
            {
                if (player != null) player.ShowNotice("Sem energia neste setor");
                return;
            }

            if (!string.IsNullOrEmpty(RequiredItem) && !RunState.TakeItem(RequiredItem, RequiredCount))
            {
                if (player != null) player.ShowNotice($"Precisa de {RequiredCount}× {ItemName(RequiredItem)}");
                return;
            }

            if (CombatEffects.Instance != null) CombatEffects.Instance.PlaySound(UseSound, InteractionPoint, 0.9f, 0.03f, 1.0f, 2.0f, 30.0f);
            if (WorkTime > 0.0f)
            {
                IsWorking = true;
                Progress = 0.0f;
                _worker = player;
                if (WorkLoop != null)
                {
                    _loop.clip = WorkLoop;
                    _loop.Play();
                }

                return;
            }

            Complete();
        }

        // time spent here, for the performance probe (Diagnostics/PerfProbe)
        private static readonly Unity.Profiling.ProfilerMarker UpdateMarker = new Unity.Profiling.ProfilerMarker("Combat.MissionDevice.Update");

        private void Update()
        {
            using (UpdateMarker.Auto()) UpdateMarked();
        }

        private void UpdateMarked()
        {
            if (!IsWorking) return;

            // the work only goes on while the player stays close
            bool close = _worker == null || (Vector3.Distance(_worker.transform.position, transform.position) <= WorkRange && !_worker.Health.IsDead);
            if (close) Progress = Mathf.Min(1.0f, Progress + Time.deltaTime / WorkTime);
            SetStatus(Mathf.PingPong(Time.time * 4.0f, 1.0f) > 0.5f);

            if (Noise > 0.0f && Time.time >= _nextNoise)
            {
                _nextNoise = Time.time + 2.5f;
                NoiseSystem.Emit(transform.position, Noise, NoiseCategory.Machine, Factions.Environment, gameObject);
            }

            if (Progress >= 1.0f) Complete();
        }

        private void Complete()
        {
            IsWorking = false;
            IsActivated = true;
            _loop.Stop();
            SetStatus(true);
            if (CombatEffects.Instance != null) CombatEffects.Instance.PlaySound(DoneSound, InteractionPoint, 0.9f, 0.0f, 1.0f, 2.0f, 30.0f);
            if (MissionManager.Instance != null) MissionManager.Instance.Apply(OnActivated);
            Activated?.Invoke(this);
        }

        // back to the starting state, e.g. a wrong step in a sequence or a mission restarting
        public void ResetDevice()
        {
            IsActivated = false;
            IsWorking = false;
            Progress = 0.0f;
            _loop.Stop();
            SetStatus(false);
        }

        private void SetStatus(bool on)
        {
            Color color = on ? new Color(0.2f, 1.0f, 0.3f) : new Color(1.0f, 0.25f, 0.1f);
            if (StatusLight != null) StatusLight.color = color;
            if (StatusLamp == null) return;
            _block ??= new MaterialPropertyBlock();
            StatusLamp.GetPropertyBlock(_block);
            _block.SetColor("_BaseColor", color);
            _block.SetColor("_EmissionColor", color * 3.0f);
            StatusLamp.SetPropertyBlock(_block);
        }
    }
}
