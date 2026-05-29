using System;
using System.IO;
using Fusion;
using Nymora.Combat.Replay;
using Nymora.Core.SceneFlow;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Button = UnityEngine.UI.Button;
using Image = UnityEngine.UI.Image;

namespace Nymora.Hub.Menu
{
    /// <summary>
    /// Écran « Replays » du menu hub — liste les fichiers .nymrep enregistrés par le joueur
    /// (sous <see cref="ReplayPaths.RootFolder"/>) et permet, pour chacun :
    ///   - « Visionner » : pose le chemin sur <see cref="ReplayPlaybackBridge"/> + charge la scène
    ///     combat d'origine (fallback 30_CombatIA) ; le ReplayPlaybackController y prend la main
    ///     (play/pause/vitesse/seek + F11 plein écran, contrôles in-scene existants).
    ///   - « Ouvrir le dossier » (global) : ouvre l'explorateur sur le dossier des replays.
    ///
    /// Construit 100% en code via HubMenuUIFactory (même pattern que HubMenuProgression /
    /// HubMenuSettings). Lecture metadata seule (pas de désérialisation du payload Quantum).
    /// </summary>
    public sealed class HubMenuReplays
    {
        private readonly HubMenuTheme _t;
        private readonly HubMenuUIFactory _f;

        public HubMenuReplays(HubMenuTheme theme, HubMenuUIFactory factory)
        {
            _t = theme;
            _f = factory;
        }

        public void Build(RectTransform parent)
        {
            // En-tête : titre + bouton « Ouvrir le dossier ».
            var header = _f.MakeRect("ReplaysHeader", parent);
            header.anchorMin = new Vector2(0f, 1f); header.anchorMax = new Vector2(1f, 1f); header.pivot = new Vector2(0.5f, 1f);
            header.anchoredPosition = new Vector2(0f, -10f);
            header.sizeDelta = new Vector2(-48f, 52f);

            var title = _f.MakeText("Title", header, "Mes replays", _t.FontSizeHeader, _t.TextPrimary, _t.FontBold, TextAlignmentOptions.MidlineLeft);
            var trt = title.rectTransform;
            trt.anchorMin = new Vector2(0f, 0f); trt.anchorMax = new Vector2(1f, 1f); trt.pivot = new Vector2(0f, 0.5f);
            trt.offsetMin = new Vector2(8f, 0f); trt.offsetMax = new Vector2(-220f, 0f);

            var openBtn = _f.MakeButton(header, "Ouvrir le dossier", false, out _);
            var ort = (RectTransform)openBtn.transform;
            ort.anchorMin = new Vector2(1f, 0.5f); ort.anchorMax = new Vector2(1f, 0.5f); ort.pivot = new Vector2(1f, 0.5f);
            ort.anchoredPosition = new Vector2(-8f, 0f);
            ort.sizeDelta = new Vector2(200f, 44f);
            openBtn.onClick.AddListener(OpenFolder);

            // Zone de liste (scroll vertical) sous l'en-tête.
            var listRoot = BuildScroll(parent);
            PopulateList(listRoot);
        }

        // ===== Liste (scroll) =====

        private RectTransform BuildScroll(RectTransform parent)
        {
            var vp = _f.MakeRect("Scroll", parent);
            vp.anchorMin = new Vector2(0f, 0f); vp.anchorMax = new Vector2(1f, 1f);
            vp.offsetMin = new Vector2(24f, 18f); vp.offsetMax = new Vector2(-24f, -72f); // -72 : place pour l'en-tête
            var vpImg = vp.gameObject.AddComponent<Image>(); vpImg.color = new Color(1f, 1f, 1f, 0.02f);
            vp.gameObject.AddComponent<RectMask2D>();
            var sr = vp.gameObject.AddComponent<ScrollRect>();
            sr.horizontal = false; sr.vertical = true; sr.scrollSensitivity = 32f; sr.viewport = vp;

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
            return content;
        }

        private void PopulateList(RectTransform listRoot)
        {
            var entries = ReadEntries();
            if (entries.Length == 0)
            {
                var msg = _f.MakeText("Empty", listRoot,
                    "Aucun replay enregistré.\nEn fin de match, clique « Sauvegarder le replay » dans l'écran de victoire.",
                    _t.FontSizeBody, _t.TextSecondary, null, TextAlignmentOptions.Center);
                msg.gameObject.AddComponent<LayoutElement>().preferredHeight = 120f;
                return;
            }

            foreach (var e in entries) BuildEntry(listRoot, e);
        }

