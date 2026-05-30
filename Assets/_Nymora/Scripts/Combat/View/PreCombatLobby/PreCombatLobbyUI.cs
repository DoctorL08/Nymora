using System.Collections.Generic;
using Nymora.Combat.View.HUD;
using Nymora.Core.Data;
using Nymora.Core.SceneFlow;
using Nymora.Core.ScriptableObjects;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Button = UnityEngine.UI.Button;
using Image = UnityEngine.UI.Image;
using NymoraClassEnum = Nymora.Core.Enums.NymoraClass;

namespace Nymora.Combat.View.PreCombatLobby
{
    /// <summary>
    /// Lobby pré-combat (B3) — UI CONSTRUITE AU RUNTIME (Canvas dédié) au-dessus de la scène combat,
    /// pilotée par <see cref="PreCombatLobbyController"/>. Affiche les deux joueurs (pseudo + classe +
    /// MMR + état Ready), un picker de deck pour le joueur local, un timer 30 s et un bouton Prêt.
    ///
    /// 100% View. Style aligné sur le menu hub via <see cref="CombatUiKit"/> (réplique du pattern
    /// ReplayPlaybackControls). Aucun Editor tool / setup scène : tout est créé en code, comme les
    /// autres overlays combat runtime (replay, match end).
    /// </summary>
    public sealed class PreCombatLobbyUI : MonoBehaviour
    {
        private static readonly Color ReadyGreen = new Color(0.42f, 0.80f, 0.47f, 1f);

        private PreCombatLobbyController _controller;
        private Canvas _uiCanvas;

        private TMP_Text _timerLabel;

        private TMP_Text _oppName, _oppClass, _oppMmr, _oppReady;
        private TMP_Text _localName, _localClass, _localMmr;

        private Image _oppPortrait;
        private UiFrameAnim _oppPortraitAnim;
        private string _oppPortraitKey; // (class|skin) appliqué, pour ne re-résoudre qu'au changement

        private CosmeticSkinCatalog _skinCatalog;
        private bool _skinCatalogLoaded;

        private Button _readyButton;
        private Image _readyButtonImg;
        private TMP_Text _readyLabel;
        private GameObject _validatedBadge;
        private GameObject _readyCheckIcon;

        private Sprite _checkSprite;
        private bool _checkLoaded;

        private struct DeckEntry { public string Id; public Image Bg; public TMP_Text Label; }
        private readonly List<DeckEntry> _deckEntries = new List<DeckEntry>();

        public void Bind(PreCombatLobbyController controller)
        {
            _controller = controller;
            BuildUI();
            _controller.OnStateChanged += Refresh;
            Refresh();
            // Le voile de chargement attend par défaut le démarrage de la sim (CombatReadyBeacon).
            // Comme le lobby s'intercale avant, on lève le voile dès qu'il s'affiche.
            SceneTransition.SignalReady();
        }

        private void OnDestroy()
        {
            if (_controller != null) _controller.OnStateChanged -= Refresh;
            if (_uiCanvas != null) Destroy(_uiCanvas.gameObject);
        }

        // ============================ Refresh ============================

