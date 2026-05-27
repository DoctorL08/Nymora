using System.Collections.Generic;
using Nymora.Core.ScriptableObjects;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Nymora.Hub.Menu
{
    /// <summary>
    /// Brique 5.11 — Tooltip de prévisu animée pour la boutique (et réutilisable ailleurs). Au
    /// survol d'une carte skin/familier, ouvre un panneau persistant où le joueur navigue entre
    /// les STAGES (0/1/2, skins combat) et les ANIMS (idle/walk/attack/cast/hurt/death) ; le sprite
    /// rejoue l'anim sélectionnée (UISpriteAnimator, mode aligné anti-jiggle).
    ///
    /// Dégrade par item : familiers = idle/walk (pas de stages) ; skins placeholder = idle/walk du
    /// hub ; skins combat (Ashen) = stages + anims complètes (frames extraites par
    /// "Extract Skin Combat Preview"). Reste ouvert pour permettre les clics ; remplacé au survol
    /// d'une autre carte, fermé via la croix.
    /// </summary>
    public sealed class CosmeticPreviewTooltip
    {
        private sealed class Anim { public string Key, Label; public Sprite[] Frames; public float Fps; }
        private sealed class Stage { public int Index; public readonly List<Anim> Anims = new List<Anim>(); }

        // Ordre + libellés FR des anims (clé = tag d'extraction).
        private static readonly (string key, string label)[] AnimOrder =
        {
            ("idle", "Idle"), ("walk", "Marche"), ("attack", "Attaque"),
            ("cast", "Sort"), ("hurt", "Touché"), ("death", "Mort"),
        };

        // Profils de taille : skins persos = grande fenêtre ; familiers = compacte.
        private static readonly Vector2 PanelSizeSkin = new Vector2(420f, 540f);
        private static readonly Vector2 PanelSizePet = new Vector2(300f, 360f);
        private const float TopReserve = 54f;      // hauteur titre
        private const float BottomReserve = 136f;  // stage row (@92) + grille d'anims (2 lignes @10)

        private readonly HubMenuTheme _t;
        private readonly HubMenuUIFactory _f;
        private readonly RectTransform _parent;
        private Canvas _canvas;

        private RectTransform _root;       // panneau (caché par défaut)
        private TextMeshProUGUI _title;
        private RectTransform _box;        // boîte de prévisu
        private Image _animImage;
        private UISpriteAnimator _player;
        private RectTransform _stageRow, _animRow;
        private GridLayoutGroup _animGrid;
        private Vector2 _curBoxSize = new Vector2(232f, 232f);

        private readonly List<Stage> _stages = new List<Stage>();
        private int _curStage;

        public CosmeticPreviewTooltip(HubMenuTheme t, HubMenuUIFactory f, RectTransform parent)
        {
            _t = t; _f = f; _parent = parent;
        }

        // ---- Construction UI (paresseuse) ----
        private void EnsureBuilt()
        {
            if (_root != null) return;
            _canvas = _parent.GetComponentInParent<Canvas>();

            // D.A. menu : fond de panneau + coins arrondis (sprite 9-slice du factory).
            var panel = _f.MakeImage("CosmeticPreviewTooltip", _parent, _t.PanelBg);
            _root = panel.rectTransform;
            _root.anchorMin = _root.anchorMax = new Vector2(0f, 0f);
            _root.pivot = new Vector2(0f, 0.5f);
            _root.sizeDelta = PanelSizePet; // taille par défaut, ajustée par ApplyProfile

            // Cadre arrondi fin (D.A. : ligne Divider derrière le panneau).
            var border = _f.MakeImage("Border", _root, _t.Divider);
            HubMenuUIFactory.Stretch(border.rectTransform, -2f, -2f, -2f, -2f);
            border.transform.SetAsFirstSibling();
            border.raycastTarget = false;

            // Titre
            _title = _f.MakeText("Title", _root, "", _t.FontSizeBody, _t.TextPrimary, _t.FontBold, TextAlignmentOptions.Center);
            _title.raycastTarget = false; _title.enableWordWrapping = true; _title.overflowMode = TextOverflowModes.Ellipsis;
            var trt = _title.rectTransform;
            trt.anchorMin = new Vector2(0f, 1f); trt.anchorMax = new Vector2(1f, 1f); trt.pivot = new Vector2(0.5f, 1f);
            trt.sizeDelta = new Vector2(-48f, 46f); trt.anchoredPosition = new Vector2(0f, -8f);

            // Croix de fermeture (X ASCII : la police Ari n'a pas le glyphe ✕)
            var close = _f.MakeButton(_root, "X", false, out _);
            var crt = (close.transform as RectTransform);
            crt.anchorMin = crt.anchorMax = new Vector2(1f, 1f); crt.pivot = new Vector2(1f, 1f);
            crt.sizeDelta = new Vector2(34f, 34f); crt.anchoredPosition = new Vector2(-6f, -6f);
            close.onClick.AddListener(Hide);

            // Boîte de prévisu (sprite animé centré), coins arrondis. Taille fixée par ApplyProfile.
            var box = _f.MakeImage("PreviewBox", _root, new Color(1f, 1f, 1f, 0.04f));
            _box = box.rectTransform;
            _box.anchorMin = new Vector2(0.5f, 1f); _box.anchorMax = new Vector2(0.5f, 1f); _box.pivot = new Vector2(0.5f, 1f);
            _box.anchoredPosition = new Vector2(0f, -TopReserve);
            box.raycastTarget = false;

            _animImage = _f.MakeImage("Anim", _box, Color.white, rounded: false);
            _animImage.raycastTarget = false; _animImage.preserveAspect = false;
            var art = _animImage.rectTransform;
            art.anchorMin = art.anchorMax = new Vector2(0.5f, 0.5f); art.pivot = new Vector2(0.5f, 0.5f);
            art.anchoredPosition = Vector2.zero;
            _player = _animImage.gameObject.AddComponent<UISpriteAnimator>();

            // Rangée de stages (0/1/2)
            _stageRow = _f.MakeRect("StageRow", _root);
            _stageRow.anchorMin = new Vector2(0f, 0f); _stageRow.anchorMax = new Vector2(1f, 0f); _stageRow.pivot = new Vector2(0.5f, 0f);
            _stageRow.sizeDelta = new Vector2(-24f, 36f); _stageRow.anchoredPosition = new Vector2(0f, 92f);
            var slg = _stageRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            slg.spacing = 6f; slg.childAlignment = TextAnchor.MiddleCenter;
            slg.childControlWidth = true; slg.childControlHeight = true; slg.childForceExpandWidth = false;

            // Rangée d'anims — grille qui passe à la ligne quand ça déborde (jusqu'à 2 lignes pour
            // 6 anims). cellSize fixé par ApplyProfile selon la largeur du panneau.
            _animRow = _f.MakeRect("AnimRow", _root);
            _animRow.anchorMin = new Vector2(0f, 0f); _animRow.anchorMax = new Vector2(1f, 0f); _animRow.pivot = new Vector2(0.5f, 0f);
            _animRow.sizeDelta = new Vector2(-16f, 72f); _animRow.anchoredPosition = new Vector2(0f, 10f);
            _animGrid = _animRow.gameObject.AddComponent<GridLayoutGroup>();
            _animGrid.constraint = GridLayoutGroup.Constraint.FixedColumnCount; _animGrid.constraintCount = 3;
            _animGrid.spacing = new Vector2(6f, 6f); _animGrid.childAlignment = TextAnchor.UpperCenter;
            _animGrid.cellSize = new Vector2(120f, 30f);

            _root.gameObject.SetActive(false);
        }

        // ---- API publique ----
        public void ShowForSkin(CosmeticSkinDefinition def, RectTransform anchorCard)
        {
            if (def == null) return;
            EnsureBuilt();
            ApplyProfile(PanelSizeSkin);
            BuildStagesForSkin(def);
            Populate(def.DisplayName ?? def.CosmeticId, anchorCard);
        }

        public void ShowForPet(PetDefinition def, RectTransform anchorCard)
        {
            if (def == null) return;
            EnsureBuilt();
            ApplyProfile(PanelSizePet);
            _stages.Clear();
            var s = new Stage { Index = 0 };
            AddAnim(s, "idle", def.IdleFrames, def.IdleFps);
            AddAnim(s, "walk", def.WalkFrames, def.WalkFps);
            if (s.Anims.Count > 0) _stages.Add(s);
            Populate(def.DisplayName ?? def.CosmeticId, anchorCard);
        }

        public void Hide()
        {
            if (_root != null) _root.gameObject.SetActive(false);
        }

        // Dimensionne panneau + boîte selon le profil (skin = grand, familier = compact). La boîte
        // remplit l'espace entre le titre (haut) et les rangées de boutons (bas).
        private void ApplyProfile(Vector2 panelSize)
        {
            _root.sizeDelta = panelSize;
            _curBoxSize = new Vector2(panelSize.x - 32f, panelSize.y - TopReserve - BottomReserve);
            _box.sizeDelta = _curBoxSize;
            _animImage.rectTransform.sizeDelta = _curBoxSize;
            // Cellules de la grille d'anims = 3 colonnes qui remplissent la largeur du panneau.
            if (_animGrid != null)
            {
                float cellW = (panelSize.x - 16f - 2f * 6f) / 3f;
                _animGrid.cellSize = new Vector2(cellW, 30f);
            }
        }

        // ---- Construction des stages ----
        private void BuildStagesForSkin(CosmeticSkinDefinition def)
        {
            _stages.Clear();
            if (def.HasCombatPreview)
            {
                for (int stage = 0; stage <= 2; stage++)
                {
                    var s = new Stage { Index = stage };
                    foreach (var (key, _) in AnimOrder)
                    {
                        var clip = def.GetPreview(stage, key);
                        if (clip != null) AddAnim(s, key, clip.Frames, clip.Fps);
                    }
                    if (s.Anims.Count > 0) _stages.Add(s);
                }
            }
            if (_stages.Count == 0)
            {
                // Placeholder : pas de prévisu combat -> idle/walk du hub.
                var s = new Stage { Index = 0 };
                AddAnim(s, "idle", def.IdleFrames, def.IdleFps);
                AddAnim(s, "walk", def.WalkFrames, def.WalkFps);
                if (s.Anims.Count > 0) _stages.Add(s);
            }
        }

        private static void AddAnim(Stage s, string key, Sprite[] frames, float fps)
        {
            if (frames == null || frames.Length == 0) return;
            string label = key;
            foreach (var (k, l) in AnimOrder) if (k == key) { label = l; break; }
            s.Anims.Add(new Anim { Key = key, Label = label, Frames = frames, Fps = fps > 0 ? fps : 8f });
        }

        // ---- Affichage / navigation ----
        private void Populate(string title, RectTransform anchorCard)
        {
            _title.text = title;
            _root.gameObject.SetActive(true);
            _root.SetAsLastSibling();
            BuildStageButtons();
            SelectStage(0);
            Position(anchorCard);
        }

        private void BuildStageButtons()
        {
            ClearChildren(_stageRow);
            // Une seule "stage" (familier / placeholder) -> pas de sélecteur de stage.
            _stageRow.gameObject.SetActive(_stages.Count > 1);
            if (_stages.Count <= 1) return;
            for (int i = 0; i < _stages.Count; i++)
            {
                int idx = i;
                var btn = _f.MakeButton(_stageRow, $"Stage {_stages[i].Index}", i == _curStage, out _);
                btn.gameObject.AddComponent<LayoutElement>().preferredWidth = 78f;
                btn.onClick.AddListener(() => SelectStage(idx));
            }
        }

        private void SelectStage(int i)
        {
            if (_stages.Count == 0) return;
            _curStage = Mathf.Clamp(i, 0, _stages.Count - 1);
            BuildStageButtons();          // reconstruit -> le contraste primary/ghost suit l'actif
            BuildAnimButtons(0);
            if (_stages[_curStage].Anims.Count > 0) Play(_stages[_curStage].Anims[0]);
        }

        // Reconstruit la rangée d'anims, l'index actif en primary (label foncé lisible).
        private void BuildAnimButtons(int activeIdx)
        {
            ClearChildren(_animRow);
            var anims = _stages[_curStage].Anims;
            for (int i = 0; i < anims.Count; i++)
            {
                int idx = i;
                var a = anims[i];
                var btn = _f.MakeButton(_animRow, a.Label, i == activeIdx, out var lbl);
                // Boutons à largeur égale : police réduite + pas de wrap pour que les libellés
                // longs (Attaque/Touché) tiennent quand il y a 6 anims.
                if (lbl != null) { lbl.fontSize = 15f; lbl.enableWordWrapping = false; lbl.overflowMode = TextOverflowModes.Overflow; }
                btn.onClick.AddListener(() => { Play(a); BuildAnimButtons(idx); });
            }
        }

        private void Play(Anim a)
        {
            if (a == null || _player == null) return;
            // Centré dans la boîte (boxCenter = (0,0) = centre du parent PreviewBox).
            _player.PlayAligned(_animImage, a.Frames, a.Fps, Vector2.zero, _curBoxSize);
        }

        // Place le panneau à droite de la carte (à gauche si la carte est dans la moitié droite).
        private void Position(RectTransform anchorCard)
        {
            if (anchorCard == null) return;
            Camera cam = (_canvas != null && _canvas.renderMode != RenderMode.ScreenSpaceOverlay) ? _canvas.worldCamera : null;
            Vector2 cardScreen = RectTransformUtility.WorldToScreenPoint(cam, anchorCard.position);
            bool toLeft = cardScreen.x > Screen.width * 0.5f;

            float halfW = anchorCard.rect.width * 0.5f + 12f;
            var edgeLocal = new Vector3(toLeft ? -halfW : halfW, 0f, 0f);
            _root.pivot = new Vector2(toLeft ? 1f : 0f, 0.5f);
            _root.position = anchorCard.TransformPoint(edgeLocal);
        }

        private static void ClearChildren(RectTransform rt)
        {
            for (int i = rt.childCount - 1; i >= 0; i--) Object.Destroy(rt.GetChild(i).gameObject);
        }
    }
}
