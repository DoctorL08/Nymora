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
    /// Restyle les 3 pop-ups de notification du hub sur la DA du menu Échap :
    ///   - IncomingChallengePopup     (demande de défi, brique 4.8)
    ///   - IncomingFriendRequestPopup (demande d'ami, brique 4.10)
    ///   - IncomingClanInvitePopup    (invitation de clan, brique 4.11)
    ///
    /// Les 3 partagent la MÊME structure de refs (_panel / _label / _acceptButton /
    /// _refuseButton, + _bannerPreview pour le clan).
    ///
    /// Apparence : sprite arrondi HubMenuTheme, police Ari, palette monochrome, boutons
    /// Accepter=primaire clair / Refuser=ghost. La logique (events WS, Respond) reste intacte.
    ///
    /// Disposition : le défi avait sa structure interne cassée (boutons empilés/coupés).
    /// On impose une disposition propre (titre en haut, 2 boutons côte à côte en bas) au
    /// DÉFI et à l'AMI en reparentant les éléments sous le panneau. La pop-up CLAN sert de
    /// référence (taille + bannière) et n'est PAS retouchée dans sa structure.
    ///
    /// Ouvre 10_CommunityHub, lance via le menu, puis sauvegarde la scène (Ctrl+S).
    /// Idempotent + réversible via Ctrl+Z.
    /// </summary>
    public static class RestyleNotificationPopupsTool
    {
        private const float PanelCornerRadius = 28f; // aligné sur BuildCenterDialog du menu

        private struct PopupRefs
        {
            public RectTransform Panel;
            public TextMeshProUGUI Label;
            public Button Accept;
            public Button Refuse;
            public Image Banner;
            public bool Valid => Panel != null;
        }

        [MenuItem("Nymora/Setup/UI Menu/Restyle Notification Popups (nouvelle DA)")]
        public static void Restyle()
        {
            var theme = LoadTheme();
            if (theme == null)
            {
                Debug.LogError("[NotifRestyle] HubMenuTheme introuvable (ScriptableObjects/Settings/HubMenuTheme.asset).");
                return;
            }

            var challenge = ReadRefs(Object.FindAnyObjectByType<IncomingChallengePopup>(FindObjectsInactive.Include));
            var friend = ReadRefs(Object.FindAnyObjectByType<IncomingFriendRequestPopup>(FindObjectsInactive.Include));
            var clan = ReadRefs(Object.FindAnyObjectByType<IncomingClanInvitePopup>(FindObjectsInactive.Include));

            int styled = 0;
            if (Style(challenge, theme)) styled++;
            if (Style(friend, theme)) styled++;
            if (Style(clan, theme)) styled++;

            if (styled == 0)
            {
                Debug.LogError("[NotifRestyle] Aucune pop-up trouvée dans la scène ouverte (ouvre 10_CommunityHub).");
                return;
            }

            // Taille de référence = celle de la pop-up clan (validée), avec un minimum sûr pour
            // que les 2 boutons côte à côte tiennent sans se chevaucher.
            Vector2 size = clan.Valid ? clan.Panel.sizeDelta : new Vector2(460f, 220f);
            size.x = Mathf.Max(size.x, 460f);
            size.y = Mathf.Max(size.y, 210f);

            // AMI : recalée sur la position de la clan (n'était plus cachée par les monnaies)
            // + disposition interne propre.
            if (friend.Valid)
            {
                if (clan.Valid) AlignPanelPosition(friend.Panel, clan.Panel);
                LayoutContent(friend, size);
                Debug.Log("[NotifRestyle] Pop-up d'ami recalée + disposition corrigée.");
            }

            // DÉFI : reste épinglé en haut-droite (sous les monnaies) MAIS agrandi + disposition
            // propre (les boutons ne se chevauchent plus).
            if (challenge.Valid)
            {
                PinTopRight(challenge.Panel, new Vector2(-28f, -110f));
                LayoutContent(challenge, size);
                Debug.Log("[NotifRestyle] Pop-up de défi épinglée haut-droite + agrandie + disposition corrigée.");
            }

            if (!Application.isPlaying)
                EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log($"[NotifRestyle] {styled}/3 pop-up(s) restylée(s) sur la DA du menu. Sauvegarde la scène (Ctrl+S).");
        }

        // ===== Lecture des refs =====

        private static PopupRefs ReadRefs(MonoBehaviour popup)
        {
            var r = new PopupRefs();
            if (popup == null) return r;
            var so = new SerializedObject(popup);
            var panelGo = GetRef(so, "_panel") as GameObject;
            r.Panel = panelGo != null ? panelGo.transform as RectTransform : null;
            r.Label = GetRef(so, "_label") as TextMeshProUGUI;
            r.Accept = GetRef(so, "_acceptButton") as Button;
            r.Refuse = GetRef(so, "_refuseButton") as Button;
            r.Banner = GetRef(so, "_bannerPreview") as Image; // clan only
            return r;
        }

        // ===== Style (apparence uniquement) =====

        private static bool Style(PopupRefs r, HubMenuTheme theme)
        {
            if (!r.Valid) return false;
            Undo.RegisterFullObjectHierarchyUndo(r.Panel.gameObject, "Restyle Notification Popup");

            // Fond du panneau : Image arrondie + couleur de panneau.
            var img = r.Panel.GetComponent<Image>();
            if (img == null) img = Undo.AddComponent<Image>(r.Panel.gameObject);
            StyleRounded(img, theme.PanelBg, PanelCornerRadius);
            img.raycastTarget = true; // bloque les clics derrière (modale légère)

            // Libellé.
            if (r.Label != null)
            {
                r.Label.font = theme.FontBold;
                r.Label.color = theme.TextPrimary;
                r.Label.fontSize = theme.FontSizeHeader;
                r.Label.alignment = TextAlignmentOptions.Center;
                r.Label.enableWordWrapping = true;
                r.Label.richText = true;
            }

            // Bannière clan : on garde sa couleur (couleur du clan), coins arrondis.
            if (r.Banner != null)
            {
                r.Banner.sprite = HubMenuUIFactory.RoundedSprite(theme.CornerRadius);
                r.Banner.type = Image.Type.Sliced;
            }

            StylePrimary(r.Accept, theme);
            StyleGhost(r.Refuse, theme);
            return true;
        }

        // ===== Disposition interne propre (titre haut + 2 boutons côte à côte en bas) =====

        private static void LayoutContent(PopupRefs r, Vector2 size)
        {
            Undo.RecordObject(r.Panel, "Layout popup panel");
            r.Panel.sizeDelta = size;

            // Reparente sous le panneau pour une disposition déterministe (indépendante de la
            // structure cassée d'origine où les boutons se chevauchaient).
            Reparent(r.Label != null ? r.Label.transform : null, r.Panel);
            Reparent(r.Accept != null ? r.Accept.transform : null, r.Panel);
            Reparent(r.Refuse != null ? r.Refuse.transform : null, r.Panel);

            // Titre : bandeau haut, centré.
            if (r.Label != null)
            {
                var l = r.Label.rectTransform;
                Undo.RecordObject(l, "Layout label");
                l.anchorMin = new Vector2(0f, 1f); l.anchorMax = new Vector2(1f, 1f); l.pivot = new Vector2(0.5f, 1f);
                l.sizeDelta = new Vector2(-44f, size.y - 110f);
                l.anchoredPosition = new Vector2(0f, -24f);
            }

            // Boutons : côte à côte, centrés, en bas. Largeur = moitié dispo - gouttière.
            const float gap = 18f;
            const float bh = 48f;
            float bw = Mathf.Min(200f, (size.x - 44f - gap) * 0.5f);
            PlaceButton(r.Accept, new Vector2(1f, 0f), new Vector2(-gap * 0.5f, 26f), new Vector2(bw, bh)); // gauche
            PlaceButton(r.Refuse, new Vector2(0f, 0f), new Vector2(gap * 0.5f, 26f), new Vector2(bw, bh));  // droite
        }

        private static void PlaceButton(Button btn, Vector2 pivot, Vector2 anchoredPos, Vector2 size)
        {
            if (btn == null) return;
            var rt = (RectTransform)btn.transform;
            Undo.RecordObject(rt, "Layout popup button");
            rt.anchorMin = new Vector2(0.5f, 0f); rt.anchorMax = new Vector2(0.5f, 0f); rt.pivot = pivot;
            rt.sizeDelta = size; rt.anchoredPosition = anchoredPos;
            // Si un LayoutElement traînait (ancien HorizontalLayoutGroup), il est inerte ici
            // (panneau sans layout group) -> on le neutralise par sécurité.
            var le = btn.GetComponent<LayoutElement>();
            if (le != null) le.ignoreLayout = true;
        }

        private static void Reparent(Transform child, RectTransform parent)
        {
            if (child == null || parent == null || child.parent == parent) return;
            Undo.SetTransformParent(child, parent, "Reparent popup element");
            child.localScale = Vector3.one;
        }

        // ===== Position du panneau =====

        private static void AlignPanelPosition(RectTransform target, RectTransform reference)
        {
            if (target == null || reference == null || target == reference) return;
            Undo.RecordObject(target, "Align popup position");
            target.anchorMin = reference.anchorMin;
            target.anchorMax = reference.anchorMax;
            target.pivot = reference.pivot;
            target.anchoredPosition = reference.anchoredPosition;
        }

        private static void PinTopRight(RectTransform panel, Vector2 margin)
        {
            if (panel == null) return;
            Undo.RecordObject(panel, "Pin popup top-right");
            panel.anchorMin = new Vector2(1f, 1f); panel.anchorMax = new Vector2(1f, 1f); panel.pivot = new Vector2(1f, 1f);
            panel.anchoredPosition = margin; // ex (-28,-110) : 28px du bord droit, sous les monnaies
        }

        // ===== Helpers style =====

        private static Object GetRef(SerializedObject so, string prop)
        {
            var p = so.FindProperty(prop);
            return p != null ? p.objectReferenceValue : null;
        }

        private static HubMenuTheme LoadTheme()
        {
            var guids = AssetDatabase.FindAssets("t:HubMenuTheme");
            return guids.Length == 0 ? null : AssetDatabase.LoadAssetAtPath<HubMenuTheme>(AssetDatabase.GUIDToAssetPath(guids[0]));
        }

        private static void StyleRounded(Image img, Color color, float radius)
        {
            if (img == null) return;
            img.sprite = HubMenuUIFactory.RoundedSprite(radius);
            img.type = Image.Type.Sliced;
            img.color = color;
        }

        private static void StylePrimary(Button btn, HubMenuTheme theme)
        {
            if (btn == null) return;
            var img = btn.GetComponent<Image>();
            if (img != null) StyleRounded(img, Color.white, theme.CornerRadius); // ColorTint teinte
            btn.transition = Selectable.Transition.ColorTint;
            var c = btn.colors;
            c.normalColor = theme.Accent;
            c.highlightedColor = Color.white;
            c.pressedColor = new Color(0.80f, 0.81f, 0.83f);
            c.selectedColor = theme.Accent;
            c.disabledColor = new Color(theme.Accent.r, theme.Accent.g, theme.Accent.b, 0.4f);
            c.fadeDuration = 0.1f;
            btn.colors = c;
            var lbl = btn.GetComponentInChildren<TextMeshProUGUI>(true);
            if (lbl != null) { lbl.font = theme.FontBold; lbl.color = theme.TextOnLight; lbl.alignment = TextAlignmentOptions.Center; StretchLabel(lbl, btn); }
        }

        private static void StyleGhost(Button btn, HubMenuTheme theme)
        {
            if (btn == null) return;
            var img = btn.GetComponent<Image>();
            if (img != null) StyleRounded(img, Color.white, theme.CornerRadius);
            btn.transition = Selectable.Transition.ColorTint;
            var c = btn.colors;
            c.normalColor = theme.ButtonGhostBg;
            c.highlightedColor = theme.ButtonGhostBgHover;
            c.pressedColor = theme.ButtonGhostBgHover;
            c.selectedColor = theme.ButtonGhostBg;
            c.fadeDuration = 0.1f;
            btn.colors = c;
            var lbl = btn.GetComponentInChildren<TextMeshProUGUI>(true);
            if (lbl != null) { lbl.font = theme.FontBold; lbl.color = theme.TextPrimary; lbl.alignment = TextAlignmentOptions.Center; StretchLabel(lbl, btn); }
        }

        /// <summary>Étire le label sur tout son bouton (s'il en est enfant direct) pour rester
        /// centré quelle que soit la taille du bouton.</summary>
        private static void StretchLabel(TextMeshProUGUI lbl, Button btn)
        {
            if (lbl == null || btn == null || lbl.transform.parent != btn.transform) return;
            var rt = lbl.rectTransform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(6f, 2f); rt.offsetMax = new Vector2(-6f, -2f);
            lbl.enableWordWrapping = false;
            lbl.raycastTarget = false;
        }
    }
}