        private void Refresh()
        {
            if (_controller == null) return;

            if (_timerLabel != null)
                _timerLabel.text = Mathf.CeilToInt(_controller.SecondsRemaining) + " s";

            // Carte adverse.
            bool oppKnown = _controller.OpponentPresent && !string.IsNullOrEmpty(_controller.OpponentPseudo);
            if (_oppName != null) _oppName.text = oppKnown ? _controller.OpponentPseudo : "En attente de l'adversaire…";
            if (_oppClass != null) _oppClass.text = oppKnown ? ColoredClass(_controller.OpponentClassValue) : "";
            if (_oppMmr != null) _oppMmr.text = oppKnown ? MmrLine(_controller.OpponentMmr) : "";
            if (_oppReady != null)
            {
                _oppReady.text = !oppKnown ? "" : (_controller.OpponentReady ? "<color=#6ccb78>PRÊT</color>" : "Choisit son deck…");
            }

            // Portrait adverse : (re)résolu quand sa classe ou son skin nous parviennent / changent.
            string oppKey = _controller.OpponentClassValue + "|" + _controller.OpponentSkinId;
            if (oppKnown && oppKey != _oppPortraitKey)
            {
                _oppPortraitKey = oppKey;
                ApplyPortrait(_oppPortrait, _oppPortraitAnim, _controller.OpponentClassValue, _controller.OpponentSkinId);
            }

            // Carte locale.
            if (_localName != null) _localName.text = _controller.LocalPseudo;
            if (_localClass != null) _localClass.text = ColoredClass(_controller.LocalClassValue);
            if (_localMmr != null) _localMmr.text = MmrLine(_controller.LocalMmr);

            // Picker de deck : surligne la sélection, fige après Ready.
            string selectedId = _controller.SelectedDeck != null ? _controller.SelectedDeck.Id : null;
            foreach (var e in _deckEntries)
            {
                bool sel = e.Id == selectedId;
                // Sélection = bouton blanc plein, texte noir (lisibilité max + contraste).
                if (e.Bg != null) e.Bg.color = sel ? Color.white : CombatUiKit.GhostBg;
                if (e.Label != null) e.Label.color = sel ? CombatUiKit.TextOnLight : CombatUiKit.TextSecondary;
            }

            // Bouton Prêt.
            bool ready = _controller.LocalReady;
            if (_readyButton != null) _readyButton.interactable = !ready;
            if (_readyLabel != null) _readyLabel.text = ready ? "Prêt" : "Prêt !";
            if (_readyButtonImg != null) _readyButtonImg.color = ready ? ReadyGreen : CombatUiKit.GhostBg;
            if (_readyCheckIcon != null) _readyCheckIcon.SetActive(ready);
            if (_validatedBadge != null) _validatedBadge.SetActive(ready);
        }

        private static string MmrLine(int mmr) => $"{RankLadder.ColoredName(mmr)}  ·  MMR {mmr}";

        private static string ColoredClass(int classValue)
        {
            if (classValue < 1 || classValue > 5) return "—";
            var cls = (NymoraClassEnum)classValue;
            return $"<color={ClassHex(cls)}>{cls}</color>";
        }

        private static string ClassHex(NymoraClassEnum cls)
        {
            switch (cls)
            {
                case NymoraClassEnum.Soulrender: return "#d9534f";
                case NymoraClassEnum.Nightseer:  return "#7da7ff";
                case NymoraClassEnum.Colossar:   return "#c9a86a";
                case NymoraClassEnum.Necram:     return "#7bd88f";
                case NymoraClassEnum.Ghostra:    return "#b06bff";
                default: return "#ffffff";
            }
        }

        // ============================ Construction UI ============================

