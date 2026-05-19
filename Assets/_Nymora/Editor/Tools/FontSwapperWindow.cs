using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Nymora.Editor.Tools
{
    /// <summary>
    /// Outil de bascule de police TMP par defaut (TMP_FontAsset -> TMP_FontAsset)
    /// sur tous les TextMeshPro / TextMeshProUGUI de Assets/_Nymora/.
    ///
    /// Mode Dry-Run : scan textuel YAML pour lister les fichiers (scenes/prefabs)
    /// qui referent l'ancienne police. Rapide, ne modifie rien.
    ///
    /// Mode Apply : ouvre chaque fichier detecte, swap m_fontAsset + m_sharedMaterial
    /// via SerializedObject sur tous les TMP_Text, sauve.
    /// Met aussi a jour TMP Settings.m_defaultFontAsset (pour les futurs composants).
    /// Backup auto dans Assets/_Backups/FontSwap_yyyyMMdd_HHmmss/.
    ///
    /// Filtre : ne touche QUE Assets/_Nymora/ ; skip /TextMesh Pro/Examples/,
    /// /Photon/, /QuantumMenu/, *_BACKUP_*, /_Backups/.
    ///
    /// Limite connue : si un TMP_Text avait un material PRESET custom (outline,
    /// gradient), il revient sur le material de base de la nouvelle police.
    /// A retoucher manuellement le cas echeant.
    /// </summary>
    public class FontSwapperWindow : EditorWindow
    {
        private const string DefaultOldGuid = "8f586378b4e144a9851e7b34d9b748ee"; // LiberationSans SDF
        private const string DefaultNewGuid = "9c8a8667e2352a841a77df1104136a55"; // Ari W9500 SDF

        [SerializeField] private string _oldGuid = DefaultOldGuid;
        [SerializeField] private string _newGuid = DefaultNewGuid;
        [SerializeField] private bool _updateTmpSettings = true;
        [SerializeField] private string _scopeOverride = "";
        [SerializeField] private Vector2 _scroll;

        private readonly List<MatchInfo> _matches = new List<MatchInfo>();
        private string _lastReport = "Click Dry-Run pour scanner.";

        private struct MatchInfo
        {
            public string Path;
            public int OccurrenceCount;
        }

        [MenuItem("Nymora/Tools/Font Swapper")]
        public static void Open()
        {
            var win = GetWindow<FontSwapperWindow>(false, "Nymora — Font Swapper", true);
            win.minSize = new Vector2(600, 500);
            win.Show();
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Font GUIDs", EditorStyles.boldLabel);
            _oldGuid = EditorGUILayout.TextField(new GUIContent("Old GUID", "GUID du TMP_FontAsset a remplacer"), _oldGuid);
            _newGuid = EditorGUILayout.TextField(new GUIContent("New GUID", "GUID du TMP_FontAsset cible"), _newGuid);

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Old → New (preview)", GUILayout.Width(150));
                var oldAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetDatabase.GUIDToAssetPath(_oldGuid));
                var newAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetDatabase.GUIDToAssetPath(_newGuid));
                EditorGUILayout.LabelField(oldAsset != null ? oldAsset.name : "<introuvable>", GUILayout.Width(180));
                EditorGUILayout.LabelField("→", GUILayout.Width(20));
                EditorGUILayout.LabelField(newAsset != null ? newAsset.name : "<introuvable>");
            }

            _updateTmpSettings = EditorGUILayout.Toggle(
                new GUIContent("Update TMP Settings default",
                    "Met aussi a jour m_defaultFontAsset dans TMP Settings.asset (impacte les composants TMP CREES APRES le swap)"),
                _updateTmpSettings);

            _scopeOverride = EditorGUILayout.TextField(
                new GUIContent("Scope override (optionnel)",
                    "Vide = Assets/_Nymora/. Sinon ex: Assets/_Nymora/Scenes/00_Login.unity ou Assets/_Nymora/Prefabs/Hub"),
                _scopeOverride);

            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(
                "Scope : Assets/_Nymora/**.unity + .prefab (sauf override).\n" +
                "Skip : /TextMesh Pro/Examples/, /Photon/, /QuantumMenu/, *_BACKUP_*, /_Backups/.\n" +
                "Backup auto dans Assets/_Backups/FontSwap_<timestamp>/ avant Apply.\n" +
                "Conseil : git status clean avant Apply pour rollback facile via git checkout.",
                MessageType.Info);

            EditorGUILayout.Space();
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Dry-Run (scan only)", GUILayout.Height(30)))
                    DryRun();

                GUI.enabled = _matches.Count > 0;
                if (GUILayout.Button($"Apply Swap ({_matches.Count} files)", GUILayout.Height(30)))
                {
                    if (EditorUtility.DisplayDialog(
                        "Confirm Font Swap",
                        $"Swap {_matches.Count} fichiers de {_oldGuid.Substring(0, 8)}... vers {_newGuid.Substring(0, 8)}...\n\nUn backup sera cree dans Assets/_Backups/.\n\nContinuer ?",
                        "Apply",
                        "Cancel"))
                    {
                        Apply();
                    }
                }
                GUI.enabled = true;
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Report", EditorStyles.boldLabel);
            _scroll = EditorGUILayout.BeginScrollView(_scroll, GUILayout.MinHeight(280));
            EditorGUILayout.TextArea(_lastReport, GUILayout.ExpandHeight(true));
            EditorGUILayout.EndScrollView();
        }

        // ------------------------------------------------------------------
        // Filtrage / scan
        // ------------------------------------------------------------------

        private bool ShouldSkip(string path)
        {
            path = path.Replace('\\', '/');
            if (!path.StartsWith("Assets/_Nymora/")) return true;
            if (path.Contains("/TextMesh Pro/Examples")) return true;
            if (path.Contains("/Photon/")) return true;
            if (path.Contains("/QuantumMenu/")) return true;
            if (path.Contains("_BACKUP_")) return true;
            if (path.Contains("/_Backups/")) return true;
            return false;
        }

        private string[] GetSearchFolders()
        {
            if (string.IsNullOrWhiteSpace(_scopeOverride))
                return new[] { "Assets/_Nymora" };

            string scope = _scopeOverride.Trim().Replace('\\', '/');

            // Si scope pointe sur un fichier precis, retourner son dossier parent ;
            // FindCandidateFiles filtrera ensuite.
            if (scope.EndsWith(".unity") || scope.EndsWith(".prefab"))
                return new[] { Path.GetDirectoryName(scope).Replace('\\', '/') };

            return new[] { scope };
        }

        private List<string> FindCandidateFiles()
        {
            var folders = GetSearchFolders();
            var paths = new List<string>();

            var prefabGuids = AssetDatabase.FindAssets("t:Prefab", folders);
            foreach (var g in prefabGuids)
            {
                var p = AssetDatabase.GUIDToAssetPath(g);
                if (!ShouldSkip(p)) paths.Add(p);
            }

            var sceneGuids = AssetDatabase.FindAssets("t:Scene", folders);
            foreach (var g in sceneGuids)
            {
                var p = AssetDatabase.GUIDToAssetPath(g);
                if (!ShouldSkip(p)) paths.Add(p);
            }

            // Filtrer sur fichier precis si override pointe sur un fichier
            if (!string.IsNullOrWhiteSpace(_scopeOverride))
            {
                string scope = _scopeOverride.Trim().Replace('\\', '/');
                if (scope.EndsWith(".unity") || scope.EndsWith(".prefab"))
                    paths = paths.Where(p => p.Replace('\\', '/') == scope).ToList();
            }

            return paths;
        }

        private void DryRun()
        {
            _matches.Clear();
            var paths = FindCandidateFiles();
            int totalOcc = 0;
            var sb = new StringBuilder();
            sb.Append("=== DRY-RUN — ").Append(paths.Count).AppendLine(" files scanned ===");

            string needle = "guid: " + _oldGuid;
            for (int i = 0; i < paths.Count; i++)
            {
                if (i % 50 == 0)
                    EditorUtility.DisplayProgressBar("Font Swapper — Dry-Run",
                        $"Scan {i + 1}/{paths.Count}", (float)(i + 1) / paths.Count);
                try
                {
                    var text = File.ReadAllText(paths[i]);
                    int count = CountOccurrences(text, needle);
                    if (count > 0)
                    {
                        _matches.Add(new MatchInfo { Path = paths[i], OccurrenceCount = count });
                        totalOcc += count;
                    }
                }
                catch (Exception e)
                {
                    sb.Append("[ERROR] ").Append(paths[i]).Append(": ").AppendLine(e.Message);
                }
            }
            EditorUtility.ClearProgressBar();

            sb.AppendLine();
            sb.Append("=== ").Append(_matches.Count).Append(" files contain old GUID (")
              .Append(totalOcc).AppendLine(" raw YAML occurrences) ===");
            sb.AppendLine();
            foreach (var m in _matches.OrderByDescending(m => m.OccurrenceCount))
                sb.Append(string.Format("  [{0,3}x] ", m.OccurrenceCount)).AppendLine(m.Path);

            sb.AppendLine();
            sb.AppendLine("NB : 1 occurrence YAML = ~1 TMP_Text component (font + material counted ensemble).");
            sb.AppendLine("Le swap reel comptera les components TMP_Text via API, plus precis.");

            _lastReport = sb.ToString();
            Debug.Log($"[FontSwapper] Dry-Run: {_matches.Count} files, {totalOcc} YAML occurrences");
        }

        private static int CountOccurrences(string haystack, string needle)
        {
            int count = 0, i = 0;
            while ((i = haystack.IndexOf(needle, i, StringComparison.Ordinal)) != -1)
            {
                count++;
                i += needle.Length;
            }
            return count;
        }

        // ------------------------------------------------------------------
        // Apply
        // ------------------------------------------------------------------

        private void Apply()
        {
            var newFontPath = AssetDatabase.GUIDToAssetPath(_newGuid);
            var newFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(newFontPath);
            if (newFont == null)
            {
                EditorUtility.DisplayDialog("Error", $"New font asset introuvable (GUID {_newGuid})", "OK");
                return;
            }

            var oldFontPath = AssetDatabase.GUIDToAssetPath(_oldGuid);
            var oldFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(oldFontPath);
            if (oldFont == null)
            {
                EditorUtility.DisplayDialog("Error", $"Old font asset introuvable (GUID {_oldGuid})", "OK");
                return;
            }

            // Cache les noms : Unity peut destroy les TMP_FontAsset assets lors des
            // OpenScene en chaine, l'access .name plus tard balance MissingRef.
            string newFontName = newFont.name;
            string oldFontName = oldFont.name;

            // Demander sauvegarde de la scene courante avant d'enchainer les OpenScene
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                Debug.Log("[FontSwapper] Apply annule (utilisateur n'a pas voulu sauver scene courante).");
                return;
            }

            // Backup OUTSIDE Assets/ pour eviter conflits GUID meta. Project root.
            string projectRoot = Path.GetDirectoryName(Application.dataPath).Replace('\\', '/');
            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string backupRoot = projectRoot + "/_FontSwapBackups/FontSwap_" + timestamp;
            Directory.CreateDirectory(backupRoot);

            var sb = new StringBuilder();
            sb.Append("=== APPLY — backup: ").Append(backupRoot).AppendLine(" ===");

            int totalComponents = 0;
            int filesProcessed = 0;
            int filesSkippedZero = 0;

            try
            {
                // PHASE 1 : Prefabs (avant scenes pour propager les inheritances)
                var prefabMatches = _matches.Where(m => m.Path.EndsWith(".prefab")).ToList();
                for (int i = 0; i < prefabMatches.Count; i++)
                {
                    var m = prefabMatches[i];
                    EditorUtility.DisplayProgressBar("Font Swapper — Prefabs",
                        $"({i + 1}/{prefabMatches.Count}) {Path.GetFileName(m.Path)}",
                        (float)(i + 1) / prefabMatches.Count);

                    BackupFile(m.Path, backupRoot);
                    var oldFontFreshP = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(oldFontPath);
                    var newFontFreshP = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(newFontPath);
                    int swapped = SwapInPrefab(m.Path, oldFontFreshP, newFontFreshP);
                    if (swapped > 0)
                    {
                        sb.Append(string.Format("  [{0,3}x] ", swapped)).AppendLine(m.Path);
                        totalComponents += swapped;
                        filesProcessed++;
                    }
                    else
                    {
                        filesSkippedZero++;
                    }
                }

                // PHASE 2 : Scenes
                // Reload des assets a chaque iteration car OpenScene(Single) peut
                // unload les TMP_FontAsset references.
                var sceneMatches = _matches.Where(m => m.Path.EndsWith(".unity")).ToList();
                for (int i = 0; i < sceneMatches.Count; i++)
                {
                    var m = sceneMatches[i];
                    EditorUtility.DisplayProgressBar("Font Swapper — Scenes",
                        $"({i + 1}/{sceneMatches.Count}) {Path.GetFileName(m.Path)}",
                        (float)(i + 1) / sceneMatches.Count);

                    BackupFile(m.Path, backupRoot);
                    var oldFontFresh = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(oldFontPath);
                    var newFontFresh = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(newFontPath);
                    int swapped = SwapInScene(m.Path, oldFontFresh, newFontFresh);
                    if (swapped > 0)
                    {
                        sb.Append(string.Format("  [{0,3}x] ", swapped)).AppendLine(m.Path);
                        totalComponents += swapped;
                        filesProcessed++;
                    }
                    else
                    {
                        filesSkippedZero++;
                    }
                }

                // TMP Settings : default font (impacte les NOUVEAUX composants).
                // Reload depuis disque car les OpenScene en chaine ont pu unload
                // l'asset original referencement par 'newFont'.
                if (_updateTmpSettings)
                {
                    const string tmpSettingsPath = "Assets/TextMesh Pro/Resources/TMP Settings.asset";
                    var settings = AssetDatabase.LoadAssetAtPath<TMP_Settings>(tmpSettingsPath);
                    var newFontReloaded = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(newFontPath);
                    if (settings != null && newFontReloaded != null)
                    {
                        BackupFile(tmpSettingsPath, backupRoot);
                        var so = new SerializedObject(settings);
                        var prop = so.FindProperty("m_defaultFontAsset");
                        prop.objectReferenceValue = newFontReloaded;
                        so.ApplyModifiedProperties();
                        EditorUtility.SetDirty(settings);
                        sb.AppendLine("  [TMP Settings] m_defaultFontAsset → " + newFontName);
                    }
                    else
                    {
                        sb.AppendLine("  [WARN] TMP Settings.asset ou newFont introuvable au reload, skip default.");
                    }
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            sb.AppendLine();
            sb.Append("=== DONE — ").Append(filesProcessed).Append(" files touched, ")
              .Append(totalComponents).Append(" TMP_Text swapped (")
              .Append(filesSkippedZero).AppendLine(" files YAML-only / skipped) ===");
            sb.AppendLine();
            sb.AppendLine("Si tu vois des differences visuelles inattendues :");
            sb.AppendLine("  - rollback git checkout Assets/_Nymora/Scenes/... + Assets/_Nymora/Prefabs/...");
            sb.AppendLine("  - ou copie le contenu de " + backupRoot + " par-dessus");

            _lastReport = sb.ToString();
            Debug.Log($"[FontSwapper] Apply: {filesProcessed} files, {totalComponents} components swapped");

            _matches.Clear();
        }

        private static void BackupFile(string srcPath, string backupRoot)
        {
            string relative = srcPath.StartsWith("Assets/") ? srcPath.Substring("Assets/".Length) : srcPath;
            string dest = Path.Combine(backupRoot, relative).Replace('\\', '/');
            Directory.CreateDirectory(Path.GetDirectoryName(dest));
            File.Copy(srcPath, dest, true);
            string meta = srcPath + ".meta";
            if (File.Exists(meta))
                File.Copy(meta, dest + ".meta", true);
        }

        private static int SwapInPrefab(string prefabPath, TMP_FontAsset oldFont, TMP_FontAsset newFont)
        {
            int swapped = 0;
            var root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                foreach (var tmp in root.GetComponentsInChildren<TMP_Text>(true))
                {
                    if (tmp.font == oldFont)
                    {
                        var so = new SerializedObject(tmp);
                        so.FindProperty("m_fontAsset").objectReferenceValue = newFont;
                        so.FindProperty("m_sharedMaterial").objectReferenceValue = newFont.material;
                        so.ApplyModifiedProperties();
                        EditorUtility.SetDirty(tmp);
                        swapped++;
                    }
                }
                if (swapped > 0)
                    PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
            return swapped;
        }

        private static int SwapInScene(string scenePath, TMP_FontAsset oldFont, TMP_FontAsset newFont)
        {
            int swapped = 0;
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            foreach (var go in scene.GetRootGameObjects())
            {
                foreach (var tmp in go.GetComponentsInChildren<TMP_Text>(true))
                {
                    if (tmp.font == oldFont)
                    {
                        var so = new SerializedObject(tmp);
                        so.FindProperty("m_fontAsset").objectReferenceValue = newFont;
                        so.FindProperty("m_sharedMaterial").objectReferenceValue = newFont.material;
                        so.ApplyModifiedProperties();
                        EditorUtility.SetDirty(tmp);
                        swapped++;
                    }
                }
            }
            if (swapped > 0)
                EditorSceneManager.SaveScene(scene);
            return swapped;
        }
    }
}
