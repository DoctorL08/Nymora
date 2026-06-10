using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Nymora.Combat.View.HUD;
using Nymora.Core.Data;
using Photon.Client;
using Photon.Realtime;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Nymora.Combat.View.PreCombatLobby
{
    /// <summary>
    /// Brique 5.7-C — Lobby RÉSEAU d'ordre d'équipe (vote capitaine), pour le 2v2/3v3 classé.
    ///
    /// S'intercale dans CombatBootstrapRanked2v2 entre la connexion à la room Photon et le
    /// Game.AddPlayer (le TeamOrder fige TurnOrder côté sim, il DOIT être connu avant AddPlayer).
    ///
    /// Flux :
    ///   - Le CAPITAINE de chaque équipe (désigné par le backend, Match2v2Bridge.Player.IsCaptain)
    ///     ordonne SA équipe via un petit panneau. Au « Valider », l'ordre (liste de subs) est
    ///     publié dans une *player custom property* Photon ("to").
    ///   - CHAQUE client (capitaine + équipier) lit la propriété "to" qui contient SON sub
    ///     (= l'ordre de son équipe) et en déduit SON rang (= TeamOrder).
    ///   - Anti-hang : si aucun ordre n'arrive avant le timeout, on retombe sur le rang PAR DÉFAUT.
    ///
    /// VIEW/NETWORK ONLY (aucun contact avec la sim Quantum). Comme PreCombatLobbyController, on
    /// pompe _client.Service() à chaque frame : avant StartAsync, aucun driver ne le fait, sans quoi
    /// SetCustomProperties n'est jamais envoyé et les props adverses jamais reçues.
    /// </summary>
    public sealed class NetworkTeamOrderLobby : MonoBehaviour
    {
        private const string KeyTeamOrder = "to"; // valeur = "subA,subB" (subs ordonnés de l'équipe du capitaine)

        public float TimeoutSeconds = 30f;

        private RealtimeClient _client;
        private string _localSub;
        private bool _localIsCaptain;
        private bool _confirmed;     // capitaine : a cliqué « Valider »
        private bool _published;     // l'ordre a été publié une fois
        private readonly List<(string sub, string label)> _members = new(); // ordre courant (capitaine réordonne)
        private readonly List<TextMeshProUGUI> _rowLabels = new();
        private TextMeshProUGUI _statusLabel;

        /// <summary>Résout le rang local (TeamOrder). Retourne defaultRank si rien n'arrive (anti-hang).</summary>
        public async Task<int> RunAsync(RealtimeClient client, Match2v2Bridge.Player[] roster, int localTeam,
                                        string localSub, bool localIsCaptain, int defaultRank, CancellationToken ct)
        {
            _client = client;
            _localSub = localSub;
            _localIsCaptain = localIsCaptain;

            // Membres de MON équipe (ordre initial = ordre du roster). Capitaine d'abord (lisible).
            foreach (var p in roster)
                if (p.Team == localTeam)
                    _members.Add((p.Sub, !string.IsNullOrEmpty(p.DisplayName) ? p.DisplayName : ShortSub(p.Sub)));

            BuildUI();
            Nymora.Core.SceneFlow.SceneTransition.SignalReady(); // lève le voile de chargement

            float start = Time.unscaledTime;
            int resolved = -1;
            while (!ct.IsCancellationRequested)
            {
                _client?.Service(); // pompe Photon (indispensable avant StartAsync)

                if (_localIsCaptain && _confirmed && !_published) PublishOrder();

                resolved = TryResolveMyRank();
                if (resolved >= 0) break;
                if (Time.unscaledTime - start >= TimeoutSeconds) break; // anti-hang -> defaultRank

                await Task.Yield();
            }

            if (resolved < 0) resolved = defaultRank;
            if (this != null && gameObject != null) Destroy(gameObject);
            return resolved;
        }

        // Lit la propriété "to" (sur n'importe quel joueur) qui contient MON sub = l'ordre de mon
        //   équipe. Mon rang = la position de mon sub dans cet ordre. -1 tant qu'aucun ordre publié.
        private int TryResolveMyRank()
        {
            var room = _client?.CurrentRoom;
            if (room?.Players == null) return -1;
            foreach (var kv in room.Players)
            {
                var cp = kv.Value?.CustomProperties;
                if (cp == null || !cp.ContainsKey(KeyTeamOrder)) continue;
                if (cp[KeyTeamOrder] is string s && !string.IsNullOrEmpty(s))
                {
                    var subs = s.Split(',');
                    int idx = Array.IndexOf(subs, _localSub);
                    if (idx >= 0) return idx;
                }
            }
            return -1;
        }

        private void PublishOrder()
        {
            if (_client?.LocalPlayer == null) return;
            var order = string.Join(",", _members.ConvertAll(m => m.sub));
            _client.LocalPlayer.SetCustomProperties(new PhotonHashtable { { KeyTeamOrder, order } });
            _published = true;
            if (_statusLabel != null) _statusLabel.text = "Ordre envoyé — lancement du combat…";
        }

        // ================================================================
        // UI (DA hub via CombatUiKit, compacte)
        // ================================================================

        private void BuildUI()
        {
            var canvasGo = new GameObject("NetTeamOrderCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 5000;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            // Fond assombri plein écran.
            var dim = NewRect("Dim", (RectTransform)canvasGo.transform);
            Stretch(dim);
            var dimImg = dim.gameObject.AddComponent<Image>();
            dimImg.color = new Color(0f, 0f, 0f, 0.72f);

            // Carte centrale.
            var card = NewRect("Card", (RectTransform)canvasGo.transform);
            card.anchorMin = card.anchorMax = card.pivot = new Vector2(0.5f, 0.5f);
            card.sizeDelta = new Vector2(640f, 460f);
            var cardImg = card.gameObject.AddComponent<Image>();
            cardImg.color = CombatUiKit.PanelBg;
            CombatUiKit.ApplyRounded(cardImg, 18f);
            var vlg = card.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(36, 36, 32, 32);
            vlg.spacing = 16f;
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.childControlWidth = true; vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;

            NewText("Title", card, "ORDRE DE JEU", 48f, bold: true, CombatUiKit.Accent, 60f);

            if (_localIsCaptain)
            {
                NewText("Sub", card, "Tu es capitaine — choisis qui joue en premier", 26f, false, CombatUiKit.TextSecondary, 34f);
                for (int i = 0; i < _members.Count; i++)
                {
                    int captured = i;
                    BuildMemberRow(card, i, () => Move(captured, -1), () => Move(captured, +1));
                }
                BuildConfirmButton(card);
            }
            else
            {
                NewText("Wait", card, "Le capitaine de ton équipe\nchoisit l'ordre de jeu…", 30f, false, CombatUiKit.TextSecondary, 120f);
            }

            _statusLabel = NewText("Status", card, "", 24f, false, CombatUiKit.TextMuted, 30f);
            RefreshRows();
        }

        private void BuildMemberRow(RectTransform parent, int index, Action up, Action down)
        {
            var row = NewRect($"Row{index}", parent);
            var rImg = row.gameObject.AddComponent<Image>();
            rImg.color = CombatUiKit.GhostBg;
            CombatUiKit.ApplyRounded(rImg, 10f);
            var rLe = row.gameObject.AddComponent<LayoutElement>(); rLe.preferredHeight = 72f;
            var hlg = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            hlg.padding = new RectOffset(20, 14, 0, 0); hlg.spacing = 10f;
            hlg.childAlignment = TextAnchor.MiddleLeft;
            hlg.childControlWidth = true; hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false; hlg.childForceExpandHeight = false;

            var lbl = NewText($"Lbl{index}", row, "", 28f, bold: true, CombatUiKit.TextPrimary, 0f);
            lbl.alignment = TextAlignmentOptions.Left;
            lbl.enableWordWrapping = false; lbl.overflowMode = TextOverflowModes.Ellipsis;
            lbl.gameObject.GetComponent<LayoutElement>().flexibleWidth = 1f;
            _rowLabels.Add(lbl);

            BuildArrow(row, "^", up);
            BuildArrow(row, "v", down);
        }

        private void BuildArrow(RectTransform parent, string glyph, Action onClick)
        {
            var btnRt = NewRect(glyph == "^" ? "Up" : "Down", parent);
            var le = btnRt.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = 54f; le.preferredHeight = 54f; le.flexibleWidth = 0f;
            var bg = btnRt.gameObject.AddComponent<Image>();
            bg.color = CombatUiKit.GhostBg;
            CombatUiKit.ApplyRounded(bg, 10f);
            var btn = btnRt.gameObject.AddComponent<Button>();
            btn.targetGraphic = bg;
            if (onClick != null) btn.onClick.AddListener(() => onClick());
            var t = NewText("G", btnRt, glyph, 28f, bold: true, CombatUiKit.TextPrimary, 0f);
            Stretch(t.rectTransform);
        }

        private void BuildConfirmButton(RectTransform parent)
        {
            var btnRt = NewRect("Confirm", parent);
            var le = btnRt.gameObject.AddComponent<LayoutElement>();
            le.preferredHeight = 72f;
            var bg = btnRt.gameObject.AddComponent<Image>();
            bg.color = CombatUiKit.Accent;
            CombatUiKit.ApplyRounded(bg, 14f);
            var btn = btnRt.gameObject.AddComponent<Button>();
            btn.targetGraphic = bg;
            btn.onClick.AddListener(() =>
            {
                if (_confirmed) return;
                _confirmed = true;
                Nymora.Core.Audio.NymoraAudioManager.Instance?.PlaySfx(Nymora.Core.Audio.SoundId.CombatStart);
                btn.interactable = false;
            });
            var t = NewText("Label", btnRt, "Valider l'ordre", 34f, bold: true, CombatUiKit.TextOnLight, 0f);
            Stretch(t.rectTransform);
        }

        private void Move(int index, int dir)
        {
            int j = index + dir;
            if (j < 0 || j >= _members.Count) return;
            (_members[index], _members[j]) = (_members[j], _members[index]);
            RefreshRows();
            Nymora.Core.Audio.NymoraAudioManager.Instance?.PlaySfx(Nymora.Core.Audio.SoundId.UiClick);
        }

        private void RefreshRows()
        {
            for (int i = 0; i < _rowLabels.Count && i < _members.Count; i++)
                _rowLabels[i].text = $"<color=#{ColorUtility.ToHtmlStringRGB(CombatUiKit.TextMuted)}>{i + 1}.</color>   {_members[i].label}";
        }

        // ===== Helpers UI compacts =====

        private static RectTransform NewRect(string name, RectTransform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            return rt;
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        }

        private static TextMeshProUGUI NewText(string name, RectTransform parent, string text, float size,
                                               bool bold, Color color, float preferredHeight)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<TextMeshProUGUI>();
            t.text = text; t.fontSize = size; t.color = color;
            t.alignment = TextAlignmentOptions.Center;
            t.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
            t.raycastTarget = false;
            if (preferredHeight > 0f)
            {
                var le = go.AddComponent<LayoutElement>();
                le.preferredHeight = preferredHeight;
            }
            else
            {
                go.AddComponent<LayoutElement>();
            }
            return t;
        }

        private static string ShortSub(string sub)
            => string.IsNullOrEmpty(sub) ? "?" : (sub.Length >= 6 ? sub.Substring(0, 6) : sub);
    }
}
