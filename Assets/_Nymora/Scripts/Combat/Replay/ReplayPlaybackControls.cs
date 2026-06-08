using System.Collections.Generic;
using Nymora.Combat.View.HUD;
using Nymora.Core.SceneFlow;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Button = UnityEngine.UI.Button;
using Image = UnityEngine.UI.Image;

namespace Nymora.Combat.Replay
{
    /// <summary>
    /// Overlay de pilotage du replay — UI CONSTRUITE AU RUNTIME sur son PROPRE Canvas dédié
    /// (indépendant du HUD de combat, pour pouvoir masquer le reste en mode cinématique).
    ///
    /// Barre de transport (HAUT-CENTRE) :  «  <<  ·  Play/Pause  ·  >>  ·  Cacher  »
    ///   - <<  : vitesse précédente (cycle 1× / 1.5× / 2× / 4× en sens inverse)
    ///   - Play/Pause : lecture / pause (« Fin » en bout de replay)
    ///   - >>  : vitesse suivante (1× -> 1.5× -> 2× -> 4× -> 1×)
    ///   - Cacher : masque TOUTE l'interface de la scène (HUD + cette barre) pour filmer en
    ///     cinématique. Touche H pour ré-afficher.
    ///   + label « Tick X / Y  ×N ».
    ///
    /// Menu Échap : pause + voile avec « Quitter le replay » (-> hub) et une croix (X) pour
    /// fermer et reprendre. Raccourcis : Espace = play/pause · ← = << · → = >> · H = cacher/montrer
    /// l'interface · F11 = plein écran · Échap = menu pause.
    /// </summary>
    public class ReplayPlaybackControls : MonoBehaviour
    {
        private const string HubSceneName = "10_CommunityHub";

        private ReplayPlaybackController _controller;

        private Canvas _uiCanvas;
        private GameObject _transportBar;
        private TMP_Text _playPauseLabel;
        private TMP_Text _tickLabel;
        private TMP_Text _errorLabel;

        private GameObject _escapeMenu;
        private bool _wasPlayingBeforeMenu;

        // Item mineur C.1 (rewind) — barre de progression scrubbable. _userScrubbing = vrai pendant
        // que le joueur tient le curseur (on ne seek qu'au relâchement, pas à chaque pixel).
        private Slider _seekSlider;
        private bool _userScrubbing;

        private bool _cinematicHidden;
        private readonly List<Canvas> _canvasesHidden = new List<Canvas>();

        private void Awake()
        {
            _controller = FindAnyObjectByType<ReplayPlaybackController>();

            // Pas de replay actif -> on s'efface (le controller a déjà self-disable, -1000).
            if (_controller == null || !_controller.enabled)
            {
                gameObject.SetActive(false);
                return;
            }

            BuildUI();
        }

        private void Update()
        {
            if (_controller == null) return;

            // Raccourcis clavier.
            if (Input.GetKeyDown(KeyCode.F11)) Screen.fullScreen = !Screen.fullScreen;
            if (Input.GetKeyDown(KeyCode.Escape)) ToggleEscapeMenu();
            if (Input.GetKeyDown(KeyCode.H)) ToggleCinematic();
            if (Input.GetKeyDown(KeyCode.Space)) OnPlayPause();
            if (Input.GetKeyDown(KeyCode.LeftArrow)) OnRewind();
            if (Input.GetKeyDown(KeyCode.RightArrow)) OnForward();

            bool hasErr = !string.IsNullOrEmpty(_controller.ErrorMessage);
            bool hasDesync = _controller.HasDesync;
            if (_errorLabel != null)
            {
                _errorLabel.gameObject.SetActive(hasErr || hasDesync);
                if (hasErr) _errorLabel.text = _controller.ErrorMessage;
                else if (hasDesync) _errorLabel.text =
                    "DESYNC au frame " + _controller.DesyncFrameNumber + " — replay divergent.";
            }

            if (_tickLabel != null)
            {
                _tickLabel.text = _controller.IsSeeking
                    ? "Chargement…"
                    : "Tick " + _controller.CurrentTick + " / " + _controller.LastTick
                      + "   ×" + _controller.Speed.ToString("0.##");
            }

            if (_playPauseLabel != null)
            {
                if (_controller.IsReplayFinished) _playPauseLabel.text = "Fin";
                else _playPauseLabel.text = _controller.IsPaused ? "Play" : "Pause";
            }

            // Item mineur C.1 — synchronise la position du scrubber sur le tick courant, SAUF quand le
            // joueur le manipule (sinon il « saute » sous le doigt) ou pendant un seek en cours.
            if (_seekSlider != null && !_userScrubbing && !_controller.IsSeeking && _controller.LastTick > 0)
            {
                _seekSlider.SetValueWithoutNotify(_controller.CurrentTick / (float)_controller.LastTick);
            }
        }

