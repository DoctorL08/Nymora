using Nymora.Combat.Grid;
using Nymora.Combat.View.HUD;
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

        [Tooltip("HUD controller. Si set, un sort 'arme' (clic icone) intercepte le clic " +
                 "gauche pour envoyer un Cast a la place du Move (2.13.a, option 2).")]
        [SerializeField] private CombatHUDController _hudController;

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

            // 2.10.a — touches 1-5 :
            //   1 = Ouvre-Plaie       (range 1, melee)  — Shift+1 = depense 1 HG (Glyphe)
            //   2 = Pacte de Sang     (self, 1/match)
            //   3 = Rugissement       (AoE rayon 3, self target)
            //   4 = Rage Insatiable   (self)
            //   5 = Riposte Carmin    (self)
            // 2.10.b — touches 6-9, 0 :
            //   6 = Marque de Carnage (range 5, enemy)
            //   7 = Empoignade        (range 3, enemy, pull adjacent)
            //   8 = Peau de Fer       (self, shield 200 HP / 2 tours)
            //   9 = Seve Vive         (self, heal 100)  — Shift+9 = depense 1 HG (+60 heal)
            //   0 = Dernier Souffle   (self, HP<30%, heal 200 + 3 HG, 1/match)
            // 2.10.c — touches F1-F4 (cliquables HUD en 2.13) :
            //   F1 = Charge Brutale       (ligne range 5, 180 dgts + Vapeur Carmin)
            //   F2 = Detonation Sanglante (range 4, croix 3, 2 HG mandatory) — Shift+F2 = HGSpend max 3 (total 5 HG)
            //   F3 = Curee                (range 2, 2 HG, kill chain)
            //   F4 = Cauterisation        (self, retire DoT + heal)
            // 2.11 — touche B (slot signature, separe du deck 6) :
            //   B  = Ame Laceree          (melee, 5 HG obligatoire, 320 dgts + heal 50%, cooldown 4 tours)
            bool key1 = UnityEngine.Input.GetKeyDown(KeyCode.Alpha1);
            bool key2 = UnityEngine.Input.GetKeyDown(KeyCode.Alpha2);
            bool key3 = UnityEngine.Input.GetKeyDown(KeyCode.Alpha3);
            bool key4 = UnityEngine.Input.GetKeyDown(KeyCode.Alpha4);
            bool key5 = UnityEngine.Input.GetKeyDown(KeyCode.Alpha5);
            bool key6 = UnityEngine.Input.GetKeyDown(KeyCode.Alpha6);
            bool key7 = UnityEngine.Input.GetKeyDown(KeyCode.Alpha7);
            bool key8 = UnityEngine.Input.GetKeyDown(KeyCode.Alpha8);
            bool key9 = UnityEngine.Input.GetKeyDown(KeyCode.Alpha9);
            bool key0 = UnityEngine.Input.GetKeyDown(KeyCode.Alpha0);
            bool keyF1 = UnityEngine.Input.GetKeyDown(KeyCode.F1);
            bool keyF2 = UnityEngine.Input.GetKeyDown(KeyCode.F2);
            bool keyF3 = UnityEngine.Input.GetKeyDown(KeyCode.F3);
            bool keyF4 = UnityEngine.Input.GetKeyDown(KeyCode.F4);
            bool keyB  = UnityEngine.Input.GetKeyDown(KeyCode.B); // 2.11 signature Ame Laceree
            bool shiftHeld = UnityEngine.Input.GetKey(KeyCode.LeftShift) || UnityEngine.Input.GetKey(KeyCode.RightShift);

            bool anySpellKey = key1 || key2 || key3 || key4 || key5
                            || key6 || key7 || key8 || key9 || key0
                            || keyF1 || keyF2 || keyF3 || keyF4
                            || keyB;
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

            // 2.10.b — sorts 6-9, 0.
            // 6 Marque de Carnage / 7 Empoignade : ciblent un ennemi (case sous la souris).
            // 8 Peau de Fer / 9 Seve Vive / 0 Dernier Souffle : self-target.
            if (key6)
            {
                SendSpellAt(game, senderPlayer, SpellId.SoulrenderMarqueDeCarnage, gx, gy, 0);
                return;
            }
            if (key7)
            {
                SendSpellAt(game, senderPlayer, SpellId.SoulrenderEmpoignade, gx, gy, 0);
                return;
            }
            if (key8)
            {
                if (TryGetCasterCell(game, senderPlayer, out int cx, out int cy))
                    SendSpellAt(game, senderPlayer, SpellId.SoulrenderPeauDeFer, cx, cy, 0);
                return;
            }
            if (key9)
            {
                byte hg = (byte)(shiftHeld ? 1 : 0);
                if (TryGetCasterCell(game, senderPlayer, out int cx, out int cy))
                    SendSpellAt(game, senderPlayer, SpellId.SoulrenderSeveVive, cx, cy, hg);
                return;
            }
            if (key0)
            {
                if (TryGetCasterCell(game, senderPlayer, out int cx, out int cy))
                    SendSpellAt(game, senderPlayer, SpellId.SoulrenderDernierSouffle, cx, cy, 0);
                return;
            }

            // 2.10.c — sorts F1-F4.
            // F1 Charge Brutale / F2 Detonation Sanglante / F3 Curee : ciblent la case sous la souris.
            // F4 Cauterisation : self-target.
            if (keyF1)
            {
                SendSpellAt(game, senderPlayer, SpellId.SoulrenderChargeBrutale, gx, gy, 0);
                return;
            }
            if (keyF2)
            {
                // Shift+F2 = HGSpend max 3 (total 5 HG avec mandatory 2). Sans Shift = HGSpend 0 (total 2 HG).
                byte hg = (byte)(shiftHeld ? 3 : 0);
                SendSpellAt(game, senderPlayer, SpellId.SoulrenderDetonationSanglante, gx, gy, hg);
                return;
            }
            if (keyF3)
            {
                SendSpellAt(game, senderPlayer, SpellId.SoulrenderCuree, gx, gy, 0);
                return;
            }
            if (keyF4)
            {
                if (TryGetCasterCell(game, senderPlayer, out int cx, out int cy))
                    SendSpellAt(game, senderPlayer, SpellId.SoulrenderCauterisation, cx, cy, 0);
                return;
            }

            // 2.11 — touche B : signature Ame Laceree (range 1, 5 HG obligatoire).
            if (keyB)
            {
                SendSpellAt(game, senderPlayer, SpellId.SoulrenderAmeLaceree, gx, gy, 0);
                return;
            }

            // Clic gauche : 3 chemins possibles (priorite descendante).
            // 1) Sort arme via le HUD (2.13.a, option 2) : on cast au lieu de bouger.
            // 2) Targeting preview debug (2.6) : bypass mouvement, l'apercu se charge du clic.
            // 3) Mouvement classique : MoveCommand.
            if (mouseDown)
            {
                if (_hudController != null && _hudController.ConsumeArmedSpell(out SpellId armedSpell))
                {
                    SendSpellAt(game, senderPlayer, armedSpell, gx, gy, 0);
                    return;
                }
                if (!_debugShowTargeting)
                {
                    var moveCmd = new MoveCommand { TargetX = gx, TargetY = gy };
                    game.SendCommand(senderPlayer, moveCmd);
                    Debug.Log($"[Nymora.CombatInput] Sent MoveCommand player={senderPlayer} target=({gx},{gy})");
                }
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
