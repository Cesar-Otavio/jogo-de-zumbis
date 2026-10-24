using UnityEditor;
using UnityEditor.SceneManagement;

namespace Combat.EditorTools
{
    /* Pressing Play always starts the game from the first scene in the build (the game's menu),
     * whatever scene is open in the editor. The project's tutorials reopen their own scene on
     * startup, which made Play run the tutorial scene instead of the game.
     */
    [InitializeOnLoad]
    public static class PlayFromGameScene
    {
        private const string MenuPath = "Tools/Combat/Always Play From Game Scene";
        private const string PrefsKey = "Combat.PlayFromGameScene";

        static PlayFromGameScene()
        {
            EditorApplication.delayCall += Apply;
        }

        private static bool Enabled
        {
            get => EditorPrefs.GetBool(PrefsKey, true);
            set => EditorPrefs.SetBool(PrefsKey, value);
        }

        [MenuItem(MenuPath)]
        private static void Toggle()
        {
            Enabled = !Enabled;
            Apply();
        }

        [MenuItem(MenuPath, true)]
        private static bool ToggleValidate()
        {
            Menu.SetChecked(MenuPath, Enabled);
            return true;
        }

        public static void Apply()
        {
            SceneAsset start = null;
            if (Enabled)
            {
                foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
                {
                    if (!scene.enabled) continue;
                    start = AssetDatabase.LoadAssetAtPath<SceneAsset>(scene.path);
                    if (start != null) break;
                }
            }

            EditorSceneManager.playModeStartScene = start;
        }
    }
}
