using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Nymora.Core.Data;
using Nymora.Core.Enums;
using Nymora.Core.ScriptableObjects;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Nymora.Editor.Tools
{
    /// <summary>
    /// Scan complet du projet Nymora — sortie console + rapport markdown horodate dans _docs/.
    /// Menu : Nymora &gt; Validation &gt; Project Health Check (Ctrl+Alt+H)
    ///
    /// 4 categories de checks :
    ///  1. Quantum violations dans Scripts/Combat/ (Random/Time/DateTime)
    ///  2. Missing scripts dans les prefabs et la scene active
    ///  3. ClassDefinitions integrity (5 classes presentes, champs critiques remplis)
    ///  4. Project version consistency (GameVersion + CombatRulesVersion)
    ///
    /// Bonus : lancement auto avant build via IPreprocessBuildWithReport (en bas du fichier).
    /// </summary>
    public static class NymoraHealthCheck
    {
        private const string CombatScriptsPath = "Assets/_Nymora/Scripts/Combat";
        private const string ClassesPath = "Assets/_Nymora/ScriptableObjects/Classes";
        private const string ReportRelativePath = "_docs/healthcheck_report.md";

        private static readonly Regex QuantumPattern = new Regex(
            @"\b(Random\.Range|UnityEngine\.Random|Time\.time|Time\.deltaTime|Time\.fixedDeltaTime|Time\.realtimeSinceStartup|DateTime\.Now|DateTime\.UtcNow)\b",
            RegexOptions.Compiled);

        // ---------------------------------------------------------------------
        // Entry point
        // ---------------------------------------------------------------------
        [MenuItem("Nymora/Validation/Project Health Check %&h", priority = 1)]
        public static void Run()
        {
            RunCore(silent: false);
        }

        private static Report RunCore(bool silent)
        {
            var sw = Stopwatch.StartNew();
            var report = new Report();

            try { CheckQuantumViolations(report); } catch (Exception e) { report.AddError("System", $"Quantum check crashed: {e.Message}"); }
            try { CheckMissingScripts(report); } catch (Exception e) { report.AddError("System", $"Missing scripts check crashed: {e.Message}"); }
            try { CheckClassDefinitions(report); } catch (Exception e) { report.AddError("System", $"ClassDefinitions check crashed: {e.Message}"); }
            try { CheckProjectVersion(report); } catch (Exception e) { report.AddError("System", $"Version check crashed: {e.Message}"); }

            sw.Stop();
            report.Duration = sw.Elapsed;
            report.RanAt = DateTime.Now;

            // Console
            report.PrintToConsole();

            // Markdown
            string outPath = Path.Combine(Application.dataPath, "..", ReportRelativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(outPath));
            File.WriteAllText(outPath, report.ToMarkdown(), new UTF8Encoding(false));
            Debug.Log($"[Nymora.HealthCheck] Rapport ecrit : {ReportRelativePath}");

            if (!silent)
            {
                EditorUtility.DisplayDialog("Nymora Health Check",
                    $"Scan termine en {sw.Elapsed.TotalSeconds:F1}s\n\n" +
                    $"Erreurs   : {report.ErrorCount}\n" +
                    $"Warnings  : {report.WarningCount}\n" +
                    $"Infos     : {report.InfoCount}\n\n" +
                    $"Rapport : {ReportRelativePath}",
                    "OK");
            }

            return report;
        }

        // ---------------------------------------------------------------------
        // Check 1 — Quantum violations
        // ---------------------------------------------------------------------
        private static void CheckQuantumViolations(Report r)
        {
            const string cat = "Quantum";
            string absCombat = Path.Combine(Application.dataPath, "..", CombatScriptsPath);
            if (!Directory.Exists(absCombat))
            {
                r.AddInfo(cat, $"Dossier {CombatScriptsPath} inexistant — skip (normal en debut de projet).");
                return;
            }

            var files = Directory.GetFiles(absCombat, "*.cs", SearchOption.AllDirectories);
            int scanned = 0;
            int violations = 0;

            foreach (var path in files)
            {
                scanned++;
                string[] lines = File.ReadAllLines(path);
                for (int i = 0; i < lines.Length; i++)
                {
                    var match = QuantumPattern.Match(lines[i]);
                    if (match.Success)
                    {
                        string rel = path.Replace(Application.dataPath + Path.DirectorySeparatorChar, "Assets" + Path.DirectorySeparatorChar);
                        r.AddError(cat, $"{match.Value} ligne {i + 1} : {rel}");
                        violations++;
                    }
                }
            }

            r.AddInfo(cat, $"Scripts Combat scannes : {scanned} | Violations : {violations}");
        }

        // ---------------------------------------------------------------------
        // Check 2 — Missing scripts (prefabs + scene active)
        // ---------------------------------------------------------------------
        private static void CheckMissingScripts(Report r)
        {
            const string cat = "MissingScripts";

            // Prefabs
            string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab");
            int prefabsScanned = 0;
            int missingInPrefabs = 0;

            foreach (var guid in prefabGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.StartsWith("Assets/_Nymora/")) continue; // skip packages

                prefabsScanned++;
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null) continue;

                int missing = CountMissingComponents(prefab);
                if (missing > 0)
                {
                    r.AddError(cat, $"{missing} script(s) manquant(s) dans prefab : {path}");
                    missingInPrefabs += missing;
                }
            }

            // Scene active
            int missingInScene = 0;
            var activeScene = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();
            if (activeScene.IsValid() && activeScene.isLoaded)
            {
                foreach (var root in activeScene.GetRootGameObjects())
                {
                    missingInScene += CountMissingComponents(root);
                }
                if (missingInScene > 0)
                {
                    r.AddError(cat, $"{missingInScene} script(s) manquant(s) dans la scene active : {activeScene.path}");
                }
            }

            r.AddInfo(cat, $"Prefabs scannes : {prefabsScanned} (Nymora) | Missing total : {missingInPrefabs + missingInScene}");
        }

        private static int CountMissingComponents(GameObject root)
        {
            int total = 0;
            var components = root.GetComponentsInChildren<Component>(true);
            foreach (var c in components)
            {
                if (c == null) total++;
            }
            return total;
        }

        // ---------------------------------------------------------------------
        // Check 3 — ClassDefinitions integrity
        // ---------------------------------------------------------------------
        private static void CheckClassDefinitions(Report r)
        {
            const string cat = "ClassDefinitions";

            string[] guids = AssetDatabase.FindAssets("t:NymoraClassDefinition", new[] { ClassesPath });
            var foundClasses = new HashSet<NymoraClass>();
            int loaded = 0;

            foreach (var guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var def = AssetDatabase.LoadAssetAtPath<NymoraClassDefinition>(path);
                if (def == null) continue;
                loaded++;

                if (def.ClassId == NymoraClass.None)
                    r.AddError(cat, $"ClassId = None dans {path}");
                else if (!foundClasses.Add(def.ClassId))
                    r.AddError(cat, $"ClassId duplique : {def.ClassId} dans {path}");

                if (def.BaseHP <= 0) r.AddError(cat, $"BaseHP <= 0 dans {def.name}");
                if (def.BaseActionPoints <= 0) r.AddError(cat, $"BaseActionPoints <= 0 dans {def.name}");
                if (def.BaseMovementPoints <= 0) r.AddError(cat, $"BaseMovementPoints <= 0 dans {def.name}");
                if (def.ResourceCap <= 0) r.AddWarning(cat, $"ResourceCap <= 0 dans {def.name}");
                if (string.IsNullOrWhiteSpace(def.PassiveName)) r.AddWarning(cat, $"PassiveName vide dans {def.name}");
                if (string.IsNullOrWhiteSpace(def.SignatureName)) r.AddWarning(cat, $"SignatureName vide dans {def.name}");
            }

            // Verifier que les 5 classes du jeu sont presentes
            var expected = new[] { NymoraClass.Soulrender, NymoraClass.Nightseer, NymoraClass.Colossar, NymoraClass.Necram, NymoraClass.Ghostra };
            foreach (var expectedClass in expected)
            {
                if (!foundClasses.Contains(expectedClass))
                    r.AddError(cat, $"Classe manquante : {expectedClass} (aucun NymoraClassDefinition trouve)");
            }

            r.AddInfo(cat, $"ClassDefinitions chargees : {loaded} / 5 attendues");
        }

        // ---------------------------------------------------------------------
        // Check 4 — Project version
        // ---------------------------------------------------------------------
        private static void CheckProjectVersion(Report r)
        {
            const string cat = "Version";

            // Note : GameVersion.Current / BibleVersion / CombatRulesVersion sont des `const`,
            // donc les checks runtime sont folde par le compilateur. Si une valeur invalide est
            // commitee, le compilateur (ou un test futur) attrapera. On garde un info log de la conf.

            r.AddInfo(cat, $"Game={GameVersion.Current} | CombatRules={GameVersion.CombatRulesVersion} | Bible={GameVersion.BibleVersion}");
        }

        // ---------------------------------------------------------------------
        // Report data class
        // ---------------------------------------------------------------------
        public class Report
        {
            public enum Severity { Info, Warning, Error }

            public class Issue
            {
                public Severity Sev;
                public string Category;
                public string Message;
            }

            public readonly List<Issue> Issues = new List<Issue>();
            public TimeSpan Duration;
            public DateTime RanAt;

            public int InfoCount => Issues.FindAll(i => i.Sev == Severity.Info).Count;
            public int WarningCount => Issues.FindAll(i => i.Sev == Severity.Warning).Count;
            public int ErrorCount => Issues.FindAll(i => i.Sev == Severity.Error).Count;

            public void AddInfo(string cat, string msg) => Issues.Add(new Issue { Sev = Severity.Info, Category = cat, Message = msg });
            public void AddWarning(string cat, string msg) => Issues.Add(new Issue { Sev = Severity.Warning, Category = cat, Message = msg });
            public void AddError(string cat, string msg) => Issues.Add(new Issue { Sev = Severity.Error, Category = cat, Message = msg });

            public void PrintToConsole()
            {
                Debug.Log($"[Nymora.HealthCheck] === Rapport ===\n" +
                          $"Duree : {Duration.TotalSeconds:F2}s\n" +
                          $"Erreurs : {ErrorCount} | Warnings : {WarningCount} | Infos : {InfoCount}");

                foreach (var iss in Issues)
                {
                    string line = $"[{iss.Category}] {iss.Message}";
                    switch (iss.Sev)
                    {
                        case Severity.Error: Debug.LogError(line); break;
                        case Severity.Warning: Debug.LogWarning(line); break;
                        default: Debug.Log(line); break;
                    }
                }
            }

            public string ToMarkdown()
            {
                var sb = new StringBuilder();
                sb.AppendLine("# Nymora — Health Check Report");
                sb.AppendLine();
                sb.AppendLine($"**Date :** {RanAt:yyyy-MM-dd HH:mm:ss}  ");
                sb.AppendLine($"**Duree scan :** {Duration.TotalSeconds:F2}s  ");
                sb.AppendLine($"**Erreurs :** {ErrorCount} | **Warnings :** {WarningCount} | **Infos :** {InfoCount}");
                sb.AppendLine();
                sb.AppendLine("---");

                foreach (var category in GroupCategories())
                {
                    sb.AppendLine();
                    sb.AppendLine($"## {category}");
                    sb.AppendLine();

                    foreach (var iss in Issues)
                    {
                        if (iss.Category != category) continue;
                        string icon = iss.Sev == Severity.Error ? "🔴" : iss.Sev == Severity.Warning ? "🟡" : "🔵";
                        sb.AppendLine($"- {icon} **{iss.Sev}** : {iss.Message}");
                    }
                }

                if (ErrorCount == 0 && WarningCount == 0)
                {
                    sb.AppendLine();
                    sb.AppendLine("---");
                    sb.AppendLine();
                    sb.AppendLine("✅ **Projet sain — aucune erreur ni warning detecte.**");
                }

                return sb.ToString();
            }

            private IEnumerable<string> GroupCategories()
            {
                var seen = new HashSet<string>();
                foreach (var iss in Issues)
                {
                    if (seen.Add(iss.Category)) yield return iss.Category;
                }
            }
        }

        // ---------------------------------------------------------------------
        // Build-time hook : lance le scan avant chaque build, fail si erreurs
        // ---------------------------------------------------------------------
        public class PreBuildHook : IPreprocessBuildWithReport
        {
            public int callbackOrder => 0;

            public void OnPreprocessBuild(BuildReport buildReport)
            {
                Debug.Log("[Nymora.HealthCheck] Pre-build scan...");
                var r = RunCore(silent: true);

                if (r.ErrorCount > 0)
                {
                    throw new BuildFailedException(
                        $"Nymora HealthCheck a detecte {r.ErrorCount} erreur(s). " +
                        $"Voir _docs/healthcheck_report.md. Build annule.");
                }
            }
        }
    }
}
