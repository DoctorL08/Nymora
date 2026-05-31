using System.IO;
using UnityEditor;
using UnityEngine;

namespace Nymora.Editor.Setup
{
    /// <summary>
    /// POLISH KYAMI — Tâche 8 (maps) + 10 (shaders maps) : les maps HUB et ARENE utilisent DÉJÀ le
    /// matériau lit Sprite-Lit-Default (URP 2D) mais N'ONT PAS de normal map → elles rendent plates
    /// sous les 2D lights. « Revoir les shaders » des maps = en fait leur appliquer la normal map.
    ///
    /// Ce tool monte les normals AUTHORED par Kyami en texture secondaire "_NormalMap" :
    ///   • HUB   : map_hub.png            ← map_hub__normal.png (single)
    ///   • ARENE : Arene1vs1_01..12.png   ← arene_retouche_animation1..12_normal.png (par index)
    ///
    /// Les bases existent déjà en projet (le pack ne fournit QUE les normals). On NE TOUCHE NI au
    /// matériau (déjà lit), NI au shader, NI aux Light2D. L'import de la normal copie filtre +
    /// compression de la base (look inchangé) et force sRGB OFF (espace linéaire, sinon relief faux).
    /// View-only : pas de bump version.
    ///
    /// Menu : Nymora > Setup > Polish Kyami > Apply Map Normal Maps (HUB + ARENE).
    /// </summary>
    public static class ApplyMapNormalMapsTool
    {
        private const string SrcMapRoot = "C:/Users/Lorenzo/Downloads/Polish_Kyami/Polish/map";

        // HUB
        private const string HubBase      = "Assets/_Nymora/Art/Hub/map_hub.png";
        private const string HubNormalSrc = SrcMapRoot + "/map_hub_normal/map_hub__normal.png";
        private const string HubNormalDst = "Assets/_Nymora/Art/Hub/map_hub_normal.png";

        // ARENE (12 frames)
        private const string AreneBaseDir      = "Assets/_Nymora/Art/UI/Maps/Arene1vs1_Anim";
        private const string AreneNormalSrcDir = SrcMapRoot + "/map_arene_normal";
        private const string AreneNormalDstDir = AreneBaseDir + "/normals";

        [MenuItem("Nymora/Setup/Polish Kyami/Apply Map Normal Maps (HUB + ARENE)", priority = 64)]
        public static void Run()
        {
            int ok = 0, fail = 0;
            var report = new System.Collections.Generic.List<string>();

            // --- HUB ---
            if (AssignNormal(HubBase, HubNormalSrc, HubNormalDst, report)) ok++; else fail++;

            // --- ARENE 01..12 ---
            EnsureFolder(AreneNormalDstDir);
            for (int n = 1; n <= 12; n++)
            {
                string baseP   = $"{AreneBaseDir}/Arene1vs1_{n:00}.png";
                string srcN    = $"{AreneNormalSrcDir}/arene_retouche_animation{n}_normal.png";
                string dstN    = $"{AreneNormalDstDir}/Arene1vs1_{n:00}_normal.png";
                if (AssignNormal(baseP, srcN, dstN, report)) ok++; else fail++;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[ApplyMapNormalMaps]\n" + string.Join("\n", report));
            EditorUtility.DisplayDialog("Apply Map Normal Maps",
                $"{ok} normal(s) assignée(s), {fail} échec(s).\n\n" + string.Join("\n", report) +
                "\n\nMatériau/shader/lights inchangés. Lance le hub + un combat : le sol doit prendre du relief sous les lights.\n" +
                "⚠ Si le relief de l'arène 'clignote' mal, l'ordre des 12 frames normales ne matche pas → dis-le moi.",
                "OK");
        }

        /// <summary>Copie la normal dans le projet, la configure, et la monte en secondaire "_NormalMap" sur la base.</summary>
        private static bool AssignNormal(string basePath, string normalSrc, string normalDst, System.Collections.Generic.List<string> report)
        {
            if (!File.Exists(basePath))
            {
                report.Add($"⚠ base absente : {basePath}");
                return false;
            }
            if (!File.Exists(normalSrc))
            {
                report.Add($"⚠ normal source absente : {normalSrc}");
                return false;
            }

            File.Copy(normalSrc, normalDst, true);
            AssetDatabase.ImportAsset(normalDst, ImportAssetOptions.ForceUpdate);

            // Importer de la normal : calé sur la base (filtre/compression) + sRGB OFF obligatoire.
            var baseTi = AssetImporter.GetAtPath(basePath) as TextureImporter;
            if (AssetImporter.GetAtPath(normalDst) is TextureImporter nti)
            {
                nti.textureType = TextureImporterType.Default;
                nti.spriteImportMode = SpriteImportMode.None;
                nti.sRGBTexture = false; // espace linéaire (relief correct)
                nti.mipmapEnabled = baseTi != null && baseTi.mipmapEnabled;
                nti.filterMode = baseTi != null ? baseTi.filterMode : FilterMode.Bilinear;
                nti.textureCompression = baseTi != null ? baseTi.textureCompression : TextureImporterCompression.Compressed;
                nti.wrapMode = TextureWrapMode.Clamp;
                nti.isReadable = false;
                nti.SaveAndReimport();
            }

            var normalTex = AssetDatabase.LoadAssetAtPath<Texture2D>(normalDst);
            if (normalTex == null)
            {
                report.Add($"⚠ normal illisible après import : {normalDst}");
                return false;
            }

            if (AssetImporter.GetAtPath(basePath) is not TextureImporter ti)
            {
                report.Add($"⚠ base sans TextureImporter : {basePath}");
                return false;
            }

            ti.secondarySpriteTextures = new[]
            {
                new SecondarySpriteTexture { name = "_NormalMap", texture = normalTex },
            };
            ti.SaveAndReimport();

            report.Add($"✓ {Path.GetFileName(basePath)} ← {Path.GetFileName(normalDst)}");
            return true;
        }

        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;
            string parent = Path.GetDirectoryName(folder)?.Replace('\\', '/');
            if (string.IsNullOrEmpty(parent)) return;
            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
        }
    }
}
