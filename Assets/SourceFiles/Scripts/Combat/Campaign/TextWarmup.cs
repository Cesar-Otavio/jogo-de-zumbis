using TMPro;
using UnityEngine;

namespace Combat
{
    /* The UI font has a dynamic fallback: a character seen for the first time (an accent, a digit in a
     * new size) is rasterised into its atlas right then, which showed up as UI frames of 25-30 ms in
     * play. Everything the game's texts use is added once at load instead, hidden by the loading.
     * Only in players: in the editor the fallback is an asset, and filling it would change the file.
     */
    public static class TextWarmup
    {
        private const string Characters =
            "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789" +
            "ÁÀÂÃÉÊÍÓÔÕÚÜÇáàâãéêíóôõúüç" +
            " .,;:!?¿¡'\"()[]{}<>+-*/=%#&@_|~^°ºª·•…–—«»×÷±→←↑↓✓";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Warm()
        {
            if (Application.isEditor) return;
            TMP_FontAsset font = TMP_Settings.defaultFontAsset;
            if (font == null) return;
            font.TryAddCharacters(Characters, out string _);
            if (font.fallbackFontAssetTable == null) return;
            foreach (TMP_FontAsset fallback in font.fallbackFontAssetTable)
            {
                if (fallback != null && fallback.atlasPopulationMode == AtlasPopulationMode.Dynamic) fallback.TryAddCharacters(Characters, out string _);
            }
        }
    }
}
