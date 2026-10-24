using System;
using System.Collections.Generic;
using UnityEngine;

namespace Combat
{
    /* What the player just picked up: icon, name and amount. Systems report here; the HUD shows it.
     * The same thing picked up again within a moment adds to the existing entry ("+30" becomes
     * "+60") instead of stacking up a column of notifications.
     */
    public static class PickupFeed
    {
        public class Entry
        {
            public string Key;
            public Sprite Icon;
            public string Label;
            public int Amount;
            public Color Tint = Color.white;
            public float Started;
            public float Updated;
            public bool Negative;
        }

        public const float MergeWindow = 2.5f;
        public const int MaxEntries = 5;

        public static readonly List<Entry> Active = new List<Entry>();
        public static event Action<Entry> Changed;

        public static void Report(string key, Sprite icon, string label, int amount, Color tint, bool negative = false)
        {
            float now = Time.unscaledTime;
            foreach (Entry entry in Active)
            {
                if (entry.Key != key || now - entry.Updated > MergeWindow) continue;
                entry.Amount += amount;
                entry.Updated = now;
                Changed?.Invoke(entry);
                return;
            }

            var added = new Entry { Key = key, Icon = icon, Label = label, Amount = amount, Tint = tint, Started = now, Updated = now, Negative = negative };
            Active.Add(added);
            if (Active.Count > MaxEntries) Active.RemoveAt(0);
            Changed?.Invoke(added);
        }

        public static void Item(string id, int amount)
        {
            ItemDefinition item = ItemCatalog.Find(id);
            Report("item:" + id, ItemCatalog.IconOf(id), item != null ? item.DisplayName : id, amount, TintOf(item != null ? item.Category : ItemCategory.Mission));
        }

        public static void Quick(QuickItem item, int amount)
        {
            string id = ItemCatalog.IdOf(item);
            Report("item:" + id, ItemCatalog.IconOf(id), ItemCatalog.NameOf(id, InventoryManager.Names[(int)item]), amount, TintOf(item == QuickItem.Medkit ? ItemCategory.Medical : item == QuickItem.Barricade ? ItemCategory.Construction : ItemCategory.Tactical));
        }

        public static void Weapon(WeaponData weapon)
        {
            if (weapon == null) return;
            Report("weapon:" + weapon.DisplayName, ItemCatalog.IconOf(weapon), weapon.DisplayName, 0, TintOf(ItemCategory.Weapon));
        }

        public static void Ammo(WeaponData weapon, int rounds)
        {
            if (weapon == null || rounds <= 0) return;
            Report("ammo:" + weapon.AmmoName, ItemCatalog.AmmoIconOf(weapon), "Munição " + weapon.AmmoName, rounds, TintOf(ItemCategory.Ammo));
        }

        public static void Health(int amount)
        {
            ItemCatalog catalog = ItemCatalog.Instance;
            Report("health", catalog != null ? catalog.HealthIcon : null, "Vida", amount, TintOf(ItemCategory.Medical));
        }

        public static void Full(string label, Sprite icon)
        {
            Report("full:" + label, icon, label + " — sem espaço", 0, new Color(1.0f, 0.55f, 0.45f), true);
        }

        // one discreet colour per category, the same in the quick bar, the shop and here
        public static Color TintOf(ItemCategory category)
        {
            switch (category)
            {
                case ItemCategory.Weapon: return new Color(0.92f, 0.92f, 0.95f);
                case ItemCategory.Ammo: return new Color(1.0f, 0.8f, 0.42f);
                case ItemCategory.Medical: return new Color(0.5f, 0.95f, 0.6f);
                case ItemCategory.Tactical: return new Color(1.0f, 0.62f, 0.38f);
                case ItemCategory.Mission: return new Color(0.5f, 0.86f, 1.0f);
                case ItemCategory.Document: return new Color(0.86f, 0.82f, 0.7f);
                case ItemCategory.Construction: return new Color(0.82f, 0.72f, 0.55f);
                default: return new Color(1.0f, 0.78f, 0.3f);
            }
        }

        public static void Clear()
        {
            Active.Clear();
        }
    }
}
