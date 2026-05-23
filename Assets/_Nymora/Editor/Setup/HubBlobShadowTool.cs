using System.Collections.Generic;
using System.IO;
using Nymora.Hub;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Nymora.Editor.Setup
{
    /// <summary>
    /// Brique VP.3b — Blob d'ombre directionnel du hub (View-side only, aucun bump CombatRulesVersion).
    ///
    /// Genere un sprite d'ombre (ovale doux, 0 licence) et l'ajoute au prefab HubAvatar via un
    /// child "ShadowBlob" : SpriteRenderer unlit (noir) + composant HubAvatarShadow (penche a
    /// l'oppose de la light la plus proche, decalage plafonne -> jamais trop longue).
    /// Nettoie au passage les restes de l'approche ShadowCaster2D.
    ///
    /// NE TOUCHE PAS aux lights.
    ///
    /// Reglages ensuite (sur le child ShadowBlob du prefab) :
    /// - Position Transform = sous les pieds.
    /// - Scale Transform    = taille de l'ombre (X > Y).
    /// - HubAvatarShadow : Max Offset (longueur max), Offset Gain, Light Range, Min/Max Alpha.
    ///
    /// Menu : Nymora > Setup > Setup Hub Blob Shadow (VP3b).
    /// </summary>
    public static class HubBlobShadowTool
    {
        private const string AvatarPrefabPath = "Assets/_Nymora/Prefabs/Hub/HubAvatar.prefab";
        private const string SpriteDir = "Assets/_Nymora/Art/VFX";
        private const string SpritePath = SpriteDir + "/blob_shadow.png";
        private const string ChildName = "ShadowBlob";
        private const string UnlitMaterialGuid = "9dfc825aed78fcd4ba02077103263b40"; // Sprite-Unlit-Default URP

        private const int TexWidth = 256;
        private const int TexHeight = 128;
        private const float PixelsPerUnit = 200f;

        [MenuItem("Nymora/Setup/Setup Hub Blob Shadow (VP3b)", priority = 62)]
        private static void Setup()
        {
            if (Application.isPlaying)
            {
                EditorUtility.DisplayDialog("Blob Shadow", "Impossible pendant Play Mode.", "OK");
                return;
            }

            var actions = new List<string>();

            var sprite = EnsureBlobSprite(actions);
            if (sprite == null)
            {
                EditorUtility.DisplayDialog("Blob Shadow", "Echec generation du sprite d'ombre.", "OK");
                return;
            }

            PatchAvatarPrefab(sprite, actions);
            AssetDatabase.SaveAssets();

            string summary = actions.Count == 0
                ? "OK Rien a faire, le blob est deja en place."
                : "VP.3b (blob directionnel) applique — lights NON modifiees :\n\n" + string.Join("\n", actions) +
                  "\n\nSur le child 'ShadowBlob' : Position = sous les pieds, Scale = taille,\nHubAvatarShadow.MaxOffset = longueur max de l'ombre.";
            EditorUtility.DisplayDialog("Blob Shadow (VP3b)", summary, "OK");
            Debug.Log("[HubBlobShadowTool] " + summary);
        }

        private static Sprite EnsureBlobSprite(List<string> actions)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Sprite>(SpritePath);
            if (existing != null) return existing;

            if (!AssetDatabase.IsValidFolder(SpriteDir))
                AssetDatabase.CreateFolder("Assets/_Nymora/Art", "VFX");

            var tex = new Texture2D(TexWidth, TexHeight, TextureFormat.RGBA32, false);
            float cx = (TexWidth - 1) / 2f;
            float cy = (TexHeight - 1) / 2f;
            float rx = TexWidth / 2f;
            float ry = TexHeight / 2f;
            for (int y = 0; y < TexHeight; y++)
            {
                for (int x = 0; x < TexWidth; x++)
                {
                    float nx = (x - cx) / rx;
                    float ny = (y - cy) / ry;
                    float d = Mathf.Sqrt(nx * nx + ny * ny);
                    float a = Mathf.SmoothStep(1f, 0f, Mathf.Clamp01(d));
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            }
            tex.Apply();

            File.WriteAllBytes(SpritePath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(SpritePath, ImportAssetOptions.ForceUpdate);

            var importer = (TextureImporter)AssetImporter.GetAtPath(SpritePath);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.spritePixelsPerUnit = PixelsPerUnit;
            importer.SaveAndReimport();

            actions.Add("- Sprite d'ombre genere : " + SpritePath);
            return AssetDatabase.LoadAssetAtPath<Sprite>(SpritePath);
        }

        private static void PatchAvatarPrefab(Sprite sprite, List<string> actions)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(AvatarPrefabPath) == null)
            {
                actions.Add("- !! Prefab introuvable : " + AvatarPrefabPath);
                return;
            }

            var root = PrefabUtility.LoadPrefabContents(AvatarPrefabPath);
            bool changed = false;
            try
            {
                // Nettoyage des restes de l'approche ShadowCaster2D.
                foreach (var dead in root.GetComponentsInChildren<ShadowCaster2D>(true))
                {
                    Object.DestroyImmediate(dead.gameObject.name == "ShadowCaster" ? dead.gameObject : (Object)dead);
                    changed = true;
                    actions.Add("- Reste ShadowCaster2D retire");
                }

                if (root.transform.Find(ChildName) != null)
                {
                    if (changed) PrefabUtility.SaveAsPrefabAsset(root, AvatarPrefabPath);
                    return; // blob deja present
                }

                // SR du perso (le child "Visual"), pour le tri.
                SpriteRenderer charSr = null;
                foreach (var sr in root.GetComponentsInChildren<SpriteRenderer>(true))
                {
                    if (sr.gameObject.name == ChildName) continue;
                    charSr = sr;
                    break;
                }

                var go = new GameObject(ChildName);
                go.transform.SetParent(root.transform, false);
                go.transform.localPosition = Vector3.zero; // a caler sous les pieds

                var blob = go.AddComponent<SpriteRenderer>();
                blob.sprite = sprite;
                blob.color = new Color(0f, 0f, 0f, 0.45f);

                var unlitPath = AssetDatabase.GUIDToAssetPath(UnlitMaterialGuid);
                var unlit = string.IsNullOrEmpty(unlitPath) ? null : AssetDatabase.LoadAssetAtPath<Material>(unlitPath);
                if (unlit != null) blob.sharedMaterial = unlit;

                if (charSr != null)
                {
                    blob.sortingLayerID = charSr.sortingLayerID;
                    blob.sortingOrder = charSr.sortingOrder - 1; // derriere le perso, devant le fond
                }

                go.AddComponent<HubAvatarShadow>();

                changed = true;
                PrefabUtility.SaveAsPrefabAsset(root, AvatarPrefabPath);
                actions.Add("- Child 'ShadowBlob' + HubAvatarShadow ajoutes au prefab HubAvatar");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }
}
