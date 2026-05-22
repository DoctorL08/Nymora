using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Nymora.Editor.Publishing
{
    /// <summary>
    /// Brique L5 — Outil de publication d'une mise a jour du launcher.
    ///
    /// Ce que fait l'outil (la partie que SEUL Unity peut faire) :
    ///   1. Bumpe le numero de version (PlayerSettings.bundleVersion -&gt; lu par GameVersion.Current).
    ///   2. Build le jeu en Windows Standalone x64 dans &lt;projet&gt;/_publish/build_&lt;version&gt;/.
    ///   3. Zippe le CONTENU du build (Nymora.exe a la racine du zip) -&gt; _publish/nymora-&lt;version&gt;.zip.
    ///   4. Calcule le sha256 du zip + ecrit un manifeste _publish/publish-&lt;version&gt;.json.
    ///
    /// Ensuite, Claude (cote serveur) prend le zip + le manifeste, les uploade sur le VPS,
    /// bumpe les constantes serveur (CURRENT_CLIENT_VERSION / LATEST_ZIP_FILENAME / LATEST_ZIP_SHA256)
    /// et redeploie. C'est le geste "mets le launcher a jour".
    ///
    /// Menu : Nymora &gt; Build &gt; Publish Update
    /// </summary>
    public class PublishUpdateWindow : EditorWindow
    {
        private string _newVersion = "0.1.0";
        private Vector2 _scroll;
        private string _lastReport = string.Empty;
        private bool _busy;

        [MenuItem("Nymora/Build/Publish Update", priority = 80)]
        private static void Open()
        {
            var w = GetWindow<PublishUpdateWindow>(true, "Publish Update (L5)");
            w.minSize = new Vector2(520, 360);
            w._newVersion = SuggestNextVersion(PlayerSettings.bundleVersion);
            w.Show();
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Publication d'une mise a jour Nymora", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            EditorGUILayout.HelpBox(
                $"Version actuelle (bundleVersion) : {PlayerSettings.bundleVersion}\n" +
                "Cet outil bumpe la version, build Windows x64, zippe et calcule le sha256.\n" +
                "Ensuite, donne le zip + le manifeste a Claude (ou dis 'mets le launcher a jour').",
                MessageType.Info);

            EditorGUILayout.Space(4);
            _newVersion = EditorGUILayout.TextField("Nouvelle version (semver)", _newVersion);

            if (!IsValidSemver(_newVersion))
            {
                EditorGUILayout.HelpBox("Format attendu : X.Y.Z (ex: 0.2.0). Trois entiers separes par des points.", MessageType.Warning);
            }

            EditorGUILayout.Space(8);
            using (new EditorGUI.DisabledScope(_busy || !IsValidSemver(_newVersion)))
            {
                if (GUILayout.Button("Build + Package", GUILayout.Height(40)))
                {
                    // Defere pour laisser l'UI se rafraichir avant le build (long).
                    EditorApplication.delayCall += RunPublish;
                }
            }

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Rapport", EditorStyles.boldLabel);
            _scroll = EditorGUILayout.BeginScrollView(_scroll, GUILayout.ExpandHeight(true));
            EditorGUILayout.TextArea(string.IsNullOrEmpty(_lastReport) ? "(rien encore)" : _lastReport,
                GUILayout.ExpandHeight(true));
            EditorGUILayout.EndScrollView();
        }

        private void RunPublish()
        {
            _busy = true;
            try
            {
                string version = _newVersion.Trim();
                string projectRoot = Directory.GetParent(Application.dataPath).FullName;
                string publishDir = Path.Combine(projectRoot, "_publish");
                string buildDir = Path.Combine(publishDir, $"build_{version}");
                string exePath = Path.Combine(buildDir, "Nymora.exe");
                string zipName = $"nymora-{version}.zip";
                string zipPath = Path.Combine(publishDir, zipName);
                string manifestPath = Path.Combine(publishDir, $"publish-{version}.json");

                Directory.CreateDirectory(publishDir);

                // 1. Bump version (recompile-free : bundleVersion est une donnee, pas du code).
                PlayerSettings.bundleVersion = version;
                Log($"bundleVersion -> {version}");

                // 2. Build Windows x64.
                if (Directory.Exists(buildDir)) Directory.Delete(buildDir, true);
                Directory.CreateDirectory(buildDir);

                string[] scenes = EditorBuildSettings.scenes
                    .Where(s => s.enabled)
                    .Select(s => s.path)
                    .ToArray();
                if (scenes.Length == 0)
                {
                    Fail("Aucune scene activee dans les Build Settings. Ajoute au moins 00_Login.");
                    return;
                }
                Log($"Build de {scenes.Length} scene(s) -> {exePath}");

                var options = new BuildPlayerOptions
                {
                    scenes = scenes,
                    locationPathName = exePath,
                    target = BuildTarget.StandaloneWindows64,
                    targetGroup = BuildTargetGroup.Standalone,
                    options = BuildOptions.None,
                };

                BuildReport report = BuildPipeline.BuildPlayer(options);
                if (report.summary.result != BuildResult.Succeeded)
                {
                    Fail($"Build ECHEC : {report.summary.result} ({report.summary.totalErrors} erreurs).");
                    return;
                }
                Log($"Build OK ({report.summary.totalSize / (1024 * 1024)} Mo).");

                // 3. Zip du CONTENU du build (Nymora.exe a la racine du zip).
                if (File.Exists(zipPath)) File.Delete(zipPath);
                Log("Compression du zip...");
                ZipFile.CreateFromDirectory(buildDir, zipPath, System.IO.Compression.CompressionLevel.Optimal, includeBaseDirectory: false);

                // 4. sha256 + manifeste.
                string sha = ComputeSha256(zipPath);
                long size = new FileInfo(zipPath).Length;
                string manifest =
                    "{\n" +
                    $"  \"version\": \"{version}\",\n" +
                    $"  \"fileName\": \"{zipName}\",\n" +
                    $"  \"sha256\": \"{sha}\",\n" +
                    $"  \"sizeBytes\": {size}\n" +
                    "}\n";
                File.WriteAllText(manifestPath, manifest);

                Log("");
                Log("=== PUBLICATION PRETE ===");
                Log($"version  : {version}");
                Log($"zip      : {zipPath}");
                Log($"taille   : {size / (1024 * 1024)} Mo");
                Log($"sha256   : {sha}");
                Log($"manifeste: {manifestPath}");
                Log("");
                Log("-> Donne ces infos a Claude (ou dis 'mets le launcher a jour').");

                EditorUtility.RevealInFinder(zipPath);
                EditorUtility.DisplayDialog("Publish Update",
                    $"Build + package OK pour la version {version}.\n\n" +
                    $"Zip : {zipName} ({size / (1024 * 1024)} Mo)\n" +
                    $"sha256 : {sha}\n\n" +
                    "Le dossier _publish s'est ouvert. Transmets le zip + le manifeste a Claude.",
                    "OK");
            }
            catch (Exception e)
            {
                Fail($"Exception : {e.Message}\n{e.StackTrace}");
            }
            finally
            {
                _busy = false;
                Repaint();
            }
        }

        // --- Helpers ---

        private void Log(string s)
        {
            _lastReport += s + "\n";
            if (!string.IsNullOrEmpty(s)) Debug.Log($"[Nymora.Publish] {s}");
            Repaint();
        }

        private void Fail(string s)
        {
            Log("ERREUR : " + s);
            EditorUtility.DisplayDialog("Publish Update", "Echec : " + s, "OK");
        }

        private static string SuggestNextVersion(string current)
        {
            if (IsValidSemver(current))
            {
                var p = current.Split('.');
                if (int.TryParse(p[2], out int patch))
                {
                    return $"{p[0]}.{p[1]}.{patch + 1}";
                }
            }
            // bundleVersion par defaut "1.0" (ou autre non-semver) -> on amorce proprement.
            return "0.1.0";
        }

        private static bool IsValidSemver(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return false;
            var p = s.Trim().Split('.');
            return p.Length == 3 && p.All(x => int.TryParse(x, out int n) && n >= 0);
        }

        private static string ComputeSha256(string path)
        {
            using var sha = SHA256.Create();
            using var fs = File.OpenRead(path);
            byte[] hash = sha.ComputeHash(fs);
            var sb = new StringBuilder(hash.Length * 2);
            foreach (byte b in hash) sb.Append(b.ToString("x2"));
            return sb.ToString();
        }
    }
}
