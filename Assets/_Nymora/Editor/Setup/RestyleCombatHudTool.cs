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
    /// Re-skin du HUD combat pour matcher la DA du menu hub (monochrome + police Ari + coins
    /// arrondis). BRIQUE 1 : barre de sorts (6 + signature) + bouton Fin de tour.
    ///
    /// Ce que fait l'outil, sur les 3 scènes combat, en lisant les refs du CombatHUDController
    /// (pas de devinette de noms d'objets) :
    ///   - Spell slots : police Ari sur les labels raccourci/cooldown, couleurs monochromes.
    ///     (La frame arrondie + ses couleurs d'état sont gérées au runtime par SpellSlotView /
    ///      CombatUiKit ; rien à sérialiser ici.)
    ///   - Bouton Fin de tour : pilule claire (Accent) + texte sombre Ari + coins arrondis
    ///     (composant CombatUiRounder) + ColorBlock monochrome (grisé quand désactivé).
    ///
    /// Idempotent (relançable). Les scènes sont modifiées -> le designer doit rebuild son
    /// standalone. 100% View -> PAS de bump CombatRulesVersion.
    ///
    /// Menu : Nymora &gt; Setup &gt; UI Menu &gt; Restyle Combat HUD (spell bar)
    /// </summary>
    public static class RestyleCombatHudTool
    {
        private static readonly string[] CombatScenes =
        {
            "Assets/_Nymora/Scenes/30_CombatIA.unity",
            "Assets/_Nymora/Scenes/33_CombatCasual.unity",
            "Assets/_Nymora/Scenes/40_CombatRanked1v1.unity",
        };

        private const string AriPath = "Assets/_Nymora/Art/Fonts/Ari W9500 SDF.asset";
        private const string AriBoldPath = "Assets/_Nymora/Art/Fonts/Ari W9500 Bold SDF.asset";

        [MenuItem("Nymora/Setup/UI Menu/Restyle Combat HUD (spell bar)", priority = 37)]
        private static void Run()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            var ari = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AriPath);
            var ariBold = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AriBoldPath);
            if (ari == null)
                Debug.LogWarning($"[RestyleCombatHud] Police Ari introuvable à {AriPath} — police conservée.");

            int scenesDone = 0, slotsDone = 0, buttonsDone = 0;

            foreach (var path in CombatScenes)
            {
                if (!File.Exists(path)) { Debug.LogWarning($"[RestyleCombatHud] Scène absente : {path}"); continue; }

                var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                var controller = Object.FindAnyObjectByType<CombatHUDController>(FindObjectsInactive.Include);
                if (controller == null)
                {
                    Debug.LogWarning($"[RestyleCombatHud] CombatHUDController introuvable dans {path}.");
                    continue;
                }

                var so = new SerializedObject(controller);

                // --- Spell slots (deck 1-6) ---
                var slotsProp = so.FindProperty("_spellSlots");
                if (slotsProp != null)
                {
                    for (int i = 0; i < slotsProp.arraySize; i++)
                    {
                        var slot = slotsProp.GetArrayElementAtIndex(i).objectReferenceValue as SpellSlotView;
                        if (RestyleSlot(slot, ari, ariBold)) slotsDone++;
                    }
                }

                // --- Slot signature ---
                var sigProp = so.FindProperty("_signatureSlot");
                var sigSlot = sigProp != null ? sigProp.objectReferenceValue as SpellSlotView : null;
                if (RestyleSlot(sigSlot, ari, ariBold)) slotsDone++;

                // --- Bouton Fin de tour ---
                var endProp = so.FindProperty("_endTurnButton");
                var endBtn = endProp != null ? endProp.objectReferenceValue as Button : null;
                if (RestyleEndTurnButton(endBtn, ariBold ?? ari)) buttonsDone++;

                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                scenesDone++;
            }

            Debug.Log($"[RestyleCombatHud] Terminé : {scenesDone} scène(s), {slotsDone} slot(s), {buttonsDone} bouton(s) Fin de tour.");
            EditorUtility.DisplayDialog("Restyle Combat HUD",
                $"Barre de sorts + Fin de tour re-skinnés (DA hub).\n\n" +
                $"- Scènes traitées : {scenesDone}\n- Slots : {slotsDone}\n- Boutons Fin de tour : {buttonsDone}\n\n" +
                "Test : Play sur 30_CombatIA -> barre de sorts arrondie monochrome + bouton clair.\n" +
                "⚠️ Scènes modifiées -> rebuild standalone côté designer.",
                "OK");
        }

        /// <summary>Police Ari + couleurs monochromes sur les labels du slot. La frame est gérée au runtime.</summary>
        private static bool RestyleSlot(SpellSlotView slot, TMP_FontAsset ari, TMP_FontAsset ariBold)
        {
            if (slot == null) return false;
            var soSlot = new SerializedObject(slot);
            var key = soSlot.FindProperty("_keyLabel")?.objectReferenceValue as TMP_Text;
            var cd = soSlot.FindProperty("_cooldownLabel")?.objectReferenceValue as TMP_Text;

            // Raccourci (1-6, è) : discret, en bas du slot.
            SetLabel(key, ariBold ?? ari, CombatUiKit.TextSecondary);
            // Cooldown "Nt" : lisible clair (reste distinct via sa position, monochrome).
            SetLabel(cd, ariBold ?? ari, CombatUiKit.TextPrimary);
            return true;
        }

        private static bool RestyleEndTurnButton(Button btn, TMP_FontAsset font)
        {
            if (btn == null) return false;

            var img = (btn.targetGraphic as Image) ?? btn.GetComponent<Image>();
            if (img != null)
            {
                img.color = CombatUiKit.Accent; // pilule claire = CTA hub
                btn.targetGraphic = img;
                if (img.GetComponent<CombatUiRounder>() == null)
                    img.gameObject.AddComponent<CombatUiRounder>();
                EditorUtility.SetDirty(img);
            }

            var cb = btn.colors;
            cb.normalColor = Color.white;
            cb.highlightedColor = Color.white;
            cb.pressedColor = new Color(0.85f, 0.86f, 0.88f, 1f);
            cb.selectedColor = Color.white;
            cb.disabledColor = new Color(0.42f, 0.43f, 0.46f, 0.6f);
            cb.fadeDuration = 0.1f;
            btn.colors = cb;
            EditorUtility.SetDirty(btn);

            var label = btn.GetComponentInChildren<TMP_Text>(true);
            SetLabel(label, font, CombatUiKit.TextOnLight); // texte sombre sur pilule claire
            return true;
        }

        private static void SetLabel(TMP_Text t, TMP_FontAsset font, Color color)
        {
            if (t == null) return;
            if (font != null) t.font = font;
            t.color = color;
            EditorUtility.SetDirty(t);
        }
    }
}
