using System.Collections.Generic;
using System.IO;
using Nymora.Core.Enums;
using Nymora.Core.ScriptableObjects;
using Nymora.Hub;
using UnityEditor;
using UnityEngine;

namespace Nymora.Editor.Setup
{
    /// <summary>
    /// Brique E1 — Importe les 15 PNG d'émotes (Art/UI/Emotes/Emote_XX_yyy.png) en Sprite +
    /// construit/rafraîchit l'asset EmoteCatalog.asset (mapping classe + id + sprite).
    ///
    /// Le préfixe de fichier donne la classe (CS=Colossar, GH=Ghostra, NC=Necram, NS=Nightseer,
    /// SR=Soulrender) ; l'id technique = nom de fichier en minuscules (ex "emote_cs_grrr").
    /// Idempotent : relançable sans dupliquer. À lancer après tout ajout/retrait d'émote.
    ///
    /// Menu : Nymora > Setup > Emotes > Import Emotes &amp; Build Catalog.
    /// </summary>
    public static class ImportEmotesTool
    {
        private const string EmoteDir = "Assets/_Nymora/Art/UI/Emotes";
        private const string CatalogPath = "Assets/_Nymora/ScriptableObjects/Settings/EmoteCatalog.asset";
        private const string AvatarPrefabPath = "Assets/_Nymora/Prefabs/Hub/HubAvatar.prefab";

        private static readonly Dictionary<string, NymoraClass> ClassByPrefix = new Dictionary<string, NymoraClass>
        {
            { "CS", NymoraClass.Colossar },
            { "GH", NymoraClass.Ghostra },
            { "NC", NymoraClass.Necram },
            { "NS", NymoraClass.Nightseer },
            { "SR", NymoraClass.Soulrender },
        };

        [MenuItem("Nymora/Setup/Emotes/Import Emotes & Build Catalog")]
        public static void Run()
        {
            if (!Directory.Exists(EmoteDir))
            {
                Debug.LogError("[Emotes] Dossier introuvable : " + EmoteDir);
                return;
            }

            // 1) Liste + tri déterministe des PNG.
            var files = new List<string>(Directory.GetFiles(EmoteDir, "*.png"));
            files.Sort(System.StringComparer.OrdinalIgnoreCase);
            if (files.Count == 0)
            {
                Debug.LogError("[Emotes] Aucun .png dans " + EmoteDir);
                return;
            }

            // 2) Import en Sprite + parse classe/id.
            var entries = new List<EmoteCatalog.EmoteEntry>();
            int unknown = 0;
            foreach (var raw in files)
            {
                string path = raw.Replace('\\', '/');
                EnsureSpriteImport(path);

                string file = Path.GetFileNameWithoutExtension(path); // Emote_CS_grrr
                var parts = file.Split('_');
                if (parts.Length < 3 || !ClassByPrefix.TryGetValue(parts[1].ToUpperInvariant(), out var cls))
                {
                    Debug.LogWarning("[Emotes] Nom non reconnu (attendu Emote_XX_yyy) : " + file);
                    unknown++;
                    continue;
                }

                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (sprite == null)
                {
                    Debug.LogWarning("[Emotes] Sprite introuvable après import : " + path);
                    continue;
                }

                entries.Add(new EmoteCatalog.EmoteEntry
                {
                    Id = file.ToLowerInvariant(),   // emote_cs_grrr
                    ClassId = cls,
                    Sprite = sprite,
                });
            }

            // 3) Crée ou charge le catalogue, assigne, sauve.
            var catalog = AssetDatabase.LoadAssetAtPath<EmoteCatalog>(CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<EmoteCatalog>();
                var dir = Path.GetDirectoryName(CatalogPath).Replace('\\', '/');
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }
            catalog.Emotes = entries.ToArray();
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // E2 — câble le catalogue sur le prefab HubAvatar (résolution des émotes reçues par RPC).
            AssignCatalogToAvatarPrefab(catalog);

            Debug.Log($"[Emotes] Catalogue OK : {entries.Count} émotes importées dans {CatalogPath}" +
                      (unknown > 0 ? $" ({unknown} fichier(s) ignoré(s))" : "") +
                      ".\nRelance maintenant 'Nymora > Setup > UI Menu > Create or Refresh Menu Shell' pour câbler le catalogue au menu.");
            Selection.activeObject = catalog;
        }

        private static void AssignCatalogToAvatarPrefab(EmoteCatalog catalog)
        {
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(AvatarPrefabPath);
            if (go == null)
            {
                Debug.LogWarning("[Emotes] Prefab HubAvatar introuvable à " + AvatarPrefabPath +
                                 " — assigne EmoteCatalog à la main sur le composant HubAvatar.");
                return;
            }
            var avatar = go.GetComponent<HubAvatar>();
            if (avatar == null)
            {
                Debug.LogWarning("[Emotes] Composant HubAvatar absent du prefab — assigne EmoteCatalog à la main.");
                return;
            }
            var so = new SerializedObject(avatar);
            var prop = so.FindProperty("_emoteCatalog");
            if (prop != null && prop.objectReferenceValue != catalog)
            {
                prop.objectReferenceValue = catalog;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(go);
                AssetDatabase.SaveAssets();
                Debug.Log("[Emotes] EmoteCatalog câblé sur le prefab HubAvatar.");
            }
        }

        private static void EnsureSpriteImport(string path)
        {
            var imp = AssetImporter.GetAtPath(path) as TextureImporter;
            if (imp == null) return;

            bool changed = false;
            if (imp.textureType != TextureImporterType.Sprite) { imp.textureType = TextureImporterType.Sprite; changed = true; }
            if (imp.spriteImportMode != SpriteImportMode.Single) { imp.spriteImportMode = SpriteImportMode.Single; changed = true; }
            if (!imp.alphaIsTransparency) { imp.alphaIsTransparency = true; changed = true; }
            if (imp.mipmapEnabled) { imp.mipmapEnabled = false; changed = true; }

            if (changed) imp.SaveAndReimport();
        }
    }
}
