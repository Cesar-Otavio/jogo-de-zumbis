using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Combat.EditorTools
{
    /* Builds the player's locomotion animator from the clips in Assets/SourceFiles/Animation/Mixamo
     * (names in Docs/FASE3_ASSETS.md). It works on a COPY of the Starter Assets controller, assigned
     * only to the player, so the soldiers (which share the original) are not affected.
     *
     *   - 2D freeform directional blend for walking/running (MoveX, MoveY in m/s, set by the controller)
     *   - a crouch state with its own 2D blend (Crouched bool) when the crouch clips exist;
     *     the controller then stops faking the crouch with hip IK
     *   - prone states (Prone bool) when the prone clips exist; prone stays disabled otherwise
     *
     * Missing clips are listed and nothing is changed: no half-built animator.
     */
    public static partial class CombatSetup
    {
        private const string MixamoFolder = "Assets/SourceFiles/Animation/Mixamo";
        private const string PlayerControllerPath = "Assets/SourceFiles/Animation/Player_Locomotion.controller";
        private const string StarterControllerPath = "Assets/SourceFiles/StarterAssets/ThirdPersonController/Character/Animations/StarterAssetsThirdPerson.controller";

        [MenuItem("Tools/Combat/Phase 3/6 - Build Locomotion Animator (needs Mixamo clips)")]
        public static void SetupPhase3Locomotion()
        {
            EnsureFolder(MixamoFolder);
            Dictionary<string, AnimationClip> clips = LoadClips(MixamoFolder);

            string[] required = { "Walk_Back", "Strafe_Left", "Strafe_Right" };
            string[] missing = required.Where(n => !clips.ContainsKey(n)).ToArray();
            if (missing.Length > 0)
            {
                Debug.LogWarning("Locomotion animator not built. Missing clips in " + MixamoFolder + ": " + string.Join(", ", missing) +
                                 ". See Docs/FASE3_ASSETS.md (3.1) for how to get them.");
                return;
            }

            ConfigureImports(MixamoFolder);
            clips = LoadClips(MixamoFolder);

            if (AssetDatabase.LoadAssetAtPath<AnimatorController>(PlayerControllerPath) == null)
            {
                AssetDatabase.CopyAsset(StarterControllerPath, PlayerControllerPath);
            }

            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(PlayerControllerPath);
            AddParameter(controller, "MoveX", AnimatorControllerParameterType.Float);
            AddParameter(controller, "MoveY", AnimatorControllerParameterType.Float);

            AnimatorStateMachine machine = controller.layers[0].stateMachine;
            Motion idle = FindStarterClip("Idle");
            Motion walk = FindStarterClip("Walk_N");
            Motion run = FindStarterClip("Run_N");

            // locomotion: one 2D tree in m/s, walking at ~2 and running at ~6
            var blend = new BlendTree { name = "Locomotion2D", blendType = BlendTreeType.FreeformDirectional2D, blendParameter = "MoveX", blendParameterY = "MoveY", hideFlags = HideFlags.HideInHierarchy };
            AssetDatabase.AddObjectToAsset(blend, controller);
            blend.AddChild(idle, Vector2.zero);
            blend.AddChild(walk, new Vector2(0.0f, 2.0f));
            blend.AddChild(clips["Walk_Back"], new Vector2(0.0f, -2.0f));
            blend.AddChild(clips["Strafe_Left"], new Vector2(-2.0f, 0.0f));
            blend.AddChild(clips["Strafe_Right"], new Vector2(2.0f, 0.0f));
            AddIf(blend, clips, "Walk_FwdLeft", new Vector2(-1.41f, 1.41f));
            AddIf(blend, clips, "Walk_FwdRight", new Vector2(1.41f, 1.41f));
            AddIf(blend, clips, "Walk_BackLeft", new Vector2(-1.41f, -1.41f));
            AddIf(blend, clips, "Walk_BackRight", new Vector2(1.41f, -1.41f));
            blend.AddChild(run, new Vector2(0.0f, 6.0f));
            AddIf(blend, clips, "Run_Back", new Vector2(0.0f, -5.0f));
            AddIf(blend, clips, "Run_Strafe_Left", new Vector2(-5.0f, 0.0f));
            AddIf(blend, clips, "Run_Strafe_Right", new Vector2(5.0f, 0.0f));

            AnimatorState locomotion = machine.states.Select(s => s.state).FirstOrDefault(s => s.name == "Idle Walk Run Blend");
            if (locomotion != null) locomotion.motion = blend;

            // crouch
            if (clips.ContainsKey("Crouch_Idle") && clips.ContainsKey("Crouch_Walk_Fwd"))
            {
                AddParameter(controller, "Crouched", AnimatorControllerParameterType.Bool);
                var crouchBlend = new BlendTree { name = "Crouch2D", blendType = BlendTreeType.FreeformDirectional2D, blendParameter = "MoveX", blendParameterY = "MoveY", hideFlags = HideFlags.HideInHierarchy };
                AssetDatabase.AddObjectToAsset(crouchBlend, controller);
                crouchBlend.AddChild(clips["Crouch_Idle"], Vector2.zero);
                crouchBlend.AddChild(clips["Crouch_Walk_Fwd"], new Vector2(0.0f, 1.4f));
                AddIf(crouchBlend, clips, "Crouch_Walk_Back", new Vector2(0.0f, -1.4f));
                AddIf(crouchBlend, clips, "Crouch_Walk_Left", new Vector2(-1.4f, 0.0f));
                AddIf(crouchBlend, clips, "Crouch_Walk_Right", new Vector2(1.4f, 0.0f));
                AnimatorState crouch = State(machine, "Crouch", crouchBlend);
                Link(locomotion, crouch, "Crouched", true, 0.2f);
                Link(crouch, locomotion, "Crouched", false, 0.25f);
            }

            // prone (never enabled without these)
            if (clips.ContainsKey("Prone_Idle") && clips.ContainsKey("Prone_Crawl"))
            {
                AddParameter(controller, "Prone", AnimatorControllerParameterType.Bool);
                var proneBlend = new BlendTree { name = "Prone1D", blendType = BlendTreeType.Simple1D, blendParameter = "Speed", hideFlags = HideFlags.HideInHierarchy };
                AssetDatabase.AddObjectToAsset(proneBlend, controller);
                proneBlend.AddChild(clips["Prone_Idle"], 0.0f);
                proneBlend.AddChild(clips["Prone_Crawl"], 0.7f);
                AnimatorState prone = State(machine, "Prone", proneBlend);
                AnimatorState from = machine.states.Select(s => s.state).FirstOrDefault(s => s.name == "Crouch") ?? locomotion;
                Link(from, prone, "Prone", true, clips.ContainsKey("Stand_To_Prone") ? 0.1f : 0.35f);
                Link(prone, from, "Prone", false, 0.35f);
            }

            EditorUtility.SetDirty(controller);
            EditPrefab("Assets/Prefabs/PlayerRobot.prefab", root =>
            {
                var animator = root.GetComponentInChildren<StarterAssets.ThirdPersonController>(true).GetComponent<Animator>();
                animator.runtimeAnimatorController = controller;
            });
            AssetDatabase.SaveAssets();
            Debug.Log("Player locomotion animator built: " + PlayerControllerPath);
        }

        private static Dictionary<string, AnimationClip> LoadClips(string folder)
        {
            var clips = new Dictionary<string, AnimationClip>();
            foreach (string guid in AssetDatabase.FindAssets("t:AnimationClip", new[] { folder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string file = System.IO.Path.GetFileNameWithoutExtension(path);
                foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(path))
                {
                    if (asset is AnimationClip clip && !clip.name.StartsWith("__preview")) clips[file] = clip;
                }
            }

            return clips;
        }

        // Humanoid on the player's avatar; locomotion loops with root motion baked into the pose
        private static void ConfigureImports(string folder)
        {
            var avatar = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/PlayerRobot.prefab").GetComponentInChildren<Animator>().avatar;
            foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { folder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var importer = AssetImporter.GetAtPath(path) as ModelImporter;
                if (importer == null) continue;
                importer.animationType = ModelImporterAnimationType.Human;
                importer.avatarSetup = ModelImporterAvatarSetup.CopyFromOther;
                importer.sourceAvatar = avatar;
                string file = System.IO.Path.GetFileNameWithoutExtension(path);
                bool loop = !file.StartsWith("Stand_") && !file.Contains("_To_") && !file.StartsWith("Hit_") && !file.StartsWith("Roll") && !file.StartsWith("Melee") && !file.StartsWith("Land") && !file.StartsWith("Jump") && !file.StartsWith("Interact") && !file.StartsWith("Use_") && !file.StartsWith("Run_Start") && !file.StartsWith("Run_Stop") && !file.StartsWith("Turn");
                ModelImporterClipAnimation[] takes = importer.defaultClipAnimations;
                foreach (ModelImporterClipAnimation take in takes)
                {
                    take.loopTime = loop;
                    take.lockRootRotation = true;
                    take.lockRootHeightY = true;
                    take.lockRootPositionXZ = loop;
                    take.keepOriginalOrientation = true;
                    take.keepOriginalPositionY = true;
                }

                importer.clipAnimations = takes;
                importer.SaveAndReimport();
            }
        }

        private static Motion FindStarterClip(string name)
        {
            foreach (string guid in AssetDatabase.FindAssets(name + " t:AnimationClip", new[] { "Assets/SourceFiles/StarterAssets" }))
            {
                foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GUIDToAssetPath(guid)))
                {
                    if (asset is AnimationClip clip && clip.name == name) return clip;
                }
            }

            return null;
        }

        private static void AddIf(BlendTree tree, Dictionary<string, AnimationClip> clips, string name, Vector2 position)
        {
            if (clips.TryGetValue(name, out AnimationClip clip)) tree.AddChild(clip, position);
        }

        private static void AddParameter(AnimatorController controller, string name, AnimatorControllerParameterType type)
        {
            if (controller.parameters.All(p => p.name != name)) controller.AddParameter(name, type);
        }

        private static AnimatorState State(AnimatorStateMachine machine, string name, Motion motion)
        {
            AnimatorState state = machine.states.Select(s => s.state).FirstOrDefault(s => s.name == name) ?? machine.AddState(name);
            state.motion = motion;
            return state;
        }

        private static void Link(AnimatorState from, AnimatorState to, string parameter, bool value, float duration)
        {
            if (from == null || to == null) return;
            foreach (AnimatorStateTransition existing in from.transitions)
            {
                if (existing.destinationState == to) from.RemoveTransition(existing);
            }

            AnimatorStateTransition transition = from.AddTransition(to);
            transition.hasExitTime = false;
            transition.duration = duration;
            transition.AddCondition(value ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, 0.0f, parameter);
        }
    }
}
