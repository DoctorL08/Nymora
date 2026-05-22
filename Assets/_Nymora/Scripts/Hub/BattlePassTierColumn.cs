using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Nymora.Hub
{
    /// <summary>
    /// Brique 5.7.b (refonte) — une colonne du Battle Pass : case GRATUITE (haut) +
    /// badge n° de palier (milieu) + case PREMIUM (bas). Format Brawl Stars (lecture
    /// horizontale). Chaque case est une Image = slot où le designer mettra l'art ;
    /// un label + un statut (verrouillé / réclamable / réclamé) se superposent.
    ///
    /// Template instancié N fois par HubBattlePassPanel dans le contenu du scroll horizontal.
    /// </summary>
    public sealed class BattlePassTierColumn : MonoBehaviour
    {
        public enum CellState { Empty, Locked, Claimable, Claimed, PremiumLocked }

        [Header("Case gratuite (haut)")]
        [SerializeField] private Button _freeButton;
        [SerializeField] private Image _freeImage;   // slot art (sprite à fournir par le designer)
        [SerializeField] private TMP_Text _freeLabel;
        [SerializeField] private TMP_Text _freeStatus;

        [Header("Badge palier (milieu)")]
        [SerializeField] private TMP_Text _tierLabel;
        [SerializeField] private Image _tierBadge;

        [Header("Case premium (bas)")]
        [SerializeField] private Button _premiumButton;
        [SerializeField] private Image _premiumImage;
        [SerializeField] private TMP_Text _premiumLabel;
        [SerializeField] private TMP_Text _premiumStatus;

        // Couleurs d'état (fond de case).
        private static readonly Color CEmpty = new Color(0.14f, 0.15f, 0.18f, 1f);
        private static readonly Color CLocked = new Color(0.20f, 0.21f, 0.25f, 1f);
        private static readonly Color CClaimable = new Color(0.85f, 0.70f, 0.25f, 1f);
        private static readonly Color CClaimed = new Color(0.22f, 0.45f, 0.28f, 1f);
        private static readonly Color CPremium = new Color(0.32f, 0.26f, 0.48f, 1f);
        private static readonly Color CBadgeNormal = new Color(0.18f, 0.20f, 0.25f, 1f);
        private static readonly Color CBadgeCurrent = new Color(0.45f, 0.38f, 0.62f, 1f);

        public int Tier { get; private set; }

        /// <summary>Invoqué au clic sur une case réclamable. (tier, track "free"|"premium").</summary>
        public event Action<int, string> OnClaim;

        private void Awake()
        {
            if (_freeButton != null) _freeButton.onClick.AddListener(() => OnClaim?.Invoke(Tier, "free"));
            if (_premiumButton != null) _premiumButton.onClick.AddListener(() => OnClaim?.Invoke(Tier, "premium"));
        }

        public void Bind(int tier, bool isCurrent,
            string freeLabel, CellState freeState,
            string premiumLabel, CellState premiumState)
        {
            Tier = tier;
            if (_tierLabel != null) _tierLabel.text = $"{tier}";
            if (_tierBadge != null) _tierBadge.color = isCurrent ? CBadgeCurrent : CBadgeNormal;

            ApplyCell(_freeButton, _freeImage, _freeLabel, _freeStatus, freeLabel, freeState);
            ApplyCell(_premiumButton, _premiumImage, _premiumLabel, _premiumStatus, premiumLabel, premiumState);
        }

        private static void ApplyCell(Button btn, Image img, TMP_Text label, TMP_Text status, string text, CellState state)
        {
            if (img != null) img.color = ColorFor(state);
            if (label != null) label.text = state == CellState.Empty ? "" : text;
            if (status != null) status.text = StatusFor(state);
            if (btn != null) btn.interactable = state == CellState.Claimable;
        }

        private static Color ColorFor(CellState s)
        {
            switch (s)
            {
                case CellState.Claimable: return CClaimable;
                case CellState.Claimed: return CClaimed;
                case CellState.PremiumLocked: return CPremium;
                case CellState.Locked: return CLocked;
                default: return CEmpty;
            }
        }

        private static string StatusFor(CellState s)
        {
            switch (s)
            {
                case CellState.Claimable: return "<b>RÉCLAMER</b>";
                case CellState.Claimed: return "<color=#7CFC7C>✓</color>";
                case CellState.PremiumLocked: return "<color=#cdb4ff>premium</color>";
                case CellState.Locked: return "<color=#888888>verrouillé</color>";
                default: return "";
            }
        }
    }
}
