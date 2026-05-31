using Nymora.Hub;
using UnityEditor;
using UnityEngine;

namespace Nymora.Editor.Setup
{
    /// <summary>
    /// POLISH KYAMI — spawn/retire la <see cref="NormalMapDebugLight"/> en Play Mode pour vérifier
    /// que les normal maps réagissent à la lumière, SANS toucher aux lights de la scène.
    ///
    /// Usage : entre en Play Mode (hub ou combat), lance ce menu, balade la souris sur un perso ou
    /// le sol → le relief doit bouger. Re-lancer le menu (ou [K]) retire la lumière. Tout disparaît
    /// en sortie de Play Mode, rien n'est sauvegardé.
    ///
    /// Menu : Nymora > Setup > Polish Kyami > Spawn Normal Map Debug Light (Play Mode).
    /// </summary>
    public static class SpawnNormalMapDebugLightTool
    {
        [MenuItem("Nymora/Setup/Polish Kyami/Spawn Normal Map Debug Light (Play Mode)", priority = 70)]
        public static void Toggle()
        {
            if (!Application.isPlaying)
            {
                EditorUtility.DisplayDialog("Normal Map Debug Light",
                    "Entre d'abord en Play Mode (hub ou combat), puis relance ce menu.", "OK");
                return;
            }

            var existing = Object.FindObjectOfType<NormalMapDebugLight>();
            if (existing != null)
            {
                Object.Destroy(existing.gameObject);
                Debug.Log("[NormalMapDebug] Lumière de debug retirée.");
                return;
            }

            var go = new GameObject("~NormalMapDebugLight");
            go.AddComponent<NormalMapDebugLight>();
            Debug.Log("[NormalMapDebug] Lumière de debug spawnée — bouge la souris, [L] on/off, [K] supprimer.");
        }
    }
}
