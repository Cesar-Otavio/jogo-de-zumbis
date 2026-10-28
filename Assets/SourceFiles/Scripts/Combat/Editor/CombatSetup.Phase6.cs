using System.Linq;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Combat.EditorTools
{
    /* Phase 6 (co-op) setup, safe to run again:
     *   1 - network prefabs (player, shared game), the scene's NetworkManager with the transport and session,
     *       the multiplayer menu
     *   2 - a Windows build for testing with a second instance (Builds/Win64)
     */
    public static partial class CombatSetup
    {
        private const string NetworkFolder = "Assets/SourceFiles/Network";

        [MenuItem("Tools/Combat/Phase 6/1 - Network Setup")]
        public static void SetupPhase6Network()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.path != BaseCombatScene)
            {
                Debug.LogError("Open " + BaseCombatScene + " first");
                return;
            }

            EnsureFolder(NetworkFolder);
            GameObject playerPrefab = NetworkPrefab("NetPlayer", typeof(NetPlayer));
            GameObject gamePrefab = NetworkPrefab("NetGame", typeof(NetGame));

            // Netcode keeps its own list of every network prefab in the project (Assets/DefaultNetworkPrefabs);
            // a second list with the same prefabs would register them twice
            const string listPath = "Assets/DefaultNetworkPrefabs.asset";
            var list = AssetDatabase.LoadAssetAtPath<NetworkPrefabsList>(listPath);
            if (list == null)
            {
                list = ScriptableObject.CreateInstance<NetworkPrefabsList>();
                AssetDatabase.CreateAsset(list, listPath);
            }

            AssetDatabase.DeleteAsset($"{NetworkFolder}/NetworkPrefabs.asset");

            foreach (GameObject prefab in new[] { playerPrefab, gamePrefab })
            {
                if (list.PrefabList.All(p => p.Prefab != prefab)) list.Add(new NetworkPrefab { Prefab = prefab });
            }

            EditorUtility.SetDirty(list);

            // the network manager, its transport and the session wrapper
            GameObject root = FindOrCreate("NetworkManager");
            var manager = Ensure<NetworkManager>(root);
            var transport = Ensure<UnityTransport>(root);
            Ensure<NetSession>(root);
            var prefabs = Ensure<NetSessionPrefabs>(root);
            manager.NetworkConfig.NetworkTransport = transport;
            manager.NetworkConfig.PlayerPrefab = playerPrefab;
            manager.NetworkConfig.Prefabs.NetworkPrefabsLists.RemoveAll(l => l == null || l != list);
            if (!manager.NetworkConfig.Prefabs.NetworkPrefabsLists.Contains(list)) manager.NetworkConfig.Prefabs.NetworkPrefabsLists.Add(list);
            // one scene, already loaded everywhere: the game state is synchronised by our own objects
            manager.NetworkConfig.EnableSceneManagement = false;
            manager.NetworkConfig.ConnectionApproval = true;
            manager.NetworkConfig.TickRate = 30;
            manager.NetworkConfig.ProtocolVersion = NetSession.Protocol;
            transport.ConnectionData.Port = NetSession.DefaultPort;

            prefabs.Game = gamePrefab;
            prefabs.Character = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/PlayerRobot.prefab");
            // the same order on every machine: by asset name
            prefabs.Weapons = AssetDatabase.FindAssets("t:WeaponData", new[] { Root + "/Weapons" })
                .Select(g => AssetDatabase.LoadAssetAtPath<WeaponData>(AssetDatabase.GUIDToAssetPath(g)))
                .Where(w => w != null).OrderBy(w => w.name).ToArray();
            EditorUtility.SetDirty(manager);
            EditorUtility.SetDirty(prefabs);

            GameObject ui = FindOrCreate("NetworkUI");
            Ensure<NetMenuView>(ui);
            Ensure<NetAutoTest>(ui);

            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"Phase 6 network: player and game prefabs, NetworkManager, {prefabs.Weapons.Length} weapons in the shared list");
        }

        private static GameObject NetworkPrefab(string name, System.Type behaviour)
        {
            string path = $"{NetworkFolder}/{name}.prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null)
            {
                if (existing.GetComponent(behaviour) != null) return existing;
            }

            var go = new GameObject(name);
            go.AddComponent<NetworkObject>();
            go.AddComponent(behaviour);
            // the shared world (pickups, doors, barricades, chests, power) travels with the match
            if (behaviour == typeof(NetGame)) go.AddComponent<NetWorld>();
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return prefab;
        }

        [MenuItem("Tools/Combat/Phase 6/2 - Windows Build (test)")]
        public static void BuildPhase6Windows()
        {
            string[] scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            if (!scenes.Contains(BaseCombatScene)) scenes = new[] { BaseCombatScene }.Concat(scenes).ToArray();
            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = "Builds/Win64/BaseMilitar.exe",
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            };
            UnityEditor.Build.Reporting.BuildReport report = BuildPipeline.BuildPlayer(options);
            var summary = report.summary;
            Debug.Log($"Build {summary.result}: {summary.outputPath}, {summary.totalSize / (1024 * 1024)} MB, {summary.totalTime.TotalSeconds:F0} s, {summary.totalErrors} erros");
        }
    }
}
