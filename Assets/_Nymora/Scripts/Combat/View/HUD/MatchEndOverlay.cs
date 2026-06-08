using System.Collections;
using System.Text;
using Nymora.Combat.Replay;
using Nymora.Core.Data;
using Nymora.Core.SceneFlow;
using Quantum;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Button = UnityEngine.UI.Button;

namespace Nymora.Combat.View.HUD
{
    /// <summary>
    /// Overlay UI de fin de match. Affiche VICTOIRE / DÉFAITE / MATCH NUL + infos (adversaire,
    /// MMR ranked avec delta exact, récompenses, stats de combat) + boutons (Retour hub, Replay).
    ///
    /// Refonte 31 mai : l'UI est ENTIÈREMENT auto-construite en code (fond plein écran + carte
    /// centrale dans un VerticalLayoutGroup → titre/infos/boutons empilés, zéro chevauchement),
    /// au design du hub (palette CombatUiKit monochrome + coins arrondis). Les visuels legacy
    /// de la scène (titre/sous-titre/boutons sérialisés) sont masqués ; seules les RÉFÉRENCES
    /// logiques (ReplayRecorder, handlers) sont réutilisées.
    ///
    /// Polled par CombatHUDController via Refresh() chaque frame (idempotent via _shown).
    /// </summary>
    public class MatchEndOverlay : MonoBehaviour
    {
        [Header("Refs logiques (les visuels legacy sont masqués au profit de l'UI auto-construite)")]
        [Tooltip("Ancien panel scène — forcé masqué (l'overlay construit son propre root plein écran).")]
        [SerializeField] private GameObject _panel;
        [SerializeField] private TMP_Text _titleText;     // legacy : sert juste de source de police titre
        [SerializeField] private TMP_Text _subtitleText;  // legacy : masqué
        [SerializeField] private Button _restartEasyButton;   // déprécié
        [SerializeField] private Button _restartMediumButton; // déprécié
        [SerializeField] private Button _returnToHubButton;   // legacy : masqué (bouton reconstruit)
        [SerializeField] private Button _saveReplayButton;    // legacy : masqué (bouton reconstruit)
        [SerializeField] private TMP_Text _saveReplayLabel;   // legacy

        [Header("Replay (Brique 3.E.1)")]
        [Tooltip("ReplayRecorder de la scene. Si laisse vide, le bouton 'Sauvegarder le replay' est masque.")]
        [SerializeField] private ReplayRecorder _replayRecorder;
        [SerializeField] private string _saveReplayDefaultText = "Sauvegarder le replay";
        [SerializeField] private string _saveReplaySavedText = "Replay sauvegardé ✓";

        [Header("Couleurs titre")]
        [SerializeField] private Color _victoryColor = new Color(1.00f, 0.83f, 0.30f, 1f); // or
        [SerializeField] private Color _defeatColor = new Color(0.90f, 0.25f, 0.25f, 1f); // rouge
        [SerializeField] private Color _drawColor = new Color(0.75f, 0.75f, 0.75f, 1f); // gris

        [Header("Carte (auto-construite)")]
        [SerializeField] private float _cardWidth = 560f;

        // UI construite en code.
        private GameObject _root;       // plein écran (dimmer), enfant du Canvas racine
        private TMP_Text _builtTitle;
        private TMP_Text _builtInfo;
        private Button _builtReturnBtn;
        private Button _builtSaveBtn;
        private TMP_Text _builtSaveLabel;

        private bool _built;
        private bool _shown;
        private bool _pendingShow; // item F : reveal différé pendant l'anim du floating text signature
        private bool _replaySavedThisMatch;
        // Snapshot des params Refresh pour OnReturnToHubClicked + le reveal différé.
        private int _localPlayerIndex;
        private int _winnerPlayerIndex;
        private bool _isPvpMatch;
        private int _turnNumber;

        private void Awake()
        {
            BuildOverlayUI();
            Hide();
        }

        // ============================ Construction UI ============================

