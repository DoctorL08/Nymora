using Nymora.Combat.View.HUD;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Button = UnityEngine.UI.Button;
using Object = UnityEngine.Object;

namespace Nymora.Editor.Setup
{
    /// <summary>
    /// Brique 3.E.polish — Replace le bouton "Sauvegarder le replay" du MatchEndOverlay
    /// directement sous les boutons Rejouer Easy/Medium (centre horizontalement, meme
    /// hauteur), pour un layout propre. Copie l'anchor/pivot/parent d'un des boutons
    /// Rejouer afin de rester coherent avec ton design existant.
    ///
    /// Menu : Nymora > Setup > Fix MatchEnd Save Replay Position
    /// Idempotent : peut etre relance autant de fois que necessaire.
    /// </summary>
    public static class FixMatchEndLayoutTool
    {
        [MenuItem("Nymora/Setup/Fix MatchEnd Save Replay Position")]
        public static void Run()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (!scene.IsValid())
            {
                EditorUtility.DisplayDialog("Fix MatchEnd Layout", "Aucune scene ouverte.", "OK");
                return;
            }

            var overlay = Object.FindAnyObjectByType<MatchEndOverlay>(FindObjectsInactive.Include);
            if (overlay == null)
            {
                EditorUtility.DisplayDialog("Fix MatchEnd Layout",
                    "MatchEndOverlay introuvable dans la scene active.\n" +
                    "Lance d'abord 'Nymora > Setup > Create Combat HUD' si manquant.",
                    "OK");
                return;
            }

            var so = new SerializedObject(overlay);
            var easyBtn = so.FindProperty("_restartEasyButton").objectReferenceValue as Button;
            var mediumBtn = so.FindProperty("_restartMediumButton").objectReferenceValue as Button;
            var saveBtn = so.FindProperty("_saveReplayButton").objectReferenceValue as Button;

            if (saveBtn == null)
            {
                EditorUtility.DisplayDialog("Fix MatchEnd Layout",
                    "_saveReplayButton n'est pas assigne dans le MatchEndOverlay.\n" +
                    "Wire-le d'abord dans l'Inspector.",
                    "OK");
                return;
            }

            Button refBtn = easyBtn != null ? easyBtn : mediumBtn;
            if (refBtn == null)
            {
                EditorUtility.DisplayDialog("Fix MatchEnd Layout",
                    "Aucun des boutons Rejouer Easy/Medium n'est assigne — impossible de calculer la position cible.",
                    "OK");
                return;
            }

            var refRt = refBtn.GetComponent<RectTransform>();
            var saveRt = saveBtn.GetComponent<RectTransform>();

            // Si pas deja dans le meme parent, on reparent pour cohérence visuelle.
            if (saveRt.parent != refRt.parent)
            {
                Undo.SetTransformParent(saveRt, refRt.parent, "Reparent SaveReplay button");
            }

            Undo.RecordObject(saveRt, "Reposition SaveReplay button");

            // Aligne anchor/pivot avec le bouton Rejouer de reference.
            saveRt.anchorMin = refRt.anchorMin;
            saveRt.anchorMax = refRt.anchorMax;
            saveRt.pivot = refRt.pivot;

            // Place 12px sous le bouton Rejouer (en tenant compte du pivot et de la hauteur).
            // Convention RectTransform : anchoredPosition.y est par rapport au pivot.
            // Pour un pivot.y = 0.5 standard, "en dessous" = anchoredPosition.y - height - gap.
            // Pour pivot.y = 1 (top), idem mais la sizeDelta s'etend vers le bas.
            float gap = 12f;
            saveRt.anchoredPosition = new Vector2(
                refRt.anchoredPosition.x * 0f, // centre horizontalement
                refRt.anchoredPosition.y - refRt.sizeDelta.y - gap);

            // Width plus grande que les Rejouer (texte "Sauvegarder le replay" plus long).
            saveRt.sizeDelta = new Vector2(
                Mathf.Max(refRt.sizeDelta.x * 1.6f, 240f),
                refRt.sizeDelta.y);

            // Place en dernier dans la hierarchy pour s'assurer qu'il est rendered au-dessus.
            saveRt.SetAsLastSibling();

            EditorSceneManager.MarkSceneDirty(scene);
            Selection.activeGameObject = saveBtn.gameObject;
            EditorGUIUtility.PingObject(saveBtn.gameObject);

            EditorUtility.DisplayDialog("Fix MatchEnd Layout",
                "Bouton 'Sauvegarder le replay' repositionne :\n" +
                "  - centre horizontalement\n" +
                "  - " + gap + "px sous le bouton Rejouer de reference\n" +
                "  - largeur " + saveRt.sizeDelta.x + "px\n\n" +
                "Ctrl+S pour sauvegarder la scene.",
                "OK");
        }
    }
}
