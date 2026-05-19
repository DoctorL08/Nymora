using System.Collections;
using Cysharp.Threading.Tasks;
using Nymora.Core.Data;
using Nymora.Network.Backend;
using UnityEngine;

namespace Nymora.Hub
{
    /// <summary>
    /// Brique 4.9 (stub) — Au Start de la scène hub, consume MatchBridge.LastMatchResult
    /// si présent et l'affiche comme system line dans le chat.
    ///
    /// Brique 5.1.e — TEMP MVP : award XP de classe selon le résultat (V=50, D=15, Draw=25).
    /// Brique 5.4.c — TEMP MVP : award Nymos selon le résultat (V=200, D=50, Draw=100).
    /// À RETIRER tous les 2 quand Phase 6 ranked livré (cf project-xp-source-ranked-only).
    /// Idempotence Nymos via `match_<id>_<result>` : si le scene est relancé, pas de double-credit.
    /// </summary>
    public sealed class HubMatchResultDisplay : MonoBehaviour
    {
        [SerializeField] private HubChatUI _chatUI;

        // 5.1.e TEMP MVP — à retirer quand ranked Phase 6 prendra le relais
        [Header("XP MVP (TEMP — retirer quand ranked Phase 6)")]
        [SerializeField] private NymoraBackendSettings _backendSettings;
        [SerializeField] private string _devClassId = "Soulrender";
        [SerializeField] private int _xpVictory = 50;
        [SerializeField] private int _xpDefeat = 15;
        [SerializeField] private int _xpDraw = 25;

        // 5.4.c TEMP MVP — award Nymos fin de match casual (à retirer Phase 6 ranked)
        [Header("Nymos MVP (TEMP — retirer quand ranked Phase 6)")]
        [SerializeField] private int _nymosVictory = 200;
        [SerializeField] private int _nymosDefeat = 50;
        [SerializeField] private int _nymosDraw = 100;

        private NymoraApiClient _api;

        private void Awake()
        {
            if (_backendSettings != null) _api = new NymoraApiClient(_backendSettings);
        }

        private void Start()
        {
            if (!MatchBridge.HasPendingResult) return;
            StartCoroutine(DisplayAfterEndOfFrame());
        }

        private IEnumerator DisplayAfterEndOfFrame()
        {
            yield return new WaitForEndOfFrame();

            // POLISH-7 (20 mai) : ConsumeLastResult retourne maintenant opponentDisplayName en plus.
            var result = MatchBridge.ConsumeLastResult(out var matchId, out var opponentEmail, out var opponentDisplayName);
            string color;
            string label;
            int xpGained;
            int nymosGained;
            switch (result)
            {
                case MatchResult.Victory:
                    color = "#88ff88";
                    label = "VICTOIRE";
                    xpGained = _xpVictory;
                    nymosGained = _nymosVictory;
                    break;
                case MatchResult.Defeat:
                    color = "#ff8888";
                    label = "DÉFAITE";
                    xpGained = _xpDefeat;
                    nymosGained = _nymosDefeat;
                    break;
                case MatchResult.Draw:
                    color = "#cccccc";
                    label = "ÉGALITÉ";
                    xpGained = _xpDraw;
                    nymosGained = _nymosDraw;
                    break;
                default:
                    yield break;
            }

            // POLISH-7 : affichage pseudo officiel ; fallback email si vieux match (champ vide).
            // Le "(id=xxxx)" cote chat a ete retire (vestige debug 4.8.b/c) ; le matchId reste
            // visible dans la console via le Debug.Log ci-dessous pour le diagnostic.
            string opponentLabel = !string.IsNullOrEmpty(opponentDisplayName) ? opponentDisplayName : opponentEmail;
            var line = $"<color={color}>[MATCH] {label} vs {opponentLabel}</color>";
            Debug.Log($"[HubMatchResultDisplay] {line} (matchId={SafeShortId(matchId)})");

            if (_chatUI != null)
            {
                _chatUI.AppendSystemLineExternal(line);
            }

            // 5.1.e TEMP MVP — award XP via REST (fire-and-forget UniTask)
            if (_api != null && xpGained > 0)
            {
                AwardXpAsync(xpGained, $"casual_{result}").Forget();
            }

            // 5.4.c TEMP MVP — award Nymos via REST avec idempotencyKey pour bloquer double-credit
            if (_api != null && nymosGained > 0)
            {
                AwardNymosAsync(nymosGained, matchId, result).Forget();
            }
        }

        private async UniTask AwardXpAsync(int amount, string source)
        {
            string token = HubChatClient.Instance?.DevToken;
            if (string.IsNullOrEmpty(token)) return;
            _api.SetBearerToken(token);
            var res = await _api.AwardXpAsync(_devClassId, amount, source);
            if (res.IsSuccess)
            {
                Debug.Log($"[HubMatchResultDisplay] XP awarded +{amount} {_devClassId} → L{res.Data.level} ({res.Data.xp}/{res.Data.xpToNext})");
                if (_chatUI != null)
                {
                    string xpLine = res.Data.leveledUp
                        ? $"<color=#ffd700>+{amount} XP {_devClassId} — NIVEAU {res.Data.level} !</color>"
                        : $"<color=#aaffaa>+{amount} XP {_devClassId}</color>";
                    _chatUI.AppendSystemLineExternal(xpLine);
                }
            }
            else
            {
                Debug.LogWarning($"[HubMatchResultDisplay] AwardXp failed: {res.StatusCode} {res.ErrorMessage}");
            }
        }

        private async UniTask AwardNymosAsync(int amount, string matchId, MatchResult result)
        {
            string token = HubChatClient.Instance?.DevToken;
            if (string.IsNullOrEmpty(token)) return;
            _api.SetBearerToken(token);

            // Idempotency : match_<id>_<result>. Si le scene relance ou si un retry a lieu,
            // le backend retourne alreadyApplied=true et le solde reste inchange.
            // Si matchId vide (cas combat IA sans matchId Photon), on degrade vers timestamp.
            string idem = !string.IsNullOrEmpty(matchId)
                ? $"match_{matchId}_{result}"
                : $"match_local_{System.DateTime.UtcNow.Ticks}_{result}";
            string reason = $"casual_{result}";

            var res = await _api.AwardCurrencyAsync("Nymos", amount, reason, idem);
            if (!res.IsSuccess)
            {
                Debug.LogWarning($"[HubMatchResultDisplay] AwardNymos failed: {res.StatusCode} {res.ErrorMessage}");
                return;
            }
            if (res.Data.alreadyApplied)
            {
                Debug.Log($"[HubMatchResultDisplay] AwardNymos idempotent skip (already credited for {idem})");
                return;
            }
            Debug.Log($"[HubMatchResultDisplay] Nymos awarded +{amount} (solde {res.Data.wallet.nymos})");
            if (_chatUI != null)
            {
                // ◆ Nymos (cf feedback-tmp-no-emoji-liberationsans)
                _chatUI.AppendSystemLineExternal($"<color=#f5dc66>+{amount} ◆ Nymos</color>");
            }
        }

        private static string SafeShortId(string id)
        {
            if (string.IsNullOrEmpty(id)) return "?";
            return id.Length >= 8 ? id.Substring(0, 8) : id;
        }
    }
}
