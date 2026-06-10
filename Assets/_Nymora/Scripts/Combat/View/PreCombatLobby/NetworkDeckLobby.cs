using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Nymora.Combat.View.HUD;
using Nymora.Core.Data;
using Photon.Client;
using Photon.Realtime;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Nymora.Combat.View.PreCombatLobby
{
    /// <summary>
    /// Brique 5.7-E1 — Lobby de DECK pré-combat RÉSEAU pour le 2v2/3v3 classé.
    ///
    /// Première étape de la séquence pré-combat 2v2 (cf project_precombat_2v2_flow) :
    ///   1) choix de deck (ICI) -> 2) vote capitaine (NetworkTeamOrderLobby) -> 3) pile ou face -> combat.
    ///
    /// Chaque joueur :
    ///   - publie sa CLASSE (nom), son DECK choisi (nom) et son état PRÊT via player custom properties
    ///     Photon (clés "ec"/"ed"/"er", + "es" = sub pour mapper au roster) ;
    ///   - voit les 4 (à 6) joueurs groupés par ÉQUIPE avec leur classe (alliés + ennemis) ;
    ///   - peut CHANGER son deck parmi ceux de sa classe (◀/▶) tant qu'il n'est pas prêt.
    /// Résout quand TOUS sont prêts, ou au timeout (auto-prêt sur la sélection courante).
    ///
    /// VIEW/NETWORK ONLY (aucun contact sim). Pompe Client.Service() à chaque frame (avant StartAsync),
    /// comme PreCombatLobbyController. La classe publiée ("ec") est relue par NetworkTeamOrderLobby (E2).
    /// </summary>
    public sealed class NetworkDeckLobby : MonoBehaviour
    {
        public const string KeySub = "es";    // sub du joueur (mappe room player -> roster)
        public const string KeyClass = "ec";  // nom de classe (ex "Nightseer")
        private const string KeyDeck = "ed";   // nom du deck choisi (affichage)
        private const string KeyReady = "er";  // prêt (bool)

        public float DurationSeconds = 30f;
        public float HardTimeoutSeconds = 35f;

        private RealtimeClient _client;
        private string _localSub;
        private string _localClass;
        private int _localTeam;
        private Match2v2Bridge.Player[] _roster;
        private IReadOnlyList<PreCombatDeckInfo> _decks;
        private int _deckIndex;
        private bool _localReady;

        // UI
        private readonly Dictionary<string, (TextMeshProUGUI cls, TextMeshProUGUI ready)> _cardBySub = new();
        private TextMeshProUGUI _deckNameLabel;
        private TextMeshProUGUI _timerLabel;
        private Button _prevBtn, _nextBtn, _readyBtn;
        private TextMeshProUGUI _readyBtnLabel;

        private static readonly Color TeamA = new Color(0.45f, 0.78f, 1.00f, 1f);
        private static readonly Color TeamB = new Color(1.00f, 0.62f, 0.42f, 1f);

        /// <summary>Pilote le lobby de deck. Retourne le deck choisi (ou défaut si timeout/erreur).</summary>
        public async Task<PreCombatDeckInfo> RunAsync(RealtimeClient client, Match2v2Bridge.Player[] roster, int localTeam,
            string localSub, string localClass, IReadOnlyList<PreCombatDeckInfo> decks, string defaultDeckId, CancellationToken ct)
        {
            _client = client;
            _roster = roster;
            _localTeam = localTeam;
            _localSub = localSub;
            _localClass = localClass;
            _decks = decks;
            _deckIndex = ResolveDefaultIndex(defaultDeckId);

            BuildUI();
            Nymora.Core.SceneFlow.SceneTransition.SignalReady(); // lève le voile

            PublishLocal();
            float start = Time.unscaledTime;
            while (!ct.IsCancellationRequested)
            {
                _client?.Service(); // pompe Photon (indispensable avant StartAsync)
                float elapsed = Time.unscaledTime - start;
                if (_timerLabel != null) _timerLabel.text = $"{Mathf.CeilToInt(Mathf.Max(0f, DurationSeconds - elapsed))} s";
                if (elapsed >= DurationSeconds && !_localReady) SetReady();
                RefreshFromRoom();
                if (AllReady()) break;
                if (elapsed >= HardTimeoutSeconds) break; // garde-fou : un joueur bloqué -> on procède
                await Task.Yield();
            }

            var chosen = (_decks != null && _deckIndex >= 0 && _deckIndex < _decks.Count) ? _decks[_deckIndex] : null;
            if (this != null && gameObject != null) Destroy(gameObject);
            return chosen;
        }

        private int ResolveDefaultIndex(string defaultDeckId)
        {
            if (_decks == null) return -1;
            for (int i = 0; i < _decks.Count; i++)
                if (_decks[i] != null && _decks[i].Id == defaultDeckId) return i;
            return _decks.Count > 0 ? 0 : -1;
        }

        // ===== Réseau (player custom properties) =====

        private void PublishLocal()
        {
            if (_client?.LocalPlayer == null) return;
            string deckName = (_decks != null && _deckIndex >= 0 && _deckIndex < _decks.Count) ? (_decks[_deckIndex]?.Name ?? "") : "";
            _client.LocalPlayer.SetCustomProperties(new PhotonHashtable
            {
                { KeySub, _localSub ?? "" },
                { KeyClass, _localClass ?? "" },
                { KeyDeck, deckName },
                { KeyReady, _localReady },
            });
        }

        private void RefreshFromRoom()
        {
            var room = _client?.CurrentRoom;
            if (room?.Players == null) return;

            // sub -> (class, ready) depuis les props publiées.
            var classBySub = new Dictionary<string, string>();
            var readyBySub = new Dictionary<string, bool>();
            foreach (var kv in room.Players)
            {
                var cp = kv.Value?.CustomProperties;
                if (cp == null || !cp.ContainsKey(KeySub)) continue;
                if (cp[KeySub] is not string s || string.IsNullOrEmpty(s)) continue;
                if (cp.ContainsKey(KeyClass) && cp[KeyClass] is string c) classBySub[s] = c;
                if (cp.ContainsKey(KeyReady) && cp[KeyReady] is bool r) readyBySub[s] = r;
            }

            foreach (var kv in _cardBySub)
            {
                string sub = kv.Key;
                var (clsLbl, readyLbl) = kv.Value;
                if (clsLbl != null)
                    clsLbl.text = classBySub.TryGetValue(sub, out var c) && !string.IsNullOrEmpty(c) ? c : "…";
                if (readyLbl != null)
                {
                    bool ready = readyBySub.TryGetValue(sub, out var r) && r;
                    readyLbl.text = ready ? "PRÊT" : "";
                    readyLbl.color = ready ? new Color(0.36f, 0.82f, 0.42f) : CombatUiKit.TextMuted;
                }
            }
        }

        private bool AllReady()
        {
            var room = _client?.CurrentRoom;
            if (room?.Players == null || _roster == null) return false;
            int readyCount = 0;
            foreach (var kv in room.Players)
            {
                var cp = kv.Value?.CustomProperties;
                if (cp != null && cp.ContainsKey(KeyReady) && cp[KeyReady] is bool r && r) readyCount++;
            }
            return readyCount >= _roster.Length;
        }

        private void SetReady()
        {
            if (_localReady) return;
            _localReady = true;
            PublishLocal();
            if (_prevBtn != null) _prevBtn.interactable = false;
            if (_nextBtn != null) _nextBtn.interactable = false;
            if (_readyBtn != null) _readyBtn.interactable = false;
            if (_readyBtnLabel != null) _readyBtnLabel.text = "En attente…";
        }

        private void CycleDeck(int dir)
        {
            if (_localReady || _decks == null || _decks.Count == 0) return;
            _deckIndex = (_deckIndex + dir + _decks.Count) % _decks.Count;
            if (_deckNameLabel != null) _deckNameLabel.text = _decks[_deckIndex]?.Name ?? "Deck";
            PublishLocal();
            Nymora.Core.Audio.NymoraAudioManager.Instance?.PlaySfx(Nymora.Core.Audio.SoundId.UiClick);
        }

        // ===== UI (DA hub via CombatUiKit) =====

        private void BuildUI()
        {
            var canvasGo = new GameObject("NetDeckLobbyCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 5000;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            var dim = NewRect("Dim", (RectTransform)canvasGo.transform);
            Stretch(dim);
            dim.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.72f);

            var card = NewRect("Card", (RectTransform)canvasGo.transform);
            card.anchorMin = card.anchorMax = card.pivot = new Vector2(0.5f, 0.5f);
            card.sizeDelta = new Vector2(900f, 560f);
            var cardImg = card.gameObject.AddComponent<Image>();
            cardImg.color = CombatUiKit.PanelBg;
            CombatUiKit.ApplyRounded(cardImg, 18f);
            var vlg = card.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(36, 36, 28, 28);
            vlg.spacing = 16f;
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.childControlWidth = true; vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;

            NewText("Title", card, "CHOIX DE DECK", 48f, true, CombatUiKit.Accent, 60f);
            NewText("Sub", card, "Choisis ton deck — les classes des 2 équipes sont révélées", 26f, false, CombatUiKit.TextSecondary, 34f);

            // 2 colonnes d'équipes.
            var teamsRow = NewRect("Teams", card);
            var tle = teamsRow.gameObject.AddComponent<LayoutElement>(); tle.preferredHeight = 250f;
            var thlg = teamsRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            thlg.spacing = 24f; thlg.childAlignment = TextAnchor.UpperCenter;
            thlg.childControlWidth = true; thlg.childControlHeight = true;
            thlg.childForceExpandWidth = true; thlg.childForceExpandHeight = true;

            BuildTeamColumn(teamsRow, 0);
            BuildTeamColumn(teamsRow, 1);

            // Sélecteur de deck local (◀ nom ▶).
            var picker = NewRect("Picker", card);
            var ple = picker.gameObject.AddComponent<LayoutElement>(); ple.preferredHeight = 70f;
            var phlg = picker.gameObject.AddComponent<HorizontalLayoutGroup>();
            phlg.spacing = 14f; phlg.childAlignment = TextAnchor.MiddleCenter;
            phlg.childControlWidth = true; phlg.childControlHeight = true;
            phlg.childForceExpandWidth = false; phlg.childForceExpandHeight = false;

            _prevBtn = BuildSmallButton(picker, "<", () => CycleDeck(-1));
            string startName = (_decks != null && _deckIndex >= 0 && _deckIndex < _decks.Count) ? (_decks[_deckIndex]?.Name ?? "Deck") : "Aucun deck";
            _deckNameLabel = NewText("DeckName", picker, startName, 32f, true, CombatUiKit.TextPrimary, 0f);
            _deckNameLabel.alignment = TextAlignmentOptions.Center;
            var dnLe = _deckNameLabel.gameObject.GetComponent<LayoutElement>();
            dnLe.preferredWidth = 360f; dnLe.flexibleWidth = 0f;
            _nextBtn = BuildSmallButton(picker, ">", () => CycleDeck(+1));

            // Bouton Prêt + timer.
            var bottom = NewRect("Bottom", card);
            var ble = bottom.gameObject.AddComponent<LayoutElement>(); ble.preferredHeight = 72f;
            var bhlg = bottom.gameObject.AddComponent<HorizontalLayoutGroup>();
            bhlg.spacing = 18f; bhlg.childAlignment = TextAnchor.MiddleCenter;
            bhlg.childControlWidth = true; bhlg.childControlHeight = true;
            bhlg.childForceExpandWidth = false; bhlg.childForceExpandHeight = false;

            _timerLabel = NewText("Timer", bottom, $"{Mathf.CeilToInt(DurationSeconds)} s", 30f, false, CombatUiKit.TextMuted, 0f);
            var tLe = _timerLabel.gameObject.GetComponent<LayoutElement>(); tLe.preferredWidth = 120f;

            var readyRt = NewRect("Ready", bottom);
            var rle = readyRt.gameObject.AddComponent<LayoutElement>(); rle.preferredWidth = 320f; rle.preferredHeight = 64f; rle.flexibleWidth = 0f;
            var rbg = readyRt.gameObject.AddComponent<Image>(); rbg.color = CombatUiKit.Accent; CombatUiKit.ApplyRounded(rbg, 12f);
            _readyBtn = readyRt.gameObject.AddComponent<Button>(); _readyBtn.targetGraphic = rbg;
            _readyBtn.onClick.AddListener(SetReady);
            _readyBtnLabel = NewText("RdyLbl", readyRt, "Prêt", 32f, true, CombatUiKit.TextOnLight, 0f);
            Stretch(_readyBtnLabel.rectTransform);
        }

        private void BuildTeamColumn(RectTransform parent, int teamId)
        {
            var col = NewRect($"Team{teamId}", parent);
            var img = col.gameObject.AddComponent<Image>(); img.color = CombatUiKit.CardBg; CombatUiKit.ApplyRounded(img, 14f);
            var vlg = col.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(18, 18, 14, 14); vlg.spacing = 10f;
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.childControlWidth = true; vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;

            bool isAlly = teamId == _localTeam;
            var header = NewText("Hdr", col, isAlly ? "TON ÉQUIPE" : "ADVERSAIRES", 26f, true,
                teamId == 0 ? TeamA : TeamB, 36f);
            header.alignment = TextAlignmentOptions.Center;

            if (_roster == null) return;
            foreach (var p in _roster)
            {
                if (p.Team != teamId) continue;
                BuildPlayerRow(col, p);
            }
        }

        private void BuildPlayerRow(RectTransform parent, Match2v2Bridge.Player p)
        {
            var row = NewRect("Row", parent);
            var rimg = row.gameObject.AddComponent<Image>(); rimg.color = CombatUiKit.GhostBg; CombatUiKit.ApplyRounded(rimg, 10f);
            var le = row.gameObject.AddComponent<LayoutElement>(); le.preferredHeight = 64f;
            var hlg = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            hlg.padding = new RectOffset(16, 16, 0, 0); hlg.spacing = 8f; hlg.childAlignment = TextAnchor.MiddleLeft;
            hlg.childControlWidth = true; hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false; hlg.childForceExpandHeight = false;

            bool isLocal = p.Sub == _localSub;
            string pseudo = !string.IsNullOrEmpty(p.DisplayName) ? p.DisplayName : "Joueur";
            var nameLbl = NewText("Name", row, isLocal ? $"{pseudo} (toi)" : pseudo, 24f, isLocal, CombatUiKit.TextPrimary, 0f);
            nameLbl.alignment = TextAlignmentOptions.Left; nameLbl.enableWordWrapping = false; nameLbl.overflowMode = TextOverflowModes.Ellipsis;
            nameLbl.gameObject.GetComponent<LayoutElement>().flexibleWidth = 1f;

            // Classe (révélée via prop ; "…" tant que pas publiée). Pour le local, on connaît déjà.
            var clsLbl = NewText("Cls", row, isLocal ? (_localClass ?? "…") : "…", 24f, true, CombatUiKit.TextSecondary, 0f);
            clsLbl.alignment = TextAlignmentOptions.Right;
            clsLbl.gameObject.GetComponent<LayoutElement>().preferredWidth = 150f;

            var readyLbl = NewText("Rdy", row, "", 20f, true, CombatUiKit.TextMuted, 0f);
            readyLbl.alignment = TextAlignmentOptions.Right;
            readyLbl.gameObject.GetComponent<LayoutElement>().preferredWidth = 70f;

            _cardBySub[p.Sub] = (clsLbl, readyLbl);
        }

        // ===== Helpers UI =====

        private Button BuildSmallButton(RectTransform parent, string glyph, Action onClick)
        {
            var rt = NewRect(glyph, parent);
            var le = rt.gameObject.AddComponent<LayoutElement>(); le.preferredWidth = 60f; le.preferredHeight = 60f; le.flexibleWidth = 0f;
            var bg = rt.gameObject.AddComponent<Image>(); bg.color = CombatUiKit.GhostBg; CombatUiKit.ApplyRounded(bg, 10f);
            var btn = rt.gameObject.AddComponent<Button>(); btn.targetGraphic = bg;
            if (onClick != null) btn.onClick.AddListener(() => onClick());
            var t = NewText("G", rt, glyph, 30f, true, CombatUiKit.TextPrimary, 0f);
            Stretch(t.rectTransform);
            return btn;
        }

        private static RectTransform NewRect(string name, RectTransform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            return rt;
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        }

        private static TextMeshProUGUI NewText(string name, RectTransform parent, string text, float size, bool bold, Color color, float preferredHeight)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<TextMeshProUGUI>();
            t.text = text; t.fontSize = size; t.color = color;
            t.alignment = TextAlignmentOptions.Center;
            t.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
            t.raycastTarget = false;
            var le = go.AddComponent<LayoutElement>();
            if (preferredHeight > 0f) le.preferredHeight = preferredHeight;
            return t;
        }
    }
}
