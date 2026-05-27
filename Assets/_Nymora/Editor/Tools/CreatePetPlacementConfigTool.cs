using System.IO;
using Nymora.Core.ScriptableObjects;
using UnityEditor;
using UnityEngine;

namespace Nymora.Editor.Tools
{
    /// <summary>
    /// Brique 5.10 (B4 — reliquat placement) — Crée l'asset PetPlacementConfig dans Resources
    /// (avec les valeurs par défaut) s'il n'existe pas, puis le sélectionne.
    ///
    /// Optionnel : le bouton "Sauvegarder" du panneau Play Mode (HubPetPlacementTuner) crée déjà
    /// l'asset. Ce menu sert juste à le générer/ouvrir hors Play Mode.
    ///
    /// Menu : Nymora > Setup > Create Pet Placement Config.
    /// </summary>
    public static class CreatePetPlacementConfigTool
    {
        private const string Dir = "Assets/_Nymora/Resources/config";
        private const string Path = Dir + "/PetPlacementConfig.asset";

        [MenuItem("Nymora/Setup/Create Pet Placement Config")]
        public static void Create()
        {
            var existing = AssetDatabase.LoadAssetAtPath<PetPlacementConfig>(Path);
            if (existing != null)
            {
                Selection.activeObject = existing;
                EditorGUIUtility.PingObject(existing);
                Debug.Log($"[PetPlacementConfig] Existe déjà : {Path}");
                return;
            }

            if (!Directory.Exists(Dir)) Directory.CreateDirectory(Dir);
            var asset = ScriptableObject.CreateInstance<PetPlacementConfig>();
            AssetDatabase.CreateAsset(asset, Path);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Selection.activeObject = asset;
            EditorGUIUtility.PingObject(asset);
            Debug.Log($"[PetPlacementConfig] Créé : {Path}");
        }
    }
}
