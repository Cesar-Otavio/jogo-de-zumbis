using System;
using UnityEngine;

namespace Combat
{
    /* The one place that makes things. A craft is checked completely before anything changes
     * (unlocked, right station, ingredients, room for the product), then the ingredients are
     * taken and the product given in the same call, so a failed check costs nothing and a
     * double click in the same frame can't make two.
     */
    public static class Crafting
    {
        public static event Action<CraftingRecipe> Crafted;
        private static int _lastCraftFrame = -1;

        public static bool IsUnlocked(CraftingRecipe recipe)
        {
            if (recipe == null) return false;
            if (Progression.Level < recipe.RequiredLevel) return false;
            return string.IsNullOrEmpty(recipe.UnlockFlag) || RunState.HasFlag(recipe.UnlockFlag);
        }

        public static bool StationAllows(CraftingRecipe recipe, CraftingStationType? station)
        {
            if (recipe.Station == CraftingStationType.Anywhere) return true;
            return station.HasValue && station.Value == recipe.Station;
        }

        // null when it can be made here and now, otherwise the reason for the UI
        public static string CannotCraft(CraftingRecipe recipe, CraftingStationType? station)
        {
            if (recipe == null) return "Receita inválida";
            if (!IsUnlocked(recipe)) return Progression.Level < recipe.RequiredLevel ? $"Requer nível {recipe.RequiredLevel}" : "Receita bloqueada";
            if (!StationAllows(recipe, station)) return $"Requer {StationName(recipe.Station)}";
            foreach (CraftingRecipe.Ingredient ingredient in recipe.Ingredients)
            {
                if (Backpack.Count(ingredient.ItemId) < ingredient.Count) return "Materiais insuficientes";
            }

            if (RoomForProduct(recipe) < recipe.ProductAmount) return "Sem espaço para o resultado";
            return null;
        }

        // co-op client: a craft the host has just allowed (it keeps this player's materials: NetWorld)
        internal static bool RemoteApproved;

        public static bool Craft(CraftingRecipe recipe, CraftingStationType? station)
        {
            if (_lastCraftFrame == Time.frameCount || CannotCraft(recipe, station) != null) return false;
            if (!RemoteApproved && NetWorld.RedirectCraft(recipe, station)) return false;
            RemoteApproved = false;
            _lastCraftFrame = Time.frameCount;

            foreach (CraftingRecipe.Ingredient ingredient in recipe.Ingredients)
            {
                // the check above makes this succeed; if it ever doesn't, put back what was taken
                if (Backpack.Remove(ingredient.ItemId, ingredient.Count)) continue;
                Refund(recipe, ingredient);
                return false;
            }

            if (Backpack.TryQuickItem(recipe.ProductId, out QuickItem quick) && InventoryManager.Player != null)
            {
                InventoryManager.Player.Add(quick, recipe.ProductAmount, true);
            }
            else
            {
                Backpack.Add(recipe.ProductId, recipe.ProductAmount);
                PickupFeed.Item(recipe.ProductId, recipe.ProductAmount);
            }

            Crafted?.Invoke(recipe);
            return true;
        }

        private static void Refund(CraftingRecipe recipe, CraftingRecipe.Ingredient failed)
        {
            foreach (CraftingRecipe.Ingredient ingredient in recipe.Ingredients)
            {
                if (ingredient == failed) break;
                Backpack.Add(ingredient.ItemId, ingredient.Count);
            }
        }

        // the ingredients' slots count as free: they'll be emptied first
        private static int RoomForProduct(CraftingRecipe recipe)
        {
            int room = Backpack.RoomFor(recipe.ProductId);
            if (Backpack.TryQuickItem(recipe.ProductId, out QuickItem quick) && InventoryManager.Player != null)
            {
                room += InventoryManager.Player.CapacityOf(quick) - InventoryManager.Player.Count(quick);
            }

            foreach (CraftingRecipe.Ingredient ingredient in recipe.Ingredients)
            {
                if (Backpack.Count(ingredient.ItemId) == ingredient.Count) room += Backpack.MaxStackOf(recipe.ProductId);
            }

            return room;
        }

        public static string StationName(CraftingStationType station)
        {
            switch (station)
            {
                case CraftingStationType.Workbench: return "bancada de trabalho";
                case CraftingStationType.Medical: return "bancada médica";
                case CraftingStationType.Maintenance: return "bancada de manutenção";
                default: return "mochila";
            }
        }
    }
}
