using System.Collections.Generic;
using Nymora.Combat.Bootstrap;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Nymora.Combat.View.HUD
{
    /// <summary>
    /// Brique 5.6 (2v2/3v3) — Panneau pré-combat « ordre de jeu » (hot-seat, panneau UNIQUE).
    ///
    /// Avant le démarrage de la partie, CombatBootstrap2v2 publie le roster via TeamOrderVote.Request()
    /// et attend. Ce panneau (auto-instancié pour les scènes 2v2/3v3, pattern procédural de
    /// CoinFlipIntroView) affiche les équipes et laisse Lorenzo réordonner chacune (rang 0 = joue en
    /// premier). « Lancer le combat » renvoie l'ordre (TeamOrder par PlayerSlot) au bootstrap.
    ///
    /// DA : calquée sur le menu hub via CombatUiKit (palette monochrome, coins arrondis, police Ari
    /// récupérée au runtime depuis le HUD comme MatchEndOverlay). Hot-seat = un seul joueur réel →
    /// pas de notion de capitaine ici (le vrai vote par-capitaine réseau viendra en 5.7). 100% View,
    /// zéro impact simulation → pas de bump CombatRulesVersion.
    /// </summary>
    public class PreCombatOrderPanel : MonoBehaviour
    {
        private static PreCombatOrderPanel _current;

        // Accent d'équipe (réservé, le reste est monochrome CombatUiKit) : aligné sur la convention
        // combat (bleu = 1re équipe, orange = 2e, vert = 3e en 3v3).
        private static readonly Color TeamAccentA = new Color(0.45f, 0.78f, 1.00f, 1f);
        private static readonly Color TeamAccentB = new Color(1.00f, 0.62f, 0.42f, 1f);
        private static readonly Color TeamAccentC = new Color(0.55f, 0.90f, 0.58f, 1f);

        private bool _built;
        private Canvas _canvas;
        private readonly List<int> _teamIds = new List<int>();
        private readonly Dictionary<int, List<TeamOrderVote.VoteSlot>> _byTeam = new Dictionary<int, List<TeamOrderVote.VoteSlot>>();
        private readonly Dictionary<int, List<TextMeshProUGUI>> _rowLabels = new Dictionary<int, List<TextMeshProUGUI>>();

        // Police Ari récupérée au runtime (cache statique).
        private static TMP_FontAsset _ariRegular, _ariBold;
        private static bool _ariTried;
        private static Sprite _arrowSprite;
        private static bool _arrowTried;

        // ===== Auto-instanciation par scène d'équipe (cf CoinFlipIntroView / CombatReadyBeacon) =====
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Register()
        {
            SceneManager.sceneLoaded -= OnAnySceneLoaded;
            SceneManager.sceneLoaded += OnAnySceneLoaded;
            TryCreateForScene(SceneManager.GetActiveScene());
        }

        private static void OnAnySceneLoaded(Scene scene, LoadSceneMode mode) => TryCreateForScene(scene);

        private static void TryCreateForScene(Scene scene)
        {
            bool isTeamScene = scene.IsValid() && (scene.name.Contains("2v2") || scene.name.Contains("3v3"));
            if (!isTeamScene || _current != null) return;
            var go = new GameObject("PreCombatOrderPanel");
            _current = go.AddComponent<PreCombatOrderPanel>();
        }

        private void Awake() { _current = this; }

        private void OnDestroy()
        {
            if (_current == this) _current = null;
            if (TeamOrderVote.HasRequest) TeamOrderVote.Cancel(); // scène quittée mid-vote -> ordre défaut
        }

        private void Update()
        {
            if (_built) return;
            if (!TeamOrderVote.HasRequest || TeamOrderVote.Roster == null || TeamOrderVote.Roster.Length == 0) return;
            BuildUI(TeamOrderVote.Roster);
            _built = true;
            TeamOrderVote.Acknowledged = true; // empêche le fallback "ordre par défaut" du bootstrap
            // Le démarrage de la partie est retardé derrière ce vote : on lève le voile de chargement
            //   (CombatReadyBeacon ne le ferait qu'à CallbackGameStarted, après le vote). No-op en Play direct.
            Nymora.Core.SceneFlow.SceneTransition.SignalReady();
        }

        // ================================================================
        // Construction UI (DA hub via CombatUiKit + layout groups, façon MatchEndOverlay)
        // ================================================================

        private void BuildUI(TeamOrderVote.VoteSlot[] roster)
        {
            // Groupe le roster par équipe en préservant l'ordre d'arrivée (= ordre par défaut).
            _teamIds.Clear(); _byTeam.Clear(); _rowLabels.Clear();
            foreach (var s in roster)
            {
                if (!_byTeam.TryGetValue(s.TeamId, out var list))
                {
                    list = new List<TeamOrderVote.VoteSlot>();
                    _byTeam[s.TeamId] = list;
                    _teamIds.Add(s.TeamId);
                }
                list.Add(s);
            }
            _teamIds.Sort();

            _canvas = gameObject.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 31000;
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            gameObject.AddComponent<GraphicRaycaster>();

            // Dimmer plein écran (assombrit le décor + bloque les clics), façon MatchEndOverlay.
            var dimmer = NewUIChild("Dimmer", _canvas.transform);
            Stretch(dimmer);
            var dimg = dimmer.gameObject.AddComponent<Image>();
            dimg.color = new Color(0.02f, 0.02f, 0.035f, 0.88f);
            dimg.raycastTarget = true;

            // Carte centrale (colonne, auto-hauteur).
            const float teamCardW = 430f, teamGap = 32f, pad = 60f;
            float teamsW = _teamIds.Count * teamCardW + (_teamIds.Count - 1) * teamGap;
            float cardW = teamsW + pad * 2f;

            var card = NewUIChild("Card", _canvas.transform);
            card.anchorMin = card.anchorMax = card.pivot = new Vector2(0.5f, 0.5f);
            card.anchoredPosition = Vector2.zero;
            card.sizeDelta = new Vector2(cardW, 0f);
            var cardImg = card.gameObject.AddComponent<Image>();
            cardImg.color = CombatUiKit.PanelBg;
            CombatUiKit.ApplyRounded(cardImg, 18f);
            var cardVlg = card.gameObject.AddComponent<VerticalLayoutGroup>();
            cardVlg.padding = new RectOffset((int)pad, (int)pad, 44, 44);
            cardVlg.spacing = 24f;
            cardVlg.childAlignment = TextAnchor.UpperCenter;
            cardVlg.childControlWidth = true; cardVlg.childControlHeight = true;
            cardVlg.childForceExpandWidth = false; cardVlg.childForceExpandHeight = false;
            var cardCsf = card.gameObject.AddComponent<ContentSizeFitter>();
            cardCsf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            cardCsf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

            // Titre + sous-titre.
            var title = NewText("Title", card, "ORDRE DE JEU", 56f, bold: true, color: CombatUiKit.Accent);
            FixedSize(title.gameObject, teamsW, 70f);
            var sub = NewText("Subtitle", card, "Choisis qui joue en premier dans chaque équipe",
                27f, bold: false, color: CombatUiKit.TextSecondary);
            FixedSize(sub.gameObject, teamsW, 38f);

            // Rangée des cartes d'équipe.
            var teamsRow = NewUIChild("TeamsRow", card);
            var teamsHlg = teamsRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            teamsHlg.spacing = teamGap;
            teamsHlg.childAlignment = TextAnchor.UpperCenter;
            teamsHlg.childControlWidth = true; teamsHlg.childControlHeight = true;
            teamsHlg.childForceExpandWidth = false; teamsHlg.childForceExpandHeight = false;
            FixedSize(teamsRow.gameObject, teamsW, 0f, fitHeight: true);
            foreach (int teamId in _teamIds)
                BuildTeamCard(teamsRow, teamId, teamCardW);

            // Bouton « Lancer le combat » : pilule accent claire, texte sombre (comme le bouton Envoyer hub).
            BuildLaunchButton(card, teamsW);
        }

        private void BuildTeamCard(RectTransform parent, int teamId, float width)
        {
            var list = _byTeam[teamId];
            var labels = new List<TextMeshProUGUI>();
            _rowLabels[teamId] = labels;

            var teamCard = NewUIChild($"Team{teamId}", parent);
            var tImg = teamCard.gameObject.AddComponent<Image>();
            tImg.color = CombatUiKit.CardBg;
            CombatUiKit.ApplyRounded(tImg, 14f);
            var vlg = teamCard.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(22, 22, 18, 22);
            vlg.spacing = 12f;
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.childControlWidth = true; vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;
            var le = teamCard.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = width; le.flexibleWidth = 0f;
            var csf = teamCard.gameObject.AddComponent<ContentSizeFitter>();
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

            // En-tête d'équipe : pastille d'accent + nom (monochrome).
            var header = NewUIChild("Header", teamCard);
            var hl = header.gameObject.AddComponent<LayoutElement>(); hl.preferredHeight = 48f;
            var hImg = header.gameObject.AddComponent<Image>();
            hImg.color = new Color(1f, 1f, 1f, 0f); // conteneur transparent (pour le layout du HLG interne)
            hImg.raycastTarget = false;
            var hHlg = header.gameObject.AddComponent<HorizontalLayoutGroup>();
            hHlg.spacing = 10f; hHlg.childAlignment = TextAnchor.MiddleCenter;
            hHlg.childControlWidth = true; hHlg.childControlHeight = true;
            hHlg.childForceExpandWidth = false; hHlg.childForceExpandHeight = false;

            var dot = NewUIChild("Dot", header);
            var dotImg = dot.gameObject.AddComponent<Image>();
            dotImg.color = TeamColor(teamId);
            CombatUiKit.ApplyRounded(dotImg, 8f);
            FixedSize(dot.gameObject, 16f, 16f);

            var hTxt = NewText("Name", header, TeamName(teamId), 30f, bold: true, color: CombatUiKit.TextPrimary);
            hTxt.alignment = TextAlignmentOptions.Left;

            // Lignes des membres.
            for (int i = 0; i < list.Count; i++)
            {
                int capturedTeam = teamId, capturedIndex = i;
                var row = NewUIChild($"Row{i}", teamCard);
                var rImg = row.gameObject.AddComponent<Image>();
                rImg.color = CombatUiKit.GhostBg;
                CombatUiKit.ApplyRounded(rImg, 10f);
                var rLe = row.gameObject.AddComponent<LayoutElement>(); rLe.preferredHeight = 74f;
                var rHlg = row.gameObject.AddComponent<HorizontalLayoutGroup>();
                rHlg.padding = new RectOffset(20, 14, 0, 0);
                rHlg.spacing = 10f; rHlg.childAlignment = TextAnchor.MiddleLeft;
                rHlg.childControlWidth = true; rHlg.childControlHeight = true;
                rHlg.childForceExpandWidth = false; rHlg.childForceExpandHeight = false;

                var lbl = NewText($"Lbl{i}", row, "", 28f, bold: true, color: CombatUiKit.TextPrimary);
                lbl.alignment = TextAlignmentOptions.Left;
                lbl.enableWordWrapping = false;
                lbl.overflowMode = TextOverflowModes.Ellipsis; // pas de débordement sur les flèches
                var lblLe = lbl.gameObject.AddComponent<LayoutElement>(); lblLe.flexibleWidth = 1f;
                labels.Add(lbl);

                BuildArrowButton(row, up: true, () => Move(capturedTeam, capturedIndex, -1));
                BuildArrowButton(row, up: false, () => Move(capturedTeam, capturedIndex, +1));
            }
            RefreshTeam(teamId);
        }

        private void BuildArrowButton(RectTransform parent, bool up, System.Action onClick)
        {
            var btnRt = NewUIChild(up ? "Up" : "Down", parent);
            var le = btnRt.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = 54f; le.preferredHeight = 54f; le.flexibleWidth = 0f;
            var bg = btnRt.gameObject.AddComponent<Image>();
            bg.color = CombatUiKit.GhostBg;
            CombatUiKit.ApplyRounded(bg, 10f);
            var btn = btnRt.gameObject.AddComponent<Button>();
            btn.targetGraphic = bg;
            var cb = btn.colors;
            cb.normalColor = Color.white;
            cb.highlightedColor = new Color(1.7f, 1.7f, 1.7f, 1f); // éclaircit le ghost
            cb.pressedColor = new Color(2.2f, 2.2f, 2.2f, 1f);
            cb.fadeDuration = 0.08f;
            btn.colors = cb;
            if (onClick != null) btn.onClick.AddListener(() => onClick());

            var arrow = GetArrowSprite();
            if (arrow != null)
            {
                var icon = NewUIChild("Icon", btnRt);
                icon.anchorMin = icon.anchorMax = icon.pivot = new Vector2(0.5f, 0.5f);
                icon.sizeDelta = new Vector2(24f, 24f);
                // direction_arrow pointe +x (droite) à 0° : +90° -> haut, -90° -> bas.
                icon.localRotation = Quaternion.Euler(0f, 0f, up ? 90f : -90f);
                var iImg = icon.gameObject.AddComponent<Image>();
                iImg.sprite = arrow;
                iImg.color = CombatUiKit.TextPrimary;
                iImg.preserveAspect = true;
                iImg.raycastTarget = false;
            }
            else
            {
                // Fallback ASCII (Ari ne porte pas ▲▼) si le SVG n'est pas importé.
                var t = NewText("Glyph", btnRt, up ? "^" : "v", 26f, bold: true, color: CombatUiKit.TextPrimary);
                Stretch(t.rectTransform);
            }
        }

        private void BuildLaunchButton(RectTransform parent, float width)
        {
            var btnRt = NewUIChild("LaunchBtn", parent);
            var le = btnRt.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = Mathf.Min(width, 500f); le.preferredHeight = 78f; le.flexibleWidth = 0f;
            var bg = btnRt.gameObject.AddComponent<Image>();
            bg.color = CombatUiKit.Accent;
            CombatUiKit.ApplyRounded(bg, 14f);
            var btn = btnRt.gameObject.AddComponent<Button>();
            btn.targetGraphic = bg;
            var cb = btn.colors;
            cb.normalColor = Color.white;
            cb.highlightedColor = new Color(0.92f, 0.92f, 0.94f, 1f);
            cb.pressedColor = new Color(0.82f, 0.82f, 0.84f, 1f);
            cb.fadeDuration = 0.08f;
            btn.colors = cb;
            btn.onClick.AddListener(OnLaunch);

            var t = NewText("Label", btnRt, "Lancer le combat", 36f, bold: true, color: CombatUiKit.TextOnLight);
            Stretch(t.rectTransform);
        }

        // ================================================================
        // Logique de réordonnancement
        // ================================================================

        private void Move(int teamId, int index, int dir)
        {
            var list = _byTeam[teamId];
            int j = index + dir;
            if (j < 0 || j >= list.Count) return;
            (list[index], list[j]) = (list[j], list[index]);
            RefreshTeam(teamId);
            Nymora.Core.Audio.NymoraAudioManager.Instance?.PlaySfx(Nymora.Core.Audio.SoundId.UiClick);
        }

        private void RefreshTeam(int teamId)
        {
            var list = _byTeam[teamId];
            var labels = _rowLabels[teamId];
            for (int i = 0; i < labels.Count && i < list.Count; i++)
                labels[i].text = $"<color=#{ColorUtility.ToHtmlStringRGB(CombatUiKit.TextMuted)}>{i + 1}.</color>   {list[i].Label}";
        }

        private void OnLaunch()
        {
            int maxSlot = 0;
            foreach (var kv in _byTeam)
                foreach (var s in kv.Value)
                    if (s.PlayerSlot > maxSlot) maxSlot = s.PlayerSlot;

            var order = new int[maxSlot + 1];
            foreach (int teamId in _teamIds)
            {
                var list = _byTeam[teamId];
                for (int rank = 0; rank < list.Count; rank++)
                    order[list[rank].PlayerSlot] = rank;
            }

            Nymora.Core.Audio.NymoraAudioManager.Instance?.PlaySfx(Nymora.Core.Audio.SoundId.CombatStart);
            TeamOrderVote.Submit(order);
            Destroy(gameObject);
        }

        // ================================================================
        // Helpers
        // ================================================================

        private static string TeamName(int teamId) => $"Équipe {(char)('A' + teamId)}";

        private static Color TeamColor(int teamId)
        {
            switch (teamId)
            {
                case 0: return TeamAccentA;
                case 1: return TeamAccentB;
                default: return TeamAccentC;
            }
        }

        private static RectTransform NewUIChild(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        }

        // Force une taille préférée via LayoutElement (pour les enfants de layout groups).
        private static void FixedSize(GameObject go, float w, float h, bool fitHeight = false)
        {
            var le = go.GetComponent<LayoutElement>() ?? go.AddComponent<LayoutElement>();
            if (w > 0f) le.preferredWidth = w;
            if (!fitHeight && h > 0f) le.preferredHeight = h;
            le.flexibleWidth = 0f;
        }

        private TextMeshProUGUI NewText(string name, Transform parent, string text, float size, bool bold, Color color)
        {
            var rt = NewUIChild(name, parent);
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            var font = bold ? AriBold() : AriRegular();
            if (font != null) t.font = font;
            t.text = text;
            t.fontSize = size;
            t.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
            t.color = color;
            t.richText = true;
            t.alignment = TextAlignmentOptions.Center;
            t.raycastTarget = false;
            return t;
        }

        // ===== Police Ari : récupérée parmi les TMP_FontAsset chargés (le HUD combat la référence). =====
        private static TMP_FontAsset AriRegular() { ResolveAri(); return _ariRegular; }
        private static TMP_FontAsset AriBold() { ResolveAri(); return _ariBold; }

        private static void ResolveAri()
        {
            if (_ariTried) return;
            _ariTried = true;
            foreach (var f in Resources.FindObjectsOfTypeAll<TMP_FontAsset>())
            {
                if (f == null || string.IsNullOrEmpty(f.name) || !f.name.Contains("Ari")) continue;
                if (f.name.Contains("Bold")) { if (_ariBold == null) _ariBold = f; }
                else if (_ariRegular == null) _ariRegular = f;
            }
            if (_ariRegular == null) _ariRegular = _ariBold;           // un seul variant trouvé
            if (_ariBold == null) _ariBold = _ariRegular;
            // null toléré -> NewText retombe sur la police TMP par défaut.
        }

        private static Sprite GetArrowSprite()
        {
            if (_arrowTried) return _arrowSprite;
            _arrowTried = true;
            _arrowSprite = Resources.Load<Sprite>("UI/Icons/direction_arrow");
            return _arrowSprite;
        }
    }
}