        private void BuildUI()
        {
            var canvasGo = new GameObject("PreCombatLobbyCanvas");
            _uiCanvas = canvasGo.AddComponent<Canvas>();
            _uiCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _uiCanvas.sortingOrder = 4500; // au-dessus du HUD, sous le replay (5000)
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();
            var uiRoot = (RectTransform)canvasGo.transform;

            // Voile plein écran (bloque les clics vers le combat derrière).
            var veil = NewRect("Veil", uiRoot);
            Stretch(veil);
            var veilImg = veil.gameObject.AddComponent<Image>();
            veilImg.color = new Color(0.04f, 0.045f, 0.06f, 0.9f);

            // Titre.
            var title = MakeText(uiRoot, "Title", "PRÉPARATION DU COMBAT", 40f, CombatUiKit.TextPrimary, TextAlignmentOptions.Center);
            var trt = title.rectTransform;
            trt.anchorMin = new Vector2(0.5f, 1f); trt.anchorMax = new Vector2(0.5f, 1f); trt.pivot = new Vector2(0.5f, 1f);
            trt.anchoredPosition = new Vector2(0f, -70f); trt.sizeDelta = new Vector2(1200f, 60f);

            // Timer.
            _timerLabel = MakeText(uiRoot, "Timer", "", 56f, CombatUiKit.Accent, TextAlignmentOptions.Center);
            var tr = _timerLabel.rectTransform;
            tr.anchorMin = new Vector2(0.5f, 1f); tr.anchorMax = new Vector2(0.5f, 1f); tr.pivot = new Vector2(0.5f, 1f);
            tr.anchoredPosition = new Vector2(0f, -140f); tr.sizeDelta = new Vector2(400f, 80f);

            // Cartes (adverse à gauche, locale à droite).
            BuildOpponentCard(uiRoot, new Vector2(-360f, -40f));
            BuildLocalCard(uiRoot, new Vector2(360f, -40f));

            // Astuce bas d'écran — chip de fond sombre pour bien ressortir.
            var chip = NewRect("HintChip", uiRoot);
            chip.anchorMin = new Vector2(0.5f, 0f); chip.anchorMax = new Vector2(0.5f, 0f); chip.pivot = new Vector2(0.5f, 0f);
            chip.anchoredPosition = new Vector2(0f, 44f); chip.sizeDelta = new Vector2(1180f, 66f);
            var chipImg = chip.gameObject.AddComponent<Image>();
            chipImg.color = new Color(0f, 0f, 0f, 0.6f);
            CombatUiKit.ApplyRounded(chipImg, CombatUiKit.CornerRadius);

            var hint = MakeText(chip, "Hint",
                "Choisis ton deck, puis clique « Prêt ! ». Le combat démarre quand les deux joueurs sont prêts (ou à la fin du chrono).",
                23f, CombatUiKit.TextPrimary, TextAlignmentOptions.Center);
            var hr = hint.rectTransform;
            Stretch(hr);
            hr.offsetMin = new Vector2(24f, 6f); hr.offsetMax = new Vector2(-24f, -6f);
            hint.enableWordWrapping = true;
            hint.fontStyle = FontStyles.Bold;
        }

        private void BuildOpponentCard(RectTransform parent, Vector2 center)
        {
            var card = MakeCard(parent, "OpponentCard", center, new Vector2(460f, 540f));
            HeaderText(card, "Lbl", 18f, CombatUiKit.TextMuted, -22f).text = "ADVERSAIRE";

            // Portrait idle animé (skin équipé sinon idle de classe). SE = face vers le bas-droit
            // → tourné vers le centre depuis la carte de gauche (pas de miroir).
            MakePortrait(card, -44f, 176f, mirror: false, out _oppPortrait, out _oppPortraitAnim);

            _oppName = HeaderText(card, "OppName", 30f, CombatUiKit.TextPrimary, -236f);
            _oppClass = HeaderText(card, "OppClass", 22f, CombatUiKit.TextPrimary, -278f);
            _oppMmr = HeaderText(card, "OppMmr", 20f, CombatUiKit.TextSecondary, -314f);

            _oppReady = MakeText(card, "OppReady", "", 24f, CombatUiKit.TextSecondary, TextAlignmentOptions.Center);
            var rr = _oppReady.rectTransform;
            rr.anchorMin = new Vector2(0.5f, 0f); rr.anchorMax = new Vector2(0.5f, 0f); rr.pivot = new Vector2(0.5f, 0f);
            rr.anchoredPosition = new Vector2(0f, 28f); rr.sizeDelta = new Vector2(400f, 36f);
        }

