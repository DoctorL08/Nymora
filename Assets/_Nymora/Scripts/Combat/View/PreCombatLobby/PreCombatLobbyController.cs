using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Nymora.Core.Data;
using Photon.Client;
using Photon.Realtime;
using UnityEngine;

namespace Nymora.Combat.View.PreCombatLobby
{
    /// <summary>
    /// Lobby pré-combat (B2) — Orchestration réseau du lobby PvP qui s'intercale entre la
    /// connexion à la room Photon et le démarrage de la session Quantum (cf CombatBootstrapCasual).
    ///
    /// VIEW/NETWORK ONLY : ce component vit dans Scripts/Combat/View/ (exclu du check temporel du
    /// HealthCheck → Time.unscaledTime y est légitime) et n'a AUCUN contact avec la simulation
    /// déterministe Quantum. L'échange d'infos (pseudo/classe/MMR/ready) passe par les
    /// *player custom properties* Photon Realtime (hors-sim). Le deck choisi repart par le canal
    /// existant (RuntimePlayer.SpellIdValues) au moment du Game.AddPlayer.
    ///
    /// Cycle de vie : créé par le bootstrap après ConnectToRoomAsync, piloté via RunAsync(ct),
    /// puis détruit. La brique B3 branchera une UI (PreCombatLobbyUI) qui s'abonne à
    /// <see cref="OnStateChanged"/> et appelle <see cref="SelectDeck"/> / <see cref="SetReady"/>.
    ///
    /// Résolution du deck (spec) : deck choisi dans le lobby ; sinon deck par défaut (celui
    /// sélectionné dans le deck builder avant le lancement, via PreCombatBridge.DefaultDeckId).
    /// </summary>
    public sealed class PreCombatLobbyController : MonoBehaviour
    {
        // Clés des player properties Photon (courtes pour limiter le trafic). Préfixe 'l' = lobby.
        private const string KeyPseudo = "ln";
        private const string KeyClass = "lc";
        private const string KeyMmr = "lm";
        private const string KeyReady = "lr";
        private const string KeySkin = "ls"; // cosmeticId du skin équipé (flex visuel dans le lobby)

        /// <summary>Durée du compte à rebours de choix (30 s, cf spec).</summary>
        public const float LobbyDurationSeconds = 30f;

        /// <summary>
        /// Garde-fou : si l'adversaire ne se signale jamais prêt (déco pendant le lobby), on
        /// procède quand même après ce délai. Le forfait post-start existant gère un adversaire
        /// réellement absent.
        /// </summary>
        public const float LobbyHardTimeoutSeconds = 35f;

        private RealtimeClient _client;
        private bool _localReady;
        private PreCombatDeckInfo _selectedDeck;

        // ===== État local (exposé pour l'UI B3) =====
        public string LocalPseudo { get; private set; }
        public int LocalClassValue { get; private set; } = -1;
        public int LocalMmr { get; private set; }
        public string LocalSkinId { get; private set; } = "";
        public bool LocalReady => _localReady;
        public PreCombatDeckInfo SelectedDeck => _selectedDeck;
        public IReadOnlyList<PreCombatDeckInfo> Decks { get; private set; }
        public float SecondsRemaining { get; private set; }

        // ===== Snapshot adverse (rafraîchi à chaque poll) =====
        public bool OpponentPresent { get; private set; }
        public string OpponentPseudo { get; private set; }
        public int OpponentClassValue { get; private set; } = -1;
        public int OpponentMmr { get; private set; }
        public string OpponentSkinId { get; private set; } = "";
        public bool OpponentReady { get; private set; }

        /// <summary>Notifie l'UI (B3) à chaque changement d'état (poll, sélection, ready).</summary>
        public event Action OnStateChanged;

        public void Init(RealtimeClient client, string localPseudo, int localClassValue, int localMmr,
                         string localSkinId, IReadOnlyList<PreCombatDeckInfo> decks, string defaultDeckId)
        {
            _client = client;
            LocalPseudo = localPseudo;
            LocalClassValue = localClassValue;
            LocalMmr = localMmr;
            LocalSkinId = localSkinId ?? "";
            Decks = decks;
            _selectedDeck = FindDeck(defaultDeckId) ?? (decks != null && decks.Count > 0 ? decks[0] : null);
            SecondsRemaining = LobbyDurationSeconds;
        }

