using Nymora.Core.Enums;
using Nymora.Core.ScriptableObjects;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Nymora.Hub.Menu
{
    /// <summary>
    /// Brique E1 — Partie "émotes" de la coquille hub : bouton émote (à droite du hamburger) +
    /// popup de sélection des 3 émotes de la classe ACTUELLEMENT équipée (HubAvatar.Local.NetClassId).
    /// Clic sur une émote → bulle au-dessus de l'avatar local (ShowEmoteBubble). 100% local en E1 ;
    /// la diffusion réseau (les autres voient la bulle) arrive en E2 via RPC Fusion.
    /// </summary>
    public sealed partial class HubMenuShell
    {
        // Assigné par HubMenuShellTool (Create or Refresh Menu Shell). Si null → le bouton reste
        // mais le popup affiche un message ; relance le tool après l'import des émotes.
        [SerializeField] private EmoteCatalog _emoteCatalog;

        private GameObject _emoteButton;
        private GameObject _emotePopup;        // panneau (rangée des 3 émotes)
        private GameObject _emoteCatcher;      // capteur plein écran (clic dehors = fermer)
        private RectTransform _emoteRow;       // conteneur des boutons (rebuild à chaque ouverture)
        private bool _emotePopupOpen;

        private void BuildEmoteButton(RectTransform parent)
        {
            // Bouton 52×52 collé à droite du hamburger (24 + 52 + 12 = 88).
            var btnImg = _f.MakeImage("EmoteButton", parent, _theme.ButtonGhostBg);
            var rt = btnImg.rectTransform;
            rt.anchorMin = new Vector2(0f, 1f); rt.anchorMax = new Vector2(0f, 1f); rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(88f, -24f);
            rt.sizeDelta = new Vector2(52f, 52f);

            var btn = btnImg.gameObject.AddComponent<Button>();
            btn.targetGraphic = btnImg;
            var c = btn.colors;
            c.normalColor = _theme.ButtonGhostBg;
            c.highlightedColor = _theme.ButtonGhostBgHover;
            c.pressedColor = _theme.ButtonGhostBgHover;
            c.fadeDuration = 0.1f;
            btn.colors = c;
            btn.onClick.AddListener(ToggleEmotePopup);

            // Glyphe émote (ASCII safe — la police Ari ne porte pas d'emoji U+1Fxxx).
            var glyph = _f.MakeText("Glyph", rt, ":)", _theme.FontSizeHeader, _theme.TextPrimary, _theme.FontBold, TextAlignmentOptions.Center);
            HubMenuUIFactory.Stretch(glyph.rectTransform);
            glyph.raycastTarget = false;

            _emoteButton = btnImg.gameObject;
            // Comme le hamburger : derrière le menu Échap (le voile le grise quand le menu est ouvert).
            _emoteButton.transform.SetAsFirstSibling();

            BuildEmotePopup(parent);
        }

        private void BuildEmotePopup(RectTransform parent)
        {
            // Capteur plein écran (transparent) : clic en dehors du popup => fermer.
            var catcher = _f.MakeImage("EmoteCatcher", parent, new Color(0f, 0f, 0f, 0f), rounded: false);
            HubMenuUIFactory.Stretch(catcher.rectTransform);
            var cBtn = catcher.gameObject.AddComponent<Button>();
            cBtn.transition = Selectable.Transition.None;
            cBtn.onClick.AddListener(CloseEmotePopup);
            _emoteCatcher = catcher.gameObject;

            // Panneau popup (sous le bouton, ancré haut-gauche).
            var panel = _f.MakeImage("EmotePopup", parent, _theme.PanelBg);
            var prt = panel.rectTransform;
            prt.anchorMin = new Vector2(0f, 1f); prt.anchorMax = new Vector2(0f, 1f); prt.pivot = new Vector2(0f, 1f);
            prt.anchoredPosition = new Vector2(24f, -88f);
            prt.sizeDelta = new Vector2(252f, 132f);

            var row = _f.MakeRect("Row", prt);
            HubMenuUIFactory.Stretch(row, 14f, 14f, 14f, 14f);
            var hlg = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 12f; hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.childControlWidth = true; hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false; hlg.childForceExpandHeight = false;
            _emoteRow = row;

            _emotePopup = panel.gameObject;
            _emoteCatcher.SetActive(false);
            _emotePopup.SetActive(false);
        }

        private void ToggleEmotePopup()
        {
            if (_emotePopupOpen) CloseEmotePopup();
            else OpenEmotePopup();
        }

        private void OpenEmotePopup()
        {
            if (_emotePopup == null) return;
            RebuildEmoteRow();
            _emotePopupOpen = true;
            _emoteCatcher.SetActive(true);
            _emotePopup.SetActive(true); // UiPanelAnimator absent ici : pop instantané (léger, OK)
        }

        private void CloseEmotePopup()
        {
            _emotePopupOpen = false;
            if (_emoteCatcher != null) _emoteCatcher.SetActive(false);
            if (_emotePopup != null) _emotePopup.SetActive(false);
        }

        /// <summary>Reconstruit la rangée avec les 3 émotes de la classe actuellement équipée.</summary>
        private void RebuildEmoteRow()
        {
            if (_emoteRow == null) return;
            for (int i = _emoteRow.childCount - 1; i >= 0; i--)
                Destroy(_emoteRow.GetChild(i).gameObject);

            var local = HubAvatar.Local;
            if (_emoteCatalog == null || local == null)
            {
                MakeEmoteHint(_emoteCatalog == null
                    ? "Catalogue d'émotes manquant.\nRelance Create or Refresh Menu Shell."
                    : "Avatar non prêt.");
                return;
            }

            if (!System.Enum.TryParse(local.NetClassId.ToString(), out NymoraClass cls) || cls == NymoraClass.None)
            {
                MakeEmoteHint("Classe inconnue.");
                return;
            }

            var emotes = _emoteCatalog.GetEmotesForClass(cls);
            if (emotes.Count == 0)
            {
                MakeEmoteHint("Aucune émote pour " + cls + ".");
                return;
            }

            foreach (var e in emotes)
                MakeEmoteButton(e.Sprite);
        }

        private void MakeEmoteButton(Sprite sprite)
        {
            var bg = _f.MakeImage("Emote", _emoteRow, _theme.ButtonGhostBg);
            var le = bg.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = 68f; le.preferredHeight = 100f;
            var btn = bg.gameObject.AddComponent<Button>();
            btn.targetGraphic = bg;
            var c = btn.colors;
            c.normalColor = _theme.ButtonGhostBg;
            c.highlightedColor = _theme.ButtonGhostBgHover;
            c.pressedColor = _theme.ButtonGhostBgHover;
            c.fadeDuration = 0.08f;
            btn.colors = c;

            var icon = _f.MakeImage("Icon", bg.rectTransform, Color.white, rounded: false);
            HubMenuUIFactory.Stretch(icon.rectTransform, 6f, 6f, 6f, 6f);
            icon.sprite = sprite;
            icon.preserveAspect = true;
            icon.raycastTarget = false;

            btn.onClick.AddListener(() =>
            {
                var local = HubAvatar.Local;
                if (local != null) local.ShowEmoteBubble(sprite);
                CloseEmotePopup();
            });
        }

        private void MakeEmoteHint(string msg)
        {
            var hint = _f.MakeText("Hint", _emoteRow, msg, _theme.FontSizeSmall, _theme.TextMuted, _theme.Font, TextAlignmentOptions.Center);
            var le = hint.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = 224f; le.preferredHeight = 100f;
        }
    }
}
