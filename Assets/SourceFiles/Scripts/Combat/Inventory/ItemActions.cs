using UnityEngine;

namespace Combat
{
    /* What the backpack screen can do with a stack, and the checks behind each button, so the UI
     * only shows valid actions and every action goes through one place.
     */
    public static class ItemActions
    {
        public static PlayerCombat Player => InventoryManager.Player != null ? InventoryManager.Player.GetComponent<PlayerCombat>() : null;

        // null when it can be used, otherwise why not
        public static string CannotUse(string id)
        {
            ItemDefinition item = ItemCatalog.Find(id);
            PlayerCombat player = Player;
            if (item == null || player == null) return "Indisponível";
            switch (item.Use)
            {
                case ItemUse.Heal:
                    return player.Health.Current >= player.Health.MaxHealth - 0.5f ? "Vida cheia" : null;
                case ItemUse.Armor:
                    return player.Health.Armor >= 59.0f ? "Colete cheio" : null;
                case ItemUse.Ammo:
                    return player.Current == null && player.Other == null ? "Sem arma" : null;
                default:
                    return Backpack.TryQuickItem(id, out _) ? "Use pela barra rápida" : item.Category == ItemCategory.Material ? "Material de fabricação" : "Não pode ser usado";
            }
        }

        public static bool Use(int slot)
        {
            ItemStack stack = Backpack.Slot(slot);
            if (stack == null || CannotUse(stack.Id) != null) return false;
            ItemDefinition item = stack.Definition;
            PlayerCombat player = Player;
            if (!Backpack.RemoveAt(slot, 1)) return false;

            switch (item.Use)
            {
                case ItemUse.Heal:
                    player.Health.Heal(item.UseAmount);
                    CampaignAssets.Play(a => a.MedkitUse, player.transform.position + Vector3.up, 0.7f, 15.0f);
                    player.ShowNotice($"<color=#66FF88>+{Mathf.RoundToInt(item.UseAmount)} vida</color>");
                    break;
                case ItemUse.Armor:
                    player.Health.Armor = Mathf.Min(60.0f, player.Health.Armor + item.UseAmount);
                    player.ShowNotice($"Colete reforçado ({Mathf.RoundToInt(player.Health.Armor)}/60)");
                    break;
                case ItemUse.Ammo:
                    foreach (WeaponInstance weapon in player.Slots)
                    {
                        if (weapon == null) continue;
                        weapon.Reserve += weapon.Data.MagazineSize * Mathf.Max(1, Mathf.RoundToInt(item.UseAmount));
                    }

                    player.ShowNotice("Munição reabastecida");
                    break;
            }

            RunState.BasicOnly = false;
            return true;
        }

        // the stack is dropped at the player's feet as a pickup, never destroyed
        public static bool Drop(int slot, int amount)
        {
            ItemStack stack = Backpack.Slot(slot);
            PlayerCombat player = Player;
            if (stack == null || player == null) return false;
            ItemDefinition item = stack.Definition;
            if (item != null && !item.Droppable) return false;
            string id = stack.Id;
            amount = Mathf.Clamp(amount, 1, stack.Count);
            if (!Backpack.RemoveAt(slot, amount)) return false;
            Vector3 at = player.transform.position + player.transform.forward * 0.9f + Vector3.up * 0.1f;
            // co-op client: the host puts it on the ground for everyone
            NetWorld.PlayerDropped(WorldItem.Spawn(id, amount, at));
            return true;
        }
    }
}
