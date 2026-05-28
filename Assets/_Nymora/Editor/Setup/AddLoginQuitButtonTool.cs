using Nymora.UI.Login;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Nymora.Editor.Setup
{
    /// <summary>
    /// Ajoute (de facon NON destructive) un bouton "Quitter" a l'ecran 00_Login,
    /// place juste SOUS le bouton "Pas de compte ? S'inscrire", en CLONANT ce dernier
    /// (donc meme style/design) puis en le recablant sur LoginScreenController._quitButton.
    ///
    /// Contrairement a "Refine Login Form", ne reconstruit/efface RIEN : le design existant
    /// (case memoriser, polices, fonds, mise en page) est preserve a 100 %.
    /// Le bouton S'inscrire est resolu via la reference reelle du controller (pas par nom),
    /// donc l'outil s'adapte a la vraie scene. Idempotent (relancable sans doublon).
    ///
    /// Menu : Nymora &gt; Setup &gt; Add Login Quit Button
    /// </summary>
    public static class AddLoginQuitButtonTool
    {
        private const string LoginScenePath = "Assets/_Nymora/Scenes/00_Login.unity";
        private const float GapBelowRegister = 18f;

        [MenuItem("Nymora/Setup/Add Login Quit Button", priority = 36)]
        private static void AddQuitButton()
        {
            if (!System.IO.File.Exists(LoginScenePath))
            {
                EditorUtility.DisplayDialog("Add Quit Button", $"Scene introuvable : {LoginScenePath}", "OK");
                return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            var scene = EditorSceneManager.OpenScene(LoginScenePath, OpenSceneMode.Single);
            var controller = Object.FindAnyObjectByType<LoginScreenController>();
            if (controller == null)
            {
                EditorUtility.DisplayDialog("Add Quit Button", "LoginScreenController introuvable.", "OK");
                return;
            }

            var so = new SerializedObject(controller);
            var registerBtn = so.FindProperty("_openRegisterButton").objectReferenceValue as Button;
            if (registerBtn == null)
            {
                EditorUtility.DisplayDialog("Add Quit Button",
                    "Le bouton S'inscrire (_openRegisterButton) n'est pas cable sur le controller.", "OK");
                return;
            }

            var existingQuit = so.FindProperty("_quitButton").objectReferenceValue as Button;
            Button quitBtn;
            if (existingQuit != null)
            {
                quitBtn = existingQuit; // deja present -> on repositionne/recable seulement
            }
            else
            {
                var clone = Object.Instantiate(registerBtn.gameObject, registerBtn.transform.parent);
                clone.name = "QuitButton";
                quitBtn = clone.GetComponent<Button>();

                var label = clone.GetComponentInChildren<TMP_Text>(true);
                if (label != null) label.text = "Quitter";

                // Le clone herite des onClick persistants de S'inscrire : on les vide
                // (le controller cable OnQuitClicked au runtime).
                var soBtn = new SerializedObject(quitBtn);
                var calls = soBtn.FindProperty("m_OnClick.m_PersistentCalls.m_Calls");
                if (calls != null) { calls.ClearArray(); soBtn.ApplyModifiedPropertiesWithoutUndo(); }
            }

            // Meme ancrage / taille que S'inscrire, juste en-dessous.
            var rReg = (RectTransform)registerBtn.transform;
            var rQuit = (RectTransform)quitBtn.transform;
            rQuit.anchorMin = rReg.anchorMin;
            rQuit.anchorMax = rReg.anchorMax;
            rQuit.pivot = rReg.pivot;
            rQuit.sizeDelta = rReg.sizeDelta;
            rQuit.anchoredPosition = rReg.anchoredPosition + new Vector2(0f, -(rReg.sizeDelta.y + GapBelowRegister));
            rQuit.SetSiblingIndex(rReg.GetSiblingIndex() + 1);

            so.FindProperty("_quitButton").objectReferenceValue = quitBtn;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Debug.Log("[Nymora.Setup] Bouton Quitter ajoute sous S'inscrire (clone, design preserve).");
            EditorUtility.DisplayDialog("Add Quit Button",
                "Bouton 'Quitter' ajoute sous 'S'inscrire' (meme style).\n" +
                "Le reste de l'ecran login est intact.", "OK");
        }
    }
}
