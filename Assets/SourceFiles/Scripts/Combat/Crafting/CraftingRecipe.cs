using System;
using UnityEngine;

namespace Combat
{
    // where a recipe can be made
    public enum CraftingStationType
    {
        // straight from the backpack screen
        Anywhere,
        Workbench,
        Medical,
        Maintenance
    }

    /* One recipe: ingredients (catalog item ids) in, a catalog item out. Products that are quick
     * items go to the quick bar (overflow into the backpack), anything else into the backpack.
     */
    [CreateAssetMenu(menuName = "Combat/Crafting Recipe", fileName = "Recipe_")]
    public class CraftingRecipe : ScriptableObject
    {
        [Serializable]
        public class Ingredient
        {
            public string ItemId;
            public int Count = 1;
        }

        public string Id = "recipe";
        public string DisplayName = "Receita";
        [TextArea] public string Description;
        public string ProductId;
        public int ProductAmount = 1;
        public Ingredient[] Ingredients = new Ingredient[0];
        public CraftingStationType Station = CraftingStationType.Workbench;
        [Tooltip("Seconds the bench takes (0 = immediate)")]
        public float CraftTime;
        [Tooltip("Player level needed (Progression)")]
        public int RequiredLevel = 1;
        [Tooltip("Run flag that unlocks it (empty = known from the start)")]
        public string UnlockFlag;
    }
}