        private void BuildLocalCard(RectTransform parent, Vector2 center)
        {
            var card = MakeCard(parent, "LocalCard", center, new Vector2(460f, 540f));
            HeaderText(card, "Lbl", 18f, CombatUiKit.TextMuted, -20f).text = "VOUS";

            // Portrait idle animé du joueur local (miroir : tourné vers le centre depuis la droite).
            MakePortrait(card, -38f, 132f, mirror: true, out var localPortrait, out var localPortraitAnim);
            ApplyPortrait(localPortrait, localPortraitAnim, _controller.LocalClassValue, _controller.LocalSkinId);

            _localName = HeaderText(card, "LocalName", 28f, CombatUiKit.TextPrimary, -184f);
            _localClass = HeaderText(card, "LocalClass", 20f, CombatUiKit.TextPrimary, -218f);
            _localMmr = HeaderText(card, "LocalMmr", 18f, CombatUiKit.TextSecondary, -248f);

            // Picker de deck (liste verticale).
            HeaderText(card, "DeckLbl", 16f, CombatUiKit.TextMuted, -278f).text = "Deck";

            var decks = _controller.Decks;
            float y = -300f;
            const float gap = 6f;
            // Hauteur de ligne adaptée au nombre de decks : ~162 px dispo entre le label et le
            // bouton Prêt. On compresse (jusqu'à 22 px) plutôt que de déborder/scroller.
            int deckCount = decks != null ? decks.Count : 0;
            float rowH = deckCount > 0
                ? Mathf.Clamp((162f - (deckCount - 1) * gap) / deckCount, 22f, 36f)
                : 36f;
            if (decks != null)
            {
                for (int i = 0; i < decks.Count; i++)
                {
                    var d = decks[i];
                    if (d == null) continue;
                    string id = d.Id;
                    var row = NewRect("Deck_" + i, card);
                    row.anchorMin = new Vector2(0.5f, 1f); row.anchorMax = new Vector2(0.5f, 1f); row.pivot = new Vector2(0.5f, 1f);
                    row.anchoredPosition = new Vector2(0f, y); row.sizeDelta = new Vector2(400f, rowH);
                    var img = row.gameObject.AddComponent<Image>();
                    img.color = CombatUiKit.GhostBg;
                    CombatUiKit.ApplyRounded(img, CombatUiKit.CornerRadius - 4f);
                    var btn = row.gameObject.AddComponent<Button>();
                    btn.targetGraphic = img;
                    btn.navigation = new Navigation { mode = Navigation.Mode.None };
                    btn.onClick.AddListener(() => { _controller.SelectDeck(id); });
                    var lbl = MakeText(row, "Label", d.Name, 20f, CombatUiKit.TextSecondary, TextAlignmentOptions.Center);
                    Stretch(lbl.rectTransform);
                    _deckEntries.Add(new DeckEntry { Id = id, Bg = img, Label = lbl });
                    y -= rowH + gap;
                }
            }

            // Bouton Prêt (bas de carte).
            var ready = NewRect("ReadyBtn", card);
            ready.anchorMin = new Vector2(0.5f, 0f); ready.anchorMax = new Vector2(0.5f, 0f); ready.pivot = new Vector2(0.5f, 0f);
            ready.anchoredPosition = new Vector2(0f, 24f); ready.sizeDelta = new Vector2(280f, 54f);
            _readyButtonImg = ready.gameObject.AddComponent<Image>();
            _readyButtonImg.color = CombatUiKit.GhostBg;
            CombatUiKit.ApplyRounded(_readyButtonImg, CombatUiKit.CornerRadius - 2f);
            _readyButton = ready.gameObject.AddComponent<Button>();
            _readyButton.targetGraphic = _readyButtonImg;
            _readyButton.navigation = new Navigation { mode = Navigation.Mode.None };
            _readyButton.onClick.AddListener(() =>
            {
                Nymora.Core.Audio.NymoraAudioManager.Instance?.PlaySfx(Nymora.Core.Audio.SoundId.UiClick);
                _controller.SetReady();
            });
            _readyLabel = MakeText(ready, "Label", "Prêt !", 24f, CombatUiKit.TextPrimary, TextAlignmentOptions.Center);
            Stretch(_readyLabel.rectTransform);

            // Icône « validé » (sprite menu ui_icon_check) sur le bouton, à droite, masquée tant
            // que le joueur n'a pas cliqué Prêt.
            var rcIcon = MakeCheckIcon(ready, new Vector2(28f, 28f));
            var rcRt = rcIcon.rectTransform;
            rcRt.anchorMin = rcRt.anchorMax = new Vector2(1f, 0.5f); rcRt.pivot = new Vector2(1f, 0.5f);
            rcRt.anchoredPosition = new Vector2(-16f, 0f);
            _readyCheckIcon = rcIcon.gameObject;
            _readyCheckIcon.SetActive(false);

            // Badge « validé » (pastille verte avec l'icône check au coin haut-droit de la carte),
            // masqué tant que le joueur n'a pas cliqué Prêt.
            var badge = NewRect("ValidatedBadge", card);
            badge.anchorMin = badge.anchorMax = new Vector2(1f, 1f); badge.pivot = new Vector2(1f, 1f);
            badge.anchoredPosition = new Vector2(-12f, -12f); badge.sizeDelta = new Vector2(50f, 50f);
            var bimg = badge.gameObject.AddComponent<Image>();
            bimg.color = ReadyGreen;
            CombatUiKit.ApplyRounded(bimg, 25f); // rayon = moitié → pastille ronde
            bimg.raycastTarget = false;
            var badgeCheck = MakeCheckIcon(badge, new Vector2(30f, 30f));
            var bcRt = badgeCheck.rectTransform;
            bcRt.anchorMin = bcRt.anchorMax = new Vector2(0.5f, 0.5f); bcRt.pivot = new Vector2(0.5f, 0.5f);
            bcRt.anchoredPosition = Vector2.zero;
            _validatedBadge = badge.gameObject;
            _validatedBadge.SetActive(false);
        }

