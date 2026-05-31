using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Nymora.Editor.Setup
{
    /// <summary>
    /// POLISH KYAMI — Tâche 9 : remplace les frames de torche du hub par le nouveau spritesheet
    /// propre <c>torch_map_hub_animation_8frame.png</c> (512×64 = 8 frames 64×64) + sa normal map.
    ///
    /// Avant : 8 PNG 1920×1080, chacun un sheet combiné de 12 torches minuscules (le prefab n'en
    /// utilisait qu'une). Après : un seul strip propre, sliced en 8 frames, normal montée en
    /// texture secondaire "_NormalMap" (lue par le matériau lit URP 2D sous les lights).
    ///
    /// On RÉÉCRIT le clip <c>Torch.anim</c> EN PLACE (même GUID) → le controller Torch.controller et
    /// le Torch.prefab (+ toutes ses instances en scène 10_CommunityHub) restent câblés, aux mêmes
    /// positions, avec leurs Light2D / flicker / halo INTACTS. View-only : pas de bump version.
    ///
    /// ⚠️ TAILLE À L'ÉCRAN : le PPU est calé (<see cref="Ppu"/> = 42) pour approcher la hauteur monde
    /// de l'ancienne torche (~49 px @ PPU 32 ≈ 1,5 u). Si la nouvelle torche paraît trop grosse/petite,
    /// c'est le SEUL réglage à toucher : soit ce PPU, soit la scale du Torch.prefab (toutes les
    /// instances suivent).
    ///
    /// Menu : Nymora > Setup > Polish Kyami > Import Hub Torch Spritesheet + Normal.
    /// </summary>
    public static class ImportHubTorchSpritesheetTool
    {
        private const string SrcSheet  = "C:/Users/Lorenzo/Downloads/Polish_Kyami/Polish/map/torch_map_hub_spritesheet/torch_map_hub_animation_8frame.png";
        private const string SrcNormal = "C:/Users/Lorenzo/Downloads/Polish_Kyami/Polish/map/torch_map_hub_spritesheet_normal/torch_map_hub_animation_8frame_normal.png";

        private const string DstDir    = "Assets/_Nymora/Art/Hub/Torch";
        private const string DstSheet  = DstDir + "/torch_map_hub_animation_8frame.png";
        private const string DstNormal = DstDir + "/torch_map_hub_animation_8frame_normal.png";
        private const string ClipPath  = "Assets/_Nymora/Animations/Hub/Torch.anim";

        private const int Cell = 64;
        private const float Ppu = 42f;                 // ≈ hauteur monde de l'ancienne torche
        private static readonly Vector2 Pivot = new Vector2(0.5f, 0f); // bas-centre (comme l'ancien)
        private const float FallbackFps = 12f;         // m_SampleRate actuel du Torch.anim

        [MenuItem("Nymora/Setup/Polish Kyami/Import Hub Torch Spritesheet + Normal", priority = 62)]
        public static void Run()
        {
            if (!File.Exists(SrcSheet))
            {
                EditorUtility.DisplayDialog("Import Hub Torch", $"Source introuvable :\n{SrcSheet}", "OK");
                return;
            }

            EnsureFolder(DstDir);

            // 1) Copie + import.
            File.Copy(SrcSheet, DstSheet, true);
            AssetDatabase.ImportAsset(DstSheet, ImportAssetOptions.ForceUpdate);

            bool hasNormal = File.Exists(SrcNormal);
            if (hasNormal)
            {
                File.Copy(SrcNormal, DstNormal, true);
                AssetDatabase.ImportAsset(DstNormal, ImportAssetOptions.ForceUpdate);
                ConfigureNormalImporter(DstNormal);
            }

            // 2+3) Slice 8×64 + normal secondaire.
            int frames = SliceSheet(DstSheet, "torch", hasNormal ? DstNormal : null);
            if (frames <= 0)
            {
                EditorUtility.DisplayDialog("Import Hub Torch", "Slice échoué (texture illisible).", "OK");
                return;
            }

            // 4) Réécrit Torch.anim en place.
            var sprites = AssetDatabase.LoadAllAssetsAtPath(DstSheet)
                .OfType<Sprite>()
                .OrderBy(s => TrailingInt(s.name))
                .ToArray();

            string msg = RewriteClip(sprites);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[ImportHubTorch] {frames} frames · normal {(hasNormal ? "oui" : "non")} · {msg}");
            EditorUtility.DisplayDialog("Import Hub Torch",
                $"Torche mise à jour : {frames} frames, normal {(hasNormal ? "oui" : "non")}.\n{msg}\n\n" +
                "Prefab/controller/Light2D inchangés. Lance le hub : vérifie taille + relief, ajuste le PPU (42) ou la scale du prefab si besoin.",
                "OK");
        }

#pragma warning disable 0618 // TextureImporter.spritesheet obsolète mais fiable en 2022.3
        private static int SliceSheet(string assetPath, string spriteBaseName, string normalAssetPath)
        {
            if (AssetImporter.GetAtPath(assetPath) is not TextureImporter ti) return 0;

            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
            int width = tex != null ? tex.width : 0;
            int height = tex != null ? tex.height : Cell;
            if (width <= 0) return 0;

            int frames = Mathf.Max(1, width / Cell);
            var metas = new SpriteMetaData[frames];
            for (int i = 0; i < frames; i++)
            {
                metas[i] = new SpriteMetaData
                {
                    name = $"{spriteBaseName}_{i}",
                    rect = new Rect(i * Cell, 0, Cell, Mathf.Min(Cell, height)),
                    alignment = (int)SpriteAlignment.Custom,
                    pivot = Pivot,
                };
            }

            ti.textureType = TextureImporterType.Sprite;
            ti.spriteImportMode = SpriteImportMode.Multiple;
            ti.spritePixelsPerUnit = Ppu;
            ti.spritesheet = metas;
            ti.filterMode = FilterMode.Point;
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            ti.alphaIsTransparency = true;
            ti.mipmapEnabled = false;
            ti.isReadable = false;
            ti.sRGBTexture = true;

            if (!string.IsNullOrEmpty(normalAssetPath))
            {
                var normalTex = AssetDatabase.LoadAssetAtPath<Texture2D>(normalAssetPath);
                if (normalTex != null)
                    ti.secondarySpriteTextures = new[]
                    {
                        new SecondarySpriteTexture { name = "_NormalMap", texture = normalTex },
                    };
            }

            ti.SaveAndReimport();
            return frames;
        }
#pragma warning restore 0618

        private static void ConfigureNormalImporter(string assetPath)
        {
            if (AssetImporter.GetAtPath(assetPath) is not TextureImporter ti) return;
            ti.textureType = TextureImporterType.Default;
            ti.spriteImportMode = SpriteImportMode.None;
            ti.sRGBTexture = false;
            ti.filterMode = FilterMode.Point;
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            ti.mipmapEnabled = false;
            ti.alphaIsTransparency = false;
            ti.wrapMode = TextureWrapMode.Clamp;
            ti.SaveAndReimport();
        }

        private static string RewriteClip(Sprite[] sprites)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(ClipPath);
            if (clip == null) return $"⚠ {ClipPath} introuvable — clip NON mis à jour";
            if (sprites == null || sprites.Length == 0) return "⚠ aucun sprite slicé — clip NON mis à jour";

            float fps = clip.frameRate > 0f ? clip.frameRate : FallbackFps;

            var binding = new EditorCurveBinding
            {
                type = typeof(SpriteRenderer),
                path = "",
                propertyName = "m_Sprite",
            };

            var keys = new ObjectReferenceKeyframe[sprites.Length];
            for (int i = 0; i < sprites.Length; i++)
                keys[i] = new ObjectReferenceKeyframe { time = i / fps, value = sprites[i] };

            AnimationUtility.SetObjectReferenceCurve(clip, binding, keys);

            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = true;
            AnimationUtility.SetAnimationClipSettings(clip, settings);

            EditorUtility.SetDirty(clip);
            return $"Torch.anim réécrit ({sprites.Length} frames @ {fps:0.#} fps, loop)";
        }

        private static int TrailingInt(string name)
        {
            int i = name.Length - 1;
            while (i >= 0 && char.IsDigit(name[i])) i--;
            return int.TryParse(name.Substring(i + 1), out var v) ? v : 0;
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
