using Nymora.Combat.View;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Nymora.Editor.Tools
{
    /// <summary>
    /// 3.6.g — Force les valeurs Scale + Y Offset sur le composant DecoyView de la scene
    /// courante pour qu'elles correspondent au prefab Ghostra (RestructureGhostraPrefabTool).
    ///
    /// Pourquoi ce tool : Lorenzo a deja ajoute DecoyView dans 30_CombatIA avec les
    /// anciennes valeurs (Scale 1 / Y 0). Modifier les SerializeField defaults dans le
    /// code ne change pas les valeurs serialisees dans la scene. Ce tool force la
    /// resync proprement.
    ///
    /// Idempotent : peut etre relance, ecrase les valeurs courantes.
    /// Marque la scene comme dirty pour que Ctrl+S la sauve.
    /// </summary>
    public static class PatchDecoyViewSettingsTool
    {
        // Doit rester aligne avec RestructureGhostraPrefabTool (DefaultScale / DefaultYOffset).
        // Valeurs finales calibrees par Lorenzo en Play Mode (16 mai 2026).
        private static readonly Vector3 TargetScale = new Vector3(1.16f, 1.16f, 1f);
        private const float TargetYOffset = -0.22f;

        [MenuItem("Nymora/Setup/Patch DecoyView Settings (Scale + Y Offset)")]
        public static void Run()
        {
            var allViews = Object.FindObjectsByType<DecoyView>(FindObjectsSortMode.None);
            if (allViews == null || allViews.Length == 0)
            {
                Debug.LogError("[PatchDecoyView] Aucun DecoyView trouve dans la scene courante. " +
                               "Ajoute le composant via Hierarchy avant de relancer ce tool.");
                return;
            }

            int patched = 0;
            foreach (var view in allViews)
            {
                var so = new SerializedObject(view);
                var scaleProp = so.FindProperty("_decoyScale");
                var yProp = so.FindProperty("_decoyYOffset");
                if (scaleProp == null || yProp == null)
                {
                    Debug.LogWarning($"[PatchDecoyView] Champs _decoyScale / _decoyYOffset introuvables sur {view.name}. " +
                                     "Recompile DecoyView.cs et relance.");
                    continue;
                }

                Vector3 oldScale = scaleProp.vector3Value;
                float oldY = yProp.floatValue;
                scaleProp.vector3Value = TargetScale;
                yProp.floatValue = TargetYOffset;
                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(view);
                patched++;

                Debug.Log($"[PatchDecoyView] OK '{view.name}' : Scale {oldScale} -> {TargetScale}, Y {oldY} -> {TargetYOffset}");
            }

            if (patched > 0)
            {
                EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
                Debug.Log($"[PatchDecoyView] {patched} composant(s) patche(s). Ctrl+S pour sauver la scene.");
            }
        }
    }
}
