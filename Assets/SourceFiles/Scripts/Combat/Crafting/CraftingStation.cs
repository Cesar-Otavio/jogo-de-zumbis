using System.Collections.Generic;
using UnityEngine;

namespace Combat
{
    /* A bench the player uses with E to make the recipes of its type. It shows on the map once
     * seen, and the first one found gets a word from Duarte (if the line exists).
     */
    public class CraftingStation : MonoBehaviour, IInteractable
    {
        public static readonly List<CraftingStation> All = new List<CraftingStation>();

        public CraftingStationType Type = CraftingStationType.Workbench;
        public string DisplayName = "Bancada de trabalho";
        [Tooltip("Played when the bench is used")]
        public AudioClip UseSound;

        public Vector3 InteractionPoint => transform.position + Vector3.up * 1.0f;
        public float InteractionRange => 2.2f;
        public string Prompt => $"{Interactables.Key}Usar {DisplayName.ToLowerInvariant()}";

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

        public void Interact(PlayerCombat player)
        {
            if (InventoryView.Instance == null) return;
            CombatEffects fx = CombatEffects.Instance;
            if (fx != null) fx.PlaySound(UseSound != null ? UseSound : fx.CrateOpenSound, InteractionPoint, 0.5f);
            // the line is marked Once in the library: Duarte only comments on the first bench
            if (DialogueDirector.Instance != null) DialogueDirector.Instance.Play("fx_workbench");
            InventoryView.Instance.OpenCrafting(this);
        }
    }
}
