using Nymora.Combat.Grid;
using Quantum;
using UnityEngine;

namespace Nymora.Combat.View
{
    /// <summary>
    /// Detecte les clics souris sur la grille de combat et envoie une MoveCommand
    /// au runtime Quantum pour demander un deplacement du combattant.
    ///
    /// En 2.4 : pas de matchmaking ni de vrai local player — par defaut on envoie au
    /// JOUEUR ACTIF (debug mode "all movable") pour permettre de tester P0 puis P1 sans
    /// attendre l'alternance auto. A desactiver en Phase 6 quand on aura un vrai
    /// LocalPlayerIndex defini par le runner Photon.
    /// </summary>
    public class CombatInputController : MonoBehaviour
    {
        [Header("Configuration")]
        [SerializeField] private GridSettings _gridSettings;
        [SerializeField] private Camera _camera;

        [Header("Local player")]
        [SerializeField] private int _localPlayerIndex = 0;

        [Tooltip("Si vrai (default Phase 2), envoie la command au joueur ACTIF courant. " +
                 "Permet de tester P0 et P1 alternativement sans setup matchmaking. " +
                 "A desactiver en Phase 6 quand on aura un vrai local player.")]
        [SerializeField] private bool _debugAllPlayersMovable = true;

        [Header("Debug — local players")]
        [Tooltip("En 2.4 sans menu/matchmaking, on doit ajouter explicitement des players locaux pour pouvoir envoyer des commands. Sinon Quantum est en mode spectator et SendCommand est rejete.")]
        [SerializeField] private bool _autoAddLocalPlayers = true;
        [SerializeField] private int _autoAddPlayerCount = 2;

        private Vector3 _centerOffset;
        private bool _gridReady;

        private void Awake()
        {
            QuantumCallback.Subscribe(this, (CallbackGameStarted c) => OnGameStarted(c.Game));
            if (_camera == null) _camera = Camera.main;
        }

        private void OnGameStarted(QuantumGame game)
        {
            if (_gridSettings == null)
            {
                Debug.LogError("[Nymora.CombatInput] GridSettings manquant — drag l'asset.", this);
                return;
            }

            // Ajoute les players locaux pour sortir du mode spectator (sinon SendCommand rejete).
            // Sera remplace par le flow menu/matchmaking en Phase 6 — on retirera ce code a ce moment-la.
            if (_autoAddLocalPlayers)
            {
                for (int i = 0; i < _autoAddPlayerCount; i++)
                {
                    game.AddPlayer(i, new RuntimePlayer());
                }
                Debug.Log($"[Nymora.CombatInput] Ajout de {_autoAddPlayerCount} player(s) local(aux) (mode debug).");
            }

            var frame = game.Frames.Verified;
            if (!frame.TryGetSingleton<GridSingleton>(out var grid))
            {
                Debug.LogError("[Nymora.CombatInput] GridSingleton introuvable.", this);
                return;
            }

            _centerOffset = _gridSettings.CenterGrid
                ? IsoProjection.CenterOffset(grid.Width, grid.Height, _gridSettings.TileWorldWidth, _gridSettings.TileWorldHeight)
                : Vector3.zero;
            _gridReady = true;
        }

        private void Update()
        {
            if (!_gridReady) return;
            if (_camera == null) return;
            // Qualifie UnityEngine.Input : Quantum a aussi un type "Input" (struct DSL) et le using Quantum cree une ambiguite.
            if (!UnityEngine.Input.GetMouseButtonDown(0)) return;

            var game = QuantumRunner.Default?.Game;
            if (game == null) return;

            Vector3 mouseWorld = _camera.ScreenToWorldPoint(UnityEngine.Input.mousePosition);
            mouseWorld.z = 0f;

            var (gx, gy) = IsoProjection.WorldToGrid(
                mouseWorld,
                _gridSettings.TileWorldWidth,
                _gridSettings.TileWorldHeight,
                _centerOffset);

            int senderPlayer = _localPlayerIndex;
            if (_debugAllPlayersMovable)
            {
                var frame = game.Frames.Verified;
                if (frame.TryGetSingleton<CombatState>(out var state))
                {
                    senderPlayer = state.ActivePlayerIndex;
                }
            }

            var cmd = new MoveCommand { TargetX = gx, TargetY = gy };
            game.SendCommand(senderPlayer, cmd);

            Debug.Log($"[Nymora.CombatInput] Sent MoveCommand player={senderPlayer} target=({gx},{gy})");
        }
    }
}
