using System;
using System.Collections.Generic;
using System.IO;
using Nymora.Combat.Replay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Nymora.Editor.Windows
{
    /// <summary>
    /// Brique 3.E.1 — Editor Window listant les fichiers .nymrep sauvegardes sous
    /// <see cref="ReplayPaths.RootFolder"/>. Lecture des metadonnees uniquement
    /// (le payload Quantum reste sur disque tant que la 3.E.2 ne le rejoue pas).
    ///
    /// Menu : Nymora &gt; Combat &gt; Replay Library
    /// </summary>
    public class ReplayLibraryWindow : EditorWindow
    {
        private class Entry
        {
            public string FullPath;
            public string FileName;
            public long FileSizeBytes;
            public DateTime FileModifiedUtc;
            public NymoraReplayMetadata Metadata;
            public string LoadError;
        }

        private const string MenuPath = "Nymora/Combat/Replay Library";

        private List<Entry> _entries = new List<Entry>();
        private Vector2 _scroll;
        private string _statusMessage;

        [MenuItem(MenuPath)]
        public static void Open()
        {
            var window = GetWindow<ReplayLibraryWindow>("Replay Library");
            window.minSize = new Vector2(640, 360);
            window.Refresh();
        }

        private void OnEnable()
        {
            Refresh();
        }

        private void OnGUI()
        {
            DrawToolbar();
            DrawHeader();
            DrawList();
            DrawStatus();
        }

        private void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.Width(80))) Refresh();
                if (GUILayout.Button("Open Folder", EditorStyles.toolbarButton, GUILayout.Width(100))) OpenRootFolder();
                GUILayout.FlexibleSpace();
                GUILayout.Label(string.Format("{0} replay(s)", _entries.Count), EditorStyles.miniLabel);
            }
        }

        private void DrawHeader()
        {
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Dossier", ReplayPaths.RootFolder, EditorStyles.miniLabel);
            EditorGUILayout.Space(2);
        }

        private void DrawList()
        {
            if (_entries.Count == 0)
            {
                EditorGUILayout.HelpBox(
                    "Aucun replay sauvegarde. Joue un match dans 30_CombatIA puis clique " +
                    "\"Sauvegarder le replay\" dans l'overlay de fin de match.",
                    MessageType.Info);
                return;
            }

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            foreach (var entry in _entries)
            {
                DrawEntry(entry);
                EditorGUILayout.Space(2);
            }
            EditorGUILayout.EndScrollView();
        }

        private void DrawEntry(Entry entry)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField(entry.FileName, EditorStyles.boldLabel);

                if (!string.IsNullOrEmpty(entry.LoadError))
                {
                    EditorGUILayout.HelpBox("Lecture metadata KO : " + entry.LoadError, MessageType.Error);
                }
                else if (entry.Metadata != null)
                {
                    var m = entry.Metadata;
                    string winner = m.WinnerPlayerIndex < 0 ? "Match nul"
                        : "P" + m.WinnerPlayerIndex + " (" + (m.WinnerPlayerIndex == 0 ? m.Player0Class : m.Player1Class) + ")";
                    EditorGUILayout.LabelField("Match",
                        string.Format("{0} vs {1} — gagnant : {2}", m.Player0Class, m.Player1Class, winner));
                    EditorGUILayout.LabelField("Duree / Rounds",
                        string.Format("{0}s — {1} round(s)", m.DurationSeconds, m.TotalRounds));
                    EditorGUILayout.LabelField("Versions",
                        string.Format("Bible {0} · CombatRulesVersion {1} · Format v{2}",
                            m.BibleVersion, m.CombatRulesVersion, m.FormatVersion));
                    EditorGUILayout.LabelField("Scene", m.SceneName);
                    EditorGUILayout.LabelField("Enregistre",
                        FormatDateForDisplay(m.CreatedAtUtc));
                }

                EditorGUILayout.LabelField("Fichier",
                    string.Format("{0} ko · modifie {1}", entry.FileSizeBytes / 1024,
                        entry.FileModifiedUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss")),
                    EditorStyles.miniLabel);

                using (new EditorGUILayout.HorizontalScope())
                {
                    var prevColor = GUI.color;
                    GUI.color = new Color(0.55f, 0.90f, 0.65f);
                    if (GUILayout.Button("Open in Replay", GUILayout.Width(130))) OpenInReplay(entry);
                    GUI.color = prevColor;
                    if (GUILayout.Button("Reveal in Explorer", GUILayout.Width(140))) RevealInExplorer(entry.FullPath);
                    if (GUILayout.Button("Copy Path", GUILayout.Width(90))) EditorGUIUtility.systemCopyBuffer = entry.FullPath;
                    GUILayout.FlexibleSpace();
                    GUI.color = new Color(0.95f, 0.55f, 0.55f);
                    if (GUILayout.Button("Delete", GUILayout.Width(80))) ConfirmDelete(entry);
                    GUI.color = prevColor;
                }
            }
        }

        private void DrawStatus()
        {
            if (string.IsNullOrEmpty(_statusMessage)) return;
            EditorGUILayout.Space(4);
            EditorGUILayout.HelpBox(_statusMessage, MessageType.None);
        }

        private void Refresh()
        {
            _entries.Clear();
            _statusMessage = null;

            string folder = ReplayPaths.RootFolder;
            if (!Directory.Exists(folder))
            {
                _statusMessage = "Dossier inexistant — sera cree au premier replay sauvegarde.";
                Repaint();
                return;
            }

            string[] files;
            try
            {
                files = Directory.GetFiles(folder, "*" + ReplayPaths.ReplayExtension, SearchOption.TopDirectoryOnly);
            }
            catch (Exception ex)
            {
                _statusMessage = "Lecture dossier KO : " + ex.Message;
                Repaint();
                return;
            }

            Array.Sort(files, (a, b) => File.GetLastWriteTimeUtc(b).CompareTo(File.GetLastWriteTimeUtc(a)));

            foreach (var path in files)
            {
                var entry = new Entry { FullPath = path, FileName = Path.GetFileName(path) };
                try
                {
                    var info = new FileInfo(path);
                    entry.FileSizeBytes = info.Length;
                    entry.FileModifiedUtc = info.LastWriteTimeUtc;
                    entry.Metadata = NymoraReplayFile.ReadMetadataOnly(path);
                    if (entry.Metadata == null) entry.LoadError = "Metadata vide ou fichier corrompu.";
                }
                catch (Exception ex)
                {
                    entry.LoadError = ex.Message;
                }
                _entries.Add(entry);
            }

            Repaint();
        }

        private void OpenRootFolder()
        {
            ReplayPaths.EnsureFolderExists();
            EditorUtility.RevealInFinder(ReplayPaths.RootFolder + Path.DirectorySeparatorChar);
        }

        private void RevealInExplorer(string fullPath)
        {
            if (!File.Exists(fullPath))
            {
                _statusMessage = "Fichier introuvable : " + fullPath;
                return;
            }
            EditorUtility.RevealInFinder(fullPath);
        }

        private void OpenInReplay(Entry entry)
        {
            if (!File.Exists(entry.FullPath))
            {
                _statusMessage = "Fichier introuvable : " + entry.FullPath;
                return;
            }

            if (EditorApplication.isPlaying)
            {
                _statusMessage = "Stop Play Mode d'abord avant d'ouvrir un replay.";
                return;
            }

            // Single-scene mode : on rouvre la scene source du replay (metadata.SceneName),
            // fallback DefaultCombatSceneName si vide / introuvable.
            string targetSceneName = entry.Metadata != null && !string.IsNullOrEmpty(entry.Metadata.SceneName)
                ? entry.Metadata.SceneName
                : ReplayPlaybackBridge.DefaultCombatSceneName;

            var sceneGuids = AssetDatabase.FindAssets("t:Scene " + targetSceneName);
            if (sceneGuids.Length == 0 && targetSceneName != ReplayPlaybackBridge.DefaultCombatSceneName)
            {
                // Retry avec la scene par defaut.
                targetSceneName = ReplayPlaybackBridge.DefaultCombatSceneName;
                sceneGuids = AssetDatabase.FindAssets("t:Scene " + targetSceneName);
            }
            if (sceneGuids.Length == 0)
            {
                _statusMessage = "Scene '" + targetSceneName + "' introuvable dans le projet.";
                return;
            }

            string scenePath = AssetDatabase.GUIDToAssetPath(sceneGuids[0]);
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            ReplayPlaybackBridge.RequestedReplayPath = entry.FullPath;
            EditorSceneManager.OpenScene(scenePath);
            EditorApplication.EnterPlaymode();
        }

        private void ConfirmDelete(Entry entry)
        {
            bool ok = EditorUtility.DisplayDialog(
                "Supprimer le replay ?",
                "Supprimer definitivement :\n" + entry.FileName + " ?",
                "Supprimer", "Annuler");
            if (!ok) return;

            try
            {
                File.Delete(entry.FullPath);
                _statusMessage = "Replay supprime : " + entry.FileName;
                Refresh();
            }
            catch (Exception ex)
            {
                _statusMessage = "Suppression KO : " + ex.Message;
                Debug.LogError(_statusMessage);
            }
        }

        private static string FormatDateForDisplay(string isoUtc)
        {
            if (string.IsNullOrEmpty(isoUtc)) return "—";
            if (DateTime.TryParse(isoUtc, null, System.Globalization.DateTimeStyles.RoundtripKind, out var dt))
            {
                return dt.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");
            }
            return isoUtc;
        }
    }
}
