using System;
using System.IO;
using Nymora.Core.Data;
using Quantum;
using UnityEngine;

namespace Nymora.Combat.Spectate
{
    /// <summary>
    /// Brique S3 (mode spectateur) — RELAYER du flux déterministe d'un match casual/ranked.
    ///
    /// Créé par CombatBootstrapCasual UNIQUEMENT sur le master client (un seul relayer par match).
    /// Capture le RecordInputStream de Quantum (flux delta-compressé, déjà alimenté car
    /// InputDeltaCompression=ON) et l'envoie au backend en streaming via SpectateRelayBus →
    /// HubChatClient. Le spectateur (S4) re-simule ce flux EN LOCAL (GameMode.Replay) → 0 CCU Quantum.
    ///
    /// Tout passe par CallbackUpdateView (et non GameStarted) car ce component est instancié
    /// APRÈS le démarrage de la session (GameStarted déjà dispatché) : on capture le game et on
    /// agit au fil des frames vérifiées.
    ///
    /// Lecture incrémentale du MemoryStream : pattern save-position / seek / read-tail / restore
    /// (identique à l'instant-replay du SDK Quantum) pour ne pas perturber l'écriture de la sim.
    /// </summary>
    public sealed class SpectateRelay : MonoBehaviour
    {
        private const float FlushIntervalSeconds = 0.5f;

        private string _matchId;
        private bool _isRelayer;

        private long _cursor;        // octets déjà envoyés du RecordInputStream
        private int _seq;            // numéro de chunk (ordre)
        private bool _headerSent;
        private bool _ended;
        private float _flushTimer;
        private QuantumGame _game;

        /// <summary>À appeler juste après l'AddComponent. isRelayer = master client (un seul relayer).</summary>
        public void Init(string matchId, bool isRelayer)
        {
            _matchId = matchId;
            _isRelayer = isRelayer;
        }

        private void Awake()
        {
            QuantumCallback.Subscribe(this, (CallbackUpdateView c) => OnUpdateView(c.Game));
            QuantumCallback.Subscribe(this, (CallbackGameDestroyed c) => OnGameDestroyed(c.Game));
        }

        private void OnUpdateView(QuantumGame game)
        {
            if (!_isRelayer || _ended || string.IsNullOrEmpty(_matchId)) return;
            _game = game;

            var frame = game.Frames.Verified;
            if (frame == null) return;

            // S'assure que l'enregistrement tourne (ReplayRecorder l'a peut-être déjà démarré).
            if (game.RecordInputStream == null)
            {
                try { game.StartRecordingInput(); }
                catch (Exception ex) { Debug.LogWarning("[SpectateRelay] StartRecordingInput : " + ex.Message); return; }
            }

            // Header (config Quantum) une seule fois, dès qu'une frame vérifiée existe.
            if (!_headerSent)
            {
                if (TrySendHeader(game)) _headerSent = true;
                else return;
            }

            // Flush throttlé des nouveaux octets.
            _flushTimer += Time.unscaledDeltaTime;
            if (_flushTimer >= FlushIntervalSeconds)
            {
                _flushTimer = 0f;
                FlushChunk(game);
            }

            // Fin de match → flush final + END.
            if (frame.TryGetSingleton<CombatState>(out var state) && state.CurrentPhase == CombatPhase.MatchEnd)
            {
                FlushChunk(game);
                SendEnd();
            }
        }

