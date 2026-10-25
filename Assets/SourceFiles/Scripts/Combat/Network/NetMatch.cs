using Unity.Netcode;
using UnityEngine;

namespace Combat
{
    /* The local side of a co-op match: starting it on this machine when the host says so, where this
     * player appears, the shared weapon list, and the small messages a match shows.
     */
    public static class NetMatch
    {
        // relative number of infected by number of players (the difficulty's own multipliers still apply)
        private static readonly float[] EnemyScale = { 1.0f, 1.0f, 1.55f, 2.1f, 2.65f };

        public static float EnemyCountFor(int players) => EnemyScale[Mathf.Clamp(players, 1, 4)];

        public static void Begin(GameMode mode)
        {
            if (GameFlow.Instance == null) return;
            if (NetMenuView.Instance != null) NetMenuView.Instance.Hide();
            GameFlow.Instance.StartGame(mode);

            // a client spawns no infected of its own; anything already standing in its scene goes
            if (!NetworkManager.Singleton.IsServer)
            {
                foreach (ZombieAI zombie in Object.FindObjectsByType<ZombieAI>()) Object.Destroy(zombie.gameObject);
                foreach (EnemyShooterAI soldier in Object.FindObjectsByType<EnemyShooterAI>()) Object.Destroy(soldier.gameObject);
            }

            PlaceLocalPlayer();
            Notice(NetworkManager.Singleton.IsServer ? "Partida cooperativa iniciada" : "Você entrou na partida do Host");
        }

        // spawn points side by side from the level's start, each one checked for a free spot
        private static void PlaceLocalPlayer()
        {
            NetPlayer me = NetPlayer.Local;
            PlayerCombat player = Object.FindAnyObjectByType<PlayerCombat>();
            if (me == null || player == null) return;
            int index = Mathf.Max(0, me.SpawnIndex.Value);
            Transform body = player.transform;
            Vector3[] offsets = { Vector3.zero, new Vector3(2.5f, 0, 0), new Vector3(-2.5f, 0, 0), new Vector3(0, 0, -2.5f) };
            Vector3 wanted = body.position + body.rotation * offsets[index % offsets.Length];
            Vector3 spot = FreeSpot(wanted, body.position);
            var controller = body.GetComponent<CharacterController>();
            if (controller != null) controller.enabled = false;
            body.position = spot;
            if (controller != null) controller.enabled = true;
        }

        private static Vector3 FreeSpot(Vector3 wanted, Vector3 fallback)
        {
            int mask = CombatLayers.Sight & ~(1 << CombatLayers.Player);
            for (int ring = 0; ring < 4; ring++)
            {
                for (int step = 0; step < (ring == 0 ? 1 : 8); step++)
                {
                    float angle = step * 45.0f * Mathf.Deg2Rad;
                    Vector3 p = wanted + new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * ring * 1.5f;
                    if (!Physics.Raycast(p + Vector3.up * 3.0f, Vector3.down, out RaycastHit ground, 8.0f, mask, QueryTriggerInteraction.Ignore)) continue;
                    if (Physics.CheckCapsule(ground.point + Vector3.up * 0.4f, ground.point + Vector3.up * 1.6f, 0.35f, mask, QueryTriggerInteraction.Ignore)) continue;
                    return ground.point + Vector3.up * 0.05f;
                }
            }

            return fallback;
        }

        // ---------------------------------------------------------------- weapons by number

        public static int WeaponIndex(WeaponData data)
        {
            NetSessionPrefabs prefabs = Prefabs;
            if (data == null || prefabs == null || prefabs.Weapons == null) return 255;
            WeaponData basic = WeaponUpgrades.BaseOf(data);
            for (int i = 0; i < prefabs.Weapons.Length; i++)
            {
                if (prefabs.Weapons[i] == basic || prefabs.Weapons[i] == data) return i;
            }

            return 255;
        }

        public static WeaponData Weapon(byte index)
        {
            NetSessionPrefabs prefabs = Prefabs;
            if (index == 255 || prefabs == null || prefabs.Weapons == null || index >= prefabs.Weapons.Length) return null;
            return prefabs.Weapons[index];
        }

        private static NetSessionPrefabs Prefabs => NetworkManager.Singleton != null ? NetworkManager.Singleton.GetComponent<NetSessionPrefabs>() : null;

        // ---------------------------------------------------------------- messages

        public static void Notice(string text)
        {
            PlayerCombat player = Object.FindAnyObjectByType<PlayerCombat>();
            if (player != null && GameFlow.Instance != null && GameFlow.Instance.Current == GameFlow.State.Playing) player.ShowNotice(text);
            if (NetMenuView.Instance != null) NetMenuView.Instance.Log(text);
            Debug.Log("[Net] " + text);
        }

        public static void KillConfirmed(bool headshot)
        {
            if (GameFlow.Instance != null) GameFlow.Instance.CreditNetworkKill(headshot);
        }
    }
}
