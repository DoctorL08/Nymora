using System.Collections;
using Cysharp.Threading.Tasks;
using Nymora.Network.Backend;
using UnityEngine;

namespace Nymora.Hub
{
    /// <summary>
    /// Brique 4.9 (stub) — Au Start de la scène hub, consume MatchBridge.LastMatchResult
    /// si présent et l'affiche comme system line dans le chat.
    ///
    /// Brique 5.1.e — TEMP MVP : award XP de classe selon le résultat (V=50, D=15, Draw=25).
    /// À RETIRER quand Phase 6 ranked livré (cf project-xp-source-ranked-only).
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

            var result = MatchBridge.ConsumeLastResult(out var matchId, out var opponentEmail);
            string color;
            string label;
            int xpGained;
            switch (result)
            {
                case MatchResult.Victory:
                    color = "#88ff88";
                    label = "VICTOIRE";
                    xpGained = _xpVictory;
                    break;
                case MatchResult.Defeat:
                    color = "#ff8888";
                    label = "DÉFAITE";
                    xpGained = _xpDefeat;
                    break;
                case MatchResult.Draw:
                    color = "#cccccc";
                    label = "ÉGALITÉ";
                    xpGained = _xpDraw;
                    break;
                default:
                    yield break;
            }

            var line = $"<color={color}>[MATCH] {label} vs {opponentEmail} (id={SafeShortId(matchId)})</color>";
            Debug.Log($"[HubMatchResultDisplay] {line}");

            if (_chatUI != null)
            {
                _chatUI.AppendSystemLineExternal(line);
            }

            // 5.1.e TEMP MVP — award XP via REST (fire-and-forget UniTask)
            if (_api != null && xpGained > 0)
            {
                AwardXpAsync(xpGained, $"casual_{result}").Forget();
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

        private static string SafeShortId(string id)
        {
            if (string.IsNullOrEmpty(id)) return "?";
            return id.Length >= 8 ? id.Substring(0, 8) : id;
        }
    }
}
