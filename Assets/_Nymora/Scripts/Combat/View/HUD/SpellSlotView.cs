using Quantum;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Button = UnityEngine.UI.Button;
using Image = UnityEngine.UI.Image;

namespace Nymora.Combat.View.HUD
{
    /// <summary>
    /// Une icone de sort dans la spell bar (6 deck + slot signature). Cliquable.
    /// Le tint du fond change selon l'etat : Normal / Disabled (pas assez de
    /// ressources, cooldown signature) / Armed (sort selectionne, en attente de cible).
    ///
    /// Click flow : Button.onClick -> CombatHUDController.OnSlotClicked(_spell).
    /// Hover flow (B6, 22 mai) : detection par POLLING (Update + RectangleContainsScreenPoint)
    /// au lieu de IPointerEnter. Raison : si la souris est deja immobile sur un slot au
    /// chargement de la scene combat, l'EventSystem ne declenche OnPointerEnter qu'au 1er
    /// event pointeur (clic / mouvement) -> le designer devait "cliquer un sort pour debloquer
    /// le tooltip". Le polling l'affiche des le survol, sans clic prealable.
    /// </summary>
    public class SpellSlotView : MonoBehaviour
    {
        public enum SlotState { Normal, Disabled, Armed }

        [SerializeField] private Image _iconImage;
        [SerializeField] private Image _frameImage;
        [SerializeField] private TMP_Text _keyLabel;
        [SerializeField] private TMP_Text _cooldownLabel;
        [SerializeField] private Button _button;

        // B6 (22 mai) — Delai d'apparition du tooltip TRES court pour rester reactif. Const
        // (plus SerializeField) : la valeur serialisee 0.2 dans la scene/prefab rendait
        // l'affichage lent. ~0.03s = quasi instant tout en evitant le clignotement 1-frame
        // quand on balaye la barre de sorts.
        private const float TooltipDelaySeconds = 0.03f;

        private CombatHUDController _controller;
        private SpellId _spell;

        // B6 — etat de hover par polling.
        private bool _isHovered;
        private float _hoverElapsed;
        private bool _tooltipShown;

        // B6 (22 mai) — feedback visuel de survol : le slot monte un peu + halo blanc brillant.
        private RectTransform _rt;
        private Image _hoverGlow;
        private float _baseY;
        private bool _baseRecorded;
        private float _hoverAnim; // 0 (repos) -> 1 (survole), lerp
        private const float HoverRiseY = 10f;        // montee en px
        private const float HoverGlowMaxAlpha = 0.85f;
        private const float HoverAnimSpeed = 14f;    // vitesse de lerp (atteint 1 en ~0.07s)
        private static Sprite _glowSpriteCache;

        private static readonly Color FrameNormal   = new Color(0.15f, 0.15f, 0.15f, 0.85f);
        private static readonly Color FrameDisabled = new Color(0.10f, 0.10f, 0.10f, 0.85f);
        private static readonly Color FrameArmed    = new Color(0.95f, 0.75f, 0.20f, 1.00f);
        private static readonly Color IconNormal    = Color.white;
        private static readonly Color IconDisabled  = new Color(0.35f, 0.35f, 0.35f, 1f);

        public SpellId Spell => _spell;
        public RectTransform RectTransform => transform as RectTransform;

        private void Awake()
        {
            _rt = transform as RectTransform;
            CreateHoverGlow();
        }

        // Halo blanc brillant derriere le slot (radial soft), alpha 0 au repos, fade-in au survol.
        private void CreateHoverGlow()
        {
            if (_hoverGlow != null || _rt == null) return;
            var glowGo = new GameObject("HoverGlow", typeof(RectTransform));
            glowGo.transform.SetParent(_rt, false);
            var grt = (RectTransform)glowGo.transform;
            grt.anchorMin = Vector2.zero;
            grt.anchorMax = Vector2.one;
            grt.offsetMin = new Vector2(-16f, -16f); // deborde de 16px -> halo autour du slot
            grt.offsetMax = new Vector2(16f, 16f);
            _hoverGlow = glowGo.AddComponent<Image>();
            _hoverGlow.sprite = GetGlowSprite();
            _hoverGlow.color = new Color(1f, 1f, 1f, 0f);
            _hoverGlow.raycastTarget = false;
            grt.SetAsFirstSibling(); // derriere l'icone / le cadre
        }

        public void Bind(CombatHUDController controller, SpellId spell, Sprite icon, string shortcutLabel)
        {
            _controller = controller;
            _spell = spell;

            if (_iconImage != null)
            {
                _iconImage.sprite = icon;
                _iconImage.enabled = icon != null;
            }
            if (_keyLabel != null)
            {
                _keyLabel.text = shortcutLabel ?? string.Empty;
            }
            if (_button != null)
            {
                _button.onClick.RemoveAllListeners();
                _button.onClick.AddListener(OnClickInternal);
            }

            SetCooldownLabel(0);
            SetState(SlotState.Normal);
        }

