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

        [Header("Debug — targeting preview (brique 2.6)")]
        [Tooltip("Active la preview de targeting. Quand actif, le clic gauche ne deplace plus le combattant (bypass MoveCommand).")]
        [SerializeField] private bool _debugShowTargeting = false;
        [SerializeField] private TargetingShape _debugShape = TargetingShape.SingleTile;
        [SerializeField] private TargetingFilter _debugFilter = TargetingFilter.Enemy;
        [SerializeField] private int _debugRangeMin = 1;
        [SerializeField] private int _debugRangeMax = 4;

        // Expose les valeurs au TargetingPreviewView (read-only).
        public bool DebugShowTargeting => _debugShowTargeting;
        public TargetingShape DebugShape => _debugShape;
        public TargetingFilter DebugFilter => _debugFilter;
        public int DebugRangeMin => _debugRangeMin;
        public int DebugRangeMax => _debugRangeMax;

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

            var game = QuantumRunner.Default?.Game;
            if (game == null) return;

            // Qualifie UnityEngine.Input : Quantum a aussi un type "Input" (struct DSL).
            bool mouseDown = UnityEngine.Input.GetMouseButtonDown(0);
            bool spaceDown = UnityEngine.Input.GetKeyDown(KeyCode.Space);

            // 2.10.a : touches 1-5 pour les 5 sorts Soulrender de la brique.
            //   1 = Ouvre-Plaie       (range 1, melee)  — Shift+1 = depense 1 HG (Glyphe)
            //   2 = Pacte de Sang     (self, 1/match)
            //   3 = Rugissement       (AoE rayon 3, self target)
            //   4 = Rage Insatiable   (self)
            //   5 = Riposte Carmin    (self)
            bool key1 = UnityEngine.Input.GetKeyDown(KeyCode.Alpha1);
            bool key2 = UnityEngine.Input.GetKeyDown(KeyCode.Alpha2);
            bool key3 = UnityEngine.Input.GetKeyDown(KeyCode.Alpha3);
            bool key4 = UnityEngine.Input.GetKeyDown(KeyCode.Alpha4);
            bool key5 = UnityEngine.Input.GetKeyDown(KeyCode.Alpha5);
            bool shiftHeld = UnityEngine.Input.GetKey(KeyCode.LeftShift) || UnityEngine.Input.GetKey(KeyCode.RightShift);

            bool anySpellKey = key1 || key2 || key3 || key4 || key5;
            if (!mouseDown && !spaceDown && !anySpellKey) return;

            // Calcule la case sous la souris (partagee entre mvt et cast).
            Vector3 mouseWorld = _camera.ScreenToWorldPoint(UnityEngine.Input.mousePosition);
            mouseWorld.z = 0f;
            var (gx, gy) = IsoProjection.WorldToGrid(
                mouseWorld,
                _gridSettings.TileWorldWidth,
                _gridSettings.TileWorldHeight,
                _centerOffset);

            // Determine le sender (joueur actif si debug, sinon local).
            int senderPlayer = _localPlayerIndex;
            if (_debugAllPlayersMovable)
            {
                var frame = game.Frames.Verified;
                if (frame.TryGetSingleton<CombatState>(out var state))
                {
                    senderPlayer = state.ActivePlayerIndex;
                }
            }

            // Espace : cast Tranche-Ame Soulrender (brique 2.8).
            if (spaceDown)
            {
                var castCmd = new CastSpellCommand { Spell = SpellId.SoulrenderTrancheAme, TargetX = gx, TargetY = gy, HGSpend = 0 };
                game.SendCommand(senderPlayer, castCmd);
                Debug.Log($"[Nymora.CombatInput] Sent Cast TrancheAme player={senderPlayer} target=({gx},{gy})");
                return;
            }

            // Touches 1-5 : sorts 2.10.a. Cible = case sous la souris (relevant uniquement
            // pour Ouvre-Plaie ; les autres sont self-target, mais on envoie quand meme la
            // case mouse pour rester coherent avec la signature CastSpellCommand).
            if (key1)
            {
                byte hg = (byte)(shiftHeld ? 1 : 0);
                SendSpellAt(game, senderPlayer, SpellId.SoulrenderOuvrePlaie, gx, gy, hg);
                return;
            }
            if (key2)
            {
                // Pacte de Sang = self, range 0. Cible = caster lui-meme (cherche le combatant actif).
                if (TryGetCasterCell(game, senderPlayer, out int cx, out int cy))
                    SendSpellAt(game, senderPlayer, SpellId.SoulrenderPacteDeSang, cx, cy, 0);
                return;
            }
            if (key3)
            {
                if (TryGetCasterCell(game, senderPlayer, out int cx, out int cy))
                    SendSpellAt(game, senderPlayer, SpellId.SoulrenderRugissement, cx, cy, 0);
                return;
            }
            if (key4)
            {
                if (TryGetCasterCell(game, senderPlayer, out int cx, out int cy))
                    SendSpellAt(game, senderPlayer, SpellId.SoulrenderRageInsatiable, cx, cy, 0);
                return;
            }
            if (key5)
            {
                if (TryGetCasterCell(game, senderPlayer, out int cx, out int cy))
                    SendSpellAt(game, senderPlayer, SpellId.SoulrenderRiposteCarmin, cx, cy, 0);
                return;
            }

            // Clic gauche : mouvement. Bypasse en mode targeting preview.
            if (mouseDown && !_debugShowTargeting)
            {
                var moveCmd = new MoveCommand { TargetX = gx, TargetY = gy };
                game.SendCommand(senderPlayer, moveCmd);
                Debug.Log($"[Nymora.CombatInput] Sent MoveCommand player={senderPlayer} target=({gx},{gy})");
            }
        }

        private static void SendSpellAt(QuantumGame game, int sender, SpellId spell, int tx, int ty, byte hgSpend)
        {
            var cmd = new CastSpellCommand { Spell = spell, TargetX = tx, TargetY = ty, HGSpend = hgSpend };
            game.SendCommand(sender, cmd);
            Debug.Log($"[Nymora.CombatInput] Sent Cast {spell} player={sender} target=({tx},{ty}) HGSpend={hgSpend}");
        }

        /// <summary>
        /// Resoud la case du caster (joueur passe en argument) en lisant la Frame verified.
        /// Utilise pour les sorts self-target : on envoie sa propre case comme TargetX/Y.
        /// </summary>
        private static bool TryGetCasterCell(QuantumGame game, int playerIndex, out int x, out int y)
        {
            x = 0; y = 0;
            var frame = game.Frames.Verified;
            var filter = frame.Filter<Quantum.Combatant>();
            while (filter.Next(out Quantum.EntityRef _, out Quantum.Combatant c))
            {
                if (c.PlayerIndex == playerIndex)
                {
                    x = c.GridX;
                    y = c.GridY;
                    return true;
                }
            }
            return false;
        }
    }
}
