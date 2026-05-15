using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Nymora.Editor.Setup
{
    /// <summary>
    /// Phase 4 — Genere le prefab Torch.prefab anime (8 frames boucle) pour la scene hub.
    ///
    /// Etapes :
    ///   1. Configure l'import des 8 PNG torch_frame1..8.png en Sprite (PPU 32, Point, no compression, pivot bottom-center)
    ///   2. Cree Assets/_Nymora/Animations/Hub/Torch.anim (12 fps, 8 keyframes m_Sprite, loop)
    ///   3. Cree Assets/_Nymora/Animations/Hub/Torch.controller (single state jouant Torch.anim)
    ///   4. Cree Assets/_Nymora/Prefabs/Hub/Torch.prefab (SpriteRenderer + Animator wires)
    ///
    /// Idempotent : overwrite si existe (confirmation utilisateur).
    ///
    /// Menu : Nymora > Setup > Create Hub Torch Prefab
    /// </summary>
    public static class CreateHubTorchPrefabTool
    {
        private const string FramesFolder = "Assets/_Nymora/Art/Hub/Torch";
        private const string AnimFolder = "Assets/_Nymora/Animations/Hub";
        private const string AnimPath = AnimFolder + "/Torch.anim";
        private const string ControllerPath = AnimFolder + "/Torch.controller";
        private const string PrefabFolder = "Assets/_Nymora/Prefabs/Hub";
        private const string PrefabPath = PrefabFolder + "/Torch.prefab";

        private const int FrameCount = 8;
        private const float FrameRate = 12f;
        private const float PixelsPerUnit = 32f;

        [MenuItem("Nymora/Setup/Create Hub Torch Prefab", priority = 36)]
        private static void CreatePrefab()
        {
            EnsureFolder(AnimFolder);
            EnsureFolder(PrefabFolder);

            // 1. Configure import des 8 PNG en Sprite pixel-perfect PPU 32
            var sprites = new Sprite[FrameCount];
            for (int i = 0; i < FrameCount; i++)
            {
                string pngPath = $"{FramesFolder}/torch_frame{i + 1}.png";
                if (!File.Exists(pngPath))
                {
                    EditorUtility.DisplayDialog("Hub Torch Prefab",
                        $"PNG introuvable : {pngPath}\n\nVerifier que les 8 frames sont dans {FramesFolder}/",
                        "OK");
                    return;
                }

                ConfigureSpriteImport(pngPath);
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(pngPath);
                if (sprite == null)
                {
                    Debug.LogError($"[Nymora.Setup] Sprite null apres import : {pngPath}");
                    return;
                }
                sprites[i] = sprite;
            }

            // Confirmation overwrite
            if (File.Exists(PrefabPath) || File.Exists(AnimPath) || File.Exists(ControllerPath))
            {
                if (!EditorUtility.DisplayDialog("Hub Torch Prefab",
                    "Des assets Torch.* existent deja. Les regenerer (ecrasement) ?",
                    "Regenerer", "Annuler"))
                {
                    return;
                }
            }

            // 2. Cree l'AnimationClip
            var clip = CreateAnimationClip(sprites);
            AssetDatabase.CreateAsset(clip, AnimPath);

            // 3. Cree l'AnimatorController qui joue le clip
            var controller = AnimatorController.CreateAnimatorControllerAtPathWithClip(ControllerPath, clip);

            // 4. Cree le prefab : SpriteRenderer + Animator
            var go = new GameObject("Torch",
                typeof(SpriteRenderer),
                typeof(Animator));

            var sr = go.GetComponent<SpriteRenderer>();
            sr.sprite = sprites[0];
            sr.sortingOrder = 50; // au-dessus du fond hub, en-dessous des avatars (sortingOrder 100)

            var animator = go.GetComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            animator.updateMode = AnimatorUpdateMode.Normal;
            animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;

            bool success;
            PrefabUtility.SaveAsPrefabAsset(go, PrefabPath, out success);
            Object.DestroyImmediate(go);

            if (!success)
            {
                Debug.LogError($"[Nymora.Setup] Echec creation prefab {PrefabPath}");
                return;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[Nymora.Setup] Torch prefab genere :\n  {AnimPath}\n  {ControllerPath}\n  {PrefabPath}");

            EditorUtility.DisplayDialog("Hub Torch Prefab",
                $"Generation OK :\n" +
                $"  Anim       : {AnimPath}\n" +
                $"  Controller : {ControllerPath}\n" +
                $"  Prefab     : {PrefabPath}\n\n" +
                "Etapes suivantes :\n" +
                "1. Ouvre 10_CommunityHub\n" +
                "2. Drag-drop Torch.prefab dans la scene aux positions voulues\n" +
                "3. Ajuste Transform.scale si la torche est trop grande/petite\n" +
                "4. Ajuste SpriteRenderer.sortingOrder si elle passe sous/sur d'autres elements",
                "OK");

            Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        }

        private static void ConfigureSpriteImport(string pngPath)
        {
            var importer = AssetImporter.GetAtPath(pngPath) as TextureImporter;
            if (importer == null)
            {
                Debug.LogError($"[Nymora.Setup] TextureImporter null pour {pngPath}");
                return;
            }

            bool dirty = false;

            if (importer.textureType != TextureImporterType.Sprite)
            {
                importer.textureType = TextureImporterType.Sprite;
                dirty = true;
            }
            if (importer.spriteImportMode != SpriteImportMode.Single)
            {
                importer.spriteImportMode = SpriteImportMode.Single;
                dirty = true;
            }
            if (!Mathf.Approximately(importer.spritePixelsPerUnit, PixelsPerUnit))
            {
                importer.spritePixelsPerUnit = PixelsPerUnit;
                dirty = true;
            }
            if (importer.filterMode != FilterMode.Point)
            {
                importer.filterMode = FilterMode.Point;
                dirty = true;
            }
            if (importer.textureCompression != TextureImporterCompression.Uncompressed)
            {
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                dirty = true;
            }
            if (importer.mipmapEnabled)
            {
                importer.mipmapEnabled = false;
                dirty = true;
            }
            if (!importer.alphaIsTransparency)
            {
                importer.alphaIsTransparency = true;
                dirty = true;
            }

            // Pivot bottom-center : facilite l'alignement sur un sol/socle
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            if (settings.spriteAlignment != (int)SpriteAlignment.BottomCenter)
            {
                settings.spriteAlignment = (int)SpriteAlignment.BottomCenter;
                settings.spritePivot = new Vector2(0.5f, 0f);
                importer.SetTextureSettings(settings);
                dirty = true;
            }

            if (dirty)
            {
                importer.SaveAndReimport();
            }
        }

        private static AnimationClip CreateAnimationClip(Sprite[] sprites)
        {
            var clip = new AnimationClip
            {
                frameRate = FrameRate,
                wrapMode = WrapMode.Loop,
            };

            var binding = new EditorCurveBinding
            {
                path = string.Empty,
                type = typeof(SpriteRenderer),
                propertyName = "m_Sprite",
            };

            var keyframes = new ObjectReferenceKeyframe[sprites.Length];
            for (int i = 0; i < sprites.Length; i++)
            {
                keyframes[i] = new ObjectReferenceKeyframe
                {
                    time = i / FrameRate,
                    value = sprites[i],
                };
            }

            AnimationUtility.SetObjectReferenceCurve(clip, binding, keyframes);

            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = true;
            settings.loopBlend = false;
            AnimationUtility.SetAnimationClipSettings(clip, settings);

            return clip;
        }

        private static void EnsureFolder(string folder)
        {
            if (!AssetDatabase.IsValidFolder(folder))
            {
                Directory.CreateDirectory(folder);
                AssetDatabase.Refresh();
            }
        }
    }
}
