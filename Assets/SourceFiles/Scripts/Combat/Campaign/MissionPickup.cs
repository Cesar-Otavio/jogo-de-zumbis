using System;
using System.Collections.Generic;
using UnityEngine;

namespace Combat
{
    /* An item a mission asks for: a fuse, a keycard, a radio module, a document...
     * Picked up with the interact key; documents open their text. Built from simple shapes
     * so it works without a model; assign one in Model to replace them.
     */
    public class MissionPickup : MonoBehaviour, IInteractable
    {
        public enum Shape
        {
            Box,
            Card,
            Document,
            Module
        }

        public string ItemId = "fuse";
        public string DisplayName = "Fusível";
        public Shape Look = Shape.Box;
        [Tooltip("Story entry opened when picked up (documents and logs)")]
        public string StoryId;
        [Tooltip("Hidden until a mission needs it")]
        public bool OnlyWhileNeeded = true;
        public GameObject Model;
        public Color GlowColor = new Color(0.4f, 0.9f, 1.0f);

        public static readonly List<MissionPickup> All = new List<MissionPickup>();
        public static event Action<MissionPickup> PickedUp;

        public Vector3 InteractionPoint => transform.position + Vector3.up * 0.4f;
        public float InteractionRange => 1.8f;
        public string Prompt => Interactables.Key + (string.IsNullOrEmpty(StoryId) ? "Pegar " : "Ler ") + DisplayName;

        private Transform _visual;
        private Light _glow;

        private void OnEnable()
        {
            All.Add(this);
            Interactables.Register(this);
        }

        private void OnDisable()
        {
            All.Remove(this);
            Interactables.Unregister(this);
        }

        private void Start()
        {
            BuildVisual();
        }

        private void BuildVisual()
        {
            _visual = new GameObject("Visual").transform;
            _visual.SetParent(transform, false);
            if (Model != null)
            {
                Instantiate(Model, _visual, false);
            }
            else
            {
                CombatEffects fx = CombatEffects.Instance;
                Material metal = fx != null ? fx.CrateMetalMaterial : null;
                Material light = fx != null ? fx.MedkitMaterial : null;
                switch (Look)
                {
                    case Shape.Card:
                        Part(new Vector3(0.0f, 0.0f, 0.0f), new Vector3(0.09f, 0.005f, 0.055f), light);
                        Part(new Vector3(0.0f, 0.004f, 0.0f), new Vector3(0.06f, 0.004f, 0.012f), metal);
                        break;
                    case Shape.Document:
                        Part(Vector3.zero, new Vector3(0.22f, 0.02f, 0.3f), light);
                        Part(new Vector3(0.0f, 0.012f, 0.0f), new Vector3(0.18f, 0.005f, 0.24f), metal);
                        break;
                    case Shape.Module:
                        Part(Vector3.zero, new Vector3(0.22f, 0.08f, 0.16f), metal);
                        Part(new Vector3(0.05f, 0.05f, 0.0f), new Vector3(0.04f, 0.02f, 0.04f), light);
                        break;
                    default:
                        Part(Vector3.zero, new Vector3(0.08f, 0.16f, 0.08f), light);
                        Part(new Vector3(0.0f, 0.09f, 0.0f), new Vector3(0.06f, 0.03f, 0.06f), metal);
                        Part(new Vector3(0.0f, -0.09f, 0.0f), new Vector3(0.06f, 0.03f, 0.06f), metal);
                        break;
                }
            }

            var glowObject = new GameObject("Glow");
            glowObject.transform.SetParent(transform, false);
            glowObject.transform.localPosition = Vector3.up * 0.5f;
            _glow = glowObject.AddComponent<Light>();
            _glow.type = LightType.Point;
            _glow.color = GlowColor;
            _glow.range = 2.5f;
            _glow.intensity = 1.5f;
            _glow.shadows = LightShadows.None;
        }

        private void Part(Vector3 position, Vector3 size, Material material)
        {
            GameObject part = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Destroy(part.GetComponent<Collider>());
            part.transform.SetParent(_visual, false);
            part.transform.localPosition = position;
            part.transform.localScale = size;
            if (material != null) part.GetComponent<MeshRenderer>().sharedMaterial = material;
        }

        // time spent here, for the performance probe (Diagnostics/PerfProbe)
        private static readonly Unity.Profiling.ProfilerMarker UpdateMarker = new Unity.Profiling.ProfilerMarker("Combat.MissionPickup.Update");

        private void Update()
        {
            using (UpdateMarker.Auto()) UpdateMarked();
        }

        private void UpdateMarked()
        {
            if (_visual == null) return;
            // only shown while some mission wants it, so the base isn't littered with quest items
            bool shown = !OnlyWhileNeeded || MissionManager.Instance == null || MissionManager.Instance.IsNeeded(ItemId);
            _visual.gameObject.SetActive(shown);
            if (_glow != null) _glow.enabled = shown;
            if (!shown) return;
            _visual.localPosition = Vector3.up * (0.35f + Mathf.Sin(Time.time * 2.0f) * 0.04f);
            _visual.localRotation = Quaternion.Euler(0.0f, Time.time * 40.0f, 0.0f);
        }

        public void Interact(PlayerCombat player)
        {
            if (OnlyWhileNeeded && MissionManager.Instance != null && !MissionManager.Instance.IsNeeded(ItemId)) return;

            RunState.AddItem(ItemId);
            PickupFeed.Item(ItemId, 1);
            if (CombatEffects.Instance != null) CombatEffects.Instance.PlayUI(CombatEffects.Instance.PickupSound, 0.8f);
            if (!string.IsNullOrEmpty(StoryId)) Story.Open(StoryId);
            PickedUp?.Invoke(this);
            gameObject.SetActive(false);
        }

        // missions that respawn their items (e.g. after failing)
        public void Restore()
        {
            gameObject.SetActive(true);
        }
    }
}
