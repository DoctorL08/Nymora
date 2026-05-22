using System;
using Nymora.Network.Backend;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Nymora.Hub
{
    /// <summary>
    /// Brique 5.8.b — une ligne de quête : titre + barre de progression + récompense + bouton Réclamer.
    /// Template cloné par HubQuestsPanel.
    /// </summary>
    public sealed class QuestRow : MonoBehaviour
    {
        [SerializeField] private TMP_Text _title;
        [SerializeField] private Image _progressFill;
        [SerializeField] private TMP_Text _progressText;
        [SerializeField] private TMP_Text _rewardText;
        [SerializeField] private Button _claimButton;
        [SerializeField] private TMP_Text _claimLabel;

        public string QuestId { get; private set; }
        public event Action<string> OnClaim;

        private void Awake()
        {
            if (_claimButton != null) _claimButton.onClick.AddListener(() => OnClaim?.Invoke(QuestId));
        }

        public void Bind(QuestDto q)
        {
            QuestId = q.id;
            if (_title != null) _title.text = $"<color=#cdb4ff>[{PeriodTag(q.period)}]</color> {q.title}";
            if (_progressText != null) _progressText.text = $"{Mathf.Min(q.progress, q.target)}/{q.target}";
            if (_progressFill != null) _progressFill.fillAmount = q.target > 0 ? Mathf.Clamp01((float)q.progress / q.target) : 0f;

            string reward = $"+{q.bpXp} XP Pass" + (q.nymos > 0 ? $"   +{q.nymos} Nymos" : "");
            if (_rewardText != null) _rewardText.text = reward;

            bool claimable = q.completed && !q.claimed;
            if (_claimButton != null)
            {
                _claimButton.interactable = claimable;
                if (_claimButton.image != null)
                    _claimButton.image.color = q.claimed ? new Color(0.22f, 0.30f, 0.24f)
                        : claimable ? new Color(0.30f, 0.55f, 0.34f)
                        : new Color(0.26f, 0.27f, 0.32f);
            }
            if (_claimLabel != null)
                _claimLabel.text = q.claimed ? "✓ Réclamé" : (claimable ? "Réclamer" : "En cours");
        }

        private static string PeriodTag(string period)
        {
            switch (period)
            {
                case "daily": return "Quotidien";
                case "weekly": return "Hebdo";
                case "seasonal": return "Saison";
                default: return period;
            }
        }
    }
}
