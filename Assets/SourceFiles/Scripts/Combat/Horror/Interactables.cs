using System.Collections.Generic;
using UnityEngine;

namespace Combat
{
    // something the player can use with the interact key (doors, generators...), next to chests and weapons
    public interface IInteractable
    {
        Vector3 InteractionPoint { get; }
        float InteractionRange { get; }
        // the text for the HUD, e.g. "[E] Abrir porta" or "Trancada"
        string Prompt { get; }
        void Interact(PlayerCombat player);
    }

    public static class Interactables
    {
        public static readonly List<IInteractable> All = new List<IInteractable>();

        public static void Register(IInteractable interactable)
        {
            if (!All.Contains(interactable)) All.Add(interactable);
        }

        public static void Unregister(IInteractable interactable)
        {
            All.Remove(interactable);
        }

        public static IInteractable Nearest(Vector3 position, out float distance)
        {
            IInteractable best = null;
            distance = float.MaxValue;
            foreach (IInteractable candidate in All)
            {
                float d = Vector3.Distance(position, candidate.InteractionPoint);
                if (d > candidate.InteractionRange || d >= distance) continue;
                // nothing to say means nothing to do (hidden mission items, broken doors)
                if (!string.IsNullOrEmpty(candidate.Prompt))
                {
                    best = candidate;
                    distance = d;
                }
            }

            return best;
        }

        public const string Key = "<color=#FFC74D>[E]</color> ";
    }
}
