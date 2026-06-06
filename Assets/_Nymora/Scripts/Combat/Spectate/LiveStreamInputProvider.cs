using System;
using System.IO;
using Photon.Deterministic;
using Quantum;

namespace Nymora.Combat.Spectate
{
    /// <summary>
    /// Brique S4 (mode spectateur) — Provider d'input REPLAY à fenêtre LIVE.
    ///
    /// Calqué sur <see cref="StreamReplayInputProvider"/> du SDK Quantum, mais avec un
    /// <see cref="MaxFrame"/> qui GRANDIT à mesure que les chunks réseau arrivent (le provider
    /// natif fige MaxFrame à la construction → inutilisable pour du live). La session replay
    /// avance jusqu'à MaxFrame (via CanSimulate) puis ATTEND au bord live ; chaque nouveau chunk
    /// repousse MaxFrame et débloque les frames suivantes.
    ///
    /// Format du flux (identique au RecordInputStream relayé par S3) : suite d'enregistrements
    /// auto-délimités [longueur:int32][frameNumber:int32][payload: longueur-4].
    ///
    /// Append() écrit en fin de flux sans perturber la position de LECTURE de la session
    /// (save/seek-end/write/restore), puis scanne les enregistrements COMPLETS pour avancer MaxFrame.
    /// </summary>
    public sealed class LiveStreamInputProvider : IDeterministicStreamReplayInputProvider
    {
        private readonly MemoryStream _stream;       // publiclyVisible → GetBuffer() autorisé
        private readonly int _localActorNumber;
        private readonly byte[] _lengthReadBuffer = new byte[4];

        private int _maxFrame = -1;
        private long _parseCursor;                   // octets déjà scannés pour MaxFrame

        public LiveStreamInputProvider(int localActorNumber = 0)
        {
            _stream = new MemoryStream(1024 * 1024);
            _localActorNumber = localActorNumber;
        }

        /// <summary>Dernière frame pour laquelle l'input est disponible (grandit avec les chunks).</summary>
        public int MaxFrame => _maxFrame;

        public int LocalActorNumber => _localActorNumber;

        /// <summary>Ajoute des octets reçus du réseau en fin de flux + avance MaxFrame.</summary>
        public void Append(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0) return;

            long readPos = _stream.Position;          // position de lecture de la session
            _stream.Seek(0, SeekOrigin.End);
            _stream.Write(bytes, 0, bytes.Length);
            _stream.Position = readPos;               // restaure pour la lecture séquentielle

            // Scanne les enregistrements complets nouvellement disponibles pour avancer MaxFrame.
            byte[] buf = _stream.GetBuffer();
            long len = _stream.Length;
            while (_parseCursor + 4 <= len)
            {
                int recLen = BitConverter.ToInt32(buf, (int)_parseCursor);
                if (recLen < 4) break;                            // garde anti-corruption
                if (_parseCursor + 4 + recLen > len) break;       // enregistrement incomplet → on attend la suite
                int frame = BitConverter.ToInt32(buf, (int)(_parseCursor + 4));
                if (frame > _maxFrame) _maxFrame = frame;
                _parseCursor += 4 + recLen;
            }
        }

        public void Reset()
        {
            _stream.Seek(0, SeekOrigin.Begin);
        }

        /// <summary>La session peut-elle simuler cette frame ? (input disponible jusqu'à MaxFrame).</summary>
        public bool CanSimulate(int frame)
        {
            return frame <= _maxFrame;
        }

        public int BeginReadFrame(int frame)
        {
            int bytesRead = _stream.Read(_lengthReadBuffer, 0, 4);
            Assert.Always(bytesRead == 4, bytesRead);
            return BitConverter.ToInt32(_lengthReadBuffer, 0);
        }

        public void CompleteReadFrame(int frame, int length, ref byte[] data)
        {
            int bytesRead = _stream.Read(data, 0, length);
            Assert.Always(bytesRead == length, bytesRead);
        }

        // ===== Membres non utilisés en mode stream-replay (mirror du provider natif) =====
        public DeterministicFrameInputTemp GetInput(int frame, int player) => new DeterministicFrameInputTemp();
        public void AddRpc(int player, byte[] data, bool command) { }
        public QTuple<byte[], bool> GetRpc(int frame, int player) => new QTuple<byte[], bool>();
    }
}
