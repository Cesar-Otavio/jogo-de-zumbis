using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;

namespace Combat.EditorTools
{
    /* Phase 7, Marco 4: the game's AudioMixer (Resources/Audio/GameMixer). Master with Music, SFX,
     * Dialogue, Radio, Ambient and UI under it, each group's volume exposed as "Vol_<group>" so the game
     * can duck it. Unity has no public API to build a mixer's groups, so this goes through the editor's
     * own (internal) AudioMixerController, the same calls the Audio Mixer window makes. Running it again
     * only adds what's missing.
     */
    public static partial class CombatSetup
    {
        public const string MixerPath = "Assets/SourceFiles/Resources/Audio/GameMixer.mixer";

        [MenuItem("Tools/Combat/Phase 7/6 - Audio Mixer (groups and ducking)")]
        public static void SetupPhase7AudioMixer()
        {
            Type controllerType = EditorType("UnityEditor.Audio.AudioMixerController");
            Type groupType = EditorType("UnityEditor.Audio.AudioMixerGroupController");
            Type pathType = EditorType("UnityEditor.Audio.AudioGroupParameterPath");
            const BindingFlags any = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

            Directory.CreateDirectory(Path.GetDirectoryName(MixerPath));
            UnityEngine.Object controller = AssetDatabase.LoadAssetAtPath(MixerPath, controllerType);
            if (controller == null)
            {
                controller = (UnityEngine.Object)Method(controllerType, "CreateMixerControllerAtPath", 1).Invoke(null, new object[] { MixerPath });
                Debug.Log($"[Audio] mixer created at {MixerPath}");
            }

            var master = (AudioMixerGroup)controllerType.GetProperty("masterGroup", any).GetValue(controller);
            foreach (string name in AudioRouting.GroupNames)
            {
                if (name == "Master" || Children(master, groupType).Any(g => g.name == name)) continue;
                object group = Method(controllerType, "CreateNewGroup", 2).Invoke(controller, new object[] { name, false });
                Method(controllerType, "AddChildToParent", 2).Invoke(controller, new[] { group, master });
                // only the Audio Mixer window's view of it; a new mixer may have no view yet
                MethodInfo toView = controllerType.GetMethod("AddGroupToCurrentView", any);
                try { if (toView != null) toView.Invoke(controller, new[] { group }); }
                catch (TargetInvocationException) { }
                Debug.Log($"[Audio] group {name} added");
            }

            // expose each group's volume under a fixed name
            PropertyInfo exposed = controllerType.GetProperty("exposedParameters", any);
            var groups = new List<AudioMixerGroup> { master };
            groups.AddRange(Children(master, groupType));
            foreach (AudioMixerGroup group in groups)
            {
                string wanted = AudioRouting.VolumeParameter(group.name);
                if (((AudioMixer)controller).GetFloat(wanted, out _)) continue;
                var guid = Method(groupType, "GetGUIDForVolume", 0).Invoke(group, null);
                object path = Activator.CreateInstance(pathType, group, guid);
                Method(controllerType, "AddExposedParameter", 1).Invoke(controller, new[] { path });

                // the new one is named "MyExposedParam..."; rename it
                var list = (Array)exposed.GetValue(controller);
                for (int i = 0; i < list.Length; i++)
                {
                    object parameter = list.GetValue(i);
                    FieldInfo guidField = parameter.GetType().GetField("guid", any);
                    FieldInfo nameField = parameter.GetType().GetField("name", any);
                    if (!Equals(guidField.GetValue(parameter), guid)) continue;
                    nameField.SetValue(parameter, wanted);
                    list.SetValue(parameter, i);
                }

                exposed.SetValue(controller, list);
                Debug.Log($"[Audio] {group.name} volume exposed as {wanted}");
            }

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();

            var mixer = (AudioMixer)controller;
            string report = string.Join(", ", AudioRouting.GroupNames.Select(n =>
                $"{n} {(mixer.FindMatchingGroups(n).Any(g => g.name == n) ? "ok" : "MISSING")}/{(mixer.GetFloat(AudioRouting.VolumeParameter(n), out float v) ? $"{v:F0} dB" : "not exposed")}"));
            Debug.Log($"[Audio] {MixerPath}: {report}");
        }

        private static IEnumerable<AudioMixerGroup> Children(AudioMixerGroup group, Type groupType)
        {
            var children = (Array)groupType.GetProperty("children", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).GetValue(group);
            return children.Cast<AudioMixerGroup>();
        }

        private static Type EditorType(string name)
        {
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type type = assembly.GetType(name);
                if (type != null) return type;
            }

            throw new InvalidOperationException($"{name} not found in this Unity version");
        }

        private static MethodInfo Method(Type type, string name, int parameters)
        {
            MethodInfo method = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static)
                .FirstOrDefault(m => m.Name == name && m.GetParameters().Length == parameters);
            if (method != null) return method;
            string known = string.Join(", ", type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static)
                .Where(m => m.Name.Contains(name.Substring(0, Math.Min(6, name.Length)))).Select(m => m.Name + "/" + m.GetParameters().Length));
            throw new InvalidOperationException($"{type.Name}.{name}({parameters}) not found; similar: {known}");
        }
    }
}
