using Nymora.Hub;
using UnityEditor;
using UnityEngine;

namespace Nymora.Editor.Windows
{
    /// <summary>
    /// Menu utilitaire : reconstruit la grille HubGridRenderer en Edit Mode immédiatement
    /// (sans avoir à Press Play). Utile après avoir changé _width / _height dans l'Inspector
    /// pour preview la nouvelle taille.
    ///
    /// Menu : Nymora > Hub > Rebuild Grid Now (Editor)
    /// </summary>
    public static class HubGridRebuildMenu
    {
        [MenuItem("Nymora/Hub/Rebuild Grid Now (Editor)", priority = 51)]
        private static void Rebuild()
        {
            var grid = Object.FindFirstObjectByType<HubGridRenderer>();
            if (grid == null)
            {
                EditorUtility.DisplayDialog("Rebuild Grid",
                    "Aucun HubGridRenderer trouvé dans la scène active.\nOuvre 10_CommunityHub d'abord.",
                    "OK");
                return;
            }
            grid.BuildGrid();
            EditorUtility.SetDirty(grid);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(grid.gameObject.scene);
            Debug.Log($"[HubGridRebuildMenu] Grille reconstruite : {grid.Width}x{grid.Height}");
        }
    }
}
