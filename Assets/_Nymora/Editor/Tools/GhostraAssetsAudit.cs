using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Nymora.Editor.Tools
{
    /// <summary>
    /// Diagnostic tool : scanne les 6 .aseprite Ghostra (stage0/1/2 × NE/SE) et
    /// liste pour chacun les AnimationClip sub-assets effectivement importes.
    /// Sert a verifier que les frame tags Aseprite (idle/walk/attack/cast/hurt/death)
    /// sont bien presents apres un reimport du designer.
    ///
    /// Si un tag manque, BuildGhostraAnimator ne le retrouvera pas → fallback Idle
    /// dans le state correspondant (Walk visible = Idle figee, etc.).
    ///
    /// Usage : Menu Nymora > Validation > Audit Ghostra Aseprite Clips
    /// Read-only, ne modifie rien.
    /// </summary>
    public static class GhostraAssetsAudit
    {
        private static readonly string[] AsepritePaths =
        {
            "Assets/_Nymora/Art/Sprites/Ghostra/Base/sources/GHOSTRA_animation_stage0_SE.aseprite",
            "Assets/_Nymora/Art/Sprites/Ghostra/Base/sources/GHOSTRA_animation_stage0_NE.aseprite",
            "Assets/_Nymora/Art/Sprites/Ghostra/Base/sources/GHOSTRA_animation_stage1_SE.aseprite",
            "Assets/_Nymora/Art/Sprites/Ghostra/Base/sources/GHOSTRA_animation_stage1_NE.aseprite",
            "Assets/_Nymora/Art/Sprites/Ghostra/Base/sources/GHOSTRA_animation_stage2_SE.aseprite",
            "Assets/_Nymora/Art/Sprites/Ghostra/Base/sources/GHOSTRA_animation_stage2_NE.aseprite",
        };

        private static readonly string[] ExpectedTags = { "idle", "walk", "attack", "cast", "hurt", "death" };

        [MenuItem("Nymora/Validation/Audit Ghostra Aseprite Clips")]
        public static void Run()
        {
            int filesMissing = 0;
            int tagsMissingTotal = 0;
            var lines = new List<string>(16);

            foreach (var path in AsepritePaths)
            {
                if (!File.Exists(path))
                {
                    Debug.LogError($"[GhostraAudit] FICHIER MANQUANT : {path}");
                    filesMissing++;
                    continue;
                }

                var assets = AssetDatabase.LoadAllAssetsAtPath(path);
                var clipNames = new List<string>();
                int spriteCount = 0;
                foreach (var a in assets)
                {
                    if (a is AnimationClip clip) clipNames.Add(clip.name);
                    else if (a is Sprite) spriteCount++;
                }

                string fileName = Path.GetFileName(path);
                var foundLower = new HashSet<string>();
                foreach (var n in clipNames) foundLower.Add(n.ToLowerInvariant());

                var missing = new List<string>();
                foreach (var tag in ExpectedTags)
                {
                    bool found = false;
                    foreach (var n in foundLower)
                    {
                        if (n.Contains(tag)) { found = true; break; }
                    }
                    if (!found) missing.Add(tag);
                }
                tagsMissingTotal += missing.Count;

                string clipList = clipNames.Count == 0 ? "(aucun)" : string.Join(", ", clipNames);
                string missingLine = missing.Count == 0
                    ? "✓ tous les tags presents"
                    : $"✗ MANQUE : {string.Join(", ", missing)}";

                lines.Add($"\n{fileName}");
                lines.Add($"   sprites    : {spriteCount}");
                lines.Add($"   clips ({clipNames.Count}) : {clipList}");
                lines.Add($"   {missingLine}");
            }

            string report = "[GhostraAudit] === RAPPORT ===" + string.Join("\n", lines) + "\n\n";
            report += $"Total fichiers manquants : {filesMissing}\n";
            report += $"Total tags manquants     : {tagsMissingTotal}\n";
            if (tagsMissingTotal > 0)
            {
                report += "\n⚠️ Pour chaque tag manquant : ouvre le .aseprite dans Aseprite, " +
                          "ajoute un FRAME TAG nomme exactement (idle/walk/attack/cast/hurt/death) " +
                          "sur la plage de frames correspondante, save, reimport.";
            }
            else if (filesMissing == 0)
            {
                report += "\n✅ Tous les .aseprite Ghostra ont les 6 tags requis. " +
                          "Si BuildGhostraAnimator donne 0 anim, le probleme est ailleurs.";
            }
            Debug.Log(report);
        }
    }
}
