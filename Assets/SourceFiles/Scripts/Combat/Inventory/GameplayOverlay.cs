using StarterAssets;
using UnityEngine;

namespace Combat
{
    /* Screens opened over live gameplay (backpack, tactical map): one at a time, the cursor free,
     * the guns quiet and the world slowed down (not stopped: the danger stays real, there's just
     * time to think). Single player only; a networked game would keep the time scale at 1.
     */
    public static class GameplayOverlay
    {
        public const float SlowMotion = 0.25f;

        public static string Open { get; private set; }
        public static bool Any => Open != null;

        // false when something else owns the screen (menu, pause, shop, a document, a cinematic)
        public static bool CanOpen(string name)
        {
            if (Open != null && Open != name) return false;
            if (GameFlow.Instance == null || GameFlow.Instance.Current != GameFlow.State.Playing) return false;
            return !ShopManager.IsOpen && !CampaignHUD.ReaderOpen && !CinematicPlayer.Playing;
        }

        public static bool TryOpen(string name)
        {
            if (Open == name) return true;
            if (!CanOpen(name)) return false;
            Open = name;
            Time.timeScale = SlowMotion;
            Input(false);
            return true;
        }

        public static void Close(string name)
        {
            if (Open != name) return;
            Open = null;
            // pause or game over may have taken over meanwhile: leave their time scale alone
            if (GameFlow.Instance != null && GameFlow.Instance.Current == GameFlow.State.Playing)
            {
                Time.timeScale = 1.0f;
                Input(true);
            }
        }

        // a new run or the menu: nothing stays open
        public static void Reset()
        {
            Open = null;
        }

        // while open, the gun doesn't fire or aim from clicks meant for the UI
        public static void SuppressGameplayInput()
        {
            StarterAssetsInputs input = InventoryManager.Player != null ? InventoryManager.Player.GetComponent<StarterAssetsInputs>() : null;
            if (input == null) return;
            input.fire = false;
            input.aim = false;
            input.useItem = false;
            input.interact = false;
        }

        private static void Input(bool gameplay)
        {
            StarterAssetsInputs input = InventoryManager.Player != null ? InventoryManager.Player.GetComponent<StarterAssetsInputs>() : null;
            if (input != null) input.SetCursorLocked(gameplay);
        }
    }
}
