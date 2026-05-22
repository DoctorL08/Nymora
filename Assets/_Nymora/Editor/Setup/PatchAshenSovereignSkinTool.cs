using System.Collections.Generic;
using Nymora.Core.Enums;
using Nymora.Core.ScriptableObjects;
using Nymora.Hub;
using UnityEditor;
using UnityEngine;

namespace Nymora.Editor.Setup
{
    /// <summary>
    /// Brique 5.5.e — Importe les frames du skin Ashen Sovereign (idle/walk × SE/NE) en Sprites
    /// pixel-art, crée/peuple la CosmeticSkinDefinition, et la câble sur le prefab HubAvatar
    /// (+ _backendSettings). La calibration scale/yOffset est copiée de la classe Soulrender
    /// comme point de départ (à ajuster sur l'asset si besoin). Idempotent.
    ///
    /// Menu : Nymora > Setup > Patch Ashen Sovereign Skin.
    /// </summary>
    public static class PatchAshenSovereignSkinTool
    {
        private const string ArtDir = "Assets/_Nymora/Art/Cosmetics/AshenSovereign";
        private const string SkinAssetPath = "Assets/_Nymora/ScriptableObjects/Cosmetics/AshenSovereign.asset";
        private const string SkinDir = "Assets/_Nymora/ScriptableObjects/Cosmetics";
        private const string HubAvatarPrefabPath = "Assets/_Nymora/Prefabs/Hub/HubAvatar.prefab";
        private const string SoulrenderDefPath = "Assets/_Nymora/ScriptableObjects/Classes/Soulrender.asset";
        private const string BackendSettingsPath = "Assets/_Nymora/Settings/NymoraBackendSettings.asset";
        private const string CosmeticId = "skin_soulrender_ashen_sovereign";

        [MenuItem("Nymora/Setup/Patch Ashen Sovereign Skin", priority = 45)]
        private static void Patch()
        {
            var actions = new List<string>();

            // 1. Référence Soulrender pour PPU + calibration.
            var soulrender = AssetDatabase.LoadAssetAtPath<NymoraClassDefinition>(SoulrenderDefPath);
            float ppu = 100f;
            float scale = 1f, yOffset = 0f;
            // Pivot/alignement copies des frames Soulrender (sinon le skin garde le pivot Centre
            // par defaut -> decalage Y vs la calibration Soulrender). Defaut = Center si introuvable.
            int srAlign = (int)SpriteAlignment.Center;
            Vector2 srPivot = new Vector2(0.5f, 0.5f);
            if (soulrender != null)
            {
                scale = soulrender.HubVisualScale > 0f ? soulrender.HubVisualScale : 1f;
                yOffset = soulrender.HubVisualYOffset;
                if (soulrender.IdleFrames != null && soulrender.IdleFrames.Length > 0 && soulrender.IdleFrames[0] != null)
                {
                    string sp = AssetDatabase.GetAssetPath(soulrender.IdleFrames[0]);
                    if (AssetImporter.GetAtPath(sp) is TextureImporter ti)
                    {
                        ppu = ti.spritePixelsPerUnit;
                        var srSettings = new TextureImporterSettings();
                        ti.ReadTextureSettings(srSettings);
                        srAlign = srSettings.spriteAlignment;
                        srPivot = srSettings.spritePivot;
                    }
                }
            }
            actions.Add($"pivot Soulrender repris : align={srAlign} pivot=({srPivot.x:0.##},{srPivot.y:0.##})");

            // 2. Import des 28 PNG en Sprite pixel-art (point, no compression, PPU + pivot = Soulrender).
            int reimported = ConfigureSpriteImports(ppu, srAlign, srPivot, actions);

            // 3. Charger les frames triées.
            var idleSE = LoadFrames("idle", "SE", 6);
            var idleNE = LoadFrames("idle", "NE", 6);
            var walkSE = LoadFrames("walk", "SE", 8);
            var walkNE = LoadFrames("walk", "NE", 8);
            if (idleSE.Length == 0)
            {
                EditorUtility.DisplayDialog("Patch Ashen Sovereign",
                    $"Aucune frame trouvée dans {ArtDir}. As-tu bien extrait les PNG ?", "OK");
                return;
            }

            // 4. Créer/peupler la CosmeticSkinDefinition.
            if (!AssetDatabase.IsValidFolder(SkinDir))
                AssetDatabase.CreateFolder("Assets/_Nymora/ScriptableObjects", "Cosmetics");
            var skin = AssetDatabase.LoadAssetAtPath<CosmeticSkinDefinition>(SkinAssetPath);
            bool created = skin == null;
            if (created)
            {
                skin = ScriptableObject.CreateInstance<CosmeticSkinDefinition>();
                AssetDatabase.CreateAsset(skin, SkinAssetPath);
            }
            skin.CosmeticId = CosmeticId;
            skin.ClassId = NymoraClass.Soulrender;
            skin.DisplayName = "Ashen Sovereign";
            skin.IdleFrames = idleSE;
            skin.IdleFramesNE = idleNE;
            skin.WalkFrames = walkSE;
            skin.WalkFramesNE = walkNE;
            skin.IdleFps = 8f;
            skin.WalkFps = 12f;
            skin.HubVisualScale = scale;
            skin.HubVisualYOffset = yOffset;
            EditorUtility.SetDirty(skin);
            AssetDatabase.SaveAssets();
            actions.Add(created ? "CosmeticSkinDefinition créée" : "CosmeticSkinDefinition mise à jour");
            actions.Add($"frames : idle {idleSE.Length}/{idleNE.Length} · walk {walkSE.Length}/{walkNE.Length} · PPU {ppu} · scale {scale} · yOff {yOffset}");
            actions.Add($"{reimported} PNG (re)importé(s) en Sprite");

            // 5. Câbler sur le prefab HubAvatar (_skinDefinitions + _backendSettings).
            WireHubAvatarPrefab(skin, actions);

            AssetDatabase.SaveAssets();
            Debug.Log($"[Nymora.Setup] Patch Ashen Sovereign : {string.Join("\n", actions)}");
            EditorUtility.DisplayDialog("Patch Ashen Sovereign",
                string.Join("\n", actions) + "\n\nAjuste HubVisualScale/YOffset sur l'asset si la taille/position est off.", "OK");
        }

