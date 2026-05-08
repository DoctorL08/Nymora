using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Nymora.Editor.Windows
{
    /// <summary>
    /// Console filtree qui n'affiche que les logs dont la stack trace contient "Nymora.".
    /// Permet de couper le bruit des packages tiers (Photon, URP, etc.).
    /// Menu : Nymora &gt; Console &gt; Nymora Logs
    /// </summary>
    public class NymoraConsoleWindow : EditorWindow
    {
        private const string NamespaceMarker = "Nymora.";
        private const int MaxEntries = 1000;

        private struct LogEntry
        {
            public LogType Type;
            public string Message;
            public string StackTrace;
            public DateTime Time;
        }

        private static readonly List<LogEntry> Entries = new List<LogEntry>();
        private static readonly object EntriesLock = new object();

        private Vector2 _scroll;
        private bool _showInfo = true;
        private bool _showWarning = true;
        private bool _showError = true;
        private bool _autoScroll = true;
        private string _searchFilter = string.Empty;
        private int _selectedIndex = -1;

        [MenuItem("Nymora/Console/Nymora Logs")]
        private static void OpenWindow()
        {
            var window = GetWindow<NymoraConsoleWindow>("Nymora Console");
            window.minSize = new Vector2(400, 200);
            window.Show();
        }

        private void OnEnable()
        {
            Application.logMessageReceivedThreaded += OnLogReceived;
        }

        private void OnDisable()
        {
            Application.logMessageReceivedThreaded -= OnLogReceived;
        }

        private static void OnLogReceived(string condition, string stackTrace, LogType type)
        {
            if (string.IsNullOrEmpty(stackTrace) || stackTrace.IndexOf(NamespaceMarker, StringComparison.Ordinal) < 0)
                return;

            lock (EntriesLock)
            {
                if (Entries.Count >= MaxEntries)
                    Entries.RemoveAt(0);

                Entries.Add(new LogEntry
                {
                    Type = type,
                    Message = condition,
                    StackTrace = stackTrace,
                    Time = DateTime.Now
                });
            }
        }

        private void OnGUI()
        {
            DrawToolbar();
            DrawLogList();
            DrawSelectedDetails();
        }

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            if (GUILayout.Button("Clear", EditorStyles.toolbarButton, GUILayout.Width(50)))
            {
                lock (EntriesLock) { Entries.Clear(); }
                _selectedIndex = -1;
            }

            GUILayout.Space(8);

            int infoCount, warnCount, errorCount;
            CountByType(out infoCount, out warnCount, out errorCount);

            _showInfo = GUILayout.Toggle(_showInfo, $"Info ({infoCount})", EditorStyles.toolbarButton, GUILayout.Width(80));
            _showWarning = GUILayout.Toggle(_showWarning, $"Warn ({warnCount})", EditorStyles.toolbarButton, GUILayout.Width(80));
            _showError = GUILayout.Toggle(_showError, $"Err ({errorCount})", EditorStyles.toolbarButton, GUILayout.Width(80));

            GUILayout.FlexibleSpace();

            _autoScroll = GUILayout.Toggle(_autoScroll, "Auto-scroll", EditorStyles.toolbarButton, GUILayout.Width(90));

            GUILayout.Space(4);
            GUILayout.Label("Search:", GUILayout.Width(50));
            _searchFilter = EditorGUILayout.TextField(_searchFilter, EditorStyles.toolbarSearchField, GUILayout.Width(160));

            EditorGUILayout.EndHorizontal();
        }

        private void DrawLogList()
        {
            float listHeight = position.height * 0.65f;

            using (var scroll = new EditorGUILayout.ScrollViewScope(_scroll, GUILayout.Height(listHeight)))
            {
                List<LogEntry> snapshot;
                lock (EntriesLock) { snapshot = new List<LogEntry>(Entries); }

                for (int i = 0; i < snapshot.Count; i++)
                {
                    var entry = snapshot[i];
                    if (!PassesFilter(entry)) continue;

                    var bg = _selectedIndex == i ? new Color(0.24f, 0.49f, 0.91f, 0.4f) : (i % 2 == 0 ? new Color(0, 0, 0, 0.05f) : Color.clear);
                    DrawLogRow(entry, i, bg);
                }

                _scroll = scroll.scrollPosition;

                if (_autoScroll && Event.current.type == EventType.Repaint)
                {
                    _scroll.y = float.MaxValue;
                }
            }
        }

        private void DrawLogRow(LogEntry entry, int index, Color background)
        {
            var rect = EditorGUILayout.BeginHorizontal(GUILayout.Height(20));
            EditorGUI.DrawRect(rect, background);

            GUILayout.Label(IconFor(entry.Type), GUILayout.Width(18), GUILayout.Height(18));
            GUILayout.Label(entry.Time.ToString("HH:mm:ss"), GUILayout.Width(60));
            GUILayout.Label(FirstLine(entry.Message), EditorStyles.label);

            EditorGUILayout.EndHorizontal();

            if (Event.current.type == EventType.MouseDown && rect.Contains(Event.current.mousePosition))
            {
                _selectedIndex = index;
                Event.current.Use();
                Repaint();
            }
        }

        private void DrawSelectedDetails()
        {
            if (_selectedIndex < 0) return;

            LogEntry entry;
            lock (EntriesLock)
            {
                if (_selectedIndex >= Entries.Count) { _selectedIndex = -1; return; }
                entry = Entries[_selectedIndex];
            }

            EditorGUILayout.LabelField("Details", EditorStyles.boldLabel);
            using (new EditorGUILayout.ScrollViewScope(Vector2.zero, GUILayout.ExpandHeight(true)))
            {
                EditorGUILayout.SelectableLabel($"{entry.Message}\n\n{entry.StackTrace}", EditorStyles.textArea, GUILayout.ExpandHeight(true));
            }
        }

        private bool PassesFilter(LogEntry entry)
        {
            if (entry.Type == LogType.Log && !_showInfo) return false;
            if (entry.Type == LogType.Warning && !_showWarning) return false;
            if ((entry.Type == LogType.Error || entry.Type == LogType.Exception || entry.Type == LogType.Assert) && !_showError) return false;

            if (!string.IsNullOrEmpty(_searchFilter)
                && entry.Message.IndexOf(_searchFilter, StringComparison.OrdinalIgnoreCase) < 0
                && entry.StackTrace.IndexOf(_searchFilter, StringComparison.OrdinalIgnoreCase) < 0)
                return false;

            return true;
        }

        private void CountByType(out int info, out int warn, out int error)
        {
            info = warn = error = 0;
            lock (EntriesLock)
            {
                foreach (var e in Entries)
                {
                    switch (e.Type)
                    {
                        case LogType.Log: info++; break;
                        case LogType.Warning: warn++; break;
                        default: error++; break;
                    }
                }
            }
        }

        private static GUIContent IconFor(LogType type)
        {
            switch (type)
            {
                case LogType.Warning: return EditorGUIUtility.IconContent("console.warnicon.sml");
                case LogType.Error:
                case LogType.Exception:
                case LogType.Assert:
                    return EditorGUIUtility.IconContent("console.erroricon.sml");
                default: return EditorGUIUtility.IconContent("console.infoicon.sml");
            }
        }

        private static string FirstLine(string s)
        {
            if (string.IsNullOrEmpty(s)) return string.Empty;
            int nl = s.IndexOf('\n');
            return nl < 0 ? s : s.Substring(0, nl);
        }
    }
}
