using System.Collections.Generic;
using Nymora.Core.ScriptableObjects;
using UnityEditor;
using UnityEngine;

namespace Nymora.Editor.Tools
{
    /// <summary>
    /// Brique 5.10 (A2) — Crée/peuple le CosmeticSkinCatalog (Resources) à partir de tous les
    /// CosmeticSkinDefinition du projet. Le CombatantRenderer le charge en runtime pour résoudre
    /// le skin équipé en combat.
    ///
    /// Asset : Assets/_Nymora/Resources/Cosmetics/CosmeticSkinCatalog.asset
    /// Idempotent. À relancer après l'ajout d'un nouveau skin.
    ///
    /// Menu : Nymora > Setup > Build Cosmetic Skin Catalog.
    /// </summary>
    public static class BuildCosmeticSkinCatalogTool
    {
        private const string ResourcesDir = "Assets/_Nymora/Resources/Cosmetics";
        private const string CatalogPath = ResourcesDir + "/CosmeticSkinCatalog.asset";

        [MenuItem("Nymora/Setup/Build Cosmetic Skin Catalog", priority = 47)]
        public static void Run()
        {
            EnsureFolder("Assets/_Nymora/Resources");
            EnsureFolder(ResourcesDir);

            var catalog = AssetDatabase.LoadAssetAtPath<CosmeticSkinCatalog>(CatalogPath);
            bool created = catalog == null;
            if (created)
            {
                catalog = ScriptableObject.CreateInstance<CosmeticSkinCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }

            var skins = new List<CosmeticSkinDefinition>();
            foreach (var guid in AssetDatabase.FindAssets("t:CosmeticSkinDefinition"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var def = AssetDatabase.LoadAssetAtPath<CosmeticSkinDefinition>(path);
                if (def != null) skins.Add(def);
            }
            skins.Sort((a, b) => string.CompareOrdinal(a.CosmeticId, b.CosmeticId));

            catalog.Skins = skins.ToArray();
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();

            int withCombat = 0;
            foreach (var s in skins) if (s != null && s.HasCombatControllers) withCombat++;

            Debug.Log($"[BuildCosmeticSkinCatalog] {(created ? "Créé" : "Mis à jour")} {CatalogPath} : {skins.Count} skin(s), {withCombat} avec variante combat.");
            Selection.activeObject = catalog;
            EditorUtility.DisplayDialog("Cosmetic Skin Catalog",
                $"{(created ? "Créé" : "Mis à jour")} : {skins.Count} skin(s) référencé(s), dont {withCombat} avec controllers de combat.", "OK");
        }

        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;
            int slash = folder.LastIndexOf('/');
            string parent = folder.Substring(0, slash);
            string name = folder.Substring(slash + 1);
            AssetDatabase.CreateFolder(parent, name);
        }
    }
}