        private void BuildOverlayUI()
        {
            if (_built) return;
            _built = true;

            // Police : titre = police du titre legacy (display) ; corps/boutons = police du sous-titre.
            TMP_FontAsset titleFont = _titleText != null ? _titleText.font : null;
            TMP_FontAsset bodyFont = _subtitleText != null && _subtitleText.font != null
                ? _subtitleText.font : titleFont;

            // Masque tous les visuels legacy (on reconstruit tout).
            if (_panel != null) _panel.SetActive(false);
            HideLegacy(_titleText); HideLegacy(_subtitleText);
            HideLegacy(_restartEasyButton); HideLegacy(_restartMediumButton);
            HideLegacy(_returnToHubButton); HideLegacy(_saveReplayButton);

            // Root plein écran sous le Canvas racine (dimmer qui assombrit le combat + bloque les clics).
            var canvas = GetComponentInParent<Canvas>();
            Transform rootParent = canvas != null ? canvas.rootCanvas.transform : transform;
            _root = new GameObject("MatchEndRoot", typeof(RectTransform), typeof(Image));
            _root.transform.SetParent(rootParent, false);
            var rootRt = (RectTransform)_root.transform;
            rootRt.anchorMin = Vector2.zero; rootRt.anchorMax = Vector2.one;
            rootRt.offsetMin = Vector2.zero; rootRt.offsetMax = Vector2.zero;
            var dimmer = _root.GetComponent<Image>();
            dimmer.color = new Color(0.02f, 0.02f, 0.035f, 0.88f);
            dimmer.raycastTarget = true; // bloque les interactions avec le HUD derrière

            // Carte centrale (colonne).
            float innerW = _cardWidth - 72f; // padding L/R 36
            var cardGo = new GameObject("Card",
                typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            cardGo.transform.SetParent(_root.transform, false);
            var cardRt = (RectTransform)cardGo.transform;
            cardRt.anchorMin = cardRt.anchorMax = new Vector2(0.5f, 0.5f);
            cardRt.pivot = new Vector2(0.5f, 0.5f);
            cardRt.anchoredPosition = Vector2.zero;
            cardRt.sizeDelta = new Vector2(_cardWidth, 0f);
            var cardBg = cardGo.GetComponent<Image>();
            cardBg.color = CombatUiKit.PanelBg;
            cardBg.raycastTarget = true;
            CombatUiKit.ApplyRounded(cardBg, 18f);
            var vlg = cardGo.GetComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(36, 36, 30, 30);
            vlg.spacing = 16f;
            vlg.childAlignment = TextAnchor.MiddleCenter;
            vlg.childControlWidth = true; vlg.childControlHeight = true;
            vlg.childForceExpandWidth = false; vlg.childForceExpandHeight = false;
            var csf = cardGo.GetComponent<ContentSizeFitter>();
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

            // Titre (auto-size pour ne jamais déborder de la carte).
            _builtTitle = MakeText(cardGo.transform, "Title", titleFont, 58f, FontStyles.Bold,
                TextAlignmentOptions.Center, innerW, false);
            _builtTitle.enableAutoSizing = true;
            _builtTitle.fontSizeMax = 58f;
            _builtTitle.fontSizeMin = 30f;

            // Séparateur fin.
            var sep = new GameObject("Sep", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            sep.transform.SetParent(cardGo.transform, false);
            sep.GetComponent<Image>().color = new Color(CombatUiKit.Accent.r, CombatUiKit.Accent.g, CombatUiKit.Accent.b, 0.25f);
            var sepLe = sep.GetComponent<LayoutElement>();
            sepLe.preferredWidth = innerW * 0.5f; sepLe.preferredHeight = 2f;

            // Bloc d'infos.
            _builtInfo = MakeText(cardGo.transform, "Info", bodyFont, 24f, FontStyles.Normal,
                TextAlignmentOptions.Center, innerW, true);
            _builtInfo.lineSpacing = 6f;

            // Boutons (empilés sous les infos).
            _builtReturnBtn = MakeButton(cardGo.transform, "ReturnBtn", "Retour au hub", true, bodyFont,
                OnReturnToHubClicked, out _);
            _builtSaveBtn = MakeButton(cardGo.transform, "SaveBtn", _saveReplayDefaultText, false, bodyFont,
                OnSaveReplayClicked, out _builtSaveLabel);

            _root.SetActive(false);
        }

        private static void HideLegacy(Component c)
        {
            if (c != null) c.gameObject.SetActive(false);
        }

        private TMP_Text MakeText(Transform parent, string name, TMP_FontAsset font, float size,
            FontStyles style, TextAlignmentOptions align, float width, bool wrap)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<TextMeshProUGUI>();
            if (font != null) t.font = font;
            t.fontSize = size;
            t.fontStyle = style;
            t.alignment = align;
            t.richText = true;
            t.enableWordWrapping = wrap;
            t.color = CombatUiKit.TextPrimary;
            t.raycastTarget = false;
            go.GetComponent<LayoutElement>().preferredWidth = width;
            return t;
        }

        private Button MakeButton(Transform parent, string name, string label, bool primary,
            TMP_FontAsset font, UnityEngine.Events.UnityAction onClick, out TMP_Text labelTmp)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            // Primaire (Retour hub) = bouton NOIR + texte blanc ; secondaire = carte gris foncé.
            img.color = primary ? new Color(0.05f, 0.05f, 0.06f, 1f) : CombatUiKit.CardBg;
            CombatUiKit.ApplyRounded(img, 10f);
            var le = go.GetComponent<LayoutElement>();
            le.preferredWidth = 340f; le.preferredHeight = 52f;

            var btn = go.GetComponent<Button>();
            btn.targetGraphic = img;
            // ColorBlock multiplie la couleur de base de l'Image -> léger éclaircissement au survol.
            var cb = btn.colors;
            cb.normalColor = Color.white;
            cb.highlightedColor = new Color(1.12f, 1.12f, 1.12f, 1f);
            cb.pressedColor = new Color(0.88f, 0.88f, 0.90f, 1f);
            cb.selectedColor = Color.white;
            cb.disabledColor = new Color(0.6f, 0.6f, 0.6f, 0.6f);
            cb.fadeDuration = 0.08f;
            btn.colors = cb;
            btn.onClick.AddListener(onClick);

            labelTmp = MakeText(go.transform, "Label", font, 22f, FontStyles.Bold,
                TextAlignmentOptions.Center, 300f, false);
            labelTmp.text = label;
            labelTmp.color = primary ? Color.white : CombatUiKit.TextPrimary;
            var lrt = labelTmp.rectTransform;
            lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one;
            lrt.offsetMin = Vector2.zero; lrt.offsetMax = Vector2.zero;
            return btn;
        }

