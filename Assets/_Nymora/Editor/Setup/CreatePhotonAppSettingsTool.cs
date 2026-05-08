using System.IO;
using Nymora.Network;
using UnityEditor;
using UnityEngine;

namespace Nymora.Editor.Setup
{
    /// <summary>
    /// Genere un asset PhotonAppSettings vide dans Assets/_Nymora/Settings/.
    /// Lorenzo doit ensuite remplir QuantumAppId et FusionAppId dans l'Inspector.
    /// L'asset est git-ignored (jamais committe) pour eviter de leak les cles.
    ///
    /// Menu : Nymora &gt; Setup &gt; Create Photon App Settings
    /// </summary>
    public static class CreatePhotonAppSettingsTool
    {
        private const string TargetFolder = "Assets/_Nymora/Settings";
        private const string AssetName = "PhotonAppSettings";

        [MenuItem("Nymora/Setup/Create Photon App Settings", priority = 30)]
        private static void CreateAsset()
        {
            EnsureFolderExists();

            string path = $"{TargetFolder}/{AssetName}.asset";

            if (File.Exists(path))
            {
                if (!EditorUtility.DisplayDialog("Photon App Settings",
                    $"{AssetName}.asset existe deja. Le selectionner ?",
                    "Selectionner", "Annuler"))
                {
                    return;
                }
                var existing = AssetDatabase.LoadAssetAtPath<PhotonAppSettings>(path);
                Selection.activeObject = existing;
                EditorGUIUtility.PingObject(existing);
                return;
            }

            var settings = ScriptableObject.CreateInstance<PhotonAppSettings>();
            settings.name = AssetName;

            AssetDatabase.CreateAsset(settings, path);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Selection.activeObject = settings;
            EditorGUIUtility.PingObject(settings);

            Debug.Log($"[Nymora.Setup] PhotonAppSettings cree : {path}");
            Debug.Log("[Nymora.Setup] -> Colle tes AppIds Quantum et Fusion dans l'Inspector.");

            EditorUtility.DisplayDialog("Photon App Settings",
                $"Asset cree : {path}\n\n" +
                "Prochaine etape :\n" +
                "1. Selectionne l'asset (deja fait)\n" +
                "2. Colle les AppIds dans l'Inspector\n" +
                "3. L'asset reste local (.gitignore)",
                "OK");
        }

        private static void EnsureFolderExists()
        {
            if (!AssetDatabase.IsValidFolder(TargetFolder))
            {
                Directory.CreateDirectory(TargetFolder);
                AssetDatabase.Refresh();
            }
        }
    }
}
