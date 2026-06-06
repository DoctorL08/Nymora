using System.Collections;
using Nymora.Hub;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Nymora.Hub.Menu
{
    /// <summary>
    /// Brique S2 (mode spectateur, Majeur #2) — Bouton œil (à droite de l'émote) + panneau
    /// listant les combats 1v1 EN COURS (casual + ranked). Données via le WS LIST_MATCHES →
    /// MATCHES_LIST (HubChatClient.SendListMatches / OnMatchesList). Le combat IA est offline
    /// donc jamais listé.
    ///
    /// Le bouton suit le pattern de l'émote/hamburger : hors MenuRoot, visible dans le hub,
    /// couvert par le voile quand le menu Échap est ouvert (SetAsFirstSibling).
    ///
    /// En S2, le bouton « Visionner » d'une ligne affiche un placeholder (la vraie scène live
    /// arrive en S4). Le point d'accroche LaunchSpectate(matchId) est prêt à être rebranché.
    /// </summary>
    public sealed partial class HubMenuShell
    {
        private GameObject _spectateButton;
        private GameObject _spectatePopup;
        private GameObject _spectateCatcher;
        private RectTransform _spectateListContent;
        private TextMeshProUGUI _spectateEmptyHint;
        private bool _spectatePopupOpen;
        private Coroutine _spectateRefreshRoutine;

        private const float SpectateRefreshSeconds = 3f;

        private void BuildSpectateButton(RectTransform parent)
        {
            // Bouton 52×52 à droite de l'émote (hamburger x=24, émote x=88, œil x=152).
            var btnImg = _f.MakeImage("SpectateButton", parent, _theme.ButtonGhostBg);
            var rt = btnImg.rectTransform;
            rt.anchorMin = new Vector2(0f, 1f); rt.anchorMax = new Vector2(0f, 1f); rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(152f, -24f);
            rt.sizeDelta = new Vector2(52f, 52f);

            var btn = btnImg.gameObject.AddComponent<Button>();
            btn.targetGraphic = btnImg;
            var c = btn.colors;
            c.normalColor = _theme.ButtonGhostBg;
            c.highlightedColor = _theme.ButtonGhostBgHover;
            c.pressedColor = _theme.ButtonGhostBgHover;
            c.fadeDuration = 0.1f;
            btn.colors = c;
            btn.onClick.AddListener(ToggleSpectatePopup);

            // Icône œil SVG (Resources/UI/Icons/ui_icon_spectate). Fallback texte si l'import
            // n'est pas prêt (piège connu reset_arrow/ui_icon_close : svgType 1 TexturedSprite).
            var icon = _f.MakeImage("Icon", rt, _theme.TextPrimary, rounded: false);
            HubMenuUIFactory.Stretch(icon.rectTransform, 12f, 12f, 12f, 12f);
            icon.raycastTarget = false;
            var sprite = HubMenuUIFactory.LoadIcon("ui_icon_spectate");
            if (sprite != null)
            {
                icon.sprite = sprite;
                icon.type = Image.Type.Simple;
                icon.preserveAspect = true;
            }
            else
            {
                icon.enabled = false;
                var glyph = _f.MakeText("Glyph", rt, "o", _theme.FontSizeHeader, _theme.TextPrimary, _theme.FontBold, TextAlignmentOptions.Center);
                HubMenuUIFactory.Stretch(glyph.rectTransform);
                glyph.raycastTarget = false;
            }

            _spectateButton = btnImg.gameObject;
            // Comme l'émote/hamburger : derrière le menu Échap (le voile le grise quand ouvert).
            _spectateButton.transform.SetAsFirstSibling();

            BuildSpectatePopup(parent);
        }

        private void BuildSpectatePopup(RectTransform parent)
        {
            // Capteur plein écran : clic dehors = fermer.
            var catcher = _f.MakeImage("SpectateCatcher", parent, new Color(0f, 0f, 0f, 0f), rounded: false);
            HubMenuUIFactory.Stretch(catcher.rectTransform);
            var cBtn = catcher.gameObject.AddComponent<Button>();
            cBtn.transition = Selectable.Transition.None;
            cBtn.onClick.AddListener(CloseSpectatePopup);
            _spectateCatcher = catcher.gameObject;

            // Panneau (ancré haut-gauche, sous la rangée de boutons).
            var panel = _f.MakeImage("SpectatePopup", parent, _theme.PanelBg);
            var prt = panel.rectTransform;
            prt.anchorMin = new Vector2(0f, 1f); prt.anchorMax = new Vector2(0f, 1f); prt.pivot = new Vector2(0f, 1f);
            prt.anchoredPosition = new Vector2(24f, -88f);
            prt.sizeDelta = new Vector2(460f, 380f);

            // Titre.
            var title = _f.MakeText("Title", prt, "Combats en cours", _theme.FontSizeHeader, _theme.TextPrimary, _theme.FontBold, TextAlignmentOptions.MidlineLeft);
            var trt = title.rectTransform;
            trt.anchorMin = new Vector2(0f, 1f); trt.anchorMax = new Vector2(1f, 1f); trt.pivot = new Vector2(0.5f, 1f);
            trt.sizeDelta = new Vector2(-32f, 34f);
            trt.anchoredPosition = new Vector2(0f, -16f);
            title.raycastTarget = false;

            // Liste scrollable.
            _spectateListContent = BuildScrollList(prt, topInset: 60f, bottomInset: 16f, sideInset: 16f);

            // Hint « aucun combat » (par-dessus la liste vide).
            _spectateEmptyHint = _f.MakeText("Empty", prt, "Aucun combat en cours.", _theme.FontSizeBody, _theme.TextMuted, _theme.Font, TextAlignmentOptions.Center);
            var ert = _spectateEmptyHint.rectTransform;
            HubMenuUIFactory.Stretch(ert, 20f, 20f, 70f, 24f);
            _spectateEmptyHint.raycastTarget = false;

            _spectatePopup = panel.gameObject;
            _spectateCatcher.SetActive(false);
            _spectatePopup.SetActive(false);
        }

        /// <summary>Construit un ScrollRect vertical et renvoie le RectTransform du contenu
        /// (VerticalLayoutGroup + ContentSizeFitter). Les lignes y sont ajoutées par RebuildSpectateList.</summary>
        private RectTransform BuildScrollList(RectTransform panel, float topInset, float bottomInset, float sideInset)
        {
            var scrollRt = _f.MakeRect("Scroll", panel);
            scrollRt.anchorMin = new Vector2(0f, 0f); scrollRt.anchorMax = new Vector2(1f, 1f);
            scrollRt.offsetMin = new Vector2(sideInset, bottomInset);
            scrollRt.offsetMax = new Vector2(-sideInset, -topInset);
            var scroll = scrollRt.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 24f;

            // Viewport masqué.
            var viewport = _f.MakeImage("Viewport", scrollRt, new Color(0f, 0f, 0f, 0f), rounded: false);
            HubMenuUIFactory.Stretch(viewport.rectTransform);
            viewport.gameObject.AddComponent<RectMask2D>();
            scroll.viewport = viewport.rectTransform;

            // Contenu (s'étend vers le bas).
            var content = _f.MakeRect("Content", viewport.rectTransform);
            content.anchorMin = new Vector2(0f, 1f); content.anchorMax = new Vector2(1f, 1f); content.pivot = new Vector2(0.5f, 1f);
            content.sizeDelta = new Vector2(0f, 0f);
            var vlg = content.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 8f; vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.childControlWidth = true; vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;
            var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.content = content;

            return content;
        }

        private void ToggleSpectatePopup()
        {
            if (_spectatePopupOpen) CloseSpectatePopup();
            else OpenSpectatePopup();
        }

        private void OpenSpectatePopup()
        {
            if (_spectatePopup == null) return;
            _spectatePopupOpen = true;
            _spectateCatcher.SetActive(true);
            _spectatePopup.SetActive(true);

            var chat = HubChatClient.Instance;
            if (chat != null)
            {
                chat.OnMatchesList -= HandleMatchesList;
                chat.OnMatchesList += HandleMatchesList;
                // Affiche tout de suite la derniere liste connue, puis demande un refresh.
                RebuildSpectateList(chat.LatestMatches);
                chat.SendListMatches();
            }
            else
            {
                RebuildSpectateList(new SpectateMatchInfo[0]);
            }

            if (_spectateRefreshRoutine != null) StopCoroutine(_spectateRefreshRoutine);
            _spectateRefreshRoutine = StartCoroutine(SpectateRefreshLoop());
        }

        private void CloseSpectatePopup()
        {
            _spectatePopupOpen = false;
            if (_spectateCatcher != null) _spectateCatcher.SetActive(false);
            if (_spectatePopup != null) _spectatePopup.SetActive(false);

            var chat = HubChatClient.Instance;
            if (chat != null) chat.OnMatchesList -= HandleMatchesList;

            if (_spectateRefreshRoutine != null)
            {
                StopCoroutine(_spectateRefreshRoutine);
                _spectateRefreshRoutine = null;
            }
        }

        private IEnumerator SpectateRefreshLoop()
        {
            var wait = new WaitForSecondsRealtime(SpectateRefreshSeconds);
            while (_spectatePopupOpen)
            {
                yield return wait;
                if (!_spectatePopupOpen) yield break;
                HubChatClient.Instance?.SendListMatches();
            }
        }

        private void HandleMatchesList(SpectateMatchInfo[] matches)
        {
            if (!_spectatePopupOpen) return;
            RebuildSpectateList(matches);
        }

        private void RebuildSpectateList(SpectateMatchInfo[] matches)
        {
            if (_spectateListContent == null) return;
            for (int i = _spectateListContent.childCount - 1; i >= 0; i--)
                Destroy(_spectateListContent.GetChild(i).gameObject);

            int count = matches?.Length ?? 0;
            if (_spectateEmptyHint != null) _spectateEmptyHint.gameObject.SetActive(count == 0);

            for (int i = 0; i < count; i++)
                MakeSpectateRow(matches[i]);
        }

        private void MakeSpectateRow(SpectateMatchInfo match)
        {
            var row = _f.MakeImage("Match", _spectateListContent, _theme.ButtonGhostBg);
            var le = row.gameObject.AddComponent<LayoutElement>();
            le.preferredHeight = 64f;
            le.minHeight = 64f;

            // Badge mode (pilule, gauche).
            bool ranked = match.mode == "ranked";
            var badge = _f.MakeImage("Mode", row.rectTransform, ranked ? _theme.Accent : _theme.ButtonGhostBgHover);
            var brt = badge.rectTransform;
            brt.anchorMin = new Vector2(0f, 0.5f); brt.anchorMax = new Vector2(0f, 0.5f); brt.pivot = new Vector2(0f, 0.5f);
            brt.sizeDelta = new Vector2(74f, 26f);
            brt.anchoredPosition = new Vector2(12f, 0f);
            var badgeTxt = _f.MakeText("T", brt, ranked ? "CLASSÉ" : "AMICAL", _theme.FontSizeSmall, ranked ? _theme.TextOnLight : _theme.TextSecondary, _theme.FontBold, TextAlignmentOptions.Center);
            HubMenuUIFactory.Stretch(badgeTxt.rectTransform);
            badgeTxt.raycastTarget = false;

            // Libellé joueurs (centre).
            var label = _f.MakeText("Players", row.rectTransform, FormatPlayers(match), _theme.FontSizeBody, _theme.TextPrimary, _theme.Font, TextAlignmentOptions.MidlineLeft);
            var lrt = label.rectTransform;
            lrt.anchorMin = new Vector2(0f, 0f); lrt.anchorMax = new Vector2(1f, 1f);
            lrt.offsetMin = new Vector2(98f, 6f);
            lrt.offsetMax = new Vector2(-118f, -6f);
            label.enableWordWrapping = false;
            label.overflowMode = TextOverflowModes.Ellipsis;
            label.raycastTarget = false;

            // Bouton Visionner (droite).
            var btn = _f.MakeButton(row.rectTransform, "Visionner", primary: true, out _);
            var srt = ((RectTransform)btn.transform);
            srt.anchorMin = new Vector2(1f, 0.5f); srt.anchorMax = new Vector2(1f, 0.5f); srt.pivot = new Vector2(1f, 0.5f);
            srt.sizeDelta = new Vector2(104f, 38f);
            srt.anchoredPosition = new Vector2(-10f, 0f);
            // Le LayoutElement de MakeButton force minWidth 120 ; on le neutralise pour tenir dans la ligne.
            var btnLe = btn.GetComponent<LayoutElement>();
            if (btnLe != null) { btnLe.minWidth = 0f; btnLe.ignoreLayout = true; }
            string matchId = match.matchId;
            btn.onClick.AddListener(() => LaunchSpectate(matchId, match));
        }

        private string FormatPlayers(SpectateMatchInfo match)
        {
            string a = "?", b = "?";
            if (match.players != null)
            {
                if (match.players.Length > 0 && match.players[0] != null) a = PlayerLabel(match.players[0]);
                if (match.players.Length > 1 && match.players[1] != null) b = PlayerLabel(match.players[1]);
            }
            return $"{a}  <color=#888888>vs</color>  {b}";
        }

        private string PlayerLabel(SpectateMatchPlayer p)
        {
            string name = string.IsNullOrEmpty(p.displayName) ? "?" : p.displayName;
            if (!string.IsNullOrEmpty(p.classId))
                return $"{name} <size=80%><color=#9A9A9A>({p.classId})</color></size>";
            return name;
        }

        /// <summary>
        /// S4 — Lance le visionnage live : pose le matchId dans LiveSpectateBridge puis charge la
        /// scène du mode (le LiveSpectateController y prend la main en GameMode.Replay).
        /// </summary>
        private void LaunchSpectate(string matchId, SpectateMatchInfo match)
        {
            CloseSpectatePopup();
            if (string.IsNullOrEmpty(matchId)) return;

            string scene = match != null && match.mode == "ranked" ? "40_CombatRanked1v1" : "33_CombatCasual";
            Nymora.Combat.Spectate.LiveSpectateBridge.RequestedMatchId = matchId;
            Nymora.Core.SceneFlow.SceneTransition.Load(scene, waitForReady: true);
        }
    }
}
