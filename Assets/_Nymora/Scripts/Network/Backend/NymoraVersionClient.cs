using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Nymora.Core.Data;

namespace Nymora.Network.Backend
{
    /// <summary>
    /// Resultat d'un check de version client vs serveur.
    /// - IsReachable : true si le backend a repondu correctement.
    /// - IsCompatible : true si GameVersion.Current >= server.minClientVersion.
    /// - IsUpdateAvailable : true si server.currentClientVersion > GameVersion.Current
    ///   (la version est encore compatible mais une mise a jour existe).
    /// </summary>
    public readonly struct VersionCheckResult
    {
        public bool IsReachable { get; }
        public bool IsCompatible { get; }
        public bool IsUpdateAvailable { get; }
        public string MinClientVersion { get; }
        public string CurrentClientVersion { get; }
        public string ErrorMessage { get; }

        /// <summary>URL du zip de la derniere version (vide si rien publie). Utilise par le launcher (L3).</summary>
        public string DownloadUrl { get; }

        /// <summary>Hash sha256 (hex lowercase) du zip, verifie apres telechargement (L3).</summary>
        public string Sha256 { get; }

        private VersionCheckResult(bool reachable, bool compatible, bool updateAvailable,
            string min, string current, string downloadUrl, string sha256, string err)
        {
            IsReachable = reachable;
            IsCompatible = compatible;
            IsUpdateAvailable = updateAvailable;
            MinClientVersion = min;
            CurrentClientVersion = current;
            DownloadUrl = downloadUrl;
            Sha256 = sha256;
            ErrorMessage = err;
        }

        public static VersionCheckResult Reachable(bool compatible, bool updateAvailable,
            string min, string current, string downloadUrl, string sha256)
            => new VersionCheckResult(true, compatible, updateAvailable, min, current, downloadUrl, sha256, null);

        public static VersionCheckResult Unreachable(string err)
            => new VersionCheckResult(false, false, false, null, null, null, null, err);
    }

    /// <summary>
    /// Interroge GET /version au demarrage et compare avec GameVersion.Current.
    /// Si IsCompatible == false, l'appelant doit bloquer l'UI (popup "Mise a jour requise").
    /// </summary>
    public class NymoraVersionClient
    {
        private readonly NymoraApiClient _api;

        public NymoraVersionClient(NymoraApiClient api)
        {
            _api = api != null ? api : throw new ArgumentNullException(nameof(api));
        }

        public async UniTask<VersionCheckResult> CheckAsync(CancellationToken ct = default)
        {
            var res = await _api.GetVersionAsync(ct);
            if (!res.IsSuccess)
            {
                return VersionCheckResult.Unreachable($"/version unreachable (HTTP {res.StatusCode}): {res.ErrorMessage}");
            }

            string clientVer = GameVersion.Current;
            string minVer = res.Data.minClientVersion ?? "0.0.0";
            string currentVer = res.Data.currentClientVersion ?? minVer;

            if (!TryParseSemver(clientVer, out var client) ||
                !TryParseSemver(minVer, out var min) ||
                !TryParseSemver(currentVer, out var current))
            {
                return VersionCheckResult.Unreachable(
                    $"semver parse failed (client={clientVer}, min={minVer}, current={currentVer})");
            }

            bool compatible = CompareSemver(client, min) >= 0;
            bool updateAvailable = CompareSemver(client, current) < 0;
            return VersionCheckResult.Reachable(compatible, updateAvailable, minVer, currentVer,
                res.Data.downloadUrl, res.Data.sha256);
        }

        private struct Semver
        {
            public int Major;
            public int Minor;
            public int Patch;
        }

        private static bool TryParseSemver(string s, out Semver v)
        {
            v = default;
            if (string.IsNullOrEmpty(s)) return false;
            string[] parts = s.Split('.');
            if (parts.Length != 3) return false;
            if (!int.TryParse(parts[0], out v.Major)) return false;
            if (!int.TryParse(parts[1], out v.Minor)) return false;
            if (!int.TryParse(parts[2], out v.Patch)) return false;
            return v.Major >= 0 && v.Minor >= 0 && v.Patch >= 0;
        }

        private static int CompareSemver(Semver a, Semver b)
        {
            if (a.Major != b.Major) return a.Major < b.Major ? -1 : 1;
            if (a.Minor != b.Minor) return a.Minor < b.Minor ? -1 : 1;
            if (a.Patch != b.Patch) return a.Patch < b.Patch ? -1 : 1;
            return 0;
        }
    }
}