        // ============================ Construction UI ============================

        private void BuildUI()
        {
            // Vide l'ancien panneau (outil) hébergé par ce GameObject.
            var root = transform as RectTransform;
            if (root != null)
                for (int i = root.childCount - 1; i >= 0; i--) Destroy(root.GetChild(i).gameObject);
            var rootImg = GetComponent<Image>();
            if (rootImg != null) rootImg.enabled = false;

            // Canvas dédié (overlay, au-dessus du HUD) — pour pouvoir masquer le HUD sans se masquer soi-même.
            var canvasGo = new GameObject("ReplayUICanvas");
            _uiCanvas = canvasGo.AddComponent<Canvas>();
            _uiCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _uiCanvas.sortingOrder = 5000;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();

            var uiRoot = (RectTransform)canvasGo.transform;
            BuildTransportBar(uiRoot);
            BuildEscapeMenu(uiRoot);

            HideReplayIrrelevantUI();
        }

        // Désactive les éléments de HUD sans objet en replay. Le timer : la lecture n'est pas
        // chronométrée. On désactive le GameObject (pas juste le Canvas) -> il reste caché même
        // après un toggle cinématique (H), qui ne touche qu'à Canvas.enabled.
        private void HideReplayIrrelevantUI()
        {
            var timer = FindAnyObjectByType<Nymora.Combat.View.HUD.TimerView>();
            if (timer != null) timer.gameObject.SetActive(false);
        }

        private void BuildTransportBar(RectTransform uiRoot)
        {
            // Conteneur HAUT-centre.
            var bar = NewRect("ReplayTransport", uiRoot);
            bar.anchorMin = new Vector2(0.5f, 1f); bar.anchorMax = new Vector2(0.5f, 1f); bar.pivot = new Vector2(0.5f, 1f);
            bar.anchoredPosition = new Vector2(0f, -16f);
            bar.sizeDelta = new Vector2(560f, 96f);
            _transportBar = bar.gameObject;

            var bg = bar.gameObject.AddComponent<Image>();
            bg.color = CombatUiKit.PanelBg;
            CombatUiKit.ApplyRounded(bg, CombatUiKit.CornerRadius);

            // Label tick + vitesse (EN HAUT de la barre).
            _tickLabel = MakeText(bar, "Tick", "", 18f, CombatUiKit.TextSecondary, TextAlignmentOptions.Center);
            var tl = _tickLabel.rectTransform;
            tl.anchorMin = new Vector2(0f, 1f); tl.anchorMax = new Vector2(1f, 1f); tl.pivot = new Vector2(0.5f, 1f);
            tl.offsetMin = new Vector2(8f, -30f); tl.offsetMax = new Vector2(-8f, -6f);

            // Boutons (rangée basse, sous le label tick).
            MakeButton(bar, "<<", new Vector2(-180f, -24f), new Vector2(90f, 50f), OnRewind);
            var playBtn = MakeButton(bar, "Play", new Vector2(-72f, -24f), new Vector2(110f, 50f), OnPlayPause);
            _playPauseLabel = playBtn.GetComponentInChildren<TMP_Text>();
            MakeButton(bar, ">>", new Vector2(36f, -24f), new Vector2(90f, 50f), OnForward);
            MakeButton(bar, "Cacher", new Vector2(160f, -24f), new Vector2(120f, 50f), ToggleCinematic);

            // Item mineur C.1 (rewind) — barre de progression scrubbable, juste SOUS la barre (enfant
            // -> masquée en cinématique avec le reste). Glisser = aller à n'importe quel tick (SeekTo).
            BuildSeekSlider(bar);

            // Erreur / desync (sous la barre + le scrubber), caché par défaut. Masqué en cinématique.
            _errorLabel = MakeText(bar, "ReplayError", "", 18f, new Color(0.86f, 0.36f, 0.33f, 1f), TextAlignmentOptions.Center);
            var er = _errorLabel.rectTransform;
            er.anchorMin = new Vector2(0.5f, 0f); er.anchorMax = new Vector2(0.5f, 0f); er.pivot = new Vector2(0.5f, 1f);
            er.anchoredPosition = new Vector2(0f, -42f);
            er.sizeDelta = new Vector2(760f, 28f);
            _errorLabel.gameObject.SetActive(false);
        }

