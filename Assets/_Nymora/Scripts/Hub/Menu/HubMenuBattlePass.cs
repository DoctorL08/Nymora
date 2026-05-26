using System;
using Nymora.Core.Audio;
using Nymora.Network.Backend;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Nymora.Hub.Menu
{
    /// <summary>
    /// M7b — Écran Battle Pass du nouveau menu hub (carte "Battle Pass" de l'accueil).
    ///
    /// Scroll horizontal de paliers : ligne du haut = piste GRATUITE, ligne du bas = piste
    /// PREMIUM (verrouillée tant que le joueur n'a pas le premium — non achetable pour l'instant).
    /// En-tête saison / palier / XP. Une cellule réclamable se clique pour réclamer (SFX RewardClaim).
    ///
    /// Réutilise 100% l'API NymoraApiClient du HubBattlePassPanel (zéro endpoint dupliqué). 100% View.
    /// </summary>
    public sealed class HubMenuBattlePass
    {
        private enum CellState { Empty, Claimed, Claimable, Locked, PremiumLocked }

        private readonly HubMenuTheme _t;
        private readonly HubMenuUIFactory _f;
        private readonly NymoraApiClient _api;

        private static readonly Color ClaimedColor = new Color(0.27f, 0.50f, 0.34f, 0.55f);
        private static readonly Color LockedColor = new Color(1f, 1f, 1f, 0.05f);
        private static readonly Color PremiumColor = new Color(1f, 0.62f, 0.18f, 1f);

        private const float CellSize = 256f;
        private const float ColWidth = 274f;
        private const float TierGap = 48f; // espace central (numéro de palier) entre les 2 cellules

        private TextMeshProUGUI _header;
        private RectTransform _content; // contenu du scroll horizontal
        private bool _busy;

        public HubMenuBattlePass(HubMenuTheme t, HubMenuUIFactory f, NymoraApiClient api)
        {
            _t = t; _f = f; _api = api;
        }

        public void Build(RectTransform parent)
        {
            // En-tête (saison / palier / XP)
            _header = _f.MakeText("Header", parent, "Chargement du Battle Pass...", _t.FontSizeBody, _t.TextPrimary, _t.Font, TextAlignmentOptions.Center);
            _header.raycastTarget = false; _header.enableWordWrapping = false;
            var hrt = _header.rectTransform;
            hrt.anchorMin = new Vector2(0f, 1f); hrt.anchorMax = new Vector2(1f, 1f); hrt.pivot = new Vector2(0.5f, 1f);
            hrt.sizeDelta = new Vector2(-48f, 30f); hrt.anchoredPosition = new Vector2(0f, -16f);

            // Gouttière gauche : libellés Gratuit / Premium alignés sur les 2 rangées
            float rowBlock = CellSize + TierGap + CellSize; // free + tier + premium
            var gutter = _f.MakeRect("Gutter", parent);
            gutter.anchorMin = new Vector2(0f, 0.5f); gutter.anchorMax = new Vector2(0f, 0.5f); gutter.pivot = new Vector2(0f, 0.5f);
            gutter.sizeDelta = new Vector2(90f, rowBlock); gutter.anchoredPosition = new Vector2(20f, -16f);

            MakeGutterLabel(gutter, "GRATUIT", 1f, _t.TextSecondary);
            MakeGutterLabel(gutter, "PREMIUM", 0f, PremiumColor);

            // Scroll horizontal des paliers (gouttière 120 à gauche, marge 24 à droite ;
            // bande de hauteur rowBlock centrée verticalement, légèrement remontée sous l'en-tête)
            float bandH = rowBlock + 16f;
            var vp = _f.MakeRect("Scroll", parent);
            vp.anchorMin = new Vector2(0f, 0.5f); vp.anchorMax = new Vector2(1f, 0.5f); vp.pivot = new Vector2(0.5f, 0.5f);
            vp.offsetMin = new Vector2(120f, -bandH * 0.5f - 16f);
            vp.offsetMax = new Vector2(-24f, bandH * 0.5f - 16f);
            var vpImg = vp.gameObject.AddComponent<Image>(); vpImg.color = new Color(1f, 1f, 1f, 0.02f);
            vp.gameObject.AddComponent<RectMask2D>();
            var sr = vp.gameObject.AddComponent<ScrollRect>();
            sr.horizontal = true; sr.vertical = false; sr.scrollSensitivity = 32f; sr.viewport = vp;

            _content = _f.MakeRect("Content", vp);
            _content.anchorMin = new Vector2(0f, 0f); _content.anchorMax = new Vector2(0f, 1f); _content.pivot = new Vector2(0f, 0.5f);
            _content.anchoredPosition = Vector2.zero; _content.sizeDelta = Vector2.zero;
            var hlg = _content.gameObject.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 8f; hlg.padding = new RectOffset(8, 8, 6, 6); hlg.childAlignment = TextAnchor.MiddleLeft;
            hlg.childControlWidth = true; hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false; hlg.childForceExpandHeight = true;
            var fit = _content.gameObject.AddComponent<ContentSizeFitter>();
            fit.horizontalFit = ContentSizeFitter.FitMode.PreferredSize; fit.verticalFit = ContentSizeFitter.FitMode.Unconstrained;
            sr.content = _content;

            LoadAsync();
        }

        private void MakeGutterLabel(RectTransform gutter, string text, float anchorY, Color color)
        {
            var lbl = _f.MakeText("G_" + text, gutter, text, _t.FontSizeSmall, color, _t.FontBold, TextAlignmentOptions.Center);
            lbl.raycastTarget = false; lbl.enableWordWrapping = false;
            var rt = lbl.rectTransform;
            rt.anchorMin = new Vector2(0f, anchorY); rt.anchorMax = new Vector2(1f, anchorY); rt.pivot = new Vector2(0.5f, anchorY);
            rt.sizeDelta = new Vector2(0f, CellSize);
            rt.anchoredPosition = new Vector2(0f, anchorY > 0.5f ? 0f : 0f);
        }

        private async void LoadAsync()
        {
            if (!EnsureToken()) { SetHeader("Non connecté."); return; }
            var res = await _api.GetBattlePassAsync();
            if (_content == null) return;
            if (!res.IsSuccess) { SetHeader($"Erreur Battle Pass ({res.StatusCode})."); return; }
            Render(res.Data);
        }

        private void Render(BattlePassMeResponse d)
        {
            int xpInTier = d.xpPerTier > 0 ? d.xp % d.xpPerTier : 0;
            int toNext = d.tier >= d.maxTier ? 0 : d.xpPerTier - xpInTier;
            string premiumTag = d.hasPremium ? "" : "      <color=#ff9d2e>● Premium verrouillé</color>";
            SetHeader($"<b>Saison {d.seasonNumber}</b>      Palier <b>{d.tier}</b>/{d.maxTier}      " +
                      $"{(d.tier >= d.maxTier ? "MAX" : $"{toNext} XP → palier suivant")}{premiumTag}");

            ClearContent();
            if (d.tiers == null) return;

            foreach (var tierDto in d.tiers)
            {
                bool isCurrent = tierDto.tier == d.tier;
                var freeState = StateOf(tierDto.free, tierDto.tier, d.tier, Contains(d.claimedFree, tierDto.tier), false, d.hasPremium);
                var premState = StateOf(tierDto.premium, tierDto.tier, d.tier, Contains(d.claimedPremium, tierDto.tier), true, d.hasPremium);
                SpawnColumn(tierDto.tier, isCurrent, tierDto.free, freeState, tierDto.premium, premState);
            }
        }

        private void SpawnColumn(int tier, bool isCurrent, BpRewardDto free, CellState freeState, BpRewardDto prem, CellState premState)
        {
            var col = _f.MakeRect("Tier_" + tier, _content);
            col.gameObject.AddComponent<LayoutElement>().preferredWidth = ColWidth;

            // Surbrillance palier courant
            if (isCurrent)
            {
                var hl = _f.MakeImage("Current", col, new Color(_t.Accent.r, _t.Accent.g, _t.Accent.b, 0.12f));
                HubMenuUIFactory.Stretch(hl.rectTransform, -2f, -2f, -2f, -2f);
                hl.raycastTarget = false;
            }

            BuildCell(col, true, free, freeState, tier, "free");

            // Numéro de palier au centre
            var num = _f.MakeText("Num", col, tier.ToString(), _t.FontSizeBody, isCurrent ? _t.Accent : _t.TextSecondary, _t.FontBold, TextAlignmentOptions.Center);
            num.raycastTarget = false;
            var nrt = num.rectTransform;
            nrt.anchorMin = new Vector2(0.5f, 0.5f); nrt.anchorMax = new Vector2(0.5f, 0.5f); nrt.pivot = new Vector2(0.5f, 0.5f);
            nrt.sizeDelta = new Vector2(ColWidth, 28f); nrt.anchoredPosition = Vector2.zero;

            BuildCell(col, false, prem, premState, tier, "premium");
        }

        private void BuildCell(RectTransform col, bool top, BpRewardDto reward, CellState state, int tier, string track)
        {
            Color boxColor;
            switch (state)
            {
                case CellState.Claimed: boxColor = ClaimedColor; break;
                case CellState.Claimable: boxColor = new Color(_t.Accent.r, _t.Accent.g, _t.Accent.b, 0.30f); break;
                case CellState.Empty: boxColor = new Color(1f, 1f, 1f, 0.03f); break;
                default: boxColor = LockedColor; break;
            }

            var box = _f.MakeImage("Cell", col, boxColor);
            var brt = box.rectTransform;
            brt.anchorMin = new Vector2(0.5f, top ? 1f : 0f); brt.anchorMax = new Vector2(0.5f, top ? 1f : 0f); brt.pivot = new Vector2(0.5f, top ? 1f : 0f);
            brt.sizeDelta = new Vector2(CellSize, CellSize);
            brt.anchoredPosition = new Vector2(0f, top ? -2f : 2f);

            // Bordure réclamable (accent) pour attirer l'œil
            if (state == CellState.Claimable)
            {
                var rim = _f.MakeImage("Rim", col, _t.Accent);
                var rrt = rim.rectTransform;
                rrt.anchorMin = brt.anchorMin; rrt.anchorMax = brt.anchorMax; rrt.pivot = brt.pivot;
                rrt.sizeDelta = new Vector2(CellSize + 4f, CellSize + 4f); rrt.anchoredPosition = brt.anchoredPosition;
                rim.raycastTarget = false;
                rim.transform.SetAsFirstSibling();
            }

            if (state == CellState.Empty) return;

            // Filigrane de récompense (icône Nymos pour les Nymos, icône de type pour les
            // cosmétiques) DERRIÈRE le label -> repère visuel rapide du contenu de la cellule.
            string rewardIcon = IconForReward(reward);
            if (rewardIcon != null)
            {
                var rsp = HubMenuUIFactory.LoadIcon(rewardIcon);
                if (rsp != null)
                {
                    bool dim = state == CellState.Locked || state == CellState.PremiumLocked;
                    var ric = _f.MakeImage("RewardIcon", brt, new Color(1f, 1f, 1f, dim ? 0.12f : 0.26f), rounded: false);
                    ric.sprite = rsp; ric.type = Image.Type.Simple; ric.preserveAspect = true; ric.raycastTarget = false;
                    var rrt = ric.rectTransform;
                    rrt.anchorMin = rrt.anchorMax = new Vector2(0.5f, 0.5f); rrt.pivot = new Vector2(0.5f, 0.5f);
                    rrt.sizeDelta = new Vector2(118f, 118f); rrt.anchoredPosition = new Vector2(0f, 6f);
                }
            }

            // Label de récompense
            string label = string.IsNullOrEmpty(reward?.label) ? "" : reward.label;
            var txt = _f.MakeText("L", brt, label, _t.FontSizeSmall, state == CellState.Locked || state == CellState.PremiumLocked ? _t.TextMuted : _t.TextPrimary, _t.Font, TextAlignmentOptions.Center);
            txt.raycastTarget = false; txt.enableWordWrapping = true; txt.overflowMode = TextOverflowModes.Ellipsis;
            HubMenuUIFactory.Stretch(txt.rectTransform, 8f, 8f, 22f, 22f);

            // Pastille d'état (haut de la cellule). Pas de glyphe ✓ (tofu en police Ari) : l'icône
            // check est déjà affichée dans le coin de la cellule pour l'état Réclamé.
            string badge = state == CellState.Claimed ? "<color=#7CFC7C>Réclamé</color>"
                         : state == CellState.Claimable ? "<color=#fff2c0>Réclamer</color>"
                         : state == CellState.PremiumLocked ? "<color=#ff9d2e>Premium</color>"
                         : "<color=#8a8f99>Verrouillé</color>";
            var bdg = _f.MakeText("Badge", brt, badge, _t.FontSizeSmall, _t.TextSecondary, _t.Font, TextAlignmentOptions.Center);
            bdg.raycastTarget = false; bdg.enableWordWrapping = false; bdg.fontSize = _t.FontSizeSmall * 0.9f;
            var brt2 = bdg.rectTransform;
            brt2.anchorMin = new Vector2(0f, 0f); brt2.anchorMax = new Vector2(1f, 0f); brt2.pivot = new Vector2(0.5f, 0f);
            brt2.sizeDelta = new Vector2(0f, 20f); brt2.anchoredPosition = new Vector2(0f, 6f);

            // Icône d'angle : check (réclamé) / premium (verrouillé faute de premium) / cadenas (palier non atteint)
            string cornerIcon = state == CellState.Claimed ? "ui_icon_check"
                              : state == CellState.PremiumLocked ? "ui_icon_premium"
                              : state == CellState.Locked ? "ui_icon_lock" : null;
            if (cornerIcon != null)
            {
                var sp = HubMenuUIFactory.LoadIcon(cornerIcon);
                if (sp != null)
                {
                    Color tint = state == CellState.Claimed ? new Color(0.55f, 0.95f, 0.60f, 1f)
                               : state == CellState.PremiumLocked ? PremiumColor : _t.TextMuted;
                    var ic = _f.MakeImage("StateIcon", brt, tint, rounded: false);
                    ic.sprite = sp; ic.type = Image.Type.Simple; ic.preserveAspect = true; ic.raycastTarget = false;
                    var irt = ic.rectTransform;
                    irt.anchorMin = new Vector2(1f, 1f); irt.anchorMax = new Vector2(1f, 1f); irt.pivot = new Vector2(1f, 1f);
                    irt.sizeDelta = new Vector2(26f, 26f); irt.anchoredPosition = new Vector2(-8f, -8f);
                }
            }

            if (state == CellState.Claimable)
            {
                var btn = box.gameObject.AddComponent<Button>();
                btn.targetGraphic = box;
                int t2 = tier; string tr = track;
                btn.onClick.AddListener(() => Claim(t2, tr));
            }
        }

        private async void Claim(int tier, string track)
        {
            if (_busy || !EnsureToken()) return;
            _busy = true;
            var res = await _api.ClaimBattlePassTierAsync(tier, track);
            _busy = false;
            if (res.IsSuccess && res.Data.status == "claimed")
            {
                NymoraAudioManager.Instance?.PlaySfx(SoundId.RewardClaim, NymoraAudioManager.SoftUiVolume);
            }
            LoadAsync();
        }

        // ===== Helpers état (calqués sur HubBattlePassPanel) =====

        private static bool IsReal(BpRewardDto r) => r != null && !string.IsNullOrEmpty(r.kind);

        /// <summary>Icône représentant une récompense : Nymos pour les Nymos, icône de type
        /// (déduite du libellé) pour les cosmétiques. Le DTO BP ne porte que kind+label.</summary>
        private static string IconForReward(BpRewardDto r)
        {
            if (r == null || string.IsNullOrEmpty(r.kind)) return null;
            if (r.kind == "nymos") return "ui_icon_nymos";
            if (r.kind != "cosmetic") return null;
            string l = r.label ?? "";
            if (l.StartsWith("Titre")) return "ui_icon_type_title";
            if (l.StartsWith("Bannière") || l.StartsWith("Banniere")) return "ui_icon_type_banner";
            if (l.StartsWith("Emote")) return "ui_icon_type_emote";
            if (l.StartsWith("Skin")) return "ui_icon_type_skin";
            return "ui_icon_type_skin"; // cosmétique générique
        }

        private static CellState StateOf(BpRewardDto r, int tier, int currentTier, bool claimed, bool isPremium, bool hasPremium)
        {
            if (!IsReal(r)) return CellState.Empty;
            if (claimed) return CellState.Claimed;
            if (isPremium && !hasPremium) return CellState.PremiumLocked;
            if (tier <= currentTier) return CellState.Claimable;
            return CellState.Locked;
        }

        private static bool Contains(int[] arr, int v) => arr != null && Array.IndexOf(arr, v) >= 0;

        private void ClearContent()
        {
            if (_content == null) return;
            for (int i = _content.childCount - 1; i >= 0; i--) UnityEngine.Object.Destroy(_content.GetChild(i).gameObject);
        }

        private bool EnsureToken()
        {
            string token = HubChatClient.Instance?.DevToken;
            if (_api == null || string.IsNullOrEmpty(token)) return false;
            _api.SetBearerToken(token);
            return true;
        }

        private void SetHeader(string s) { if (_header != null) _header.text = s; }
    }
}
