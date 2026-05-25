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
    /// arrondis). Couvre, au fil des briques :
    ///   - B1 : barre de sorts (6 + signature) + bouton Fin de tour.
    ///   - B2 : panneaux ressources P0/P1 (police Ari, fond arrondi façon hub).
    ///   - B3 : timer + n° de tour (police Ari) + bandeau « ton tour » (instance de scène
    ///     câblée avec la police Ari, neutralise l'auto-instance par défaut).
    ///   - B4 : timeline d'initiative (couleurs monochromes : cadre actif = accent clair,
    ///     inactif = surface carte ; cadres arrondis gérés au runtime).
    ///   - B5 : tooltips. SpellTooltipView (scène) : police Ari + couleurs + fond arrondi
    ///     PanelBg. PassiveTooltipView (code) : instance de scène câblée Ari (arrondi + palette
    ///     gérés au runtime). CombatantTooltipView (nameplate world-space) laissé tel quel
    ///     (il reproduit déjà le tooltip avatar du hub).
    ///   - B6 : overlay fin de match (scène : titre/sous-titre Ari, backdrop sombre, boutons
    ///     pilule/ghost) + bouton Abandonner (instance de scène câblée Ari, monochrome arrondi).
    ///
    /// Ce que fait l'outil, sur les 3 scènes combat, en lisant les refs du CombatHUDController
    /// (pas de devinette de noms d'objets) :
    ///   - Spell slots : police Ari sur les labels raccourci/cooldown, couleurs monochromes.
    ///     (La frame arrondie + ses couleurs d'état sont gérées au runtime par SpellSlotView /
    ///      CombatUiKit ; rien à sérialiser ici.)
    ///   - Bouton Fin de tour : pilule claire (Accent) + texte sombre Ari + coins arrondis
    ///     (composant CombatUiRounder) + ColorBlock monochrome (grisé quand désactivé).
    ///   - Panneaux ressources : police Ari sur label + ligne de statuts ; fond translucide
    ///     arrondi (si le panneau porte une Image). Les couleurs actif/inactif du label sont
    ///     gérées au runtime par ResourcePanelView / CombatUiKit.
    ///
    /// Idempotent (relançable). Les scènes sont modifiées -> le designer doit rebuild son
    /// standalone. 100% View -> PAS de bump CombatRulesVersion.
    ///
    /// Menu : Nymora &gt; Setup &gt; UI Menu &gt; Restyle Combat HUD
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

        [MenuItem("Nymora/Setup/UI Menu/Restyle Combat HUD", priority = 37)]
        private static void Run()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            var ari = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AriPath);
            var ariBold = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AriBoldPath);
            if (ari == null)
                Debug.LogWarning($"[RestyleCombatHud] Police Ari introuvable à {AriPath} — police conservée.");

            int scenesDone = 0, slotsDone = 0, buttonsDone = 0, panelsDone = 0, timersDone = 0, bannersDone = 0, timelinesDone = 0, tooltipsDone = 0, overlaysDone = 0;

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

                // --- Panneaux ressources P0 / P1 ---
                foreach (var prop in new[] { "_p0Panel", "_p1Panel" })
                {
                    var panel = so.FindProperty(prop)?.objectReferenceValue as ResourcePanelView;
                    if (RestyleResourcePanel(panel, ari, ariBold)) panelsDone++;
                }

                // --- Timer + n° de tour ---
                var timer = so.FindProperty("_timer")?.objectReferenceValue as TimerView;
                if (RestyleTimer(timer, ariBold ?? ari)) timersDone++;

                // --- Bandeau « ton tour » (instance de scène câblée Ari) ---
                if (EnsureTurnIndicator(ariBold ?? ari)) bannersDone++;

                // --- Timeline d'initiative ---
                var timeline = so.FindProperty("_timeline")?.objectReferenceValue as TimelineView;
                if (RestyleTimeline(timeline)) timelinesDone++;

                // --- Tooltips ---
                var spellTooltip = so.FindProperty("_tooltip")?.objectReferenceValue as SpellTooltipView;
                if (RestyleSpellTooltip(spellTooltip, ari, ariBold)) tooltipsDone++;
                if (EnsurePassiveTooltip(ari)) tooltipsDone++;

                // --- Overlay fin de match + bouton Abandonner ---
                var matchEnd = so.FindProperty("_matchEndOverlay")?.objectReferenceValue as MatchEndOverlay;
                if (RestyleMatchEnd(matchEnd, ari, ariBold)) overlaysDone++;
                if (EnsureForfeitButton(ariBold ?? ari)) overlaysDone++;

                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                scenesDone++;
            }

            Debug.Log($"[RestyleCombatHud] Terminé : {scenesDone} scène(s), {slotsDone} slot(s), {buttonsDone} Fin de tour, {panelsDone} panneau(x) ressources, {timersDone} timer(s), {bannersDone} bandeau(x), {timelinesDone} timeline(s), {tooltipsDone} tooltip(s), {overlaysDone} overlay/abandon.");
            EditorUtility.DisplayDialog("Restyle Combat HUD",
                $"HUD combat re-skinné (DA hub).\n\n" +
                $"- Scènes traitées : {scenesDone}\n- Slots : {slotsDone}\n- Boutons Fin de tour : {buttonsDone}\n" +
                $"- Panneaux ressources : {panelsDone}\n- Timers : {timersDone}\n- Bandeaux de tour : {bannersDone}\n- Timelines : {timelinesDone}\n- Tooltips : {tooltipsDone}\n- Overlay/Abandon : {overlaysDone}\n\n" +
                "Test : Play sur 30_CombatIA -> joue jusqu'à la fin + teste Abandonner.\n" +
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

        /// <summary>Police Ari sur le label + la ligne de statuts ; fond translucide arrondi si présent.
        /// Les couleurs actif/inactif du label restent gérées au runtime (ResourcePanelView).</summary>
        private static bool RestyleResourcePanel(ResourcePanelView panel, TMP_FontAsset ari, TMP_FontAsset ariBold)
        {
            if (panel == null) return false;
            var soPanel = new SerializedObject(panel);
            var label = soPanel.FindProperty("_label")?.objectReferenceValue as TMP_Text;
            var statusLine = soPanel.FindProperty("_statusLine")?.objectReferenceValue as TMP_Text;

            // Label : police seulement (couleur pilotée par Refresh).
            if (label != null && (ariBold ?? ari) != null) { label.font = ariBold ?? ari; EditorUtility.SetDirty(label); }
            // Ligne de statuts : police + couleur secondaire (non pilotée par le code).
            SetLabel(statusLine, ari, CombatUiKit.TextSecondary);

            // Fond du panneau : si une Image porte le panneau, on l'arrondit + teinte hub.
            var bg = panel.GetComponent<Image>();
            if (bg != null)
            {
                bg.color = new Color(0.07f, 0.072f, 0.085f, 0.85f); // chip sombre translucide (façon barres hub)
                if (bg.GetComponent<CombatUiRounder>() == null)
                    bg.gameObject.AddComponent<CombatUiRounder>();
                EditorUtility.SetDirty(bg);
            }
            return true;
        }

        /// <summary>Police Ari sur le timer + le label de tour ; couleur du tour = secondaire (le temps
        /// reste piloté par TimerView : clair / rouge urgence).</summary>
        private static bool RestyleTimer(TimerView timer, TMP_FontAsset font)
        {
            if (timer == null) return false;
            var so = new SerializedObject(timer);
            var label = so.FindProperty("_label")?.objectReferenceValue as TMP_Text;
            var turn = so.FindProperty("_turnLabel")?.objectReferenceValue as TMP_Text;
            // Temps : police seule (couleur pilotée par Refresh).
            if (label != null && font != null) { label.font = font; EditorUtility.SetDirty(label); }
            // "Tour N" : police + couleur secondaire (non pilotée par le code).
            SetLabel(turn, font, CombatUiKit.TextSecondary);
            return true;
        }

        /// <summary>Écrase les couleurs sérialisées de la timeline (au cas où la scène a override les
        /// défauts) par la palette monochrome : cadre actif = accent, inactif = surface carte.</summary>
        private static bool RestyleTimeline(TimelineView timeline)
        {
            if (timeline == null) return false;
            var so = new SerializedObject(timeline);
            SetColor(so, "_frameActive", CombatUiKit.Accent);
            SetColor(so, "_frameInactive", CombatUiKit.CardBg);
            SetColor(so, "_portraitActive", Color.white);
            SetColor(so, "_portraitInactive", new Color(0.55f, 0.55f, 0.58f, 1f));
            so.ApplyModifiedPropertiesWithoutUndo();

            // Cadre noir transparent de fond (Image posée sur le GO racine de la timeline,
            // derrière les slots) -> désactivé (réversible, le GameObject reste). On ne touche
            // PAS aux Images enfants (cadres de slots).
            var rootBg = timeline.GetComponent<Image>();
            if (rootBg != null && rootBg.enabled)
            {
                rootBg.enabled = false;
                EditorUtility.SetDirty(rootBg);
            }
            return true;
        }

        private static void SetColor(SerializedObject so, string prop, Color c)
        {
            var p = so.FindProperty(prop);
            if (p != null) p.colorValue = c;
        }

        /// <summary>Tooltip de sorts (scène) : police Ari + couleurs + fond arrondi PanelBg.</summary>
        private static bool RestyleSpellTooltip(SpellTooltipView tooltip, TMP_FontAsset ari, TMP_FontAsset ariBold)
        {
            if (tooltip == null) return false;
            var so = new SerializedObject(tooltip);
            var panel = so.FindProperty("_panel")?.objectReferenceValue as RectTransform;
            var title = so.FindProperty("_titleText")?.objectReferenceValue as TMP_Text;
            var cost = so.FindProperty("_costText")?.objectReferenceValue as TMP_Text;
            var desc = so.FindProperty("_descriptionText")?.objectReferenceValue as TMP_Text;

            if (panel != null)
            {
                var bg = panel.GetComponent<Image>();
                if (bg != null)
                {
                    bg.color = CombatUiKit.PanelBg;
                    CombatUiKit.ApplyRounded(bg, CombatUiKit.CornerRadius);
                    EditorUtility.SetDirty(bg);
                }
            }
            SetLabel(title, ariBold ?? ari, CombatUiKit.TextPrimary);
            SetLabel(cost, ari, CombatUiKit.TextSecondary);
            SetLabel(desc, ari, CombatUiKit.TextSecondary);
            return true;
        }

        /// <summary>Pose (ou re-câble) une instance PassiveTooltipView dans la scène, avec la police Ari.
        /// À runtime elle gagne le singleton lazy -> l'auto-création par défaut (police TMP) est évitée.</summary>
        private static bool EnsurePassiveTooltip(TMP_FontAsset font)
        {
            var tip = Object.FindAnyObjectByType<PassiveTooltipView>(FindObjectsInactive.Include);
            if (tip == null)
            {
                var go = new GameObject("PassiveTooltipView", typeof(PassiveTooltipView));
                tip = go.GetComponent<PassiveTooltipView>();
            }
            var so = new SerializedObject(tip);
            var p = so.FindProperty("_font");
            if (p != null && font != null) p.objectReferenceValue = font;
            so.ApplyModifiedPropertiesWithoutUndo();
            return true;
        }

        /// <summary>Pose (ou re-câble) une instance TurnIndicatorView dans la scène, avec la police Ari.
        /// À runtime elle gagne le singleton -> l'auto-instance par défaut (police TMP) est neutralisée.</summary>
        private static bool EnsureTurnIndicator(TMP_FontAsset font)
        {
            var indicator = Object.FindAnyObjectByType<TurnIndicatorView>(FindObjectsInactive.Include);
            if (indicator == null)
            {
                var go = new GameObject("TurnIndicatorView", typeof(TurnIndicatorView));
                indicator = go.GetComponent<TurnIndicatorView>();
            }
            var so = new SerializedObject(indicator);
            var p = so.FindProperty("_font");
            if (p != null && font != null) p.objectReferenceValue = font;
            so.ApplyModifiedPropertiesWithoutUndo();
            return true;
        }

        private static bool RestyleEndTurnButton(Button btn, TMP_FontAsset font)
        {
            if (btn == null) return false;
            StyleButtonPill(btn, primary: true, font); // pilule claire = CTA hub
            return true;
        }

        /// <summary>Style un bouton à la DA hub : pilule claire (primaire, texte sombre) ou ghost
        /// translucide (secondaire, texte clair), coins arrondis + police Ari.</summary>
        private static void StyleButtonPill(Button btn, bool primary, TMP_FontAsset font)
        {
            if (btn == null) return;
            var img = (btn.targetGraphic as Image) ?? btn.GetComponent<Image>();
            if (img != null)
            {
                img.color = primary ? CombatUiKit.Accent : CombatUiKit.GhostBg;
                btn.targetGraphic = img;
                if (img.GetComponent<CombatUiRounder>() == null)
                    img.gameObject.AddComponent<CombatUiRounder>();
                EditorUtility.SetDirty(img);
            }

            var cb = btn.colors;
            if (primary)
            {
                cb.normalColor = Color.white;
                cb.highlightedColor = Color.white;
                cb.pressedColor = new Color(0.85f, 0.86f, 0.88f, 1f);
                cb.selectedColor = Color.white;
                cb.disabledColor = new Color(0.42f, 0.43f, 0.46f, 0.6f);
            }
            else
            {
                cb.normalColor = Color.white;
                cb.highlightedColor = new Color(2f, 2f, 2f, 2f); // éclaircit le ghost
                cb.pressedColor = new Color(1.6f, 1.6f, 1.6f, 1.6f);
                cb.selectedColor = Color.white;
                cb.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.4f);
            }
            cb.fadeDuration = 0.1f;
            btn.colors = cb;
            EditorUtility.SetDirty(btn);

            var label = btn.GetComponentInChildren<TMP_Text>(true);
            SetLabel(label, font, primary ? CombatUiKit.TextOnLight : CombatUiKit.TextPrimary);
        }

        /// <summary>Overlay fin de match (scène) : titre/sous-titre Ari (couleur titre = sémantique,
        /// pilotée par Refresh), backdrop sombre, boutons (Retour Hub = pilule, autres = ghost).</summary>
        private static bool RestyleMatchEnd(MatchEndOverlay overlay, TMP_FontAsset ari, TMP_FontAsset ariBold)
        {
            if (overlay == null) return false;
            var so = new SerializedObject(overlay);
            var title = so.FindProperty("_titleText")?.objectReferenceValue as TMP_Text;
            var subtitle = so.FindProperty("_subtitleText")?.objectReferenceValue as TMP_Text;
            var panel = so.FindProperty("_panel")?.objectReferenceValue as GameObject;
            var returnHub = so.FindProperty("_returnToHubButton")?.objectReferenceValue as Button;
            var saveReplay = so.FindProperty("_saveReplayButton")?.objectReferenceValue as Button;
            var restartE = so.FindProperty("_restartEasyButton")?.objectReferenceValue as Button;
            var restartM = so.FindProperty("_restartMediumButton")?.objectReferenceValue as Button;

            // Titre : police seule (couleur victoire-or / défaite-rouge pilotée par Refresh).
            if (title != null && (ariBold ?? ari) != null) { title.font = ariBold ?? ari; EditorUtility.SetDirty(title); }
            SetLabel(subtitle, ari, CombatUiKit.TextSecondary);

            // Backdrop sombre plein écran (si l'Image est portée par le panel root).
            if (panel != null)
            {
                var bg = panel.GetComponent<Image>();
                if (bg != null) { bg.color = new Color(0.02f, 0.02f, 0.03f, 0.86f); EditorUtility.SetDirty(bg); }
            }

            StyleButtonPill(returnHub, primary: true, ariBold ?? ari); // action principale
            StyleButtonPill(saveReplay, primary: false, ari);
            StyleButtonPill(restartE, primary: false, ari);
            StyleButtonPill(restartM, primary: false, ari);
            return true;
        }

        /// <summary>Pose (ou re-câble) une instance ForfeitButtonView dans la scène, avec la police Ari.</summary>
        private static bool EnsureForfeitButton(TMP_FontAsset font)
        {
            var fb = Object.FindAnyObjectByType<ForfeitButtonView>(FindObjectsInactive.Include);
            if (fb == null)
            {
                var go = new GameObject("ForfeitButtonView", typeof(ForfeitButtonView));
                fb = go.GetComponent<ForfeitButtonView>();
            }
            var so = new SerializedObject(fb);
            var p = so.FindProperty("_font");
            if (p != null && font != null) p.objectReferenceValue = font;
            so.ApplyModifiedPropertiesWithoutUndo();
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
