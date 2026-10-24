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
        Currency
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
        public int MaxStack = 1;
    }
}