        /// <summary>Appelé par l'UI (B3) : change le deck sélectionné tant que le joueur n'est pas prêt.</summary>
        public void SelectDeck(string deckId)
        {
            if (_localReady) return;
            var d = FindDeck(deckId);
            if (d == null || d == _selectedDeck) return;
            _selectedDeck = d;
            OnStateChanged?.Invoke();
        }

        /// <summary>Appelé par l'UI (B3) ou automatiquement au timeout : verrouille la sélection + diffuse ready.</summary>
        public void SetReady()
        {
            if (_localReady) return;
            _localReady = true;
            PublishLocalProps();
            OnStateChanged?.Invoke();
        }

        /// <summary>
        /// Boucle du lobby. Résout quand (local prêt && adversaire prêt), ou au timeout 30 s
        /// (auto-ready avec la sélection courante), ou au garde-fou 35 s (adversaire absent).
        /// Retourne le deck résolu (sélection lobby, déjà initialisée sur le deck par défaut).
        /// </summary>
        public async Task<PreCombatDeckInfo> RunAsync(CancellationToken ct)
        {
            // B3 — Construit l'UI runtime (sur ce même GameObject) et la lie à cet état.
            // L'UI lève le voile de chargement (SceneTransition.SignalReady) dès qu'elle s'affiche.
            var ui = gameObject.AddComponent<PreCombatLobbyUI>();
            ui.Bind(this);

            PublishLocalProps(); // ready = false au départ
            float start = Time.unscaledTime;

            while (!ct.IsCancellationRequested)
            {
                // CRITIQUE : pendant le lobby (avant StartAsync), aucun driver de plus haut niveau
                // ne pompe le client Photon (le QuantumNetworkCommunicator ne prend la main qu'une
                // fois la session démarrée). Sans Service() ici, SetCustomProperties n'est jamais
                // envoyé, les props adverses jamais reçues, et le client finit par se déconnecter
                // ("SendOutgoingCommands not called"). On pompe donc manuellement à chaque frame.
                _client?.Service();

                float elapsed = Time.unscaledTime - start;
                SecondsRemaining = Mathf.Max(0f, LobbyDurationSeconds - elapsed);
                ReadOpponent();

                // Timer écoulé → auto-ready (verrouille la sélection courante).
                if (SecondsRemaining <= 0f && !_localReady) SetReady();

                OnStateChanged?.Invoke();

                if (_localReady && OpponentReady) break;      // les deux prêts → combat
                if (elapsed >= LobbyHardTimeoutSeconds) break; // adversaire bloqué/absent → on procède

                await Task.Yield();
            }

            return _selectedDeck;
        }

        private void PublishLocalProps()
        {
            if (_client?.LocalPlayer == null) return;
            var props = new PhotonHashtable();
            props[KeyPseudo] = LocalPseudo ?? string.Empty;
            props[KeyClass] = LocalClassValue;
            props[KeyMmr] = LocalMmr;
            props[KeySkin] = LocalSkinId ?? string.Empty;
            props[KeyReady] = _localReady;
            _client.LocalPlayer.SetCustomProperties(props);
        }

        private void ReadOpponent()
        {
            var room = _client?.CurrentRoom;
            if (room == null || room.Players == null)
            {
                OpponentPresent = false;
                return;
            }

            Player opp = null;
            int localActor = _client.LocalPlayer != null ? _client.LocalPlayer.ActorNumber : -1;
            foreach (var kv in room.Players)
            {
                if (kv.Value != null && kv.Value.ActorNumber != localActor)
                {
                    opp = kv.Value;
                    break;
                }
            }

            if (opp == null)
            {
                OpponentPresent = false;
                return;
            }

            OpponentPresent = true;
            var cp = opp.CustomProperties;
            if (cp == null) return;

            if (cp.ContainsKey(KeyPseudo) && cp[KeyPseudo] is string n) OpponentPseudo = n;
            if (cp.ContainsKey(KeyClass) && cp[KeyClass] is int c) OpponentClassValue = c;
            if (cp.ContainsKey(KeyMmr) && cp[KeyMmr] is int m) OpponentMmr = m;
            if (cp.ContainsKey(KeySkin) && cp[KeySkin] is string sk) OpponentSkinId = sk;
            if (cp.ContainsKey(KeyReady) && cp[KeyReady] is bool r) OpponentReady = r;
        }

        private PreCombatDeckInfo FindDeck(string deckId)
        {
            if (string.IsNullOrEmpty(deckId) || Decks == null) return null;
            foreach (var d in Decks)
                if (d != null && d.Id == deckId) return d;
            return null;
        }
    }
}
