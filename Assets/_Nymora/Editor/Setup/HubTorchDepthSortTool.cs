using System.Collections.Generic;
using Nymora.Hub;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Nymora.Editor.Setup
{
    /// <summary>
    /// Outil — configure le tri iso (perso devant/derriere) sur les sprites torche.
    /// Pour chaque torche : ajoute le composant IsoDepthSort + cree un enfant "DepthPivot"
    /// place AUX PIEDS du sprite (bas du sprite) + le branche dans IsoDepthSort.
    ///
    /// Usage : selectionne tes GameObjects de sprite torche dans la Hierarchy puis lance le
    /// menu. Si rien n'est selectionne, l'outil scanne la scene pour les SpriteRenderer dont
    /// le nom contient "torch".
    ///
    /// Idempotent (reutilise DepthPivot + IsoDepthSort existants, repositionne le pivot).
    /// Ne touche pas aux lights.
    ///
    /// Menu : Nymora > Setup > Setup Torch Depth Sort.
    /// </summary>
    public static class HubTorchDepthSortTool
    {
        private const string PivotName = "DepthPivot";

        [MenuItem("Nymora/Setup/Setup Torch Depth Sort", priority = 68)]
        private static void Setup()
        {
            if (Application.isPlaying)
            {
                EditorUtility.DisplayDialog("Torch Depth Sort", "Impossible pendant Play Mode.", "OK");
                return;
            }

            var targets = CollectTargets();
            if (targets.Count == 0)
            {
                EditorUtility.DisplayDialog("Torch Depth Sort",
                    "Aucune torche trouvee.\n\nSelectionne tes GameObjects de sprite torche dans la Hierarchy " +
                    "(ceux qui ont un Sprite Renderer), puis relance le menu.\n\n" +
                    "Sinon, nomme-les avec 'torch' dans le nom pour l'auto-detection.", "OK");
                return;
            }

            int done = 0;
            foreach (var sr in targets)
            {
                ConfigureTorch(sr);
                done++;
            }

            var scene = EditorSceneManager.GetActiveScene();
            if (scene.IsValid()) EditorSceneManager.MarkSceneDirty(scene);

            string summary = $"{done} torches configurees (IsoDepthSort + DepthPivot aux pieds).\n" +
                             "Le perso passe maintenant devant/derriere selon sa position. Ctrl+S pour sauver.";
            EditorUtility.DisplayDialog("Torch Depth Sort", summary, "OK");
            Debug.Log("[HubTorchDepthSortTool] " + summary);
        }

        private static List<SpriteRenderer> CollectTargets()
        {
            var result = new List<SpriteRenderer>();

            // 1) Selection explicite.
            foreach (var go in Selection.gameObjects)
            {
                var sr = go.GetComponent<SpriteRenderer>();
                if (sr != null && !result.Contains(sr)) result.Add(sr);
            }
            if (result.Count > 0) return result;

            // 2) Fallback : scan de la scene par nom "torch".
            foreach (var sr in Object.FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None))
            {
                if (sr.gameObject.name.ToLowerInvariant().Contains("torch"))
                    result.Add(sr);
            }
            return result;
        }

        private static void ConfigureTorch(SpriteRenderer sr)
        {
            var go = sr.gameObject;

            // Composant IsoDepthSort.
            var iso = go.GetComponent<IsoDepthSort>();
            if (iso == null) iso = Undo.AddComponent<IsoDepthSort>(go);

            // Enfant DepthPivot place au bas du sprite (les "pieds"). On ne repositionne QUE
            // si on vient de le creer : un DepthPivot deja present a pu etre regle a la main
            // (cf Bug 1, 23 mai) -> ne jamais ecraser ce reglage. Pour re-aligner les autres
            // torches sur un pivot regle a la main, utiliser HubTorchPivotSyncTool.
            var pivot = go.transform.Find(PivotName);
            if (pivot == null)
            {
                var p = new GameObject(PivotName);
                Undo.RegisterCreatedObjectUndo(p, "Create DepthPivot");
                p.transform.SetParent(go.transform, false);
                pivot = p.transform;

                // bounds.min.y = bas du RECTANGLE du sprite (padding inclus) : heuristique
                // grossiere, a affiner a la main puis propager via HubTorchPivotSyncTool.
                float feetY = sr.bounds.min.y;
                pivot.position = new Vector3(go.transform.position.x, feetY, go.transform.position.z);
            }

            // Branche le pivot dans IsoDepthSort (_depthPivot prive).
            var so = new SerializedObject(iso);
            var prop = so.FindProperty("_depthPivot");
            if (prop != null) prop.objectReferenceValue = pivot;
            var baseProp = so.FindProperty("_baseOrder");
            if (baseProp != null) baseProp.intValue = 100; // match avatar
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(iso);
        }
    }
}