        private void BuildEscapeMenu(RectTransform uiRoot)
        {
            _escapeMenu = NewRect("ReplayEscapeMenu", uiRoot).gameObject;
            var veil = _escapeMenu.GetComponent<RectTransform>();
            Stretch(veil);
            var veilImg = _escapeMenu.AddComponent<Image>();
            veilImg.color = new Color(0f, 0f, 0f, 0.6f);

            var box = NewRect("Box", veil);
            box.anchorMin = box.anchorMax = new Vector2(0.5f, 0.5f); box.pivot = new Vector2(0.5f, 0.5f);
            box.anchoredPosition = Vector2.zero;
            box.sizeDelta = new Vector2(460f, 250f);
            var boxImg = box.gameObject.AddComponent<Image>();
            boxImg.color = CombatUiKit.PanelBg;
            CombatUiKit.ApplyRounded(boxImg, CombatUiKit.CornerRadius);

            var title = MakeText(box, "Title", "Replay en pause", 26f, CombatUiKit.TextPrimary, TextAlignmentOptions.Center);
            var trt = title.rectTransform;
            trt.anchorMin = new Vector2(0f, 1f); trt.anchorMax = new Vector2(1f, 1f); trt.pivot = new Vector2(0.5f, 1f);
            trt.offsetMin = new Vector2(16f, -62f); trt.offsetMax = new Vector2(-16f, -16f);

            var quit = MakeButton(box, "Quitter le replay", new Vector2(0f, -10f), new Vector2(300f, 56f), OnQuitToHub);
            var qrt = (RectTransform)quit.transform;
            qrt.anchorMin = qrt.anchorMax = new Vector2(0.5f, 0.5f);

            var x = MakeButton(box, "X", Vector2.zero, new Vector2(40f, 40f), CloseEscapeMenu);
            var xrt = (RectTransform)x.transform;
            xrt.anchorMin = xrt.anchorMax = new Vector2(1f, 1f); xrt.pivot = new Vector2(1f, 1f);
            xrt.anchoredPosition = new Vector2(-8f, -8f);

            var hint = MakeText(box, "Hint",
                "Échap : reprendre   ·   Espace : lecture   ·   ← / → : vitesse   ·   H : cacher l'interface",
                14f, CombatUiKit.TextMuted, TextAlignmentOptions.Center);
            var hrt = hint.rectTransform;
            hrt.anchorMin = new Vector2(0f, 0f); hrt.anchorMax = new Vector2(1f, 0f); hrt.pivot = new Vector2(0.5f, 0f);
            hrt.offsetMin = new Vector2(12f, 12f); hrt.offsetMax = new Vector2(-12f, 52f);
            hint.enableWordWrapping = true;

            _escapeMenu.SetActive(false);
        }

        // ============================ Actions playback ============================

        private void OnPlayPause() { if (_controller != null) _controller.TogglePause(); }

        // >> : vitesse suivante (1× -> 1.5× -> 2× -> 4× -> 1×) + reprend la lecture.
        private void OnForward()
        {
            if (_controller == null) return;
            _controller.CycleSpeed();
            _controller.Resume();
        }

