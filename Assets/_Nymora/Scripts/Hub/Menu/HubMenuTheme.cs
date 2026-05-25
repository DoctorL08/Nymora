using TMPro;
using UnityEngine;

namespace Nymora.Hub.Menu
{
    /// <summary>
    /// M0 — Thème central du nouveau menu hub (refonte UI "Échap").
    ///
    /// Règle sacrée n°1 du projet : ZÉRO valeur magique en dur -> toute la palette,
    /// les métriques et les fonts passent par cet unique ScriptableObject.
    /// Un seul asset attendu : Assets/_Nymora/ScriptableObjects/Settings/HubMenuTheme.asset
    /// (créé + peuplé par Nymora > Setup > UI Menu > Create Theme + Style Preview).
    ///
    /// Identité visuelle (décisions Lorenzo 24 mai) : monochrome quasi pur, fonds
    /// gris/anthracite translucides par-dessus le hub assombri, font Ari W9500 SDF.
    /// </summary>
    [CreateAssetMenu(menuName = "Nymora/UI/Hub Menu Theme", fileName = "HubMenuTheme")]
    public sealed class HubMenuTheme : ScriptableObject
    {
        [Header("Fonts (Ari W9500 SDF)")]
        public TMP_FontAsset Font;
        public TMP_FontAsset FontBold;

        [Header("Backdrop / surfaces")]
        public Color Backdrop = new Color(0f, 0f, 0f, 0.82f);
        public Color PanelBg = new Color(0.10f, 0.105f, 0.12f, 0.96f);
        public Color TabBarBg = new Color(0.07f, 0.072f, 0.085f, 0.98f);
        public Color CardBg = new Color(0.16f, 0.165f, 0.185f, 0.96f);
        public Color CardBgHover = new Color(0.22f, 0.225f, 0.25f, 0.98f);
        public Color Divider = new Color(1f, 1f, 1f, 0.08f);

        [Header("Texte")]
        public Color TextPrimary = new Color(0.93f, 0.94f, 0.96f, 1f);
        public Color TextSecondary = new Color(0.66f, 0.67f, 0.71f, 1f);
        public Color TextMuted = new Color(0.45f, 0.46f, 0.50f, 1f);
        public Color TextOnLight = new Color(0.08f, 0.085f, 0.10f, 1f);

        [Header("Boutons / accents (monochrome)")]
        public Color Accent = new Color(0.93f, 0.94f, 0.96f, 1f);        // soulignement onglet actif + CTA clair
        public Color ButtonGhostBg = new Color(1f, 1f, 1f, 0.06f);
        public Color ButtonGhostBgHover = new Color(1f, 1f, 1f, 0.12f);

        [Header("Métriques (px)")]
        public float CornerRadius = 14f;
        public Vector2 CardSize = new Vector2(250f, 460f);
        public float CardFooterHeight = 150f;
        public float CardSpacing = 34f;
        public float TabHeight = 64f;
        public float TabSpacing = 10f;
        public float TabIconSize = 38f;
        public float ContentPadding = 32f;
        public float HintBarHeight = 34f;
        public float AnimDuration = 0.18f;

        [Header("Tailles de police (pt)")]
        public float FontSizeTitle = 34f;
        public float FontSizeHeader = 24f;
        public float FontSizeBody = 19f;
        public float FontSizeSmall = 16f;
        public float FontSizeCardTitle = 26f;
    }
}
