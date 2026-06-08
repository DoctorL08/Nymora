using System.Collections;
using Cysharp.Threading.Tasks;
using Nymora.Core.Data;
using Nymora.Network.Backend;
using UnityEngine;

namespace Nymora.Hub
{
    /// <summary>
    /// Brique 4.9 — Au Start de la scène hub, consume MatchBridge.LastMatchResult et l'affiche
    /// en system line dans le chat.
    ///
    /// Brique 6.3.b/c — RANKED-ONLY :
    ///   - Si le match était CLASSÉ (MatchBridge.LastIsRanked) → POST /ranked/report-result.
    ///     Le backend settle (double-accord) : MMR + XP + Nymos + push WS. Le client ne fait
    ///     PLUS d'award lui-même.
    ///   - L'ancien wiring MVP temporaire (award XP/Nymos client-side pour casual + IA) est RETIRÉ
    ///     (décision verrouillée : XP/Nymos = ranked-only en prod, cf project-xp-source-ranked-only).
    ///   - Affiche le nouveau MMR (event WS MMR_UPDATED) en system line.
    /// </summary>
    public sealed class HubMatchResultDisplay : MonoBehaviour
    {
        [SerializeField] private HubChatUI _chatUI;

        [Header("Backend (report ranked)")]
        [SerializeField] private NymoraBackendSettings _backendSettings;
        // Fallback de classe si DeckBridge.PendingClassId + SelectedClassPreferences sont vides.
        [SerializeField] private string _fallbackClassId = "Soulrender";

        private NymoraApiClient _api;

        private void Awake()
        {
            if (_backendSettings != null) _api = new NymoraApiClient(_backendSettings);
            var chat = HubChatClient.Instance;
            if (chat != null)
            {
                chat.OnMmrUpdated += HandleMmrUpdated;
                // 6.3.b — feedback XP/Nymos ranked piloté par les events WS du backend
                // (le backend award et push ; on affiche). Filtré sur source/reason "ranked".
                chat.OnXpAwarded += HandleXpAwarded;
                chat.OnWalletUpdate += HandleWalletUpdate;
            }
        }

        private void OnDestroy()
        {
            var chat = HubChatClient.Instance;
            if (chat != null)
            {
                chat.OnMmrUpdated -= HandleMmrUpdated;
                chat.OnXpAwarded -= HandleXpAwarded;
                chat.OnWalletUpdate -= HandleWalletUpdate;
            }
        }

        private void HandleXpAwarded(HubChatClient.XpAwardedData d)
        {
            if (string.IsNullOrEmpty(d.Source) || !d.Source.StartsWith("ranked")) return;
            string line = d.NewLevel > d.OldLevel
                ? $"<color=#ffd700>+{d.Gained} XP {d.ClassId} — NIVEAU {d.NewLevel} !</color>"
                : $"<color=#aaffaa>+{d.Gained} XP {d.ClassId}</color>";
            Debug.Log($"[HubMatchResultDisplay] {line}");
            if (_chatUI != null) _chatUI.AppendSystemLineExternal(line);
        }

        private void HandleWalletUpdate(HubChatClient.WalletUpdateData d)
        {
            if (string.IsNullOrEmpty(d.Reason) || !d.Reason.StartsWith("ranked")) return;
            if (d.DeltaAmount == 0) return;
            string sign = d.DeltaAmount >= 0 ? "+" : "";
            // ◆ ASCII (cf feedback-tmp-no-emoji-liberationsans)
            var line = $"<color=#f5dc66>{sign}{d.DeltaAmount} ◆ {d.DeltaCurrency}</color>";
            Debug.Log($"[HubMatchResultDisplay] {line}");
            if (_chatUI != null) _chatUI.AppendSystemLineExternal(line);
        }

        private void Start()
        {
            if (!MatchBridge.HasPendingResult) return;
            StartCoroutine(DisplayAfterEndOfFrame());
        }

