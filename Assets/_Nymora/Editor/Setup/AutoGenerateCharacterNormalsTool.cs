using System.Collections.Generic;
using System.IO;
using Nymora.Editor.Generators;
using UnityEditor;
using UnityEngine;

namespace Nymora.Editor.Setup
{
    /// <summary>
    /// POLISH KYAMI — STOPGAP normals persos. Diagnostic runtime : les normal maps perso/skins
    /// fournies par Kyami sont malformées (bleu ~107 au lieu de ~255 → pas de relief), alors que
    /// celles des maps sont correctes. En attendant un ré-export propre, ce tool AUTO-GÉNÈRE des
    /// normals depuis le diffuse de chaque frame (luminance -> Sobel, via
    /// <see cref="NormalMapGeneratorTool.GenerateAndAssign"/>) et les assigne en _NormalMap —
    /// REMPLAÇANT la normal Kyami foireuse sur chaque sprite.
    ///
    /// Couvre les 5 classes (base) + 5 skins. Les normals générées sont écrites à côté des sheets
    /// (`&lt;frame&gt;_normal.png` dans .../sheets/), donc relançable. Qualité « auto » (correcte mais
    /// pas hand-authored) — à remplacer par les vraies de Kyami quand il aura ré-exporté.
    ///
    /// ⚠ Modifie l'import des sprites (secondary texture) → View-only, pas de bump version.
    /// Si l'éclairage paraît inversé verticalement, repasse avec <see cref="FlipY"/> = true.
    ///
    /// Menu : Nymora > Setup > Polish Kyami > Auto-Generate Character Normals (stopgap).
    /// </summary>
    public static class AutoGenerateCharacterNormalsTool
    {
        // Paramètres de génération (cf NormalMapGeneratorTool).
        private const float Strength = 2.5f;
        private const int Blur = 1;
        private const bool InvertHeight = false;
        private const bool FlipY = false;

        private static readonly string[] ClassSheetDirs =
        {
            "Assets/_Nymora/Art/Sprites/Colossar/Base/sheets",
            "Assets/_Nymora/Art/Sprites/Soulrender/Base/sheets",
            "Assets/_Nymora/Art/Sprites/Nightseer/Base/sheets",
            "Assets/_Nymora/Art/Sprites/Necram/Base/sheets",
            "Assets/_Nymora/Art/Sprites/Ghostra/Base/sheets",
            "Assets/_Nymora/Art/Cosmetics/ObsidianTitan/sheets",
            "Assets/_Nymora/Art/Cosmetics/AshenSovereign/sheets",
            "Assets/_Nymora/Art/Cosmetics/VoidOracle/sheets",
            "Assets/_Nymora/Art/Cosmetics/PlagueApostle/sheets",
            "Assets/_Nymora/Art/Cosmetics/PaleRevenant/sheets",
        };

        [MenuItem("Nymora/Setup/Polish Kyami/Auto-Generate Character Normals (stopgap)", priority = 66)]
        public static void Run()
        {
            var report = new List<string>();
            int total = 0, ok = 0;

            foreach (var dir in ClassSheetDirs)
            {
                if (!AssetDatabase.IsValidFolder(dir))
                {
                    report.Add($"⚠ dossier absent : {dir}");
                    continue;
                }

                int dirOk = 0, dirN = 0;
                foreach (var file in Directory.GetFiles(dir, "*.png"))
                {
                    string name = Path.GetFileNameWithoutExtension(file);
                    if (name.EndsWith("_normal")) continue; // skip les normals (les nôtres + l'ancienne)

                    string assetPath = dir + "/" + Path.GetFileName(file);
                    dirN++; total++;
                    if (NormalMapGeneratorTool.GenerateAndAssign(assetPath, Strength, Blur, InvertHeight, FlipY, out _))
                    {
                        dirOk++; ok++;
                    }
                }
                report.Add($"{dir.Substring(dir.IndexOf("Art/"))} : {dirOk}/{dirN}");
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[AutoGenCharNormals]\n" + string.Join("\n", report));
            EditorUtility.DisplayDialog("Auto-Generate Character Normals",
                $"{ok}/{total} frames re-normalisées (auto).\n\n" + string.Join("\n", report) +
                "\n\nLance un combat : les persos doivent prendre du relief sous les lights.\n" +
                "Si l'éclairage paraît inversé verticalement, dis-le moi (je repasse avec FlipY).",
                "OK");
        }
    }
}