        // ============================ Affichage ============================

        /// <summary>
        /// Appele par CombatHUDController chaque tick. Affiche l'overlay en phase MatchEnd, sinon cache.
        /// </summary>
        public void Refresh(Quantum.CombatPhase phase, int winnerPlayerIndex, int localPlayerIndex, int turnNumber, bool isPvpMatch = false)
        {
            if (phase != Quantum.CombatPhase.MatchEnd)
            {
                if (_shown || _pendingShow) Hide();
                return;
            }
            if (_shown || _pendingShow) return; // déjà affiché OU reveal différé en cours, idempotent

            _localPlayerIndex = localPlayerIndex;
            _winnerPlayerIndex = winnerPlayerIndex;
            _isPvpMatch = isPvpMatch;
            _turnNumber = turnNumber;

            // Item mineur F (9 juin) — si le kill final vient d'une signature, son floating text
            // "SMASH!!" est encore en cours d'anim : on RETARDE l'overlay jusqu'à sa fin (sinon
            // l'écran victoire/défaite le masque avant qu'il se termine).
            if (FloatingTextManager.IsSignatureTextActive)
            {
                _pendingShow = true;
                StartCoroutine(RevealAfterSignature());
                return;
            }

            RevealNow();
        }

        // Attend la fin de l'anim du floating text signature (cap de sécurité), puis révèle l'overlay.
        private IEnumerator RevealAfterSignature()
        {
            float safety = 0f;
            while (FloatingTextManager.IsSignatureTextActive && _pendingShow && safety < 4f)
            {
                safety += Time.unscaledDeltaTime;
                yield return null;
            }
            if (_pendingShow) // pas annulé entre-temps (ex : Hide si la phase a changé)
            {
                _pendingShow = false;
                RevealNow();
            }
        }

