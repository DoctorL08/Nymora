using System.Collections.Generic;
using Nymora.Core.Data;
using Nymora.Core.ScriptableObjects;
using Nymora.Network.Backend;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Nymora.Hub.Menu
{
    /// <summary>
    /// M3c — Onglet Cosmétique au style du nouveau menu (colonne droite de Personnage).
    ///
    /// Sous-onglets par type (Skins / Familiers / Titres / Bannières) ; affiche les
    /// cosmétiques possédés du type actif, avec équiper / déséquiper et garde-fou class-lock.
    /// Réutilise les appels backend de HubProfilePanel (/shop/inventory, /shop/equip,
    /// /shop/unequip) et applique le skin sur l'avatar hub via RefreshEquippedSkin().
    ///
    /// NB : la clé de type backend pour "Familiers" est supposée "pet" — à ajuster si le
    /// catalogue utilise une autre clé.
    /// </summary>
    public sealed class HubMenuCosmetics
    {
        private readonly HubMenuTheme _t;
        private readonly HubMenuUIFactory _f;
        private readonly NymoraApiClient _api;

        private static readonly string[] TypeKeys = { "skin", "pet", "title", "banner" };
        private static readonly string[] TypeLabels = { "Skins", "Familiers", "Titres", "Bannières" };

        private string _classId;
        private string _activeType = "skin";
        private readonly List<ShopItemDto> _items = new List<ShopItemDto>();

        private RectTransform _listContent;
        private TextMeshProUGUI _status;
        private readonly List<(string key, Image bg, TextMeshProUGUI label, Image icon)> _tabs = new List<(string, Image, TextMeshProUGUI, Image)>();
        private readonly List<GameObject> _spawned = new List<GameObject>();
        private bool _busy;

        public HubMenuCosmetics(HubMenuTheme t, HubMenuUIFactory f, NymoraApiClient api)
        {
            _t = t; _f = f; _api = api;
        }

        public void Build(RectTransform parent, string classId)
        {
            _classId = classId;

            // Onglets type (haut)
            var tabsRow = _f.MakeRect("CosmeticTabs", parent);
            tabsRow.anchorMin = new Vector2(0f, 1f); tabsRow.anchorMax = new Vector2(1f, 1f); tabsRow.pivot = new Vector2(0.5f, 1f);
            tabsRow.sizeDelta = new Vector2(-8f, 42f); tabsRow.anchoredPosition = new Vector2(0f, -4f);
            var thlg = tabsRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            thlg.spacing = 8f; thlg.childAlignment = TextAnchor.MiddleCenter;
            thlg.childControlWidth = true; thlg.childControlHeight = true;
            thlg.childForceExpandWidth = false; thlg.childForceExpandHeight = false;
            for (int i = 0; i < TypeKeys.Length; i++) SpawnTypeTab(tabsRow, TypeKeys[i], TypeLabels[i]);

            // Statut (petit, sous les onglets)
            _status = _f.MakeText("Status", parent, "", _t.FontSizeSmall, _t.TextMuted, _t.Font, TextAlignmentOptions.Center);
            _status.raycastTarget = false;
            var strt = _status.rectTransform;
            strt.anchorMin = new Vector2(0f, 1f); strt.anchorMax = new Vector2(1f, 1f); strt.pivot = new Vector2(0.5f, 1f);
            strt.sizeDelta = new Vector2(-8f, 22f); strt.anchoredPosition = new Vector2(0f, -50f);

            // Scroll vertical
            var vp = _f.MakeRect("Scroll", parent);
            vp.anchorMin = new Vector2(0f, 0f); vp.anchorMax = new Vector2(1f, 1f);
            vp.offsetMin = new Vector2(0f, 8f); vp.offsetMax = new Vector2(0f, -78f);
            var vpImg = vp.gameObject.AddComponent<Image>(); vpImg.color = new Color(1f, 1f, 1f, 0.02f);
            vp.gameObject.AddComponent<RectMask2D>();
            var sr = vp.gameObject.AddComponent<ScrollRect>();
            sr.horizontal = false; sr.vertical = true; sr.scrollSensitivity = 28f; sr.viewport = vp;

            var content = _f.MakeRect("Content", vp);
            content.anchorMin = new Vector2(0f, 1f); content.anchorMax = new Vector2(1f, 1f); content.pivot = new Vector2(0.5f, 1f);
            var vlg = content.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 6f; vlg.padding = new RectOffset(8, 8, 8, 8); vlg.childAlignment = TextAnchor.UpperLeft;
            vlg.childControlWidth = true; vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;
            var fit = content.gameObject.AddComponent<ContentSizeFitter>();
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize; fit.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            sr.content = content;
            _listContent = content;

            UpdateTabStyles();
            LoadAsync();
        }

        private void SpawnTypeTab(RectTransform parent, string key, string label)
        {
            var img = _f.MakeImage("Tab_" + key, parent, _t.ButtonGhostBg);
            var le = img.gameObject.AddComponent<LayoutElement>(); le.preferredWidth = 150f; le.preferredHeight = 40f;
            var btn = img.gameObject.AddComponent<Button>(); btn.targetGraphic = img;
            var lbl = _f.MakeText("L", img.rectTransform, label, _t.FontSizeBody, _t.TextSecondary, _t.Font, TextAlignmentOptions.Center);
            HubMenuUIFactory.Stretch(lbl.rectTransform, 30f, 8f, 0f, 0f); lbl.raycastTarget = false; lbl.enableWordWrapping = false;

            // Icône de type (SVG) à gauche, sinon rien (label seul).
            Image icon = null;
            var sp = HubMenuUIFactory.LoadIcon("ui_icon_type_" + key);
            if (sp != null)
            {
                icon = _f.MakeImage("Icon", img.rectTransform, _t.TextSecondary, rounded: false);
                icon.sprite = sp; icon.type = Image.Type.Simple; icon.preserveAspect = true; icon.raycastTarget = false;
                var irt = icon.rectTransform;
                irt.anchorMin = new Vector2(0f, 0.5f); irt.anchorMax = new Vector2(0f, 0.5f); irt.pivot = new Vector2(0.5f, 0.5f);
                irt.sizeDelta = new Vector2(20f, 20f); irt.anchoredPosition = new Vector2(18f, 0f);
            }

            btn.onClick.AddListener(() => { _activeType = key; UpdateTabStyles(); RenderList(); });
            _tabs.Add((key, img, lbl, icon));
        }

        private void UpdateTabStyles()
        {
            foreach (var (key, bg, label, icon) in _tabs)
            {
                bool active = key == _activeType;
                if (bg != null) bg.color = active ? _t.Accent : _t.ButtonGhostBg;
                if (label != null)
                {
                    label.color = active ? _t.TextOnLight : _t.TextSecondary;
                    label.fontStyle = active ? FontStyles.Bold : FontStyles.Normal;
                }
                if (icon != null) icon.color = active ? _t.TextOnLight : _t.TextSecondary;
            }
        }

        private async void LoadAsync()
        {
            SetStatus("Chargement...");
            if (!EnsureToken()) { SetStatus("Non connecté."); return; }

            var res = await _api.GetInventoryAsync();
            if (_listContent == null) return; // écran fermé pendant le fetch
            if (!res.IsSuccess) { SetStatus($"Erreur {res.StatusCode}."); return; }

            _items.Clear();
            if (res.Data.items != null) foreach (var it in res.Data.items) _items.Add(it);
            RenderList();
        }

        private void RenderList()
        {
            if (_listContent == null) return;
            foreach (var go in _spawned) if (go != null) Object.Destroy(go);
            _spawned.Clear();

            // Filtre par type ACTIF + par classe : on ne montre que les cosmétiques sans
            // class-lock ou verrouillés sur la classe affichée (Soulrender ne voit pas les
            // skins des autres classes).
            var inType = new List<ShopItemDto>();
            foreach (var it in _items)
            {
                if (it.type != _activeType) continue;
                if (!string.IsNullOrEmpty(it.classLock) && it.classLock != _classId) continue;
                inType.Add(it);
            }

            SetStatus($"Classe active : <color=#8be0ff>{_classId}</color>");

            if (inType.Count == 0)
            {
                SpawnInfoRow("<i>Aucun cosmétique de ce type. Direction la Boutique !</i>");
                return;
            }
            foreach (var it in inType) SpawnRow(it);
        }

        private void SpawnInfoRow(string richText)
        {
            var tmp = _f.MakeText("Info", _listContent, richText, _t.FontSizeSmall, _t.TextMuted, _t.Font, TextAlignmentOptions.Center);
            tmp.raycastTarget = false;
            tmp.gameObject.AddComponent<LayoutElement>().preferredHeight = 48f;
            _spawned.Add(tmp.gameObject);
        }

        private void SpawnRow(ShopItemDto item)
        {
            bool classOk = string.IsNullOrEmpty(item.classLock) || item.classLock == _classId;

            var row = _f.MakeImage("Cosmetic_" + item.id, _listContent, _t.CardBg);
            row.gameObject.AddComponent<LayoutElement>().preferredHeight = 66f;
            var hlg = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            hlg.padding = new RectOffset(12, 12, 8, 8); hlg.spacing = 16f; hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.childControlWidth = true; hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false; hlg.childForceExpandHeight = true;

            // Vignette
            var prev = _f.MakeImage("Preview", row.rectTransform, new Color(1f, 1f, 1f, 0.06f), rounded: false);
            prev.preserveAspect = true; prev.raycastTarget = false;
            var sprite = Resources.Load<Sprite>(item.previewKey);
            // Familiers : 1re frame idle SE du PetCatalog (pas de sprite Resources dédié).
            if (sprite == null && item.type == "pet") sprite = PetPreviewSprite(item.id);
            if (sprite != null) { prev.sprite = sprite; prev.color = Color.white; }
            else
            {
                // Pas d'asset de preview (ex : titres) -> repli sur l'icône de type.
                var typeSp = HubMenuUIFactory.LoadIcon("ui_icon_type_" + item.type);
                if (typeSp != null) { prev.sprite = typeSp; prev.color = new Color(1f, 1f, 1f, 0.6f); }
            }
            prev.gameObject.AddComponent<LayoutElement>().preferredWidth = 50f;

            // Nom + sous-titre — largeur = contenu (pas de flexible -> le bouton se colle au texte)
            string sub = string.IsNullOrEmpty(item.classLock) ? RarityLabel(item.rarity) : $"{RarityLabel(item.rarity)} · {item.classLock}";
            var info = _f.MakeText("Info", row.rectTransform, $"{item.name}\n<size=80%><color=#9aa0ac>{sub}</color></size>", _t.FontSizeBody, _t.TextPrimary, _t.Font, TextAlignmentOptions.MidlineLeft);
            info.raycastTarget = false; info.enableWordWrapping = false;

            // Bouton action
            var actBtn = _f.MakeButton(row.rectTransform, "", false, out var actLabel);
            actBtn.gameObject.GetComponent<LayoutElement>().preferredWidth = 160f;

            string id = item.id;
            if (item.equipped)
            {
                // Icône check (sprite) + "Équipé" en vert. Pas de glyphe ✓ (tofu en police Ari).
                var ckSp = HubMenuUIFactory.LoadIcon("ui_icon_check");
                var eqGreen = new Color(0.7f, 0.95f, 0.75f, 1f);
                if (ckSp != null)
                {
                    actLabel.text = "Équipé"; actLabel.color = eqGreen;
                    HubMenuUIFactory.Stretch(actLabel.rectTransform, 24f, 6f, 0f, 0f);
                    var ck = _f.MakeImage("Check", (RectTransform)actBtn.transform, eqGreen, rounded: false);
                    ck.sprite = ckSp; ck.type = Image.Type.Simple; ck.preserveAspect = true; ck.raycastTarget = false;
                    var crt = ck.rectTransform;
                    crt.anchorMin = new Vector2(0f, 0.5f); crt.anchorMax = new Vector2(0f, 0.5f); crt.pivot = new Vector2(0f, 0.5f);
                    crt.sizeDelta = new Vector2(18f, 18f); crt.anchoredPosition = new Vector2(12f, 0f);
                }
                else { actLabel.text = "✓ Équipé"; actLabel.color = eqGreen; }
                actBtn.onClick.AddListener(() => Unequip(id));
            }
            else if (classOk)
            {
                actLabel.text = "Équiper";
                actBtn.onClick.AddListener(() => Equip(id));
            }
            else
            {
                actLabel.text = $"Passe en {item.classLock}";
                actBtn.interactable = false;
            }

            _spawned.Add(row.gameObject);
        }

        private async void Equip(string itemId)
        {
            if (_busy || !EnsureToken()) return;
            _busy = true;
            var res = await _api.EquipItemAsync(itemId, _classId);
            _busy = false;
            if (res.IsSuccess && res.Data.ok)
            {
                if (HubAvatar.Local != null) HubAvatar.Local.RefreshEquippedSkin();
                if (HubMenuShell.Instance != null) HubMenuShell.Instance.RefreshClassPortrait();
                LoadAsync();
            }
            else
            {
                string err = res.IsSuccess ? res.Data.error : res.ErrorMessage;
                SetStatus($"Équiper refusé : {err}");
            }
        }

        private async void Unequip(string itemId)
        {
            if (_busy || !EnsureToken()) return;
            _busy = true;
            var res = await _api.UnequipItemAsync(itemId);
            _busy = false;
            if (res.IsSuccess)
            {
                if (HubAvatar.Local != null) HubAvatar.Local.RefreshEquippedSkin();
                if (HubMenuShell.Instance != null) HubMenuShell.Instance.RefreshClassPortrait();
                LoadAsync();
            }
        }

        // Familiers : vignette = 1re frame idle SE de la PetDefinition (PetCatalog en Resources).
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

        private static string RarityLabel(string rarity)
        {
            switch (rarity)
            {
                case "legendary": return "<color=#ffa733>Légendaire</color>";
                case "epic": return "<color=#c773ff>Épique</color>";
                case "rare": return "<color=#66b3ff>Rare</color>";
                default: return "<color=#d8d8e0>Commun</color>";
            }
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
