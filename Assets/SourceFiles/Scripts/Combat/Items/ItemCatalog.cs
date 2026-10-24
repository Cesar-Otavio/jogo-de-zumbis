using System.Collections.Generic;
using UnityEngine;

namespace Combat
{
    /* All item definitions plus the shared icons (credits, health, a fallback), looked up by id.
     * Lives in Resources so any system can resolve an icon without a scene reference.
     */
    [CreateAssetMenu(menuName = "Combat/Item Catalog")]
    public class ItemCatalog : ScriptableObject
    {
        public const string ResourcePath = "ItemCatalog";

        public ItemDefinition[] Items = new ItemDefinition[0];
        public Sprite CreditsIcon;
        public Sprite HealthIcon;
        public Sprite ArmorIcon;
        [Header("Shop services")]
        public Sprite UpgradeIcon;
        public Sprite StaminaIcon;
        public Sprite CapacityIcon;
        [Header("Map")]
        public Sprite WorkbenchIcon;
        [Tooltip("Shown for anything without its own icon, so a missing sprite is obvious but not broken")]
        public Sprite FallbackIcon;

        private Dictionary<string, ItemDefinition> _byId;
        private static ItemCatalog _instance;
        private static bool _loaded;

        public static ItemCatalog Instance
        {
            get
            {
                if (!_loaded)
                {
                    _loaded = true;
                    _instance = Resources.Load<ItemCatalog>(ResourcePath);
                }

                return _instance;
            }
        }

        public static ItemDefinition Find(string id)
        {
            ItemCatalog catalog = Instance;
            if (catalog == null || string.IsNullOrEmpty(id)) return null;
            if (catalog._byId == null)
            {
                catalog._byId = new Dictionary<string, ItemDefinition>();
                foreach (ItemDefinition item in catalog.Items)
                {
                    if (item != null && !string.IsNullOrEmpty(item.Id)) catalog._byId[item.Id] = item;
                }
            }

            return catalog._byId.TryGetValue(id, out ItemDefinition found) ? found : null;
        }

        // the item a story document belongs to (the black box, a note, the evacuation codes...)
        public static ItemDefinition FindByStory(string storyId)
        {
            ItemCatalog catalog = Instance;
            if (catalog == null || string.IsNullOrEmpty(storyId)) return null;
            foreach (ItemDefinition item in catalog.Items)
            {
                if (item == null || item.StoryIds == null) continue;
                foreach (string story in item.StoryIds)
                {
                    if (story == storyId) return item;
                }
            }

            return null;
        }

        public static Sprite IconOf(string id)
        {
            ItemDefinition item = Find(id);
            if (item != null && item.Icon != null) return item.Icon;
            return Instance != null ? Instance.FallbackIcon : null;
        }

        public static string NameOf(string id, string fallback = null)
        {
            ItemDefinition item = Find(id);
            return item != null ? item.DisplayName : fallback ?? id;
        }

        public static string IdOf(QuickItem item)
        {
            switch (item)
            {
                case QuickItem.Medkit: return "medkit";
                case QuickItem.Grenade: return "grenade";
                case QuickItem.Molotov: return "molotov";
                case QuickItem.Flare: return "flare";
                case QuickItem.Barricade: return "barricade";
                default: return "decoy";
            }
        }

        public static Sprite IconOf(QuickItem item) => IconOf(IdOf(item));

        public static Sprite IconOf(WeaponData weapon)
        {
            if (weapon != null && weapon.Icon != null) return weapon.Icon;
            return Instance != null ? Instance.FallbackIcon : null;
        }

        public static Sprite AmmoIconOf(WeaponData weapon)
        {
            if (weapon != null && weapon.AmmoIcon != null) return weapon.AmmoIcon;
            return IconOf("ammo_box");
        }
    }
}