        // Construit le contenu (titre/infos/SFX) et affiche l'overlay. Utilise les params snapshottés.
        private void RevealNow()
        {
            int winnerPlayerIndex = _winnerPlayerIndex;
            int localPlayerIndex = _localPlayerIndex;
            int turnNumber = _turnNumber;

            // S5 spectateur : pas de "local", donc pas de Victoire/Défaite. On nomme le gagnant.
            if (Nymora.Combat.Spectate.LiveSpectateController.LiveSpectateActive)
            {
                string p0 = string.IsNullOrEmpty(PlayerProfileBridge.LocalPseudo) ? "Joueur 1" : PlayerProfileBridge.LocalPseudo;
                string p1 = string.IsNullOrEmpty(PlayerProfileBridge.OpponentPseudo) ? "Joueur 2" : PlayerProfileBridge.OpponentPseudo;
                if (_builtTitle != null)
                {
                    if (winnerPlayerIndex < 0) { _builtTitle.text = "MATCH NUL"; _builtTitle.color = _drawColor; }
                    else
                    {
                        string winner = winnerPlayerIndex == 0 ? p0 : p1;
                        _builtTitle.text = winner.ToUpperInvariant() + " L'EMPORTE";
                        _builtTitle.color = _victoryColor;
                    }
                }
                if (_builtInfo != null)
                    _builtInfo.text = $"<color=#9A9BA2>{p0} vs {p1} · Round {turnNumber}</color>";
                Show();
                return;
            }

            string title;
            Color titleColor;
            if (winnerPlayerIndex < 0) { title = "MATCH NUL"; titleColor = _drawColor; }
            else if (winnerPlayerIndex == localPlayerIndex) { title = "VICTOIRE"; titleColor = _victoryColor; }
            else { title = "DÉFAITE"; titleColor = _defeatColor; }

            if (_builtTitle != null) { _builtTitle.text = title; _builtTitle.color = titleColor; }

            MatchResult result = winnerPlayerIndex < 0 ? MatchResult.Draw
                : winnerPlayerIndex == localPlayerIndex ? MatchResult.Victory : MatchResult.Defeat;
            if (_builtInfo != null) _builtInfo.text = BuildInfoText(result, turnNumber);

            // SFX + musique de fin (une fois, garanti par _shown).
            var audio = Nymora.Core.Audio.NymoraAudioManager.Instance;
            if (audio != null)
            {
                if (winnerPlayerIndex < 0) { /* nul : pas de stinger dédié */ }
                else if (winnerPlayerIndex == localPlayerIndex)
                {
                    audio.PlaySfx(Nymora.Core.Audio.SoundId.Victory);
                    audio.PlayMusic(Nymora.Core.Audio.SoundId.MusicVictory);
                }
                else
                {
                    audio.PlaySfx(Nymora.Core.Audio.SoundId.Defeat);
                    audio.PlayMusic(Nymora.Core.Audio.SoundId.MusicDefeat);
                }
            }

            Show();
        }

        private void Show()
        {
            if (_root == null) return;
            _root.SetActive(true);
            _root.transform.SetAsLastSibling(); // au-dessus du reste du HUD
            _shown = true;

            // En mode replay (.nymrep) : pas de Retour hub (sortie via ReplayControlsPanel) ni de Save
            // (recorder force-disabled). Sinon : Retour hub toujours, Save si un recorder est présent.
            // Spectateur : pas de "Retour hub" ici (sortie via le bouton Quitter du LiveSpectateController),
            // ni de Save (pas de recorder) — comme le mode replay.
            bool replayMode = IsInReplayMode() || Nymora.Combat.Spectate.LiveSpectateController.LiveSpectateActive;
            if (_builtReturnBtn != null) _builtReturnBtn.gameObject.SetActive(!replayMode);
            if (_builtSaveBtn != null) _builtSaveBtn.gameObject.SetActive(!replayMode && _replayRecorder != null);
            RefreshSaveReplayButton();
        }

        private void Hide()
        {
            if (_root != null) _root.SetActive(false);
            _shown = false;
            _pendingShow = false; // annule un reveal différé en cours (RevealAfterSignature)
            _replaySavedThisMatch = false;
        }

        private static bool IsInReplayMode()
        {
            var c = Object.FindAnyObjectByType<ReplayPlaybackController>();
            return c != null && c.enabled;
        }

        // ============================ Contenu infos ============================

