using Nymora.Hub;
using Nymora.Hub.Menu;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Nymora.Editor.Setup
{
    /// <summary>
    /// Restyle le chat (HubChatUI) sur la DA du nouveau menu Échap : police Ari, palette
    /// monochrome (HubMenuTheme), fonds arrondis, boutons thémés. Ne touche QUE l'apparence —
    /// la logique (pseudos cliquables, tabs, whispers) reste intacte.
    ///
    /// Ouvre 10_CommunityHub, lance, puis sauvegarde la scène (Ctrl+S). Réversible via Ctrl+Z.
    /// </summary>
    public static class HubChatRestyleTool
    {
        [MenuItem("Nymora/Setup/UI Menu/Restyle Chat (nouvelle DA)")]
        public static void Restyle()
        {
            var chat = Object.FindAnyObjectByType<HubChatUI>(FindObjectsInactive.Include);
            if (chat == null) { Debug.LogError("[ChatRestyle] HubChatUI introuvable dans la scène ouverte (ouvre 10_CommunityHub)."); return; }

            var theme = LoadTheme();
            if (theme == null) { Debug.LogError("[ChatRestyle] HubMenuTheme introuvable (ScriptableObjects/Settings/HubMenuTheme.asset)."); return; }

            Undo.RegisterFullObjectHierarchyUndo(chat.gameObject, "Restyle Chat");

            var so = new SerializedObject(chat);
            var input = so.FindProperty("_inputField").objectReferenceValue as TMP_InputField;
            var send = so.FindProperty("_sendButton").objectReferenceValue as Button;
            var scroll = so.FindProperty("_scrollRect").objectReferenceValue as ScrollRect;
            var history = so.FindProperty("_historyText").objectReferenceValue as TextMeshProUGUI;
            var tabG = so.FindProperty("_tabGlobalButton").objectReferenceValue as Button;
            var tabP = so.FindProperty("_tabPrivateButton").objectReferenceValue as Button;

            // Crée l'onglet "Clan" (clone de l'onglet Privé) s'il n'existe pas encore, et l'assigne.
            var tabClanProp = so.FindProperty("_tabClanButton");
            var tabClan = tabClanProp.objectReferenceValue as Button;
            if (tabClan == null && tabP != null)
            {
                var clone = Object.Instantiate(tabP.gameObject, tabP.transform.parent);
                clone.name = "TabClanButton";
                var clbl = clone.GetComponentInChildren<TextMeshProUGUI>(true);
                if (clbl != null) clbl.text = "Clan";
                var prt = (RectTransform)tabP.transform;
                var crt = (RectTransform)clone.transform;
                if (tabG != null) crt.anchoredPosition = prt.anchoredPosition + (prt.anchoredPosition - (Vector2)((RectTransform)tabG.transform).anchoredPosition);
                else crt.anchoredPosition = prt.anchoredPosition + new Vector2(prt.sizeDelta.x + 6f, 0f);
                clone.transform.SetSiblingIndex(tabP.transform.GetSiblingIndex() + 1);
                tabClan = clone.GetComponent<Button>();
                tabClanProp.objectReferenceValue = tabClan;
                Undo.RegisterCreatedObjectUndo(clone, "Create Clan tab");
            }

            // Fond du panneau chat (ancêtre du scroll portant une Image)
            var panelBg = FindPanelBg(scroll, input, chat);
            if (panelBg != null)
            {
                StyleRounded(panelBg, theme.PanelBg, theme);
                // Rend le chat déplaçable + redimensionnable (poignée haut-droite), persisté.
                var win = panelBg.GetComponent<UiResizableWindow>();
                if (win == null) win = Undo.AddComponent<UiResizableWindow>(panelBg.gameObject);
                // Ancre de largeur min = onglet Clan (sinon Privé) : le resize horizontal s'arrête
                // juste après, pour ne jamais couper/faire dépasser l'onglet.
                var anchor = tabClan != null ? (RectTransform)tabClan.transform
                           : tabP != null ? (RectTransform)tabP.transform : null;
                if (win != null && anchor != null)
                {
                    var wso = new SerializedObject(win);
                    var p = wso.FindProperty("_minWidthAnchor");
                    if (p != null) { p.objectReferenceValue = anchor; wso.ApplyModifiedProperties(); }
                }
            }
            else Debug.LogWarning("[ChatRestyle] Fond du panneau chat non trouvé — stylé partiellement (composants OK).");

            // Viewport du scroll : léger voile cohérent
            if (scroll != null)
            {
                var vpImg = (scroll.viewport != null ? scroll.viewport : scroll.transform as RectTransform)?.GetComponent<Image>();
                if (vpImg != null) vpImg.color = new Color(1f, 1f, 1f, 0.02f);
            }

            // Historique
            if (history != null)
            {
                history.font = theme.Font;
                history.color = theme.TextSecondary;
                history.fontSize = theme.FontSizeSmall;
            }

            // Champ de saisie
            if (input != null)
            {
                var bg = input.GetComponent<Image>();
                if (bg != null) StyleRounded(bg, theme.ButtonGhostBg, theme);
                if (input.textComponent != null) { input.textComponent.font = theme.Font; input.textComponent.color = theme.TextPrimary; }
                if (input.placeholder is TextMeshProUGUI ph) { ph.font = theme.Font; ph.color = theme.TextMuted; ph.fontStyle = FontStyles.Italic; }
            }

            // Bouton Envoyer (accent)
            StyleButton(send, theme.Accent, theme.TextOnLight, theme, colorTint: true);

            // Onglets Global/Privé : sprite arrondi + police, couleurs gérées par ApplyTabColor
            // (transition None pour ne pas être écrasé par le ColorTint du Button).
            StyleTab(tabG, theme);
            StyleTab(tabP, theme);
            StyleTab(tabClan, theme);
            so.FindProperty("_tabActiveColor").colorValue = theme.Accent;
            so.FindProperty("_tabInactiveColor").colorValue = theme.ButtonGhostBg;
            // Texte d'onglet : sombre sur actif (clair), clair sur inactif.
            var actTxt = so.FindProperty("_tabActiveTextColor");
            var inaTxt = so.FindProperty("_tabInactiveTextColor");
            if (actTxt != null) actTxt.colorValue = theme.TextOnLight;
            if (inaTxt != null) inaTxt.colorValue = theme.TextSecondary;
            so.ApplyModifiedProperties();

            EditorUtility.SetDirty(chat);
            if (!Application.isPlaying)
                EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log("[ChatRestyle] Chat restylé sur la DA du menu. Sauvegarde la scène (Ctrl+S).");
        }

        private static HubMenuTheme LoadTheme()
        {
            var guids = AssetDatabase.FindAssets("t:HubMenuTheme");
            return guids.Length == 0 ? null : AssetDatabase.LoadAssetAtPath<HubMenuTheme>(AssetDatabase.GUIDToAssetPath(guids[0]));
        }

        private static Image FindPanelBg(ScrollRect scroll, TMP_InputField input, HubChatUI chat)
        {
            Transform t = scroll != null ? scroll.transform.parent
                        : input != null ? input.transform.parent
                        : chat.transform;
            while (t != null)
            {
                var img = t.GetComponent<Image>();
                if (img != null && t.GetComponent<Canvas>() == null) return img;
                t = t.parent;
            }
            return chat.GetComponent<Image>();
        }

        private static void StyleRounded(Image img, Color color, HubMenuTheme theme)
        {
            img.sprite = HubMenuUIFactory.RoundedSprite(theme.CornerRadius);
            img.type = Image.Type.Sliced;
            img.color = color;
        }

        private static void StyleButton(Button btn, Color bg, Color textColor, HubMenuTheme theme, bool colorTint)
        {
            if (btn == null) return;
            var img = btn.GetComponent<Image>();
            if (img != null) StyleRounded(img, colorTint ? Color.white : bg, theme);
            if (colorTint)
            {
                var c = btn.colors;
                c.normalColor = bg;
                c.highlightedColor = Color.Lerp(bg, Color.white, 0.15f);
                c.pressedColor = Color.Lerp(bg, Color.black, 0.10f);
                c.selectedColor = bg; c.fadeDuration = 0.1f;
                btn.colors = c;
            }
            var lbl = btn.GetComponentInChildren<TextMeshProUGUI>(true);
            if (lbl != null) { lbl.font = theme.Font; lbl.color = textColor; }
        }

        private static void StyleTab(Button btn, HubMenuTheme theme)
        {
            if (btn == null) return;
            btn.transition = Selectable.Transition.None; // ApplyTabColor pilote img.color
            // Arrondit l'Image propre du bouton ET sa targetGraphic : selon la structure du prefab,
            // la rect visible de l'onglet peut être l'une ou l'autre -> on couvre les deux.
            RoundImage(btn.GetComponent<Image>(), theme);
            RoundImage(btn.targetGraphic as Image, theme);
            var lbl = btn.GetComponentInChildren<TextMeshProUGUI>(true);
            if (lbl != null) { lbl.font = theme.Font; lbl.color = theme.TextPrimary; }
        }

        private static void RoundImage(Image img, HubMenuTheme theme)
        {
            if (img == null) return;
            img.sprite = HubMenuUIFactory.RoundedSprite(theme.CornerRadius);
            img.type = Image.Type.Sliced;
        }
    }
}