        // << : vitesse précédente (sens inverse) + reprend la lecture.
        private void OnRewind()
        {
            if (_controller == null) return;
            _controller.CycleSpeedBack();
            _controller.Resume();
        }

        // ============================ Scrubber (rewind, C.1) ============================

        // Barre de progression cliquable/glissable. value 0..1 = tick 0..LastTick. On seek au
        // RELÂCHEMENT (PointerUp) : SeekTo re-simule depuis 0, inutile de le faire à chaque frame de drag.
        private void BuildSeekSlider(RectTransform bar)
        {
            var rt = NewRect("SeekBar", bar);
            rt.anchorMin = new Vector2(0.5f, 0f); rt.anchorMax = new Vector2(0.5f, 0f); rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, -8f);
            rt.sizeDelta = new Vector2(520f, 22f);

            _seekSlider = rt.gameObject.AddComponent<Slider>();
            _seekSlider.minValue = 0f; _seekSlider.maxValue = 1f; _seekSlider.wholeNumbers = false;
            _seekSlider.navigation = new Navigation { mode = Navigation.Mode.None };

            var track = NewRect("Track", rt);
            track.anchorMin = new Vector2(0f, 0.5f); track.anchorMax = new Vector2(1f, 0.5f); track.pivot = new Vector2(0.5f, 0.5f);
            track.offsetMin = new Vector2(0f, -4f); track.offsetMax = new Vector2(0f, 4f);
            var trackImg = track.gameObject.AddComponent<Image>();
            trackImg.color = new Color(1f, 1f, 1f, 0.14f);
            CombatUiKit.ApplyRounded(trackImg, 4f);

            var fillArea = NewRect("Fill Area", rt);
            fillArea.anchorMin = new Vector2(0f, 0.5f); fillArea.anchorMax = new Vector2(1f, 0.5f); fillArea.pivot = new Vector2(0.5f, 0.5f);
            fillArea.offsetMin = new Vector2(0f, -4f); fillArea.offsetMax = new Vector2(0f, 4f);
            var fill = NewRect("Fill", fillArea);
            fill.anchorMin = new Vector2(0f, 0f); fill.anchorMax = new Vector2(1f, 1f); fill.offsetMin = Vector2.zero; fill.offsetMax = Vector2.zero;
            var fillImg = fill.gameObject.AddComponent<Image>();
            fillImg.color = CombatUiKit.Accent;
            CombatUiKit.ApplyRounded(fillImg, 4f);
            fillImg.raycastTarget = false;

            var handleArea = NewRect("Handle Slide Area", rt);
            handleArea.anchorMin = new Vector2(0f, 0f); handleArea.anchorMax = new Vector2(1f, 1f);
            handleArea.offsetMin = new Vector2(8f, 0f); handleArea.offsetMax = new Vector2(-8f, 0f);
            var handle = NewRect("Handle", handleArea);
            handle.anchorMin = new Vector2(0f, 0.5f); handle.anchorMax = new Vector2(0f, 0.5f); handle.pivot = new Vector2(0.5f, 0.5f);
            handle.sizeDelta = new Vector2(16f, 16f);
            var handleImg = handle.gameObject.AddComponent<Image>();
            handleImg.color = Color.white;
            CombatUiKit.ApplyRounded(handleImg, 8f);

            _seekSlider.fillRect = fill;
            _seekSlider.handleRect = handle;
            _seekSlider.targetGraphic = handleImg;
            _seekSlider.direction = Slider.Direction.LeftToRight;

            // Pointer down = on commence à scruber (pause visuelle, on fige la sync) ; up = on seek.
            var et = rt.gameObject.AddComponent<EventTrigger>();
            var down = new EventTrigger.Entry { eventID = EventTriggerType.PointerDown };
            down.callback.AddListener(_ => { _userScrubbing = true; });
            et.triggers.Add(down);
            var up = new EventTrigger.Entry { eventID = EventTriggerType.PointerUp };
            up.callback.AddListener(_ => OnSeekRelease());
            et.triggers.Add(up);
        }

