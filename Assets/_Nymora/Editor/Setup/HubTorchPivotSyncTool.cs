using System.Collections.Generic;
using Nymora.Hub;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Nymora.Editor.Setup
{
    /// <summary>
    /// Fix tri iso torches (23 mai, suite Bug 1) — HubTorchDepthSortTool plaçait le DepthPivot
    /// à sr.bounds.min.y = BAS DU RECTANGLE du sprite (padding transparent inclus), pas aux
    /// pieds VISIBLES de la torche. Resultat : profondeur fausse, le perso passe devant/derriere
    /// au mauvais moment selon chaque torche.
    ///
    /// Comme toutes les torches partagent le MEME sprite, l'ecart "bas du rectangle -> pieds
    /// visibles" est une FRACTION CONSTANTE de la hauteur des bounds. Lorenzo a regle UNE torche
    /// a la main (DP de reference) ; ce tool mesure cette fraction sur la torche selectionnee et
    /// la reapplique a TOUTES les autres torches, ancree sur les bounds propres de chacune
    /// (donc independant de leur position monde ET de leur scale).
    ///
    /// Ne touche QUE le Y du DepthPivot (X/Z preserves). Idempotent. Ne touche pas aux lights
    /// ni aux valeurs IsoDepthSort.
    ///
    /// Usage : selectionne dans la Hierarchy la torche que tu as deja reglee a la main
    /// (son GameObject de sprite, celui qui porte IsoDepthSort), puis lance le menu.
    ///
    /// Menu : Nymora > Setup > Sync Torch DepthPivots From Selected.
    /// </summary>
    public static class HubTorchPivotSyncTool
    {
        [MenuItem("Nymora/Setup/Sync Torch DepthPivots From Selected", priority = 69)]
        private static void Sync()
        {
            if (Application.isPlaying)
            {
                EditorUtility.DisplayDialog("Sync Torch DepthPivots", "Impossible pendant Play Mode.", "OK");
                return;
            }

            var reference = ResolveReference();
            if (reference == null)
            {
                EditorUtility.DisplayDialog("Sync Torch DepthPivots",
                    "Selectionne d'abord la torche que tu as reglee a la main\n" +
                    "(le GameObject du sprite torche, celui qui porte IsoDepthSort), puis relance.", "OK");
                return;
            }

            var refSr = reference.GetComponent<SpriteRenderer>();
            var refPivot = GetPivot(reference);
            if (refSr == null || refPivot == null)
            {
                EditorUtility.DisplayDialog("Sync Torch DepthPivots",
                    "La torche de reference n'a pas de SpriteRenderer ou pas de DepthPivot branche.", "OK");
                return;
            }

            // Fraction "pieds visibles" mesuree depuis le bas des bounds de la torche de reference.
            // Scale-invariant : on stocke un ratio de la hauteur des bounds, pas un offset absolu.
            float refHeight = refSr.bounds.size.y;
            if (refHeight <= 0.0001f)
            {
                EditorUtility.DisplayDialog("Sync Torch DepthPivots",
                    "Bounds de la torche de reference degeneres (hauteur ~0).", "OK");
                return;
            }
            float feetFraction = (refPivot.position.y - refSr.bounds.min.y) / refHeight;

            int count = 0;
            var skipped = new List<string>();
            foreach (var iso in Object.FindObjectsByType<IsoDepthSort>(FindObjectsSortMode.None))
            {
                if (iso == reference) continue; // ne pas re-toucher la reference
                var sr = iso.GetComponent<SpriteRenderer>();
                var pivot = GetPivot(iso);
                if (sr == null || pivot == null)
                {
                    skipped.Add(iso.gameObject.name);
                    continue;
                }

                float targetY = sr.bounds.min.y + feetFraction * sr.bounds.size.y;
                Vector3 wp = pivot.position;
                Undo.RecordObject(pivot, "Sync DepthPivot Y");
                pivot.position = new Vector3(wp.x, targetY, wp.z);
                count++;
            }

            var scene = EditorSceneManager.GetActiveScene();
            if (scene.IsValid()) EditorSceneManager.MarkSceneDirty(scene);

            string summary = $"Reference : {reference.name} (pieds a {feetFraction:P0} du bas des bounds).\n" +
                             $"{count} torches re-alignees sur cette fraction.";
            if (skipped.Count > 0)
                summary += $"\nIgnorees (pas de SpriteRenderer/DepthPivot) : {string.Join(", ", skipped)}.";
            summary += "\n\nVerifie en bougeant le perso, puis Ctrl+S.";
            EditorUtility.DisplayDialog("Sync Torch DepthPivots", summary, "OK");
            Debug.Log("[HubTorchPivotSyncTool] " + summary);
        }

        /// <summary>Resout l'IsoDepthSort de reference depuis la selection (le GO selectionne ou un parent).</summary>
        private static IsoDepthSort ResolveReference()
        {
            foreach (var go in Selection.gameObjects)
            {
                var iso = go.GetComponent<IsoDepthSort>();
                if (iso != null) return iso;
                iso = go.GetComponentInParent<IsoDepthSort>();
                if (iso != null) return iso;
            }
            return null;
        }

        /// <summary>Lit le DepthPivot branche dans IsoDepthSort (_depthPivot prive), fallback transform.</summary>
        private static Transform GetPivot(IsoDepthSort iso)
        {
            var so = new SerializedObject(iso);
            var prop = so.FindProperty("_depthPivot");
            if (prop != null && prop.objectReferenceValue is Transform t && t != null) return t;
            return iso.transform;
        }
    }
}