        // Crée une Image portant l'icône « validé » du menu (Resources/UI/Icons/ui_icon_check),
        // teintée blanche pour ressortir sur le vert. Si l'asset manque, l'Image est désactivée.
        private Image MakeCheckIcon(RectTransform parent, Vector2 size)
        {
            var rt = NewRect("CheckIcon", parent);
            rt.sizeDelta = size;
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = CheckSprite;
            img.color = Color.white;
            img.preserveAspect = true;
            img.raycastTarget = false;
            img.enabled = CheckSprite != null;
            return img;
        }

        private Sprite CheckSprite
        {
            get
            {
                if (!_checkLoaded)
                {
                    _checkSprite = Resources.Load<Sprite>("UI/Icons/ui_icon_check");
                    _checkLoaded = true;
                }
                return _checkSprite;
            }
        }

        // ============================ Helpers UGUI ============================

        // Texte d'en-tête centré, ancré en haut de la carte à l'offset vertical donné (px, négatif).
        private static TMP_Text HeaderText(RectTransform card, string name, float size, Color color, float topY)
        {
            var t = MakeText(card, name, "", size, color, TextAlignmentOptions.Center);
            var rt = t.rectTransform;
            rt.anchorMin = new Vector2(0.5f, 1f); rt.anchorMax = new Vector2(0.5f, 1f); rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, topY); rt.sizeDelta = new Vector2(420f, size + 12f);
            return t;
        }

        private static RectTransform MakeCard(RectTransform parent, string name, Vector2 center, Vector2 size)
        {
            var card = NewRect(name, parent);
            card.anchorMin = card.anchorMax = new Vector2(0.5f, 0.5f); card.pivot = new Vector2(0.5f, 0.5f);
            card.anchoredPosition = center; card.sizeDelta = size;
            var bg = card.gameObject.AddComponent<Image>();
            bg.color = CombatUiKit.CardBg;
            CombatUiKit.ApplyRounded(bg, CombatUiKit.CornerRadius);
            return card;
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        }

