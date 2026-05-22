using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using Nymora.Core.Logging;
using UnityEngine;

namespace Nymora.Network.Backend
{
    /// <summary>
    /// Resultat du LANCEMENT de l'installation (pas de sa fin : le jeu se ferme juste apres).
    /// - Started : le script de patch a ete lance et le jeu doit maintenant quitter.
    /// - Message : info a afficher (ex: pourquoi on n'a pas pu lancer, ou mode editeur).
    /// </summary>
    public readonly struct InstallStartResult
    {
        public bool Started { get; }
        public string Message { get; }

        private InstallStartResult(bool started, string message)
        {
            Started = started;
            Message = message;
        }

        public static InstallStartResult Ok(string msg) => new InstallStartResult(true, msg);
        public static InstallStartResult Skipped(string msg) => new InstallStartResult(false, msg);
    }

    /// <summary>
    /// Brique L4 — Installe une mise a jour deja telechargee (zip) puis relance le jeu.
    ///
    /// Contrainte Windows : un process verrouille son propre .exe + ses DLL. On ne peut donc
    /// pas s'auto-ecraser depuis le process en cours. La parade : on ecrit un script update.bat
    /// (dans le dossier temp, HORS de l'install) qui :
    ///   1. attend la fermeture du jeu (par PID),
    ///   2. extrait le zip (PowerShell Expand-Archive),
    ///   3. copie par-dessus l'install (robocopy /E),
    ///   4. relance Nymora.exe,
    ///   5. nettoie (zip + staging + lui-meme).
    /// Puis l'appelant ferme le jeu (Application.Quit), laissant le bat finir le travail.
    ///
    /// Desactive dans l'editeur (pas de .exe a remplacer) et hors Windows.
    /// </summary>
    public static class LauncherInstaller
    {
        public static InstallStartResult StartInstall(string zipPath)
        {
            if (Application.isEditor)
            {
                return InstallStartResult.Skipped(
                    "Installation auto desactivee dans l'editeur. Teste dans un vrai build Windows.");
            }

            if (Application.platform != RuntimePlatform.WindowsPlayer)
            {
                return InstallStartResult.Skipped("Installation auto supportee uniquement sur Windows.");
            }

            if (string.IsNullOrEmpty(zipPath) || !File.Exists(zipPath))
            {
                return InstallStartResult.Skipped($"Zip introuvable : {zipPath}");
            }

            try
            {
                var current = Process.GetCurrentProcess();
                string exePath = current.MainModule != null ? current.MainModule.FileName : null;
                if (string.IsNullOrEmpty(exePath) || !File.Exists(exePath))
                {
                    return InstallStartResult.Skipped("Impossible de localiser Nymora.exe.");
                }

                int pid = current.Id;
                string installDir = Path.GetDirectoryName(exePath);
                string updateDir = Path.GetDirectoryName(zipPath);
                string stagingDir = Path.Combine(updateDir, "staging");
                string batPath = Path.Combine(updateDir, "update.bat");
                string logPath = Path.Combine(updateDir, "update.log");

                File.WriteAllText(batPath, BuildBatScript(pid, zipPath, installDir, exePath, stagingDir, logPath), new UTF8Encoding(false));
                NymoraLog.Info("Launcher", $"update.bat ecrit : {batPath}");

                // Lance le bat detache, dans une fenetre minimisee (l'utilisateur voit la progression).
                var psi = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/c \"\"{batPath}\"\"",
                    UseShellExecute = true,
                    CreateNoWindow = false,
                    WindowStyle = ProcessWindowStyle.Minimized,
                    WorkingDirectory = updateDir,
                };
                Process.Start(psi);

                return InstallStartResult.Ok("Patch lance, fermeture du jeu...");
            }
            catch (Exception e)
            {
                return InstallStartResult.Skipped($"Echec du lancement de l'installation : {e.Message}");
            }
        }

        /// <summary>
        /// Genere le contenu du update.bat. Les chemins sont injectes tels quels (chemins Windows
        /// avec backslash, valides en batch). Toutes les variables sont quotees a l'usage.
        /// </summary>
        private static string BuildBatScript(int pid, string zipPath, string installDir, string exePath,
            string stagingDir, string logPath)
        {
            var sb = new StringBuilder();
            sb.AppendLine("@echo off");
            sb.AppendLine("setlocal");
            sb.AppendLine("chcp 65001 >nul");
            sb.AppendLine($"set \"PID={pid}\"");
            sb.AppendLine($"set \"ZIP={zipPath}\"");
            sb.AppendLine($"set \"INSTALL={installDir}\"");
            sb.AppendLine($"set \"EXE={exePath}\"");
            sb.AppendLine($"set \"STAGING={stagingDir}\"");
            sb.AppendLine($"set \"LOG={logPath}\"");
            sb.AppendLine();
            sb.AppendLine("echo Mise a jour de Nymora en cours... > \"%LOG%\"");
            sb.AppendLine("echo Mise a jour de Nymora en cours, ne fermez pas cette fenetre.");
            sb.AppendLine();
            sb.AppendLine("rem 1. Attendre la fermeture du jeu (par PID)");
            sb.AppendLine(":waitloop");
            sb.AppendLine("tasklist /FI \"PID eq %PID%\" 2>nul | find \"%PID%\" >nul");
            sb.AppendLine("if not errorlevel 1 (");
            sb.AppendLine("  timeout /t 1 /nobreak >nul");
            sb.AppendLine("  goto waitloop");
            sb.AppendLine(")");
            sb.AppendLine();
            sb.AppendLine("rem 2. Extraire le zip dans un dossier de staging");
            sb.AppendLine("if exist \"%STAGING%\" rmdir /s /q \"%STAGING%\"");
            sb.AppendLine("powershell -NoProfile -ExecutionPolicy Bypass -Command \"Expand-Archive -LiteralPath '%ZIP%' -DestinationPath '%STAGING%' -Force\" >> \"%LOG%\" 2>&1");
            sb.AppendLine("if not exist \"%STAGING%\\Nymora.exe\" (");
            sb.AppendLine("  echo ERREUR: extraction echouee, Nymora.exe absent du zip. >> \"%LOG%\"");
            sb.AppendLine("  start \"\" \"%EXE%\"");
            sb.AppendLine("  goto cleanup");
            sb.AppendLine(")");
            sb.AppendLine();
            sb.AppendLine("rem 3. Copier par-dessus l'install (/E sous-dossiers, garde les fichiers en plus)");
            sb.AppendLine("robocopy \"%STAGING%\" \"%INSTALL%\" /E /NFL /NDL /NJH /NJS /NC /NS >> \"%LOG%\" 2>&1");
            sb.AppendLine();
            sb.AppendLine("rem 4. Relancer le jeu (working dir = dossier d'install)");
            sb.AppendLine("start \"\" /D \"%INSTALL%\" \"%EXE%\"");
            sb.AppendLine();
            sb.AppendLine(":cleanup");
            sb.AppendLine("rem 5. Nettoyage (le bat se supprime lui-meme en dernier)");
            sb.AppendLine("if exist \"%STAGING%\" rmdir /s /q \"%STAGING%\"");
            sb.AppendLine("del /q \"%ZIP%\"");
            sb.AppendLine("del /q \"%~f0\"");
            return sb.ToString();
        }
    }
}
