using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Nymora.Editor.Setup
{
    /// <summary>
    /// POLISH KYAMI — fix relief HUB. L'avatar du hub n'utilise PAS les prefabs de combat : il anime
    /// les frames <c>IdleFrames / IdleFramesNE / WalkFrames / WalkFramesNE</c> des
    /// NymoraClassDefinition (base) et CosmeticSkinDefinition (skins). Ces frames pointaient encore
    /// les ANCIENS sprites .aseprite, qui n'ont pas les normales auto-générées → hub plat alors que
    /// le combat a du relief.
    ///
    /// Ce tool repointe ces 4 tableaux vers les sprites des NOUVEAUX sheets stage 0 (idle/walk SE+NE)
    /// — les mêmes qui portent la _NormalMap auto-générée. Résultat : l'avatar hub utilise des sprites
    /// avec relief. View-only, pas de bump version.
    ///
    /// Prérequis : avoir lancé l'import des spritesheets + l'auto-gen des normals.
    ///
    /// Menu : Nymora > Setup > Polish Kyami > Retarget Hub Frames to New Sheets.
    /// </summary>
    public static class RetargetHubFramesToNewSheetsTool
    {
        private struct Entry { public string Asset; public string SheetsDir; public string Label; }

        private static readonly Entry[] Entries =
        {
            // Classes (base)
            E("Classes/Colossar",   "Sprites/Colossar/Base",   "Colossar"),
            E("Classes/Soulrender", "Sprites/Soulrender/Base", "Soulrender"),
            E("Classes/Nightseer",  "Sprites/Nightseer/Base",  "Nightseer"),
            E("Classes/Necram",     "Sprites/Necram/Base",     "Necram"),
            E("Classes/Ghostra",    "Sprites/Ghostra/Base",    "Ghostra"),
            // Skins
            ESkin("ObsidianTitan"), ESkin("AshenSovereign"), ESkin("VoidOracle"),
            ESkin("PlagueApostle"), ESkin("PaleRevenant"),
        };

        private static Entry E(string assetRel, string artRel, string label) => new Entry
        {
            Asset = $"Assets/_Nymora/ScriptableObjects/{assetRel}.asset",
            SheetsDir = $"Assets/_Nymora/Art/{artRel}/sheets",
            Label = label,
        };
        private static Entry ESkin(string skin) => new Entry
        {
            Asset = $"Assets/_Nymora/ScriptableObjects/Cosmetics/{skin}.asset",
            SheetsDir = $"Assets/_Nymora/Art/Cosmetics/{skin}/sheets",
            Label = skin + " (skin)",
        };

        [MenuItem("Nymora/Setup/Polish Kyami/Retarget Hub Frames to New Sheets", priority = 67)]
        public static void Run()
        {
            var report = new System.Collections.Generic.List<string>();
            int ok = 0;

            foreach (var e in Entries)
            {
                var asset = AssetDatabase.LoadMainAssetAtPath(e.Asset);
                if (asset == null) { report.Add($"⚠ {e.Label} : asset absent ({e.Asset})"); continue; }

                var idleSE = Load($"{e.SheetsDir}/idle_stage0_SE.png");
                var idleNE = Load($"{e.SheetsDir}/idle_stage0_NE.png");
                var walkSE = Load($"{e.SheetsDir}/walk_stage0_SE.png");
                var walkNE = Load($"{e.SheetsDir}/walk_stage0_NE.png");

                if (idleSE.Length == 0)
                {
                    report.Add($"⚠ {e.Label} : pas de sprites dans {e.SheetsDir}/idle_stage0_SE.png");
                    continue;
                }

                var so = new SerializedObject(asset);
                SetArray(so, "IdleFrames", idleSE);
                SetArray(so, "IdleFramesNE", idleNE.Length > 0 ? idleNE : idleSE);
                SetArray(so, "WalkFrames", walkSE.Length > 0 ? walkSE : idleSE);
                SetArray(so, "WalkFramesNE", walkNE.Length > 0 ? walkNE : (idleNE.Length > 0 ? idleNE : idleSE));
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(asset);

                report.Add($"✓ {e.Label} : idle {idleSE.Length}/{idleNE.Length} · walk {walkSE.Length}/{walkNE.Length}");
                ok++;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[RetargetHubFrames]\n" + string.Join("\n", report));
            EditorUtility.DisplayDialog("Retarget Hub Frames",
                $"{ok}/{Entries.Length} définition(s) repointée(s).\n\n" + string.Join("\n", report) +
                "\n\nLance le hub : l'avatar utilise maintenant les sprites avec normales → relief sous les lights.",
                "OK");
        }

        private static Sprite[] Load(string sheetPath)
        {
            return AssetDatabase.LoadAllAssetsAtPath(sheetPath)
                .OfType<Sprite>()
                .OrderBy(s => TrailingInt(s.name))
                .ToArray();
        }

        private static void SetArray(SerializedObject so, string prop, Sprite[] sprites)
        {
            var p = so.FindProperty(prop);
            if (p == null || !p.isArray) return;
            p.arraySize = sprites.Length;
            for (int i = 0; i < sprites.Length; i++)
                p.GetArrayElementAtIndex(i).objectReferenceValue = sprites[i];
        }

        private static int TrailingInt(string name)
        {
            int i = name.Length - 1;
            while (i >= 0 && char.IsDigit(name[i])) i--;
            return int.TryParse(name.Substring(i + 1), out var v) ? v : 0;
        }
    }
}