        private IEnumerator DisplayAfterEndOfFrame()
        {
            yield return new WaitForEndOfFrame();

            var result = MatchBridge.ConsumeLastResult(out var matchId, out var opponentEmail,
                out var opponentDisplayName, out var opponentSub, out var isRanked);

            string color;
            string label;
            switch (result)
            {
                case MatchResult.Victory: color = "#88ff88"; label = "VICTOIRE"; break;
                case MatchResult.Defeat:  color = "#ff8888"; label = "DÉFAITE"; break;
                case MatchResult.Draw:    color = "#cccccc"; label = "ÉGALITÉ"; break;
                default: yield break;
            }

            string opponentLabel = !string.IsNullOrEmpty(opponentDisplayName) ? opponentDisplayName : opponentEmail;
            string rankedTag = isRanked ? "[CLASSÉ] " : "[MATCH] ";
            var line = $"<color={color}>{rankedTag}{label} vs {opponentLabel}</color>";
            Debug.Log($"[HubMatchResultDisplay] {line} (matchId={SafeShortId(matchId)} ranked={isRanked})");
            if (_chatUI != null) _chatUI.AppendSystemLineExternal(line);

            // 6.3.b — report ranked au backend (qui award MMR + XP + Nymos via double-accord).
            // Casual / IA : aucun award (6.3.c — XP/Nymos = ranked-only).
            if (isRanked)
            {
                if (_api == null || string.IsNullOrEmpty(opponentSub))
                {
                    Debug.LogWarning($"[HubMatchResultDisplay] Report ranked impossible (api={_api != null}, opponentSub='{opponentSub}').");
                }
                else
                {
                    string resultStr = result == MatchResult.Victory ? "win"
                        : result == MatchResult.Defeat ? "loss" : "draw";
                    ReportRankedAsync(matchId, opponentSub, ResolveClassId(), resultStr).Forget();
                }
            }
        }

        private string ResolveClassId()
        {
            if (!string.IsNullOrEmpty(DeckBridge.PendingClassId)) return DeckBridge.PendingClassId;
            string sel = SelectedClassPreferences.Get();
            return !string.IsNullOrEmpty(sel) ? sel : _fallbackClassId;
        }

        // Item mineur I (8 juin) — deck joué (6 spellIds tech) pour les stats sorts/decks les plus
        // joués. Source = DeckBridge (deck envoyé au combat). Null si pas de deck valide -> backend OK.
        private static string[] ResolveDeck()
            => DeckBridge.HasPending ? DeckBridge.PendingSpellIds : null;

        private async UniTask ReportRankedAsync(string matchId, string opponentSub, string classId, string result)
        {
            string token = HubChatClient.Instance?.DevToken;
            if (string.IsNullOrEmpty(token)) return;
            _api.SetBearerToken(token);
            string[] deck = ResolveDeck();

            // S-STATS.b — joint les stats de combat (View-observed via CombatStatsCollector)
            // si elles ont ete collectees. Sinon report classique (pas de stats -> pas de
            // succes de combat faussement debloque). Reset apres pour ne pas fuiter au prochain match.
            ApiResult<RankedReportResponse> res;
            if (MatchBridge.HasCombatStats)
            {
                var stats = new RankedReportStats
                {
                    damageDealt = MatchBridge.StatDamageDealt,
                    damageTaken = MatchBridge.StatDamageTaken,
                    spellsCast = MatchBridge.StatSpellsCast,
                    turns = MatchBridge.StatTurns,
                };
                Debug.Log($"[HubMatchResultDisplay] Report ranked AVEC stats : dealt={stats.damageDealt} taken={stats.damageTaken} spells={stats.spellsCast} tours={stats.turns}");
                res = await _api.ReportRankedResultAsync(matchId, opponentSub, classId, result, stats, deck);
            }
            else
            {
                res = await _api.ReportRankedResultAsync(matchId, opponentSub, classId, result, deck);
            }
            MatchBridge.ResetCombatStats();
            if (!res.IsSuccess)
            {
                Debug.LogWarning($"[HubMatchResultDisplay] ReportRankedResult failed: {res.StatusCode} {res.ErrorMessage}");
                return;
            }
            Debug.Log($"[HubMatchResultDisplay] Ranked report → status={res.Data.status} (matchId={SafeShortId(matchId)})");
            // Le MMR final + XP + Nymos arrivent via WS (MMR_UPDATED / XP_AWARDED / WALLET_UPDATE)
            // une fois que l'adversaire a aussi reporté (double-accord).
        }

        // 6.3.b — affiche le nouveau MMR + son delta quand le match ranked est settle.
        private void HandleMmrUpdated(HubChatClient.MmrUpdateData d)
        {
            string sign = d.Delta >= 0 ? "+" : "";
            string deltaColor = d.Delta >= 0 ? "#88ff88" : "#ff8888";
            var line = $"<color=#cdb4ff>[CLASSÉ]</color> {RankLadder.ColoredName(d.Mmr)} <color=#cdb4ff>— MMR {d.Mmr}</color> " +
                       $"<color={deltaColor}>({sign}{d.Delta})</color> <color=#999999>— {d.RankedWins}V / {d.RankedLosses}D</color>";
            Debug.Log($"[HubMatchResultDisplay] {line}");
            if (_chatUI != null) _chatUI.AppendSystemLineExternal(line);
        }

        private static string SafeShortId(string id)
        {
            if (string.IsNullOrEmpty(id)) return "?";
            return id.Length >= 8 ? id.Substring(0, 8) : id;
        }
    }
}
