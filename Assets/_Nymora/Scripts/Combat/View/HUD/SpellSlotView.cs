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
    /// ressources) / Armed (sort selectionne, en attente de cible).
    ///
    /// Click flow : Button.onClick -> CombatHUDController.OnSlotClicked(_spell).
    /// </summary>
    public class SpellSlotView : MonoBehaviour
    {
        public enum SlotState { Normal, Disabled, Armed }

        [SerializeField] private Image _iconImage;
        [SerializeField] private Image _frameImage;
        [SerializeField] private TMP_Text _keyLabel;
        [SerializeField] private Button _button;

        private CombatHUDController _controller;
        private SpellId _spell;

        private static readonly Color FrameNormal   = new Color(0.15f, 0.15f, 0.15f, 0.85f);
        private static readonly Color FrameDisabled = new Color(0.10f, 0.10f, 0.10f, 0.85f);
        private static readonly Color FrameArmed    = new Color(0.95f, 0.75f, 0.20f, 1.00f);
        private static readonly Color IconNormal    = Color.white;
        private static readonly Color IconDisabled  = new Color(0.35f, 0.35f, 0.35f, 1f);

        public SpellId Spell => _spell;

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

            SetState(SlotState.Normal);
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
    }
}
