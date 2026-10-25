using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Rendering;
using UnityEngine;

namespace Combat.EditorTools
{
    /* Keeps editor-only packages out of the player build. The AI Inference package (Sentis) ships its
     * shaders in a Resources folder, so every build compiled ~100 shaders and compute kernels the game never
     * uses (hundreds of warnings, extra size). The package stays installed for the editor tools; only its
     * shader variants are dropped from players.
     */
    internal class BuildStripping : IPreprocessShaders, IPreprocessComputeShaders
    {
        private static readonly string[] EditorOnlyPackages = { "Packages/com.unity.ai.inference/" };

        public int callbackOrder => 0;

        public void OnProcessShader(Shader shader, ShaderSnippetData snippet, IList<ShaderCompilerData> data)
        {
            if (IsEditorOnly(shader)) data.Clear();
        }

        public void OnProcessComputeShader(ComputeShader shader, string kernelName, IList<ShaderCompilerData> data)
        {
            if (IsEditorOnly(shader)) data.Clear();
        }

        private static bool IsEditorOnly(Object asset)
        {
            string path = AssetDatabase.GetAssetPath(asset);
            foreach (string package in EditorOnlyPackages)
                if (path.StartsWith(package)) return true;
            return false;
        }
    }
}