        private bool TrySendHeader(QuantumGame game)
        {
            // On attend que le PlayerRef local soit résolu pour mapper correctement les pseudos
            // sur P0/P1 (sinon le spectateur démarrerait avec des noms génériques). < 1s en pratique.
            int localSlot = Nymora.Combat.Bootstrap.CombatBootstrapCasual.Instance?.LocalPlayerSlot ?? -1;
            if (localSlot < 0) return false;

            QuantumReplayFile replay;
            try { replay = game.GetRecordedReplay(includeChecksums: false); }
            catch (Exception ex) { Debug.LogWarning("[SpectateRelay] GetRecordedReplay : " + ex.Message); return false; }
            if (replay == null) return false;

            string runtimeConfigB64;
            try { runtimeConfigB64 = Convert.ToBase64String(replay.RuntimeConfigData.Decode()); }
            catch (Exception ex) { Debug.LogWarning("[SpectateRelay] RuntimeConfig encode : " + ex.Message); return false; }

            var header = new SpectateHeader
            {
                runtimeConfigB64 = runtimeConfigB64,
                sessionConfigJson = JsonUtility.ToJson(replay.DeterministicConfig),
                initialTick = replay.InitialTick,
                initialFrameDataB64 = (replay.InitialFrameData != null && replay.InitialFrameData.Length > 0)
                    ? Convert.ToBase64String(replay.InitialFrameData)
                    : "",
                localActorNumber = replay.LocalActorNumber,
            };

            // Noms par PlayerIndex : on mappe les pseudos local/adverse (MatchBridge, renseigné par
            // le hub) sur P0/P1 via le slot local résolu plus haut.
            string localName = Nymora.Core.Data.MatchBridge.LocalDisplayName;
            string oppName = Nymora.Core.Data.MatchBridge.OpponentDisplayName;
            if (localSlot == 0) { header.p0Name = localName; header.p1Name = oppName; }
            else { header.p0Name = oppName; header.p1Name = localName; }

            SpectateRelayBus.RaiseStart(_matchId, JsonUtility.ToJson(header));
            Debug.Log($"[SpectateRelay] Header envoyé (match {_matchId}, initialTick={header.initialTick}).");
            return true;
        }

        /// <summary>Lit la queue non-envoyée du RecordInputStream et l'émet en chunk base64.</summary>
        private void FlushChunk(QuantumGame game)
        {
            if (!(game.RecordInputStream is MemoryStream s)) return;
            s.Flush();
            long writePos = s.Position;          // position d'écriture de la sim
            if (writePos <= _cursor) return;

            int len = (int)(writePos - _cursor);
            s.Position = _cursor;
            byte[] buf = new byte[len];
            int read = 0;
            while (read < len)
            {
                int n = s.Read(buf, read, len - read);
                if (n <= 0) break;
                read += n;
            }
            s.Position = writePos;               // restaure pour l'écriture de la sim
            _cursor = writePos;

            if (read > 0)
                SpectateRelayBus.RaiseChunk(_matchId, _seq++, Convert.ToBase64String(buf, 0, read));
        }

        private void SendEnd()
        {
            if (_ended) return;
            _ended = true;
            SpectateRelayBus.RaiseEnd(_matchId);
            Debug.Log($"[SpectateRelay] Flux clôturé (match {_matchId}, {_seq} chunks).");
        }

        private void OnGameDestroyed(QuantumGame game)
        {
            if (game != _game) return;
            if (_isRelayer && _headerSent && !_ended)
            {
                FlushChunk(game);
                SendEnd();
            }
        }

        private void OnDestroy()
        {
            // Filet : si la scène se ferme sans MatchEnd (abandon), clôt le flux.
            if (_isRelayer && _headerSent && !_ended)
            {
                if (_game != null) FlushChunk(_game);
                SendEnd();
            }
        }
    }

    /// <summary>
    /// Header du flux spectateur (config Quantum). Sérialisé par le relayer (S3), stocké opaque
    /// par le backend, reparsé par le spectateur (S4) pour démarrer sa session GameMode.Replay.
    /// </summary>
    [Serializable]
    public sealed class SpectateHeader
    {
        public string runtimeConfigB64;     // RuntimeConfig compressé (ConfigFromByteArray compressed:true)
        public string sessionConfigJson;    // DeterministicSessionConfig (JsonUtility)
        public int initialTick;
        public string initialFrameDataB64;  // "" si null
        public int localActorNumber;
        public string p0Name;               // pseudo du joueur PlayerIndex 0
        public string p1Name;               // pseudo du joueur PlayerIndex 1
    }
}