        // MMR + récompenses uniquement en ranked. Le delta MMR est le PREVIEW exact (réplique de la
        // formule ELO serveur, cf RankedEloPreview) ; le hub affiche la valeur autoritative au settle.
        private static string BuildInfoText(MatchResult result, int turnNumber)
        {
            const string muted = "#9A9BA2";
            var sb = new StringBuilder();

            string opp = !string.IsNullOrEmpty(MatchBridge.OpponentDisplayName)
                ? MatchBridge.OpponentDisplayName : "Adversaire";
            sb.Append($"<color={muted}>vs</color> {opp}   <size=80%><color={muted}>· Round {turnNumber}</color></size>");

            if (MatchBridge.IsRanked)
            {
                int myMmr = MatchBridge.RankedLocalMmr;
                int oppMmr = MatchBridge.RankedOpponentMmr;
                float score = RankedEloPreview.ScoreFor(result);
                int delta = RankedEloPreview.ComputeDelta(myMmr, oppMmr, MatchBridge.RankedLocalGames, score);
                int newMmr = RankedEloPreview.NewMmr(myMmr, delta);
                string deltaStr = delta > 0 ? $"<color=#5FD06A>+{delta}</color>"
                                : delta < 0 ? $"<color=#E06565>{delta}</color>"
                                : $"<color={muted}>±0</color>";
                sb.Append($"\n\n<size=150%>MMR {newMmr}  {deltaStr}</size>");
                sb.Append($"\n<size=90%>{RankLadder.ColoredName(newMmr)}</size>");
                var rw = RankedRewards.For(result);
                sb.Append($"\n<size=90%><color={muted}>+{rw.Nymos} Nymos · +{rw.ClassXp} XP</color></size>");
            }

            if (MatchBridge.HasCombatStats)
            {
                sb.Append($"\n\n<size=85%><color={muted}>" +
                          $"{MatchBridge.StatDamageDealt} dégâts infligés · {MatchBridge.StatDamageTaken} subis\n" +
                          $"{MatchBridge.StatSpellsCast} sorts lancés · {MatchBridge.StatTurns} tours</color></size>");
            }

            return sb.ToString();
        }

        // ============================ Boutons / actions ============================

        private void RefreshSaveReplayButton()
        {
            if (_builtSaveBtn == null) return;
            _builtSaveBtn.interactable = !_replaySavedThisMatch;
            if (_builtSaveLabel != null)
                _builtSaveLabel.text = _replaySavedThisMatch ? _saveReplaySavedText : _saveReplayDefaultText;
        }

        private void OnSaveReplayClicked()
        {
            if (_replayRecorder == null)
            {
                Debug.LogWarning("[Nymora.HUD] ReplayRecorder ref non assignee dans le MatchEndOverlay Inspector.");
                return;
            }
            if (!_replayRecorder.HasPendingReplay)
            {
                Debug.LogWarning("[Nymora.HUD] Aucun replay en attente — la capture Quantum a peut-etre echoue. " +
                                 "Verifie les logs [Nymora.Replay] precedents.");
                return;
            }
            string path = _replayRecorder.SaveCurrentReplay();
            if (!string.IsNullOrEmpty(path))
            {
                _replaySavedThisMatch = true;
                RefreshSaveReplayButton();
            }
        }

        /// <summary>
        /// 4.14.g — Retour Hub. En PvP : set le resultat dans MatchBridge (consume cote hub par
        /// HubMatchResultDisplay = ligne chat + report ranked). En IA : pas de SetMatchResult.
        /// Toujours : shutdown Quantum + LoadScene hub.
        /// </summary>
        private void OnReturnToHubClicked()
        {
            if (_isPvpMatch)
            {
                MatchResult result;
                if (_winnerPlayerIndex < 0) result = MatchResult.Draw;
                else if (_winnerPlayerIndex == _localPlayerIndex) result = MatchResult.Victory;
                else result = MatchResult.Defeat;

                string matchId = MatchBridge.PendingMatchId;
                string opponentEmail = MatchBridge.OpponentEmail;
                string opponentDisplayName = MatchBridge.OpponentDisplayName;
                MatchBridge.SetMatchResult(result, matchId, opponentEmail, opponentDisplayName);
                Debug.Log($"[Nymora.HUD] Retour Hub clique (PvP) — result={result} matchId={matchId} opponent='{opponentDisplayName}'");
            }
            else
            {
                Debug.Log("[Nymora.HUD] Retour Hub clique (IA) — pas de SetMatchResult, pas d'XP MVP");
            }

            SceneTransition.Load("10_CommunityHub", () => QuantumRunner.ShutdownAll(), waitForReady: true);
        }
    }
}