        /// <summary>
        /// Affiche un nombre de tours restants en surimpression (rouge), ou cache si <= 0.
        /// Utilise pour la signature Ame Laceree (cooldown 4 tours).
        /// </summary>
        public void SetCooldownLabel(int turnsLeft)
        {
            if (_cooldownLabel == null) return;
            if (turnsLeft > 0)
            {
                _cooldownLabel.text = $"{turnsLeft}t";
                _cooldownLabel.gameObject.SetActive(true);
            }
            else
            {
                _cooldownLabel.gameObject.SetActive(false);
            }
        }

        public void SetState(SlotState state)
        {
            switch (state)
            {
                case SlotState.Normal:
                    if (_frameImage != null) _frameImage.color = FrameNormal;
                    if (_iconImage  != null) _iconImage.color  = IconNormal;
                    if (_button     != null) _button.interactable = true;
                    break;
                case SlotState.Disabled:
                    if (_frameImage != null) _frameImage.color = FrameDisabled;
                    if (_iconImage  != null) _iconImage.color  = IconDisabled;
                    if (_button     != null) _button.interactable = false;
                    break;
                case SlotState.Armed:
                    if (_frameImage != null) _frameImage.color = FrameArmed;
                    if (_iconImage  != null) _iconImage.color  = IconNormal;
                    if (_button     != null) _button.interactable = true;
                    break;
            }
        }

        private void OnClickInternal()
        {
            if (_controller != null)
            {
                _controller.OnSlotClicked(_spell);
            }
        }

        // -- Hover par polling (B6) --
        private void Update()
        {
            if (_controller == null || _spell == SpellId.None) { ClearHover(); return; }

            if (IsPointerOverSlot())
            {
                if (!_isHovered)
                {
                    _isHovered = true;
                    _hoverElapsed = 0f;
                    _tooltipShown = false;
                }
                if (!_tooltipShown)
                {
                    _hoverElapsed += Time.unscaledDeltaTime;
                    if (_hoverElapsed >= TooltipDelaySeconds)
                    {
                        _controller.ShowTooltip(_spell, RectTransform);
                        _tooltipShown = true;
                    }
                }
            }
            else
            {
                ClearHover();
            }

            AnimateHover();
        }

        // Lerp montee + halo selon l'etat de survol. Tourne chaque frame (entree ET sortie).
        private void AnimateHover()
        {
            if (_rt == null) return;
            if (!_baseRecorded)
            {
                _baseY = _rt.anchoredPosition.y;
                _baseRecorded = true;
            }

            float target = _isHovered ? 1f : 0f;
            _hoverAnim = Mathf.MoveTowards(_hoverAnim, target, Time.unscaledDeltaTime * HoverAnimSpeed);

            var pos = _rt.anchoredPosition;
            pos.y = _baseY + HoverRiseY * _hoverAnim;
            _rt.anchoredPosition = pos;

            if (_hoverGlow != null)
            {
                var c = _hoverGlow.color;
                c.a = HoverGlowMaxAlpha * _hoverAnim;
                _hoverGlow.color = c;
            }
        }

        /// <summary>Reset l'etat de hover et cache le tooltip si c'est CE slot qui l'affichait.</summary>
        private void ClearHover()
        {
            if (!_isHovered) return;
            _isHovered = false;
            if (_tooltipShown)
            {
                _tooltipShown = false;
                if (_controller != null) _controller.HideTooltip();
            }
        }

        private bool IsPointerOverSlot()
        {
            var rt = RectTransform;
            if (rt == null) return false;
            // Camera : null pour un canvas ScreenSpaceOverlay, sinon la worldCamera du canvas.
            Camera cam = null;
            var canvas = GetComponentInParent<Canvas>();
            if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
                cam = canvas.worldCamera;
            return RectTransformUtility.RectangleContainsScreenPoint(rt, UnityEngine.Input.mousePosition, cam);
        }

        private void OnDisable()
        {
            // Slot cache (swap de scene, etc.) : reset hover + cache le tooltip si besoin.
            ClearHover();
            // Reset visuel pour ne pas rester "souleve" si desactive en plein survol.
            _hoverAnim = 0f;
            if (_rt != null && _baseRecorded)
            {
                var pos = _rt.anchoredPosition;
                pos.y = _baseY;
                _rt.anchoredPosition = pos;
            }
            if (_hoverGlow != null)
            {
                var c = _hoverGlow.color; c.a = 0f; _hoverGlow.color = c;
            }
        }

        // Sprite de halo radial blanc (genere une fois, partage par tous les slots). Centre
        // opaque -> bords transparents, ce qui donne un contour blanc brillant autour du slot.
        private static Sprite GetGlowSprite()
        {
            if (_glowSpriteCache != null) return _glowSpriteCache;
            const int size = 64;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            float half = size * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f - half) / half;
                    float dy = (y + 0.5f - half) / half;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Clamp01(1f - d);
                    a = a * a; // falloff plus doux -> aspect "glow"
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            }
            tex.Apply();
            tex.filterMode = FilterMode.Bilinear;
            _glowSpriteCache = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
            return _glowSpriteCache;
        }
    }
}
