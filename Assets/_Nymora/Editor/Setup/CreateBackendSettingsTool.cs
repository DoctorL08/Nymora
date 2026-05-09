using System.IO;
using Nymora.Network.Backend;
using UnityEditor;
using UnityEngine;

namespace Nymora.Editor.Setup
{
    /// <summary>
    /// Genere un asset NymoraBackendSettings dans Assets/_Nymora/Settings/.
    /// Contrairement a PhotonAppSettings, l'asset peut etre commit (la BaseUrl n'est pas un secret).
    ///
    /// Menu : Nymora &gt; Setup &gt; Create Backend Settings
    /// </summary>
    public static class CreateBackendSettingsTool
    {
        private const string TargetFolder = "Assets/_Nymora/Settings";
        private const string AssetName = "NymoraBackendSettings";
        public const string AssetPath = TargetFolder + "/" + AssetName + ".asset";

        [MenuItem("Nymora/Setup/Create Backend Settings", priority = 31)]
        private static void CreateAsset()
        {
            EnsureFolderExists();

            if (File.Exists(AssetPath))
            {
                if (!EditorUtility.DisplayDialog("Backend Settings",
                    $"{AssetName}.asset existe deja. Le selectionner ?",
                    "Selectionner", "Annuler"))
                {
                    return;
                }
                var existing = AssetDatabase.LoadAssetAtPath<NymoraBackendSettings>(AssetPath);
                Selection.activeObject = existing;
                EditorGUIUtility.PingObject(existing);
                return;
            }

            var settings = ScriptableObject.CreateInstance<NymoraBackendSettings>();
            settings.name = AssetName;

            AssetDatabase.CreateAsset(settings, AssetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Selection.activeObject = settings;
            EditorGUIUtility.PingObject(settings);

            Debug.Log($"[Nymora.Setup] NymoraBackendSettings cree : {AssetPath}");
            Debug.Log("[Nymora.Setup] -> Verifie BaseUrl dans l'Inspector (defaut: http://localhost:3000).");
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
