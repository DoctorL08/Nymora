using System.Collections;
using Quantum;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
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
    /// Hover flow (2.13.c) : IPointerEnter -> tooltip apres delay, IPointerExit -> hide.
    /// </summary>
    public class SpellSlotView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public enum SlotState { Normal, Disabled, Armed }

        [SerializeField] private Image _iconImage;
        [SerializeField] private Image _frameImage;
        [SerializeField] private TMP_Text _keyLabel;
        [SerializeField] private TMP_Text _cooldownLabel;
        [SerializeField] private Button _button;

        [Header("Tooltip (2.13.c)")]
        [Tooltip("Delai avant affichage de la tooltip (anti-clignotement quand on balaye).")]
        [SerializeField] private float _tooltipDelaySeconds = 0.2f;

        private CombatHUDController _controller;
        private SpellId _spell;
        private Coroutine _tooltipCoroutine;

        private static readonly Color FrameNormal   = new Color(0.15f, 0.15f, 0.15f, 0.85f);
        private static readonly Color FrameDisabled = new Color(0.10f, 0.10f, 0.10f, 0.85f);
        private static readonly Color FrameArmed    = new Color(0.95f, 0.75f, 0.20f, 1.00f);
        private static readonly Color IconNormal    = Color.white;
        private static readonly Color IconDisabled  = new Color(0.35f, 0.35f, 0.35f, 1f);

        public SpellId Spell => _spell;
        public RectTransform RectTransform => transform as RectTransform;

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

        // -- Pointer events (2.13.c) --
        public void OnPointerEnter(PointerEventData eventData)
        {
            if (_controller == null || _spell == SpellId.None) return;
            if (_tooltipCoroutine != null) StopCoroutine(_tooltipCoroutine);
            _tooltipCoroutine = StartCoroutine(ShowTooltipAfterDelay());
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (_tooltipCoroutine != null)
            {
                StopCoroutine(_tooltipCoroutine);
                _tooltipCoroutine = null;
            }
            if (_controller != null) _controller.HideTooltip();
        }

        private IEnumerator ShowTooltipAfterDelay()
        {
            if (_tooltipDelaySeconds > 0f)
            {
                yield return new WaitForSeconds(_tooltipDelaySeconds);
            }
            if (_controller != null && _spell != SpellId.None)
            {
                _controller.ShowTooltip(_spell, RectTransform);
            }
            _tooltipCoroutine = null;
        }

        private void OnDisable()
        {
            // Si le slot est cache (ex. swap de scene), kill la coroutine + hide tooltip.
            if (_tooltipCoroutine != null)
            {
                StopCoroutine(_tooltipCoroutine);
                _tooltipCoroutine = null;
            }
        }
    }
}
