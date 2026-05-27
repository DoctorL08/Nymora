using System;
using System.Collections.Generic;
using Nymora.Core.Audio;
using Nymora.Core.ScriptableObjects;
using Nymora.Network.Backend;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Nymora.Hub.Menu
{
    /// <summary>
    /// M7a — Boutique « vitrine » du nouveau menu hub (carte "Boutique" de l'accueil).
    ///
    /// Quasi plein écran. Articles regroupés par catégorie (Skins / Titres / Bannières / Emotes),
    /// chaque catégorie en grille de CARTES : grand visuel, cadre teinté selon la rareté (effet
    /// vitrine), nom + rareté, bouton d'achat par devise (◆ Nymos / ◇ Shards) ou bandeau
    /// « Possédé ». Achat via BuyItemAsync (SFX PurchaseSuccess) ; le wallet haut-droite du menu
    /// se met à jour via le push WS WALLET_UPDATE. En-tête = info rotation.
    ///
    /// Réutilise 100% l'API NymoraApiClient du HubShopPanel (zéro endpoint dupliqué). 100% View.
    /// </summary>
    public sealed class HubMenuShop
    {
        private readonly HubMenuTheme _t;
        private readonly HubMenuUIFactory _f;
        private readonly NymoraApiClient _api;

        private static readonly Color NymosColor = new Color(0.96f, 0.82f, 0.40f, 1f);
        private static readonly Color ShardsColor = new Color(0.55f, 0.88f, 1f, 1f);
        private static readonly Color CardBgColor = new Color(0.07f, 0.07f, 0.09f, 0.98f);

        private static readonly Vector2 CellSize = new Vector2(264f, 380f);

        // Ordre + libellés des catégories (clé type backend).
        private static readonly string[] CatKeys = { "skin", "pet", "title", "banner", "emote" };
        private static readonly string[] CatLabels = { "Skins", "Familiers", "Titres", "Bannières", "Emotes" };

        private RectTransform _listRoot;
        private TextMeshProUGUI _status;
        private bool _busy;
        // 5.11 — tooltip de prévisu animée (survol skin/familier).
        private CosmeticPreviewTooltip _preview;
        private static CosmeticSkinCatalog _skinCatalog;
        private static bool _skinCatalogLoaded;

        public HubMenuShop(HubMenuTheme t, HubMenuUIFactory f, NymoraApiClient api)
        {
            _t = t; _f = f; _api = api;
        }

        // Familiers : preview = 1re frame idle SE de la PetDefinition (pas de sprite Resources
        // dédié). PetCatalog chargé une fois depuis Resources (partagé hub/combat).
        private static PetCatalog _petCatalog;
        private static bool _petCatalogLoaded;

        private static Sprite PetPreviewSprite(string cosmeticId)
        {
            if (!_petCatalogLoaded)
            {
                _petCatalog = Resources.Load<PetCatalog>("Cosmetics/PetCatalog");
                _petCatalogLoaded = true;
            }
            var def = _petCatalog != null ? _petCatalog.Resolve(cosmeticId) : null;
            if (def != null && def.IdleFrames != null && def.IdleFrames.Length > 0) return def.IdleFrames[0];
            return null;
        }

        private static PetDefinition PetDef(string cosmeticId)
        {
            if (!_petCatalogLoaded) { _petCatalog = Resources.Load<PetCatalog>("Cosmetics/PetCatalog"); _petCatalogLoaded = true; }
            return _petCatalog != null ? _petCatalog.Resolve(cosmeticId) : null;
        }

        private static CosmeticSkinDefinition SkinDef(string cosmeticId)
        {
            if (!_skinCatalogLoaded) { _skinCatalog = Resources.Load<CosmeticSkinCatalog>("Cosmetics/CosmeticSkinCatalog"); _skinCatalogLoaded = true; }
            return _skinCatalog != null ? _skinCatalog.Resolve(cosmeticId) : null;
        }

        // Icône œil générée procéduralement (aucun asset dédié) : lentille en amande claire + pupille
        // sombre + contour. Mise en cache (1 seule texture pour toutes les cartes).
        private static Sprite _eyeSprite;
        private static Sprite EyeSprite()
        {
            if (_eyeSprite != null) return _eyeSprite;
            const int S = 64;
            var tex = new Texture2D(S, S, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
            var clear = new Color(0f, 0f, 0f, 0f);
            var light = new Color(0.93f, 0.94f, 0.97f, 1f);
            var dark = new Color(0.06f, 0.06f, 0.09f, 1f);
            float cx = (S - 1) * 0.5f, cy = (S - 1) * 0.5f, rx = 29f, ry = 16f;
            for (int y = 0; y < S; y++)
            {
                for (int x = 0; x < S; x++)
                {
                    float nx = (x - cx) / rx, ny = (y - cy) / ry;
                    float e = nx * nx + ny * ny;          // <=1 dans la lentille
                    Color col = clear;
                    if (e <= 1f)
                    {
                        col = e > 0.80f ? dark : light;   // contour sombre de la lentille
                        float d = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
                        if (d <= 9f) col = dark;           // pupille
                        else if (d <= 11.5f) col = light;  // liseré autour de la pupille
                    }
                    tex.SetPixel(x, y, col);
                }
            }
            tex.Apply();
            _eyeSprite = Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), 100f);
            return _eyeSprite;
        }

        // Ouvre le tooltip de prévisu pour la carte survolée (skin ou familier).
        private void ShowPreview(string type, string id, RectTransform card)
        {
            if (_preview == null) return;
            if (type == "skin") { var d = SkinDef(id); if (d != null) _preview.ShowForSkin(d, card); }
            else if (type == "pet") { var d = PetDef(id); if (d != null) _preview.ShowForPet(d, card); }
        }

        public void Build(RectTransform parent)
        {
            _status = _f.MakeText("Status", parent, "Chargement...", _t.FontSizeSmall, _t.TextMuted, _t.Font, TextAlignmentOptions.Center);
            _status.raycastTarget = false;
            var srt = _status.rectTransform;
            srt.anchorMin = new Vector2(0f, 1f); srt.anchorMax = new Vector2(1f, 1f); srt.pivot = new Vector2(0.5f, 1f);
            srt.sizeDelta = new Vector2(-48f, 24f); srt.anchoredPosition = new Vector2(0f, -14f);

            _preview = new CosmeticPreviewTooltip(_t, _f, parent);
            BuildScroll(parent);
            LoadAsync();
        }

        private async void LoadAsync()
        {
            if (!EnsureToken()) { SetStatus("Non connecté."); return; }
            var res = await _api.GetShopAsync();
            if (_listRoot == null) return;
            if (!res.IsSuccess) { SetStatus($"Erreur boutique ({res.StatusCode})."); return; }

            ClearList();
            var items = res.Data.items ?? Array.Empty<ShopItemDto>();

            // Regroupe par catégorie, dans l'ordre défini ; les types inconnus vont en "Autres".
            int shown = 0;
            for (int c = 0; c < CatKeys.Length; c++)
            {
                var inCat = new List<ShopItemDto>();
                foreach (var it in items) if (it.type == CatKeys[c]) inCat.Add(it);
                if (inCat.Count == 0) continue;
                SpawnCategoryHeader(CatLabels[c], CatKeys[c]);
                var grid = SpawnGridContainer();
                foreach (var it in inCat) SpawnCard(grid, it);
                shown += inCat.Count;
            }
            // Catégorie fourre-tout pour tout type non listé.
            var others = new List<ShopItemDto>();
            foreach (var it in items) { bool known = false; foreach (var k in CatKeys) if (it.type == k) { known = true; break; } if (!known) others.Add(it); }
            if (others.Count > 0)
            {
                SpawnCategoryHeader("Autres");
                var grid = SpawnGridContainer();
                foreach (var it in others) SpawnCard(grid, it);
                shown += others.Count;
            }

            string rot = res.Data.rotation != null ? $"Rotation renouvelée dans {DaysUntil(res.Data.rotation.endsAt)}" : "";
            SetStatus(shown == 0 ? "Aucun article en vente pour l'instant." : rot);
        }

        private void SpawnCard(RectTransform parent, ShopItemDto item)
        {
            Color rarity = RarityColor(item.rarity);

            // Cadre teinté rareté (= cellule de la grille). Le contenu sombre est posé dessus.
            var frame = _f.MakeImage("Card_" + item.id, parent, rarity);
            var bg = _f.MakeImage("BG", frame.rectTransform, CardBgColor);
            HubMenuUIFactory.Stretch(bg.rectTransform, 3f, 3f, 3f, 3f);
            var root = bg.rectTransform;

            // 5.11 — bouton œil en haut à droite (skin/familier) -> ouvre la fenêtre de prévisu
            // animée (stages + anims). Plus fiable que le survol pour naviguer/cliquer.
            if (item.type == "skin" || item.type == "pet")
            {
                string pid = item.id, ptype = item.type;
                var card = frame.rectTransform;
                var eyeBtn = _f.MakeImage("EyeBtn", root, new Color(0f, 0f, 0f, 0.6f));
                var ert = eyeBtn.rectTransform;
                ert.anchorMin = ert.anchorMax = new Vector2(1f, 1f); ert.pivot = new Vector2(1f, 1f);
                ert.sizeDelta = new Vector2(42f, 42f); ert.anchoredPosition = new Vector2(-8f, -8f);
                var eyeIco = _f.MakeImage("Eye", ert, Color.white, rounded: false);
                eyeIco.sprite = EyeSprite(); eyeIco.preserveAspect = true; eyeIco.raycastTarget = false;
                HubMenuUIFactory.Stretch(eyeIco.rectTransform, 8f, 8f, 8f, 8f);
                eyeBtn.gameObject.AddComponent<Button>().onClick.AddListener(() => ShowPreview(ptype, pid, card));
            }

            // Lueur rareté en haut
            var glow = _f.MakeImage("Glow", root, new Color(rarity.r, rarity.g, rarity.b, 0.16f), rounded: false);
            var grt = glow.rectTransform;
            grt.anchorMin = new Vector2(0f, 1f); grt.anchorMax = new Vector2(1f, 1f); grt.pivot = new Vector2(0.5f, 1f);
            grt.offsetMin = new Vector2(3f, -168f); grt.offsetMax = new Vector2(-3f, -3f);
            glow.raycastTarget = false;

            // Grand visuel
            var prev = _f.MakeImage("Preview", root, new Color(1f, 1f, 1f, 0.05f), rounded: false);
            prev.preserveAspect = true; prev.raycastTarget = false;
            var prt = prev.rectTransform;
            prt.anchorMin = new Vector2(0f, 1f); prt.anchorMax = new Vector2(1f, 1f); prt.pivot = new Vector2(0.5f, 1f);
            prt.offsetMin = new Vector2(16f, -236f); prt.offsetMax = new Vector2(-16f, -16f);
            var sprite = Resources.Load<Sprite>(item.previewKey);
            // Familiers : pas de sprite Resources dédié -> 1re frame idle SE du PetCatalog.
            if (sprite == null && item.type == "pet") sprite = PetPreviewSprite(item.id);
            if (sprite != null) { prev.sprite = sprite; prev.color = Color.white; }
            else
            {
                // Pas d'asset de preview (ex : titres) -> repli sur l'icône de type, atténuée.
                var typeSp = HubMenuUIFactory.LoadIcon("ui_icon_type_" + item.type);
                if (typeSp != null) { prev.sprite = typeSp; prev.color = new Color(1f, 1f, 1f, 0.5f); }
            }

            // Nom (retour à la ligne + ellipse, contenu dans la carte)
            var name = _f.MakeText("Name", root, item.name, _t.FontSizeBody, _t.TextPrimary, _t.FontBold, TextAlignmentOptions.Center);
            name.raycastTarget = false; name.enableWordWrapping = true; name.overflowMode = TextOverflowModes.Ellipsis;
            var nrt = name.rectTransform;
            nrt.anchorMin = new Vector2(0f, 0f); nrt.anchorMax = new Vector2(1f, 0f); nrt.pivot = new Vector2(0.5f, 0f);
            nrt.offsetMin = new Vector2(10f, 98f); nrt.offsetMax = new Vector2(-10f, 144f);

            // Rareté
            var rar = _f.MakeText("Rarity", root, RarityLabel(item.rarity), _t.FontSizeSmall, _t.TextSecondary, _t.FontBold, TextAlignmentOptions.Center);
            rar.raycastTarget = false; rar.enableWordWrapping = false;
            var rrt = rar.rectTransform;
            rrt.anchorMin = new Vector2(0f, 0f); rrt.anchorMax = new Vector2(1f, 0f); rrt.pivot = new Vector2(0.5f, 0f);
            rrt.offsetMin = new Vector2(8f, 74f); rrt.offsetMax = new Vector2(-8f, 96f);

            // Bandeau achat / possédé (bas)
            var buyRow = _f.MakeRect("BuyRow", root);
            buyRow.anchorMin = new Vector2(0f, 0f); buyRow.anchorMax = new Vector2(1f, 0f); buyRow.pivot = new Vector2(0.5f, 0f);
            buyRow.offsetMin = new Vector2(12f, 14f); buyRow.offsetMax = new Vector2(-12f, 60f);
            var hlg = buyRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 8f; hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.childControlWidth = true; hlg.childControlHeight = true;
            hlg.childForceExpandWidth = true; hlg.childForceExpandHeight = true;

            if (item.owned)
            {
                var tag = _f.MakeImage("Owned", buyRow, new Color(0.27f, 0.55f, 0.36f, 0.35f));
                var ownedColor = new Color(0.7f, 0.95f, 0.75f, 1f);
                var sp = HubMenuUIFactory.LoadIcon("ui_icon_check");
                var ol = _f.MakeText("L", tag.rectTransform, sp != null ? "Possédé" : "✓ Possédé", _t.FontSizeBody, ownedColor, _t.FontBold, TextAlignmentOptions.Center);
                ol.raycastTarget = false;
                if (sp != null)
                {
                    HubMenuUIFactory.Stretch(ol.rectTransform, 22f, 4f, 0f, 0f);
                    var ck = _f.MakeImage("Check", tag.rectTransform, ownedColor, rounded: false);
                    ck.sprite = sp; ck.type = Image.Type.Simple; ck.preserveAspect = true; ck.raycastTarget = false;
                    var ckrt = ck.rectTransform;
                    ckrt.anchorMin = new Vector2(0f, 0.5f); ckrt.anchorMax = new Vector2(0f, 0.5f); ckrt.pivot = new Vector2(0f, 0.5f);
                    ckrt.sizeDelta = new Vector2(20f, 20f); ckrt.anchoredPosition = new Vector2(12f, 0f);
                }
                else HubMenuUIFactory.Stretch(ol.rectTransform);
            }
            else if (item.priceNymos > 0 || item.priceShards > 0)
            {
                if (item.priceNymos > 0) SpawnBuyButton(buyRow, item.id, "Nymos", item.priceNymos, "ui_icon_nymos", "◆", NymosColor);
                if (item.priceShards > 0) SpawnBuyButton(buyRow, item.id, "Shards", item.priceShards, "ui_icon_shards", "◇", ShardsColor);
            }
            else
            {
                var na = _f.MakeText("NA", buyRow, "<i>indisponible</i>", _t.FontSizeSmall, _t.TextMuted, _t.Font, TextAlignmentOptions.Center);
                na.raycastTarget = false;
            }
        }

        private void SpawnBuyButton(RectTransform parent, string itemId, string currency, int price, string iconName, string glyph, Color tint)
        {
            var btn = _f.MakeButton(parent, price.ToString(), false, out var lbl);
            var img = btn.GetComponent<Image>(); if (img != null) img.color = Color.white;
            var c = btn.colors;
            Color baseCol = new Color(tint.r * 0.30f, tint.g * 0.30f, tint.b * 0.30f, 1f);
            c.normalColor = baseCol;
            c.highlightedColor = new Color(tint.r * 0.45f, tint.g * 0.45f, tint.b * 0.45f, 1f);
            c.pressedColor = new Color(tint.r * 0.22f, tint.g * 0.22f, tint.b * 0.22f, 1f);
            c.selectedColor = baseCol; c.fadeDuration = 0.1f;
            btn.colors = c;
            lbl.color = tint; lbl.enableWordWrapping = false;

            // Icône de devise (ash/blood) à gauche ; fallback glyphe (◆/◇) si sprite absent.
            var sp = HubMenuUIFactory.LoadIcon(iconName);
            if (sp != null)
            {
                HubMenuUIFactory.Stretch(lbl.rectTransform, 40f, 6f, 0f, 0f);
                var icon = _f.MakeImage("CurIcon", btn.transform, Color.white, rounded: false);
                icon.sprite = sp; icon.type = Image.Type.Simple; icon.preserveAspect = true; icon.raycastTarget = false;
                var irt = icon.rectTransform;
                irt.anchorMin = new Vector2(0f, 0.5f); irt.anchorMax = new Vector2(0f, 0.5f); irt.pivot = new Vector2(0f, 0.5f);
                irt.sizeDelta = new Vector2(30f, 30f); irt.anchoredPosition = new Vector2(10f, 0f);
            }
            else
            {
                lbl.text = $"{glyph} {price}";
            }

            btn.onClick.AddListener(() => Buy(itemId, currency));
        }

        private async void Buy(string itemId, string currency)
        {
            if (_busy || !EnsureToken()) return;
            _busy = true;
            var res = await _api.BuyItemAsync(itemId, currency);
            _busy = false;
            if (res.IsSuccess && res.Data.ok)
            {
                NymoraAudioManager.Instance?.PlaySfx(SoundId.PurchaseSuccess, NymoraAudioManager.SoftUiVolume);
                SetStatus("<color=#7CFC7C>Achat effectué !</color>");
                LoadAsync();
            }
            else
            {
                string err = res.IsSuccess ? res.Data.error : res.ErrorMessage;
                SetStatus(err == "INSUFFICIENT_FUNDS" ? "<color=#ff8080>Solde insuffisant.</color>"
                        : err == "ALREADY_OWNED" ? "<color=#ff8080>Déjà possédé.</color>"
                        : $"<color=#ff8080>Achat refusé ({err}).</color>");
            }
        }

        // ===== Helpers =====

        private static Color RarityColor(string rarity)
        {
            switch (rarity)
            {
                case "legendary": return new Color(1f, 0.655f, 0.20f, 1f);
                case "epic": return new Color(0.78f, 0.45f, 1f, 1f);
                case "rare": return new Color(0.40f, 0.70f, 1f, 1f);
                default: return new Color(0.42f, 0.45f, 0.52f, 1f);
            }
        }

        private static string RarityLabel(string rarity)
        {
            switch (rarity)
            {
                case "legendary": return "<color=#ffa733>LÉGENDAIRE</color>";
                case "epic": return "<color=#c773ff>ÉPIQUE</color>";
                case "rare": return "<color=#66b3ff>RARE</color>";
                default: return "<color=#aab0bc>COMMUN</color>";
            }
        }

        private static string DaysUntil(string isoEndsAt)
        {
            if (DateTime.TryParse(isoEndsAt, null,
                System.Globalization.DateTimeStyles.AdjustToUniversal | System.Globalization.DateTimeStyles.AssumeUniversal,
                out var ends))
            {
                var span = ends - DateTime.UtcNow;
                if (span.TotalHours <= 0) return "bientôt";
                return span.TotalDays >= 1 ? $"{(int)span.TotalDays}j" : $"{(int)span.TotalHours}h";
            }
            return "—";
        }

        // ===== Liste / grille =====

        private void BuildScroll(RectTransform parent)
        {
            var vp = _f.MakeRect("Scroll", parent);
            vp.anchorMin = new Vector2(0f, 0f); vp.anchorMax = new Vector2(1f, 1f);
            vp.offsetMin = new Vector2(24f, 18f); vp.offsetMax = new Vector2(-24f, -42f);
            var vpImg = vp.gameObject.AddComponent<Image>(); vpImg.color = new Color(1f, 1f, 1f, 0.02f);
            vp.gameObject.AddComponent<RectMask2D>();
            var sr = vp.gameObject.AddComponent<ScrollRect>();
            sr.horizontal = false; sr.vertical = true; sr.scrollSensitivity = 32f; sr.viewport = vp;

            // Contenu = pile verticale de sections (en-tête + grille).
            var content = _f.MakeRect("Content", vp);
            content.anchorMin = new Vector2(0f, 1f); content.anchorMax = new Vector2(1f, 1f); content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero; content.sizeDelta = Vector2.zero;
            var vlg = content.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 8f; vlg.padding = new RectOffset(10, 10, 10, 14); vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.childControlWidth = true; vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;
            var fit = content.gameObject.AddComponent<ContentSizeFitter>();
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize; fit.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            sr.content = content;
            _listRoot = content;
        }

        private void SpawnCategoryHeader(string text, string typeKey = null)
        {
            // En-tête = icône de type (si dispo) + libellé, groupe centré.
            var holder = _f.MakeRect("Cat", _listRoot);
            holder.gameObject.AddComponent<LayoutElement>().preferredHeight = 40f;
            var hlg = holder.gameObject.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 10f; hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.childControlWidth = true; hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false; hlg.childForceExpandHeight = false;

            var sp = typeKey != null ? HubMenuUIFactory.LoadIcon("ui_icon_type_" + typeKey) : null;
            if (sp != null)
            {
                var icon = _f.MakeImage("Icon", holder, _t.TextSecondary, rounded: false);
                icon.sprite = sp; icon.type = Image.Type.Simple; icon.preserveAspect = true; icon.raycastTarget = false;
                var le = icon.gameObject.AddComponent<LayoutElement>(); le.preferredWidth = 26f; le.preferredHeight = 26f;
            }

            var tmp = _f.MakeText("Lbl", holder, text, _t.FontSizeHeader, _t.TextPrimary, _t.FontBold, TextAlignmentOptions.MidlineLeft);
            tmp.raycastTarget = false; tmp.enableWordWrapping = false;
            tmp.gameObject.AddComponent<LayoutElement>();
        }

        /// <summary>Conteneur grille pour une catégorie. Sa hauteur (préférée GridLayoutGroup) est
        /// pilotée par le VerticalLayoutGroup parent — pas de ContentSizeFitter ici.</summary>
        private RectTransform SpawnGridContainer()
        {
            var c = _f.MakeRect("Grid", _listRoot);
            var grid = c.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = CellSize;
            grid.spacing = new Vector2(20f, 20f);
            grid.padding = new RectOffset(4, 4, 4, 8);
            grid.childAlignment = TextAnchor.UpperCenter;
            grid.constraint = GridLayoutGroup.Constraint.Flexible;
            return c;
        }

        private void ClearList()
        {
            if (_listRoot == null) return;
            for (int i = _listRoot.childCount - 1; i >= 0; i--) UnityEngine.Object.Destroy(_listRoot.GetChild(i).gameObject);
        }

        private bool EnsureToken()
        {
            string token = HubChatClient.Instance?.DevToken;
            if (_api == null || string.IsNullOrEmpty(token)) return false;
            _api.SetBearerToken(token);
            return true;
        }

        private void SetStatus(string s) { if (_status != null) _status.text = s; }
    }
}
