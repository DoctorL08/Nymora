using System;
using Nymora.Network.Backend;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Nymora.Hub
{
    /// <summary>
    /// Brique 5.5.c — une cellule de la grille boutique : vignette + nom (coloré rareté) +
    /// prix (Nymos/Shards) + bouton Acheter / Possédé. Template cloné par HubShopPanel.
    ///
    /// L'équipement (depuis l'inventaire) arrive en 5.5.d ; ici on se limite à browse + achat.
    /// </summary>
    public sealed class ShopItemCell : MonoBehaviour
    {
        [SerializeField] private Image _preview;
        [SerializeField] private TMP_Text _nameText;
        [SerializeField] private TMP_Text _priceText;
        [SerializeField] private Button _buyButton;
        [SerializeField] private TMP_Text _buyLabel;

        // Icônes monnaie alignées sur HubWalletWidget (LiberationSans SDF = pas d'emoji).
        private const string NymosIcon = "◆";
        private const string ShardsIcon = "◇";

        public string ItemId { get; private set; }
        /// <summary>(itemId, currency "Nymos"|"Shards").</summary>
        public event Action<string, string> OnBuy;

        private string _buyCurrency;

        private void Awake()
        {
            if (_buyButton != null) _buyButton.onClick.AddListener(() => OnBuy?.Invoke(ItemId, _buyCurrency));
        }

        public void Bind(ShopItemDto item)
        {
            ItemId = item.id;

            if (_nameText != null)
            {
                _nameText.text = item.name;
                _nameText.color = RarityColor(item.rarity);
            }

            if (_preview != null)
            {
                var sprite = Resources.Load<Sprite>(item.previewKey);
                _preview.sprite = sprite;
                _preview.enabled = sprite != null;
            }

            // Monnaie : Shards prioritaire si défini, sinon Nymos (le catalogue MVP n'a qu'une monnaie/item).
            bool hasShards = item.priceShards > 0;
            _buyCurrency = hasShards ? "Shards" : "Nymos";
            int price = hasShards ? item.priceShards : item.priceNymos;
            string icon = hasShards ? ShardsIcon : NymosIcon;

            bool owned = item.owned;
            if (_buyButton != null) _buyButton.interactable = !owned;
            if (_priceText != null)
            {
                _priceText.text = owned ? "" : $"{icon} {price}";
                _priceText.color = hasShards ? new Color(0.55f, 0.88f, 1f) : new Color(0.96f, 0.86f, 0.40f);
            }
            if (_buyLabel != null)
                _buyLabel.text = owned ? (item.equipped ? "✓ Équipé" : "Possédé") : "Acheter";
        }

        private static Color RarityColor(string rarity)
        {
            switch (rarity)
            {
                case "legendary": return new Color(1f, 0.65f, 0.20f);   // orange
                case "epic":      return new Color(0.78f, 0.45f, 1f);   // violet
                case "rare":      return new Color(0.40f, 0.70f, 1f);   // bleu
                default:           return new Color(0.85f, 0.85f, 0.88f); // common gris
            }
        }
    }
}
