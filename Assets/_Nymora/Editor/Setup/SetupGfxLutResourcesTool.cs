using UnityEditor;
using UnityEngine;

namespace Nymora.Editor.Setup
{
    /// <summary>
    /// GFX — Copie les 4 LUT colorimétriques dans Resources/PostProcessing/ pour que les profils
    /// graphiques puissent les charger au runtime (Resources.Load) et swapper la LUT par ambiance.
    ///
    /// Copie (pas de déplacement) : les originaux + les références des Volume Profiles de base
    /// (Hub_PostFX / Combat_PostFX) restent intacts. Idempotent (skip si déjà copié).
    ///
    /// Menu : Nymora > Setup > Copy GFX LUTs to Resources.
    /// </summary>
    public static class SetupGfxLutResourcesTool
    {
        private const string SrcDir = "Assets/_Nymora/Settings/PostProcessing";
        private const string ResRoot = "Assets/_Nymora/Resources";
        private const string DstDir = "Assets/_Nymora/Resources/PostProcessing";

        private static readonly string[] Luts = { "LUT_Neutral", "LUT_Cinematic", "LUT_Cold", "LUT_Warm" };

        [MenuItem("Nymora/Setup/Copy GFX LUTs to Resources", priority = 63)]
        private static void Run()
        {
            if (!AssetDatabase.IsValidFolder(ResRoot))
            {
                EditorUtility.DisplayDialog("GFX LUTs", $"Dossier introuvable : {ResRoot}", "OK");
                return;
            }
            if (!AssetDatabase.IsValidFolder(DstDir))
                AssetDatabase.CreateFolder(ResRoot, "PostProcessing");

            int copied = 0, skipped = 0, missing = 0;
            foreach (var name in Luts)
            {
                string src = $"{SrcDir}/{name}.png";
                string dst = $"{DstDir}/{name}.png";

                if (AssetDatabase.LoadAssetAtPath<Texture>(dst) != null) { skipped++; continue; }
                if (AssetDatabase.LoadAssetAtPath<Texture>(src) == null)
                {
                    Debug.LogWarning($"[GfxLut] LUT source introuvable : {src}");
                    missing++;
                    continue;
                }
                if (AssetDatabase.CopyAsset(src, dst)) copied++;
                else Debug.LogWarning($"[GfxLut] Échec copie {src} -> {dst}");
            }

            AssetDatabase.Refresh();
            string msg = $"LUT copiées : {copied}\nDéjà présentes : {skipped}" +
                         (missing > 0 ? $"\nIntrouvables : {missing}" : "") +
                         "\n\nLes profils graphiques peuvent maintenant swapper la LUT par ambiance.";
            EditorUtility.DisplayDialog("GFX LUTs", msg, "OK");
            Debug.Log("[SetupGfxLutResourcesTool] " + msg);
        }
    }
}
