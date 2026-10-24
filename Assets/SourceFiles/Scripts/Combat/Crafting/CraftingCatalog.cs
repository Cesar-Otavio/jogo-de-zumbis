using UnityEngine;

namespace Combat
{
    // every recipe in the game, in display order; lives in Resources
    [CreateAssetMenu(menuName = "Combat/Crafting Catalog", fileName = "CraftingCatalog")]
    public class CraftingCatalog : ScriptableObject
    {
        public const string ResourcePath = "CraftingCatalog";
        public CraftingRecipe[] Recipes = new CraftingRecipe[0];

        private static CraftingCatalog _instance;
        private static bool _loaded;

        public static CraftingCatalog Instance
        {
            get
            {
                if (!_loaded)
                {
                    _loaded = true;
                    _instance = Resources.Load<CraftingCatalog>(ResourcePath);
                }

                return _instance;
            }
        }
    }
}
