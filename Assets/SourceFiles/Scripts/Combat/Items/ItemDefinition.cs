using UnityEngine;

namespace Combat
{
    public enum ItemCategory
    {
        Weapon,
        Ammo,
        Medical,
        Tactical,
        Mission,
        Document,
        Construction,
        Currency,
        // crafting materials (added last so saved categories keep their values)
        Material
    }

    // what "Usar" does for an item kept in the backpack (quick items keep their own use on the quick bar)
    public enum ItemUse
    {
        None,
        Heal,
        Armor,
        // full magazines and reserves for the guns carried
        Ammo
    }

    public enum ItemRarity
    {
        Common,
        Uncommon,
        Rare,
        Story
    }

    /* What an item is, for presentation: name, description, icon and stacking. One asset per item id;
     * gameplay keeps its own logic (QuickItem for the quick bar, RunState for mission items,
     * WeaponData for weapons) and looks the presentation up here by id, so nothing is defined twice.
     */
    [CreateAssetMenu(menuName = "Combat/Item Definition")]
    public class ItemDefinition : ScriptableObject
    {
        [Tooltip("Stable id used by the game code: quick items (medkit, grenade...), mission items (fuse, keycard...)")]
        public string Id = "item";
        public string DisplayName = "Item";
        [TextArea] public string Description;
        public ItemCategory Category = ItemCategory.Mission;
        public ItemRarity Rarity = ItemRarity.Common;
        public Sprite Icon;
        [Tooltip("Shown instead of the icon when the item can't be used right now (optional)")]
        public Sprite UnavailableIcon;
        [Tooltip("How many share one backpack slot")]
        public int MaxStack = 1;
        public ItemUse Use;
        [Tooltip("Health healed or armor added by Use")]
        public float UseAmount;
        [Tooltip("Can be dropped from the backpack (mission items never are)")]
        public bool Droppable = true;
        [Tooltip("Story documents this item is (or carries): the document reader shows the item's icon and name")]
        public string[] StoryIds = new string[0];
    }
}