        private static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        private static TMP_Text MakeText(Transform parent, string name, string text, float size, Color color, TextAlignmentOptions align)
        {
            var rt = NewRect(name, parent);
            var tmp = rt.gameObject.AddComponent<TextMeshProUGUI>();
            tmp.text = text; tmp.fontSize = size; tmp.color = color; tmp.alignment = align;
            tmp.raycastTarget = false; tmp.richText = true; tmp.enableWordWrapping = false;
            return tmp;
        }

        // ============================ Portrait idle animé ============================

        private static void MakePortrait(RectTransform card, float topY, float size, bool mirror,
                                         out Image img, out UiFrameAnim anim)
        {
            var rt = NewRect("Portrait", card);
            rt.anchorMin = new Vector2(0.5f, 1f); rt.anchorMax = new Vector2(0.5f, 1f); rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, topY); rt.sizeDelta = new Vector2(size, size);
            if (mirror) rt.localScale = new Vector3(-1f, 1f, 1f);
            img = rt.gameObject.AddComponent<Image>();
            img.preserveAspect = true; img.raycastTarget = false; img.enabled = false;
            anim = rt.gameObject.AddComponent<UiFrameAnim>();
        }

        // Résout les frames idle (skin équipé en priorité, sinon idle de la classe) et les joue.
        private void ApplyPortrait(Image img, UiFrameAnim anim, int classValue, string skinId)
        {
            if (img == null) return;
            Sprite[] frames = null;
            float fps = 8f;

            if (!string.IsNullOrEmpty(skinId))
            {
                var def = SkinCatalog != null ? SkinCatalog.Resolve(skinId) : null;
                if (def != null && def.IdleFrames != null && def.IdleFrames.Length > 0)
                {
                    frames = def.IdleFrames; fps = def.IdleFps;
                }
            }
            if (frames == null && classValue >= 1 && classValue <= 5 && CombatHUDController.Instance != null)
            {
                var cd = CombatHUDController.Instance.ResolveClassDefinition((NymoraClassEnum)classValue);
                if (cd != null && cd.IdleFrames != null && cd.IdleFrames.Length > 0)
                {
                    frames = cd.IdleFrames; fps = cd.IdleFps;
                }
            }

            if (frames != null)
            {
                img.enabled = true;
                img.sprite = frames[0];
                if (anim != null) anim.Play(img, frames, fps);
            }
            else
            {
                img.enabled = false;
                if (anim != null) anim.Stop();
            }
        }

        private CosmeticSkinCatalog SkinCatalog
        {
            get
            {
                if (!_skinCatalogLoaded)
                {
                    _skinCatalog = Resources.Load<CosmeticSkinCatalog>("Cosmetics/CosmeticSkinCatalog");
                    _skinCatalogLoaded = true;
                }
                return _skinCatalog;
            }
        }
    }

    /// <summary>
    /// Mini-animateur de frames sprite pour une Image UGUI (idle du portrait lobby). Évite de
    /// référencer Nymora.Hub.UISpriteAnimator (asmdef Hub, non accessible depuis Combat).
    /// Time.unscaledDeltaTime est légitime ici (Scripts/Combat/View/, exclu du check temporel).
    /// </summary>
    internal sealed class UiFrameAnim : MonoBehaviour
    {
        private Image _img;
        private Sprite[] _frames;
        private float _fps;
        private float _t;
        private int _i;

        public void Play(Image img, Sprite[] frames, float fps)
        {
            _img = img; _frames = frames; _fps = fps > 0f ? fps : 8f; _t = 0f; _i = 0;
            if (_frames != null && _frames.Length > 0 && _img != null) _img.sprite = _frames[0];
            enabled = true;
        }

        public void Stop() => enabled = false;

        private void Update()
        {
            if (_frames == null || _frames.Length < 2 || _img == null) return;
            _t += Time.unscaledDeltaTime * _fps;
            int idx = ((int)_t) % _frames.Length;
            if (idx != _i) { _i = idx; _img.sprite = _frames[idx]; }
        }
    }
}
