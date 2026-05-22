using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using Nymora.Core.Logging;
using UnityEngine.Networking;

namespace Nymora.Network.Backend
{
    /// <summary>
    /// Resultat d'un telechargement de mise a jour.
    /// - IsSuccess : zip telecharge ET (si un hash etait attendu) verifie.
    /// - FilePath  : chemin local du zip telecharge (dossier temp). Consomme par la Brique L4.
    /// </summary>
    public readonly struct UpdateDownloadResult
    {
        public bool IsSuccess { get; }
        public string FilePath { get; }
        public string ErrorMessage { get; }

        private UpdateDownloadResult(bool ok, string path, string err)
        {
            IsSuccess = ok;
            FilePath = path;
            ErrorMessage = err;
        }

        public static UpdateDownloadResult Success(string path) => new UpdateDownloadResult(true, path, null);
        public static UpdateDownloadResult Failure(string err) => new UpdateDownloadResult(false, null, err);
    }

    /// <summary>
    /// Telecharge le zip de mise a jour (Brique L3) en streaming sur disque, rapporte la
    /// progression, et verifie le sha256 attendu. Le zip atterrit dans un dossier temp dedie
    /// que la Brique L4 viendra extraire/installer.
    ///
    /// 100% reseau/IO : aucune dependance UI. La progression est rapportee via IProgress&lt;float&gt;
    /// (0..1) que l'appelant branche sur sa barre.
    /// </summary>
    public class LauncherUpdateService
    {
        /// <summary>Dossier temp dedie aux MaJ Nymora (cree si absent).</summary>
        public static string UpdateDirectory => Path.Combine(Path.GetTempPath(), "Nymora_Update");

        /// <summary>
        /// Telecharge <paramref name="url"/> vers le dossier temp et verifie <paramref name="expectedSha256"/>.
        /// Si le hash attendu est vide, la verification est sautee (avec un warning).
        /// </summary>
        public async UniTask<UpdateDownloadResult> DownloadAndVerifyAsync(
            string url, string expectedSha256, IProgress<float> progress, CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(url))
            {
                return UpdateDownloadResult.Failure("URL de telechargement vide (aucun build publie cote serveur).");
            }

            string fileName;
            try
            {
                fileName = Path.GetFileName(new Uri(url).AbsolutePath);
                if (string.IsNullOrEmpty(fileName)) fileName = "nymora-update.zip";
            }
            catch (Exception e)
            {
                return UpdateDownloadResult.Failure($"URL invalide : {e.Message}");
            }

            string destPath = Path.Combine(UpdateDirectory, fileName);

            try
            {
                Directory.CreateDirectory(UpdateDirectory);
                // Un reliquat d'un essai precedent fausserait le hash : on repart propre.
                if (File.Exists(destPath)) File.Delete(destPath);
            }
            catch (Exception e)
            {
                return UpdateDownloadResult.Failure($"Impossible de preparer le dossier temp : {e.Message}");
            }

            NymoraLog.Info("Launcher", $"Telechargement {url} -> {destPath}");

            using (var req = UnityWebRequest.Get(url))
            {
                req.downloadHandler = new DownloadHandlerFile(destPath) { removeFileOnAbort = true };

                try
                {
                    await req.SendWebRequest().ToUniTask(
                        progress: Progress.Create<float>(p => progress?.Report(p)),
                        cancellationToken: ct);
                }
                catch (OperationCanceledException)
                {
                    SafeDelete(destPath);
                    return UpdateDownloadResult.Failure("Telechargement annule.");
                }
                catch (Exception e)
                {
                    SafeDelete(destPath);
                    return UpdateDownloadResult.Failure($"Echec reseau : {e.Message}");
                }

                if (req.result != UnityWebRequest.Result.Success)
                {
                    SafeDelete(destPath);
                    return UpdateDownloadResult.Failure($"HTTP {req.responseCode} : {req.error}");
                }
            }

            // Verification d'integrite (hors thread principal pour ne pas freezer l'UI).
            if (!string.IsNullOrEmpty(expectedSha256))
            {
                string actual;
                try
                {
                    actual = await UniTask.RunOnThreadPool(() => ComputeSha256(destPath), cancellationToken: ct);
                }
                catch (Exception e)
                {
                    SafeDelete(destPath);
                    return UpdateDownloadResult.Failure($"Echec calcul du hash : {e.Message}");
                }

                if (!string.Equals(actual, expectedSha256.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    SafeDelete(destPath);
                    return UpdateDownloadResult.Failure(
                        $"Integrite KO : sha256 attendu {expectedSha256}, obtenu {actual}. Fichier corrompu, retente.");
                }

                NymoraLog.Info("Launcher", $"sha256 verifie OK ({actual}).");
            }
            else
            {
                NymoraLog.Warn("Launcher", "Aucun sha256 attendu : verification d'integrite sautee.");
            }

            progress?.Report(1f);
            return UpdateDownloadResult.Success(destPath);
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

        private static void SafeDelete(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); }
            catch { /* best effort */ }
        }
    }
}
