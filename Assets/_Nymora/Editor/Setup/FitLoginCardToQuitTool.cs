#if UNITY_EDITOR
using Nymora.UI.Login;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Nymora.Editor.Setup
{
    /// <summary>
    /// Fix NON-DESTRUCTIF : agrandit la carte de connexion de 00_Login VERS LE BAS pour qu'elle
    /// englobe le bouton "Quitter" (ajouté sous "S'inscrire") + le hint. Ne reconstruit RIEN
    /// (contrairement à Restyle Login Scene) : il déplace/redimensionne uniquement le RectTransform
    /// de la carte et décale ses enfants pour qu'ils ne bougent pas à l'écran.
    ///
    /// Math : on étend le bas de Δ en gardant le HAUT et les enfants à leur position écran :
    ///   carte.y -= Δ/2 ; carte.height += Δ ; chaque enfant.y += Δ/2.
    /// Idempotent : si la carte englobe déjà le Quitter (+ marge), ne fait rien.
    ///
    /// Menu : Nymora > Setup > Fit Login Card To Quit.
    /// </summary>
    public static class FitLoginCardToQuitTool
    {
        private const string LoginScenePath = "Assets/_Nymora/Scenes/00_Login.unity";
        private const float Margin = 64f; // marge sous le Quitter (englobe aussi le hint de statut)

        [MenuItem("Nymora/Setup/Fit Login Card To Quit", priority = 37)]
        private static void Fit()
        {
            if (!System.IO.File.Exists(LoginScenePath))
            {
                EditorUtility.DisplayDialog("Fit Login Card", $"Scène introuvable : {LoginScenePath}", "OK");
                return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            var scene = EditorSceneManager.OpenScene(LoginScenePath, OpenSceneMode.Single);
            var controller = Object.FindAnyObjectByType<LoginScreenController>();
            if (controller == null)
            {
                EditorUtility.DisplayDialog("Fit Login Card", "LoginScreenController introuvable.", "OK");
                return;
            }

            var so = new SerializedObject(controller);
            var quit = so.FindProperty("_quitButton").objectReferenceValue as Button;
            if (quit == null)
            {
                EditorUtility.DisplayDialog("Fit Login Card",
                    "Bouton Quitter (_quitButton) non câblé. Lance d'abord 'Add Login Quit Button'.", "OK");
                return;
            }

            var quitRt = (RectTransform)quit.transform;
            var card = quitRt.parent as RectTransform; // le Quitter est enfant de la carte de connexion
            if (card == null)
            {
                EditorUtility.DisplayDialog("Fit Login Card", "Parent du Quitter introuvable (carte).", "OK");
                return;
            }

            // Positions en espace du PARENT de la carte (centre-pivot supposé, comme MakeCard).
            float cardBottom = card.anchoredPosition.y - card.sizeDelta.y * 0.5f;
            float quitBottom = card.anchoredPosition.y + quitRt.anchoredPosition.y - quitRt.sizeDelta.y * 0.5f;
            float target = quitBottom - Margin; // bas de carte voulu

            if (cardBottom <= target + 0.5f)
            {
                EditorUtility.DisplayDialog("Fit Login Card",
                    "La carte englobe déjà le bouton Quitter. Rien à faire.", "OK");
                return;
            }

            float delta = cardBottom - target; // > 0 : on étend le bas de `delta`

            Undo.RecordObject(card, "Fit Login Card");
            card.anchoredPosition += new Vector2(0f, -delta * 0.5f);
            card.sizeDelta += new Vector2(0f, delta);
            // Décale les enfants de +Δ/2 pour qu'ils restent à la même position à l'écran.
            for (int i = 0; i < card.childCount; i++)
            {
                var child = card.GetChild(i) as RectTransform;
                if (child == null) continue;
                Undo.RecordObject(child, "Fit Login Card");
                child.anchoredPosition += new Vector2(0f, delta * 0.5f);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Debug.Log($"[Nymora.Setup] Carte login agrandie de {delta:0}px vers le bas (englobe le Quitter). " +
                      "Le haut et les champs n'ont pas bougé à l'écran.");
            EditorUtility.DisplayDialog("Fit Login Card",
                $"Carte agrandie de {delta:0}px vers le bas → elle englobe maintenant le bouton Quitter.\n" +
                "Aucun élément n'a bougé à l'écran (seul le bas de la carte s'allonge).", "OK");
        }
    }
}
#endif
