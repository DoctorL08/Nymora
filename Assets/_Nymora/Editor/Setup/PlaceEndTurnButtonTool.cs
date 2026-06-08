using System.IO;
using Nymora.Combat.View.HUD;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Nymora.Editor.Setup
{
    /// <summary>
    /// Patch UI combat 8 juin (3c) — Repositionne le bouton « Fin de tour » au MILIEU-DROIT
    /// (révision Lorenzo : un poil plus grand, à droite). Applique le même RectTransform sur les
    /// 3 scènes combat, en lisant la ref _endTurnButton du CombatHUDController (pas de devinette
    /// de nom).
    ///
    /// Les valeurs ci-dessous sont des points de départ : après avoir lancé l'outil, tu peux
    /// nudger finement la position (surtout Y) directement dans l'Inspector du bouton en Play/Edit.
    ///
    /// 100% View (RectTransform de scène) -> PAS de bump CombatRulesVersion. Scènes modifiées ->
    /// rebuild standalone côté designer.
    ///
    /// Menu : Nymora &gt; Setup &gt; UI Menu &gt; Place End Turn Button
    /// </summary>
    public static class PlaceEndTurnButtonTool
    {
        // ----- Tunables (nudge libre dans l'Inspector après coup) -----
        private static readonly Vector2 ButtonSize = new Vector2(180f, 52f);
        // Ancré au MILIEU-DROIT du parent (pivot milieu-droit). X négatif = marge depuis le bord
        // droit. Y = décalage vertical par rapport au centre (0 = pile au milieu). Nudge si besoin.
        private static readonly Vector2 AnchoredPos = new Vector2(-24f, 0f);
        private const float LabelFontSize = 20f;
        // ---------------------------------------------------------------

        private static readonly string[] CombatScenes =
        {
            "Assets/_Nymora/Scenes/30_CombatIA.unity",
            "Assets/_Nymora/Scenes/33_CombatCasual.unity",
            "Assets/_Nymora/Scenes/40_CombatRanked1v1.unity",
        };

        [MenuItem("Nymora/Setup/UI Menu/Place End Turn Button", priority = 38)]
        private static void Run()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            int done = 0;
            foreach (var path in CombatScenes)
            {
                if (!File.Exists(path)) { Debug.LogWarning($"[PlaceEndTurnButton] Scène absente : {path}"); continue; }

                var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                var controller = Object.FindAnyObjectByType<CombatHUDController>(FindObjectsInactive.Include);
                if (controller == null)
                {
                    Debug.LogWarning($"[PlaceEndTurnButton] CombatHUDController introuvable dans {path}.");
                    continue;
                }

                var so = new SerializedObject(controller);
                var btn = so.FindProperty("_endTurnButton")?.objectReferenceValue as Button;
                if (btn == null)
                {
                    Debug.LogWarning($"[PlaceEndTurnButton] _endTurnButton non câblé dans {path}.");
                    continue;
                }

                var rt = (RectTransform)btn.transform;
                // Ancrage + pivot milieu-droit -> AnchoredPos est relatif au bord droit, centré en Y.
                rt.anchorMin = new Vector2(1f, 0.5f);
                rt.anchorMax = new Vector2(1f, 0.5f);
                rt.pivot = new Vector2(1f, 0.5f);
                rt.sizeDelta = ButtonSize;
                rt.anchoredPosition = AnchoredPos;

                // Label plus petit pour matcher la taille réduite du bouton.
                var label = btn.GetComponentInChildren<TMP_Text>(true);
                if (label != null)
                {
                    label.enableAutoSizing = false; // cf mémoire : éviter l'auto-sizing TMP
                    label.fontSize = LabelFontSize;
                    EditorUtility.SetDirty(label);
                }

                EditorUtility.SetDirty(btn);
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                done++;
            }

            Debug.Log($"[PlaceEndTurnButton] Terminé : {done} scène(s).");
            EditorUtility.DisplayDialog("Place End Turn Button",
                $"Bouton Fin de tour repositionné (milieu-droit, {ButtonSize.x}x{ButtonSize.y}) sur {done} scène(s).\n\n" +
                "Nudge la position dans l'Inspector du bouton si besoin.\n\n" +
                "⚠️ Scènes modifiées -> rebuild standalone côté designer.",
                "OK");
        }
    }
}
