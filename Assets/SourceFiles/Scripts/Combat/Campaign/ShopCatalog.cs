using System;
using UnityEngine;

namespace Combat
{
    public enum ShopCategory
    {
        Armas,
        Municao,
        Protecao,
        Consumiveis,
        Melhorias
    }

    public enum ShopItemKind
    {
        // a gun, handed over with a full magazine and some reserve
        Weapon,
        // magazines for both carried guns
        Ammo,
        // reserves filled to the top
        AmmoFull,
        // a vest that soaks part of the damage until it's spent
        Armor,
        // back to full health
        Heal,
        Consumable,
        WeaponUpgrade,
        // more stamina, a few times per run
        Stamina,
        // one more of every quick item fits
        Capacity
    }

    // what the shop sells and for how much; prices and limits live here, not in code
    [CreateAssetMenu(menuName = "Combat/Shop Catalog", fileName = "ShopCatalog")]
    public class ShopCatalog : ScriptableObject
    {
        [Serializable]
        public class Entry
        {
            public string Id;
            public string DisplayName;
            [Tooltip("Optional: otherwise the icon comes from the weapon, the quick item or the item catalog")]
            public Sprite Icon;
            [TextArea] public string Description;
            public ShopCategory Category;
            public ShopItemKind Kind;
            public int Price = 100;
            [Tooltip("Player level needed before it shows up")]
            public int RequiredLevel = 1;
            [Tooltip("How many times per run it can be bought (0 = unlimited)")]
            public int LimitPerRun;
            public WeaponData Weapon;
            public QuickItem Item;
            public int Amount = 1;
            public WeaponUpgrade Upgrade;
        }

        public Entry[] Entries;
    }
}
