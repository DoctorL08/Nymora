using System;
using System.Collections.Generic;
using Nymora.Core.Audio;
using Nymora.Network.Backend;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Nymora.Hub.Menu
{
    /// <summary>
    /// M5 — Écran Progression du nouveau menu hub (onglet "Progression" de la barre haute).
    ///
    /// Deux sous-onglets pilule :
    ///   - Quêtes : sections par période (Quotidiennes / Hebdomadaires / Saisonnières),
    ///              barre de progression + récompense (Nymos + XP Pass) + bouton Réclamer
    ///              (si complétée & non réclamée). Réutilise GetQuests/ClaimQuest + SFX RewardClaim.
    ///   - Succès : lecture seule. Fusionne le catalogue (titre/desc/points/target/catégorie)
    ///              avec l'état joueur (progress/unlocked). En-tête points + débloqués/total,
    ///              liste groupée par catégorie. Pas de succès social (anti-farm).
    ///
    /// Réutilise 100% l'API NymoraApiClient (zéro endpoint dupliqué). 100% View — pas de bump
    /// CombatRulesVersion.
    /// </summary>
    public sealed class HubMenuProgression
    {
        private readonly HubMenuTheme _t;
        private readonly HubMenuUIFactory _f;
        private readonly NymoraApiClient _api;

        private static readonly Color ClaimColor = new Color(0.27f, 0.55f, 0.36f, 1f);
        private static readonly Color UnlockedColor = new Color(0.45f, 0.82f, 0.50f, 1f);
        private static readonly Color GoldColor = new Color(0.96f, 0.80f, 0.36f, 1f);

        private static readonly string[] QuestPeriods = { "daily", "weekly", "seasonal" };
        private static readonly string[] QuestPeriodLabels = { "Quotidiennes", "Hebdomadaires", "Saisonnières" };
        private static readonly string[] AchCategories = { "first_steps", "combat", "progression" };
        private static readonly string[] AchCategoryLabels = { "Premiers pas", "Combat", "Progression" };

        private int _tab; // 0 = Quêtes, 1 = Succès
        private readonly List<(Image bg, TextMeshProUGUI label)> _tabRefs = new List<(Image, TextMeshProUGUI)>();

        private RectTransform _content;
        private RectTransform _listRoot;
        private TextMeshProUGUI _status;
        private bool _busy;

        public HubMenuProgression(HubMenuTheme t, HubMenuUIFactory f, NymoraApiClient api)
        {
            _t = t; _f = f; _api = api;
        }

        // ===== Entrée =====

        public void Build(RectTransform parent)
        {
            var tabsRow = _f.MakeRect("ProgTabs", parent);
            tabsRow.anchorMin = new Vector2(0.5f, 1f); tabsRow.anchorMax = new Vector2(0.5f, 1f); tabsRow.pivot = new Vector2(0.5f, 1f);
            tabsRow.sizeDelta = new Vector2(360f, 42f); tabsRow.anchoredPosition = new Vector2(0f, -18f);
            var thlg = tabsRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            thlg.spacing = 10f; thlg.childAlignment = TextAnchor.MiddleCenter;
            thlg.childControlWidth = true; thlg.childControlHeight = true;
            thlg.childForceExpandWidth = false; thlg.childForceExpandHeight = false;
            SpawnTab(tabsRow, "Quêtes", 0);
            SpawnTab(tabsRow, "Succès", 1);

            _status = _f.MakeText("Status", parent, "", _t.FontSizeSmall, _t.TextMuted, _t.Font, TextAlignmentOptions.Center);
            _status.raycastTarget = false;
            var srt = _status.rectTransform;
            srt.anchorMin = new Vector2(0f, 1f); srt.anchorMax = new Vector2(1f, 1f); srt.pivot = new Vector2(0.5f, 1f);
            srt.sizeDelta = new Vector2(-48f, 22f); srt.anchoredPosition = new Vector2(0f, -66f);

            _content = _f.MakeRect("ProgContent", parent);
            _content.anchorMin = new Vector2(0f, 0f); _content.anchorMax = new Vector2(1f, 1f);
            _content.offsetMin = new Vector2(28f, 18f); _content.offsetMax = new Vector2(-28f, -92f);

            UpdateTabStyles();
            ShowTab(0);
        }

        private void SpawnTab(RectTransform parent, string label, int idx)
        {
            var img = _f.MakeImage("Tab_" + label, parent, _t.ButtonGhostBg);
            var le = img.gameObject.AddComponent<LayoutElement>(); le.preferredWidth = 160f; le.preferredHeight = 40f;
            var btn = img.gameObject.AddComponent<Button>(); btn.targetGraphic = img;
            var lbl = _f.MakeText("L", img.rectTransform, label, _t.FontSizeBody, _t.TextSecondary, _t.Font, TextAlignmentOptions.Center);
            HubMenuUIFactory.Stretch(lbl.rectTransform); lbl.raycastTarget = false; lbl.enableWordWrapping = false;
            btn.onClick.AddListener(() => { if (_tab != idx) ShowTab(idx); });
            _tabRefs.Add((img, lbl));
        }

        private void UpdateTabStyles()
        {
            for (int i = 0; i < _tabRefs.Count; i++)
            {
                bool active = i == _tab;
                if (_tabRefs[i].bg != null) _tabRefs[i].bg.color = active ? _t.Accent : _t.ButtonGhostBg;
                if (_tabRefs[i].label != null)
                {
                    _tabRefs[i].label.color = active ? _t.TextOnLight : _t.TextSecondary;
                    _tabRefs[i].label.fontStyle = active ? FontStyles.Bold : FontStyles.Normal;
                }
            }
        }

        private void ShowTab(int idx)
        {
            _tab = idx;
            UpdateTabStyles();
            SetStatus("");
            for (int i = _content.childCount - 1; i >= 0; i--) UnityEngine.Object.Destroy(_content.GetChild(i).gameObject);
            _listRoot = null;

            BuildScroll(_content);
            if (idx == 0) LoadQuestsAsync();
            else LoadAchievementsAsync();
        }

        // ===== Quêtes =====

        private async void LoadQuestsAsync()
        {
            SetStatus("Chargement...");
            if (!EnsureToken()) { SetStatus("Non connecté."); return; }
            var res = await _api.GetQuestsAsync();
            if (_listRoot == null || _tab != 0) return;
            if (!res.IsSuccess) { SetStatus($"Erreur quêtes ({res.StatusCode})."); return; }

            ClearList();
            var quests = res.Data.quests ?? Array.Empty<QuestDto>();
            int claimable = 0;
            foreach (var q in quests) if (q.completed && !q.claimed) claimable++;

            for (int p = 0; p < QuestPeriods.Length; p++)
            {
                var inPeriod = new List<QuestDto>();
                foreach (var q in quests) if (q.period == QuestPeriods[p]) inPeriod.Add(q);
                if (inPeriod.Count == 0) continue;
                SpawnSectionHeader(QuestPeriodLabels[p]);
                foreach (var q in inPeriod) SpawnQuestRow(q);
            }

            if (quests.Length == 0) SpawnInfoRow("<i>Aucune quête active pour l'instant.</i>");
            SetStatus(claimable > 0 ? $"<color=#7CFC7C>{claimable} quête(s) à réclamer</color>" : "");
        }

        private void SpawnQuestRow(QuestDto q)
        {
            var row = MakeListRow(86f);

            // Titre en haut à gauche
            var info = _f.MakeText("Title", row, q.title, _t.FontSizeBody, _t.TextPrimary, _t.FontBold, TextAlignmentOptions.MidlineLeft);
            info.raycastTarget = false; info.enableWordWrapping = false;
            var irt = info.rectTransform;
            irt.anchorMin = new Vector2(0f, 1f); irt.anchorMax = new Vector2(1f, 1f); irt.pivot = new Vector2(0.5f, 1f);
            irt.offsetMin = new Vector2(16f, -34f); irt.offsetMax = new Vector2(-220f, -8f);

            // Récompense (Nymos + XP Pass), haut-droite
            var reward = _f.MakeText("Reward", row, $"<color=#f5dba0>◆ {q.nymos}</color>   <color=#8be0ff>+{q.bpXp} XP</color>", _t.FontSizeSmall, _t.TextSecondary, _t.Font, TextAlignmentOptions.MidlineRight);
            reward.raycastTarget = false; reward.enableWordWrapping = false;
            var rrt = reward.rectTransform;
            rrt.anchorMin = new Vector2(1f, 1f); rrt.anchorMax = new Vector2(1f, 1f); rrt.pivot = new Vector2(1f, 1f);
            rrt.sizeDelta = new Vector2(260f, 24f); rrt.anchoredPosition = new Vector2(-16f, -12f);

            // Progression "x / y" sur sa propre ligne (gauche, sous le titre, sur fond sombre = lisible)
            var cnt = _f.MakeText("Count", row, $"{Mathf.Min(q.progress, q.target)} / {q.target}", _t.FontSizeSmall, _t.TextSecondary, _t.Font, TextAlignmentOptions.MidlineLeft);
            cnt.raycastTarget = false; cnt.enableWordWrapping = false;
            var crt = cnt.rectTransform;
            crt.anchorMin = new Vector2(0f, 0f); crt.anchorMax = new Vector2(0f, 0f); crt.pivot = new Vector2(0f, 0f);
            crt.sizeDelta = new Vector2(200f, 16f); crt.anchoredPosition = new Vector2(16f, 32f);

            // Barre, en bas, zone gauche
            float t = q.target > 0 ? Mathf.Clamp01((float)q.progress / q.target) : (q.completed ? 1f : 0f);
            MakeProgressBar(row, 16f, -220f, 16f, t);

            // Action / statut, en bas à droite
            if (q.completed && !q.claimed)
            {
                var btn = _f.MakeButton(row, "Réclamer", false, out var bl);
                ColorButton(btn, ClaimColor); bl.color = Color.white;
                var brt = (RectTransform)btn.transform;
                brt.anchorMin = new Vector2(1f, 0f); brt.anchorMax = new Vector2(1f, 0f); brt.pivot = new Vector2(1f, 0f);
                brt.sizeDelta = new Vector2(150f, 38f); brt.anchoredPosition = new Vector2(-16f, 14f);
                string id = q.id;
                btn.onClick.AddListener(() => ClaimQuest(id));
            }
            else
            {
                var tag = _f.MakeText("Tag", row, q.claimed ? "<color=#7CFC7C>✓ Réclamée</color>" : "En cours", _t.FontSizeSmall, _t.TextMuted, _t.Font, TextAlignmentOptions.MidlineRight);
                tag.raycastTarget = false; tag.enableWordWrapping = false;
                var trt = tag.rectTransform;
                trt.anchorMin = new Vector2(1f, 0f); trt.anchorMax = new Vector2(1f, 0f); trt.pivot = new Vector2(1f, 0f);
                trt.sizeDelta = new Vector2(180f, 24f); trt.anchoredPosition = new Vector2(-16f, 18f);
            }
        }

        private async void ClaimQuest(string questId)
        {
            if (_busy || !EnsureToken()) return;
            _busy = true;
            var res = await _api.ClaimQuestAsync(questId);
            _busy = false;
            if (res.IsSuccess && res.Data.status == "claimed")
            {
                NymoraAudioManager.Instance?.PlaySfx(SoundId.RewardClaim, NymoraAudioManager.SoftUiVolume);
                SetStatus($"<color=#7CFC7C>Réclamé : +{res.Data.nymos} Nymos · +{res.Data.bpXp} XP Pass</color>");
            }
            else
            {
                SetStatus($"Réclamation refusée : {res.Data?.error ?? res.ErrorMessage}");
            }
            LoadQuestsAsync();
        }

        // ===== Succès =====

        private async void LoadAchievementsAsync()
        {
            SetStatus("Chargement...");
            if (!EnsureToken()) { SetStatus("Non connecté."); return; }
            var catRes = await _api.GetAchievementsCatalogAsync();
            var meRes = await _api.GetAchievementsMeAsync();
            if (_listRoot == null || _tab != 1) return;
            if (!catRes.IsSuccess) { SetStatus($"Erreur succès ({catRes.StatusCode})."); return; }

            ClearList();
            var defs = catRes.Data.achievements ?? Array.Empty<AchievementDefDto>();
            var byId = new Dictionary<string, UserAchievementDto>();
            if (meRes.IsSuccess && meRes.Data.items != null)
                foreach (var it in meRes.Data.items) if (it != null && it.achievementId != null) byId[it.achievementId] = it;

            // En-tête points / progression globale
            if (meRes.IsSuccess)
                SetStatus($"<color=#f5dba0>{meRes.Data.totalPoints} pts</color>   ·   {meRes.Data.unlockedCount} / {meRes.Data.totalCount} débloqués");

            for (int c = 0; c < AchCategories.Length; c++)
            {
                var inCat = new List<AchievementDefDto>();
                foreach (var d in defs) if (d.category == AchCategories[c]) inCat.Add(d);
                if (inCat.Count == 0) continue;
                SpawnSectionHeader(AchCategoryLabels[c]);
                foreach (var d in inCat)
                {
                    byId.TryGetValue(d.id, out var me);
                    SpawnAchievementRow(d, me);
                }
            }

            if (defs.Length == 0) SpawnInfoRow("<i>Catalogue de succès vide.</i>");
        }

        private void SpawnAchievementRow(AchievementDefDto def, UserAchievementDto me)
        {
            bool unlocked = me != null && me.unlocked;
            int progress = me != null ? me.progress : 0;
            int target = def.target > 0 ? def.target : (me != null ? me.target : 1);

            var row = MakeListRow(72f);

            // Icône état (check si débloqué / cadenas sinon) à gauche — sprite SVG + fallback glyphe.
            Color iconColor = unlocked ? UnlockedColor : _t.TextMuted;
            var iconSprite = HubMenuUIFactory.LoadIcon(unlocked ? "ui_icon_check" : "ui_icon_lock");
            if (iconSprite != null)
            {
                var icon = _f.MakeImage("Icon", row, iconColor, rounded: false);
                icon.sprite = iconSprite; icon.type = Image.Type.Simple; icon.preserveAspect = true; icon.raycastTarget = false;
                var icrt = icon.rectTransform;
                icrt.anchorMin = new Vector2(0f, 0.5f); icrt.anchorMax = new Vector2(0f, 0.5f); icrt.pivot = new Vector2(0.5f, 0.5f);
                icrt.sizeDelta = new Vector2(26f, 26f); icrt.anchoredPosition = new Vector2(30f, 0f);
            }
            else
            {
                var icon = _f.MakeText("Icon", row, unlocked ? "✓" : "○", _t.FontSizeHeader, iconColor, _t.FontBold, TextAlignmentOptions.Center);
                icon.raycastTarget = false;
                var icrt = icon.rectTransform;
                icrt.anchorMin = new Vector2(0f, 0.5f); icrt.anchorMax = new Vector2(0f, 0.5f); icrt.pivot = new Vector2(0.5f, 0.5f);
                icrt.sizeDelta = new Vector2(36f, 36f); icrt.anchoredPosition = new Vector2(28f, 0f);
            }

            // Titre + description (la progression "x/y" est intégrée à la description si verrouillé,
            // pour rester lisible — la barre n'est qu'un repère visuel)
            string titleColor = unlocked ? "#ffffff" : "#b8bcc6";
            string prog = unlocked ? "" : $"   ·   <color=#c8ccd4>{Mathf.Min(progress, target)} / {target}</color>";
            string body = $"<color={titleColor}><b>{def.title}</b></color>\n<size=80%><color=#9aa0ac>{def.description}</color>{prog}</size>";
            var info = _f.MakeText("Info", row, body, _t.FontSizeBody, _t.TextPrimary, _t.Font, TextAlignmentOptions.MidlineLeft);
            info.raycastTarget = false; info.enableWordWrapping = false;
            var irt = info.rectTransform;
            irt.anchorMin = new Vector2(0f, 0f); irt.anchorMax = new Vector2(1f, 1f); irt.pivot = new Vector2(0f, 0.5f);
            // bas relevé à 24px pour ne pas chevaucher la barre des succès verrouillés
            irt.offsetMin = new Vector2(56f, unlocked ? 8f : 24f); irt.offsetMax = new Vector2(-200f, 0f);

            // Points (droite)
            var pts = _f.MakeText("Pts", row, $"{def.points} pts", _t.FontSizeBody, unlocked ? GoldColor : _t.TextMuted, _t.FontBold, TextAlignmentOptions.MidlineRight);
            pts.raycastTarget = false; pts.enableWordWrapping = false;
            var prt = pts.rectTransform;
            prt.anchorMin = new Vector2(1f, 0.5f); prt.anchorMax = new Vector2(1f, 0.5f); prt.pivot = new Vector2(1f, 0.5f);
            prt.sizeDelta = new Vector2(170f, 30f); prt.anchoredPosition = new Vector2(-18f, 0f);

            // Barre de progression seulement si pas encore débloqué
            if (!unlocked)
                MakeProgressBar(row, 56f, -200f, 8f, target > 0 ? Mathf.Clamp01((float)progress / target) : 0f);
        }

        // ===== Helpers UI =====

        private void BuildScroll(RectTransform parent)
        {
            var vp = _f.MakeRect("Scroll", parent);
            vp.anchorMin = new Vector2(0f, 0f); vp.anchorMax = new Vector2(1f, 1f);
            vp.offsetMin = new Vector2(0f, 8f); vp.offsetMax = new Vector2(0f, -4f);
            var vpImg = vp.gameObject.AddComponent<Image>(); vpImg.color = new Color(1f, 1f, 1f, 0.02f);
            vp.gameObject.AddComponent<RectMask2D>();
            var sr = vp.gameObject.AddComponent<ScrollRect>();
            sr.horizontal = false; sr.vertical = true; sr.scrollSensitivity = 28f; sr.viewport = vp;

            var content = _f.MakeRect("Content", vp);
            content.anchorMin = new Vector2(0f, 1f); content.anchorMax = new Vector2(1f, 1f); content.pivot = new Vector2(0.5f, 1f);
            // sizeDelta défaut (100×100) sur ancrages étirés -> débord ; on force la largeur du viewport.
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = Vector2.zero;
            var vlg = content.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 6f; vlg.padding = new RectOffset(8, 8, 8, 8); vlg.childAlignment = TextAnchor.UpperLeft;
            vlg.childControlWidth = true; vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;
            var fit = content.gameObject.AddComponent<ContentSizeFitter>();
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize; fit.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            sr.content = content;
            _listRoot = content;
        }

        private void ClearList()
        {
            if (_listRoot == null) return;
            for (int i = _listRoot.childCount - 1; i >= 0; i--) UnityEngine.Object.Destroy(_listRoot.GetChild(i).gameObject);
        }

        private void SpawnSectionHeader(string text)
        {
            var tmp = _f.MakeText("Header", _listRoot, text, _t.FontSizeHeader, _t.TextPrimary, _t.FontBold, TextAlignmentOptions.MidlineLeft);
            tmp.raycastTarget = false;
            tmp.gameObject.AddComponent<LayoutElement>().preferredHeight = 34f;
        }

        private void SpawnInfoRow(string richText)
        {
            var tmp = _f.MakeText("Info", _listRoot, richText, _t.FontSizeSmall, _t.TextMuted, _t.Font, TextAlignmentOptions.MidlineLeft);
            tmp.raycastTarget = false;
            tmp.gameObject.AddComponent<LayoutElement>().preferredHeight = 40f;
        }

        /// <summary>Ligne carte de hauteur fixe ; les enfants sont positionnés par ancrage explicite.</summary>
        private RectTransform MakeListRow(float height)
        {
            var row = _f.MakeImage("Row", _listRoot, _t.CardBg);
            row.gameObject.AddComponent<LayoutElement>().preferredHeight = height;
            return row.rectTransform;
        }

        /// <summary>Barre de progression fine en bas de la ligne (de leftX à rightX). Visuelle
        /// uniquement — le texte "x / y" est posé par l'appelant (hors de la barre, lisible).</summary>
        private void MakeProgressBar(RectTransform row, float leftX, float rightX, float bottomY, float fill01)
        {
            var bg = _f.MakeImage("BarBg", row, new Color(1f, 1f, 1f, 0.10f));
            var brt = bg.rectTransform;
            brt.anchorMin = new Vector2(0f, 0f); brt.anchorMax = new Vector2(1f, 0f); brt.pivot = new Vector2(0.5f, 0f);
            brt.offsetMin = new Vector2(leftX, bottomY); brt.offsetMax = new Vector2(rightX, bottomY + 8f);
            bg.raycastTarget = false;

            var fill = _f.MakeImage("BarFill", brt, _t.Accent);
            HubMenuUIFactory.Stretch(fill.rectTransform);
            fill.type = Image.Type.Filled; fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = (int)Image.OriginHorizontal.Left; fill.fillAmount = fill01;
            fill.raycastTarget = false;
        }

        private static void ColorButton(Button btn, Color bg)
        {
            var img = btn.GetComponent<Image>(); if (img != null) img.color = Color.white;
            var c = btn.colors;
            c.normalColor = bg;
            c.highlightedColor = Color.Lerp(bg, Color.white, 0.18f);
            c.pressedColor = Color.Lerp(bg, Color.black, 0.12f);
            c.selectedColor = bg;
            c.fadeDuration = 0.1f;
            btn.colors = c;
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