        private static int ConfigureSpriteImports(float ppu, int align, Vector2 pivot, List<string> actions)
        {
            int count = 0;
            string[] anims = { "idle", "walk" };
            string[] dirs = { "SE", "NE" };
            foreach (var anim in anims)
            foreach (var dir in dirs)
            {
                int max = anim == "idle" ? 6 : 8;
                for (int i = 0; i < max; i++)
                {
                    string path = $"{ArtDir}/ashen_{anim}_{dir}_{i}.png";
                    if (AssetImporter.GetAtPath(path) is not TextureImporter ti) continue;

                    // Tout via TextureImporterSettings pour piloter le pivot/alignement comme Soulrender.
                    var s = new TextureImporterSettings();
                    ti.ReadTextureSettings(s);
                    s.textureType = TextureImporterType.Sprite;
                    s.spriteMode = (int)SpriteImportMode.Single;
                    s.spriteAlignment = align;
                    s.spritePivot = pivot;        // utilise quand align == Custom (9)
                    s.spritePixelsPerUnit = ppu;
                    s.filterMode = FilterMode.Point;
                    ti.SetTextureSettings(s);
                    ti.textureCompression = TextureImporterCompression.Uncompressed;
                    ti.SaveAndReimport();
                    count++;
                }
            }
            return count;
        }

        private static Sprite[] LoadFrames(string anim, string dir, int max)
        {
            var list = new List<Sprite>();
            for (int i = 0; i < max; i++)
            {
                var s = AssetDatabase.LoadAssetAtPath<Sprite>($"{ArtDir}/ashen_{anim}_{dir}_{i}.png");
                if (s != null) list.Add(s);
            }
            return list.ToArray();
        }

        private static void WireHubAvatarPrefab(CosmeticSkinDefinition skin, List<string> actions)
        {
            var root = PrefabUtility.LoadPrefabContents(HubAvatarPrefabPath);
            if (root == null) { actions.Add("⚠ HubAvatar.prefab introuvable"); return; }
            try
            {
                var avatar = root.GetComponentInChildren<HubAvatar>(true);
                if (avatar == null) { actions.Add("⚠ HubAvatar component absent du prefab"); return; }

                var so = new SerializedObject(avatar);
                var skinsProp = so.FindProperty("_skinDefinitions");
                if (skinsProp != null)
                {
                    skinsProp.arraySize = 1;
                    skinsProp.GetArrayElementAtIndex(0).objectReferenceValue = skin;
                    actions.Add("HubAvatar._skinDefinitions câblé");
                }
                var settingsProp = so.FindProperty("_backendSettings");
                if (settingsProp != null && settingsProp.objectReferenceValue == null)
                {
                    var settings = AssetDatabase.LoadMainAssetAtPath(BackendSettingsPath);
                    if (settings != null)
                    {
                        settingsProp.objectReferenceValue = settings;
                        actions.Add("HubAvatar._backendSettings câblé");
                    }
                }
                so.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, HubAvatarPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }
}