        private void OnSeekRelease()
        {
            if (_controller == null || _seekSlider == null) { _userScrubbing = false; return; }
            int target = Mathf.RoundToInt(_seekSlider.value * _controller.LastTick);
            _userScrubbing = false;
            _controller.SeekTo(target);
        }

        // ============================ Menu Échap ============================

        private void ToggleEscapeMenu()
        {
            if (_escapeMenu == null) return;
            if (_escapeMenu.activeSelf) CloseEscapeMenu();
            else OpenEscapeMenu();
        }

        private void OpenEscapeMenu()
        {
            if (_escapeMenu == null || _controller == null) return;
            _wasPlayingBeforeMenu = !_controller.IsPaused;
            _controller.Pause();
            _escapeMenu.SetActive(true);
            _escapeMenu.transform.SetAsLastSibling();
        }

        private void CloseEscapeMenu()
        {
            if (_escapeMenu == null) return;
            _escapeMenu.SetActive(false);
            if (_controller != null && _wasPlayingBeforeMenu) _controller.Resume();
        }

        private void OnQuitToHub()
        {
            ReplayPlaybackBridge.RequestedReplayPath = null;
            SceneTransition.Load(HubSceneName, () =>
            {
                try { Quantum.QuantumRunner.ShutdownAll(); }
                catch { }
            });
        }

        // ============================ Mode cinématique ============================

        // Masque / ré-affiche TOUTE l'interface : la barre de transport + tous les autres Canvas
        // de la scène (HUD combat). Le Canvas replay dédié reste actif (pour le menu Échap), mais
        // on cache sa barre. Re-bascule via le bouton « Cacher » ou la touche H.
        private void ToggleCinematic()
        {
            _cinematicHidden = !_cinematicHidden;

            if (_transportBar != null) _transportBar.SetActive(!_cinematicHidden);

            if (_cinematicHidden)
            {
                _canvasesHidden.Clear();
                foreach (var c in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
                {
                    if (c == null || c == _uiCanvas) continue;        // garde notre canvas replay
                    if (!c.isRootCanvas) continue;                    // un seul toggle par hiérarchie
                    if (!c.enabled) continue;                         // déjà masqué : ne pas le rallumer après
                    c.enabled = false;
                    _canvasesHidden.Add(c);
                }
            }
            else
            {
                foreach (var c in _canvasesHidden) if (c != null) c.enabled = true;
                _canvasesHidden.Clear();
            }
        }

        // ============================ Helpers UGUI ============================

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
            tmp.raycastTarget = false; tmp.enableWordWrapping = false; tmp.richText = true;
            return tmp;
        }

        private Button MakeButton(Transform parent, string label, Vector2 anchoredPos, Vector2 size, UnityEngine.Events.UnityAction onClick)
        {
            var rt = NewRect("Btn_" + label, parent);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f); rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = anchoredPos; rt.sizeDelta = size;

            var img = rt.gameObject.AddComponent<Image>();
            img.color = CombatUiKit.GhostBg;
            CombatUiKit.ApplyRounded(img, CombatUiKit.CornerRadius - 4f); // coins arrondis (DA menu)
            var btn = rt.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            // Pas de navigation : évite qu'Espace/Entrée ne « clique » un bouton resté sélectionné
            // (sinon conflit avec le raccourci Espace = play/pause).
            btn.navigation = new Navigation { mode = Navigation.Mode.None };
            var colors = btn.colors;
            colors.normalColor = CombatUiKit.GhostBg;
            colors.highlightedColor = CombatUiKit.GhostBgHover;
            colors.pressedColor = CombatUiKit.GhostBgHover;
            btn.colors = colors;
            if (onClick != null) btn.onClick.AddListener(onClick);

            var lbl = MakeText(rt, "Label", label, 22f, CombatUiKit.TextPrimary, TextAlignmentOptions.Center);
            Stretch(lbl.rectTransform);
            return btn;
        }

        private void OnDestroy()
        {
            // Nettoie notre canvas dédié (sinon il survit au reload de scène via rien — mais par
            // sécurité on le détruit avec le component).
            if (_uiCanvas != null) Destroy(_uiCanvas.gameObject);
        }
    }
}
