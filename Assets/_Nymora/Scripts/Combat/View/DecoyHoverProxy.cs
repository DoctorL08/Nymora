using Quantum;
using TMPro;
using UnityEngine;

namespace Nymora.Combat.View
{
    /// <summary>
    /// Marqueur attaché aux GameObjects leurre Ghostra par DecoyView. Permet à TileHoverView de
    /// détecter le survol d'un leurre.
    ///
    /// Patch 5 juin (choix Lorenzo) : au survol, on n'affiche plus le tooltip du vrai Ghostra mais
    /// le MÊME label d'HP « HP/MaxHP » que les piliers du Colossar (cf ObstacleView) — petit
    /// TextMeshPro world-space avec fond sombre, révélé au survol. Visible par tout le monde
    /// (l'indiscernabilité Bible est volontairement abandonnée ici).
    /// </summary>
    public sealed class DecoyHoverProxy : MonoBehaviour
    {
        /// <summary>Entity du Ghostra parent (conservée pour debug / éventuels usages).</summary>
        public EntityRef GhostraParentEntity { get; set; }

        /// <summary>Index du slot de leurre (0..2) sur le Ghostra parent.</summary>
        public int SlotIndex { get; set; }

        private int _hp;
        private int _maxHp;
        private bool _hovered;

        // --- Style IDENTIQUE à ObstacleView (label HP des piliers) ---
        private const float HpFontSize = 1.6f;
        private const float HpOutlineWidth = 0.3f;
        private const float HpBackgroundPadX = 0.22f;
        private const float HpBackgroundPadY = 0.12f;
        private const float HpLocalY = 1.45f; // au-dessus du leurre (× scale leurre)
        private static readonly Color HpTextColor = new Color(1f, 0.96f, 0.75f, 1f);
        private static readonly Color HpOutlineColor = new Color(0f, 0f, 0f, 1f);
        private static readonly Color HpBackgroundColor = new Color(0f, 0f, 0f, 0.7f);

        private TextMeshPro _hpLabel;
        private SpriteRenderer _hpBg;
        private static Sprite _solidSprite;

        /// <summary>Met à jour les HP affichés (poussé chaque frame par DecoyView).</summary>
        public void SetHp(int hp, int maxHp)
        {
            _hp = hp;
            _maxHp = maxHp;
            if (_hovered) RefreshLabel();
        }

        /// <summary>Affiche / masque le label d'HP (piloté par TileHoverView selon le survol).</summary>
        public void SetHovered(bool hovered)
        {
            if (_hovered == hovered) return;
            _hovered = hovered;
            if (_hovered)
            {
                EnsureLabel();
                if (_hpLabel != null) { _hpLabel.gameObject.SetActive(true); RefreshLabel(); }
            }
            else if (_hpLabel != null)
            {
                _hpLabel.gameObject.SetActive(false);
            }
        }

        private void OnDisable()
        {
            // Leurre détruit/désactivé en plein survol -> on coupe l'état hover.
            _hovered = false;
            if (_hpLabel != null) _hpLabel.gameObject.SetActive(false);
        }

        private void EnsureLabel()
        {
            if (_hpLabel != null) return;

            var go = new GameObject("DecoyHpLabel");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, HpLocalY, 0f);
            _hpLabel = go.AddComponent<TextMeshPro>();
            _hpLabel.alignment = TextAlignmentOptions.Center;
            _hpLabel.fontSize = HpFontSize;
            _hpLabel.color = HpTextColor;
            _hpLabel.fontStyle |= FontStyles.Bold;
            // RectTransform du TMP : petite zone, pas de wrap.
            _hpLabel.rectTransform.sizeDelta = new Vector2(4f, 1.2f);
            _hpLabel.enableWordWrapping = false;
            // Contour via matériau instancié (ne déborde pas sur les autres TMP).
            _ = _hpLabel.fontMaterial;
            _hpLabel.outlineColor = HpOutlineColor;
            _hpLabel.outlineWidth = HpOutlineWidth;

            // Fond sombre dimensionné sur le texte (cf ObstacleView).
            var bgGo = new GameObject("HPBackground");
            bgGo.transform.SetParent(_hpLabel.transform, false);
            bgGo.transform.localPosition = Vector3.zero;
            _hpBg = bgGo.AddComponent<SpriteRenderer>();
            _hpBg.sprite = GetSolidSprite();
            _hpBg.color = HpBackgroundColor;
        }

        private void RefreshLabel()
        {
            if (_hpLabel == null) return;
            _hpLabel.text = $"{_hp}/{_maxHp}";

            // Tri : au-dessus du sprite du leurre (lu sur son SpriteRenderer).
            var leurreSr = GetComponent<SpriteRenderer>();
            int order = leurreSr != null ? leurreSr.sortingOrder : 5;
            string layerName = leurreSr != null ? leurreSr.sortingLayerName : "Default";
            _hpLabel.sortingLayerID = SortingLayer.NameToID(layerName);
            _hpLabel.sortingOrder = order + 3;
            if (_hpBg != null)
            {
                _hpBg.sortingLayerID = _hpLabel.sortingLayerID;
                _hpBg.sortingOrder = _hpLabel.sortingOrder - 1;
                Vector2 pref = _hpLabel.GetPreferredValues();
                _hpBg.transform.localScale = new Vector3(pref.x + HpBackgroundPadX, pref.y + HpBackgroundPadY, 1f);
            }
        }

        // Sprite blanc plein 1x1 unité monde (cache static), tinté via SpriteRenderer.color.
        private static Sprite GetSolidSprite()
        {
            if (_solidSprite != null) return _solidSprite;
            const int Size = 4;
            var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false) { name = "DecoyHpBgTex" };
            var pixels = new Color32[Size * Size];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color32(255, 255, 255, 255);
            tex.SetPixels32(pixels);
            tex.Apply(false, false);
            _solidSprite = Sprite.Create(tex, new Rect(0, 0, Size, Size), new Vector2(0.5f, 0.5f), Size);
            _solidSprite.name = "DecoyHpBgSprite";
            return _solidSprite;
        }
    }
}