        private void BuildEntry(RectTransform listRoot, ReplayEntry e)
        {
            var card = _f.MakeImage("ReplayCard", listRoot, _t.CardBg);
            var le = card.gameObject.AddComponent<LayoutElement>();
            le.preferredHeight = 96f; le.flexibleWidth = 1f;
            var cardRt = card.rectTransform;

            var m = e.Metadata;
            string line1, line2;
            if (m != null)
            {
                string winner = m.WinnerPlayerIndex < 0
                    ? "Match nul"
                    : "Vainqueur : P" + m.WinnerPlayerIndex + " (" + (m.WinnerPlayerIndex == 0 ? m.Player0Class : m.Player1Class) + ")";
                line1 = string.Format("<b>{0}</b>  vs  <b>{1}</b>", Safe(m.Player0Class), Safe(m.Player1Class));
                line2 = string.Format("{0} · {1}s · {2} round(s) · {3}", winner, m.DurationSeconds, m.TotalRounds, FormatDate(m.CreatedAtUtc));
            }
            else
            {
                line1 = "<b>" + e.FileName + "</b>";
                line2 = "<color=#D08C8C>Métadonnées illisibles</color>";
            }

            var titleTxt = _f.MakeText("L1", cardRt, line1, _t.FontSizeBody, _t.TextPrimary, _t.FontBold, TextAlignmentOptions.TopLeft);
            var t1 = titleTxt.rectTransform;
            t1.anchorMin = new Vector2(0f, 1f); t1.anchorMax = new Vector2(1f, 1f); t1.pivot = new Vector2(0f, 1f);
            t1.offsetMin = new Vector2(18f, -38f); t1.offsetMax = new Vector2(-190f, -10f);

            var infoTxt = _f.MakeText("L2", cardRt, line2, _t.FontSizeSmall, _t.TextSecondary, null, TextAlignmentOptions.TopLeft);
            var t2 = infoTxt.rectTransform;
            t2.anchorMin = new Vector2(0f, 1f); t2.anchorMax = new Vector2(1f, 1f); t2.pivot = new Vector2(0f, 1f);
            t2.offsetMin = new Vector2(18f, -74f); t2.offsetMax = new Vector2(-190f, -42f);

            var viewBtn = _f.MakeButton(cardRt, "Visionner", true, out _);
            var vrt = (RectTransform)viewBtn.transform;
            vrt.anchorMin = new Vector2(1f, 0.5f); vrt.anchorMax = new Vector2(1f, 0.5f); vrt.pivot = new Vector2(1f, 0.5f);
            vrt.anchoredPosition = new Vector2(-16f, 0f);
            vrt.sizeDelta = new Vector2(150f, 52f);
            string path = e.FullPath;
            string scene = (m != null && !string.IsNullOrEmpty(m.SceneName)) ? m.SceneName : ReplayPlaybackBridge.DefaultCombatSceneName;
            viewBtn.interactable = e.Metadata != null;
            viewBtn.onClick.AddListener(() => Watch(path, scene));
        }

        // ===== Données =====

        private sealed class ReplayEntry
        {
            public string FullPath;
            public string FileName;
            public DateTime ModifiedUtc;
            public NymoraReplayMetadata Metadata;
        }

        private static ReplayEntry[] ReadEntries()
        {
            try
            {
                ReplayPaths.EnsureFolderExists();
                var files = Directory.GetFiles(ReplayPaths.RootFolder, "*" + ReplayPaths.ReplayExtension, SearchOption.TopDirectoryOnly);
                Array.Sort(files, (a, b) => File.GetLastWriteTimeUtc(b).CompareTo(File.GetLastWriteTimeUtc(a)));
                var list = new ReplayEntry[files.Length];
                for (int i = 0; i < files.Length; i++)
                {
                    var entry = new ReplayEntry { FullPath = files[i], FileName = Path.GetFileName(files[i]) };
                    try
                    {
                        entry.ModifiedUtc = File.GetLastWriteTimeUtc(files[i]);
                        entry.Metadata = NymoraReplayFile.ReadMetadataOnly(files[i]);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"[HubMenuReplays] Lecture metadata KO ({files[i]}) : {ex.Message}");
                    }
                    list[i] = entry;
                }
                return list;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[HubMenuReplays] Lecture dossier replays KO : {ex.Message}");
                return Array.Empty<ReplayEntry>();
            }
        }

        // ===== Actions =====

        private static void OpenFolder()
        {
            ReplayPaths.EnsureFolderExists();
            // Windows : Application.OpenURL avec un URI file:// ouvre l'explorateur sur le dossier.
            string folder = ReplayPaths.RootFolder.Replace('\\', '/');
            Application.OpenURL("file:///" + folder);
        }

        // Lance la lecture : pose le chemin sur le bridge, coupe le runner Fusion du hub sous le
        // voile, puis charge la scène combat d'origine. async void = handler bouton (cf StartTraining).
        private async void Watch(string fullPath, string sceneName)
        {
            if (!File.Exists(fullPath))
            {
                Debug.LogWarning($"[HubMenuReplays] Fichier introuvable : {fullPath}");
                return;
            }

            ReplayPlaybackBridge.RequestedReplayPath = fullPath;
            Debug.Log($"[HubMenuReplays] Visionnage replay '{Path.GetFileName(fullPath)}' -> scène '{sceneName}'");

            await SceneTransition.LoadAsync(sceneName, async () =>
            {
                var runner = UnityEngine.Object.FindFirstObjectByType<NetworkRunner>();
                if (runner != null && runner.IsRunning)
                {
                    try { await runner.Shutdown(); }
                    catch (Exception ex) { Debug.LogWarning($"[HubMenuReplays] Shutdown Fusion a throw : {ex.Message} — on continue."); }
                }
            }, waitForReady: false);
        }

        // ===== Helpers =====

        private static string Safe(string s) => string.IsNullOrEmpty(s) ? "?" : s;

        private static string FormatDate(string isoUtc)
        {
            if (string.IsNullOrEmpty(isoUtc)) return "—";
            if (DateTime.TryParse(isoUtc, null, System.Globalization.DateTimeStyles.RoundtripKind, out var dt))
                return dt.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
            return isoUtc;
        }
    }
}
