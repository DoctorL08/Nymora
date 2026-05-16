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

            // 2.13.a fix : si le clic gauche tombe sur un GameObject UI (icone HUD, bouton
            // End Turn, etc.), on l'ignore cote grille. Sinon le meme clic
            //   1) arme un sort via SpellSlotView.OnClick (event UI)
            //   2) ET, dans la meme frame, serait consume comme clic grille -> cast instantane.
            // Les inputs clavier restent traites normalement (UI ne capture pas les keys).
            if (mouseDown
                && UnityEngine.EventSystems.EventSystem.current != null
                && UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
            {
                mouseDown = false;
            }

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
            // 3.3.c — COLOSSAR SURVIE (touches F5-F9, self-target) :
            //   F5 = Stoicisme            (3 PA, shield 200 / 2T + immune push/pull/tp 2T ; +80 HP si shield survit)
            //   F6 = Garde Protectrice    (2 PA, -30% dmg subis 2T ; cap combine 50% avec Densite Inerte)
            //   F7 = Ressac Vital         (2 PA, heal 80 + 30/hit subi tour precedent, max +120)
            //   F8 = Renvoi du Bouclier   (3 PA, reflect 60 dgts melee+distance 1T, cap 4 retours)
            //   F9 = Soin Lourd           (3 PA, heal 150 HP ; MVP 1v1 self-only, range 3 en 2v2/3v3)
            bool keyF5 = UnityEngine.Input.GetKeyDown(KeyCode.F5);
            bool keyF6 = UnityEngine.Input.GetKeyDown(KeyCode.F6);
            bool keyF7 = UnityEngine.Input.GetKeyDown(KeyCode.F7);
            bool keyF8 = UnityEngine.Input.GetKeyDown(KeyCode.F8);
            bool keyF9 = UnityEngine.Input.GetKeyDown(KeyCode.F9);
            // 3.4 — touche F11 : DEBUG, applique +1 marque venin Necram sur la cible
            // ennemie sous la souris. Sera retiree en 3.5.a quand Crachat Acide /
            // Inoculation feront ca proprement via SpellSystem.
            bool keyF11 = UnityEngine.Input.GetKeyDown(KeyCode.F11);
            // 3.6 — touche F12 : DEBUG, spawn un leurre Standard Ghostra sur la case sous la souris.
            //                    Sert a tester l'Angle Mort + Permutation avant 3.7.a (Réplique Fantôme).
            bool keyF12 = UnityEngine.Input.GetKeyDown(KeyCode.F12);
            bool keyB  = UnityEngine.Input.GetKeyDown(KeyCode.B); // 2.11 signature Ame Laceree
            // 2.14 — touche T : DEBUG, pose un Voile Nightseer 2 tours sur la case sous la souris.
            // Sera retiree en 2.15 quand les sorts Nightseer (Pas Furtif, Voile d'Ombre, Champ
            // de Mines) feront ca proprement via SpellSystem.
            bool keyT  = UnityEngine.Input.GetKeyDown(KeyCode.T);
            // 3.3.b.iii — Colossar TACTIQUES Bible-correct (touches P/O/Y/,/. identiques AZERTY/QWERTY) :
            //   P = Pilier               (3 PA, range 3 case vide, 200 HP / 3T, +1 FD, bloque LoS+mvt)
            //   O = Mur de Pierre        (4 PA, range 4, 3 segments perp 150 HP / 2T ; Shift+O = 1 FD -> 5 segments)
            //   Y = Ancrage              (2 PA, range 4 ENEMY, -2 PM 2T + immune push/pull/tp 1T)
            //   , = Provocation          (2 PA, range 5 ENEMY, 1T : -1 PM + sorts non-cibling +2 PA + 100 dmg auto si pas adjacent fin tour)
            //   . = Brisure              (3 PA, range 2 ENEMY, 90 dgts + retire 1 buff/bouclier (sinon TRAUMA -2 PA))
            //   U = DEBUG damage 50 sur obstacle sous souris (gardee pour test destruction +30 HP Densite Inerte)
            bool keyP  = UnityEngine.Input.GetKeyDown(KeyCode.P);
            bool keyO  = UnityEngine.Input.GetKeyDown(KeyCode.O);
            bool keyU  = UnityEngine.Input.GetKeyDown(KeyCode.U);
            bool keyY      = UnityEngine.Input.GetKeyDown(KeyCode.Y);
            bool keyComma  = UnityEngine.Input.GetKeyDown(KeyCode.Comma);
            bool keyPeriod = UnityEngine.Input.GetKeyDown(KeyCode.Period);
            // 3.3.a.i — COLOSSAR OFFENSIFS (touches H/J identiques AZERTY/QWERTY) :
            //   H = Frappe Lourde       (3 PA, melee 1, 180 dgts +100 si epinglee)
            //   J = Represailles        (3 PA, melee 1, 100 dgts + reflect 80 dgts melee 2 tours)
            // 3.3.a.ii — COLOSSAR OFFENSIFS AoE (touches I/K/L identiques AZERTY/QWERTY) :
            //   I = Onde de Choc        (3 PA, AoE rayon 1, 80 dgts + push 2 ; +80 + TRAUMA si push contre obstacle/bord)
            //   K = Marteau Punisseur   (4 PA, range 1-2, 160 dgts ; 240 + TRAUMA -2 PA si target.PA < 4)
            //   L = Choc Sismique       (4 PA, ligne 4, 130 dgts cibles + -1 PM ; traverse Pilier OWN +50 dgts next)
            bool keyH  = UnityEngine.Input.GetKeyDown(KeyCode.H);
            bool keyJ  = UnityEngine.Input.GetKeyDown(KeyCode.J);
            bool keyI  = UnityEngine.Input.GetKeyDown(KeyCode.I);
            bool keyK  = UnityEngine.Input.GetKeyDown(KeyCode.K);
            bool keyL  = UnityEngine.Input.GetKeyDown(KeyCode.L);
            // BIND AZERTY FR (Lorenzo) : les Unity KeyCode reflètent la position physique
            // (= scancode QWERTY US) ; on map ici à la LETTRE AFFICHEE sur clavier AZERTY.
            //
            // 2.15.a — NIGHTSEER OFFENSIFS (sorts 30-34) — rangee HAUT en AZERTY :
            //   A = Tir Precis              (3 PA, range 6, 200 dgts +80 si Traque)
            //   Z = Volee d'Epines          (4 PA, ligne 5, 130 dgts + Filet sur derniere case)
            //   E = Detonation Onirique     (4 PA, range 5, 170 dgts +80 si Voile)
            //   R = Frappe de l'Ombre       (4 PA, range 3, 200 dgts +100 si target deja deplacee)
            //   V = Salve Mortelle          (5 PA, range 6, croix 5, 3 PR, 220/130 +60 Traque +50 Voile)
            bool keyAzA = UnityEngine.Input.GetKeyDown(KeyCode.Q); // 'A' AZERTY = scancode Q
            bool keyAzZ = UnityEngine.Input.GetKeyDown(KeyCode.W); // 'Z' AZERTY = scancode W
            bool keyE   = UnityEngine.Input.GetKeyDown(KeyCode.E);
            bool keyR   = UnityEngine.Input.GetKeyDown(KeyCode.R);
            bool keyV   = UnityEngine.Input.GetKeyDown(KeyCode.V);
            // 2.15.b — NIGHTSEER TACTIQUES (sorts 35-39) — rangee MILIEU en AZERTY :
            //   Q = Marque du Chasseur      (1 PA, range 5, applique Traque 3 tours)
            //   S = Filet de Ronces         (2 PA, range 4, pose Filet voile)
            //   D = Champ de Mines          (4 PA, range 3, AoE 3x3, pose 3 mines)
            //   F = Bourrasque              (3 PA, range 5, push 3 cases — Shift+F : 5 cases via 1 PR)
            //   G = Souffle Glacial         (3 PA, AoE croix 3 autour caster, 70 dgts + push 1 + -1 PM)
            bool keyAzQ = UnityEngine.Input.GetKeyDown(KeyCode.A); // 'Q' AZERTY = scancode A
            bool keyS   = UnityEngine.Input.GetKeyDown(KeyCode.S);
            bool keyD   = UnityEngine.Input.GetKeyDown(KeyCode.D);
            bool keyF   = UnityEngine.Input.GetKeyDown(KeyCode.F);
            bool keyG   = UnityEngine.Input.GetKeyDown(KeyCode.G);
            // 2.15.c — NIGHTSEER SURVIE (sorts 40-44) — rangee BAS en AZERTY :
            //   W = Voile d'Ombre           (3 PA, self, Untargetable 1 round)
            //   X = Pas Furtif              (2 PA, teleport ≤4 cases — Shift+X = 1 PR Voile sur arrivee)
            //   C = Camouflage Ronces       (3 PA, self, shield 130 + RoncesAura 70/round 2 rounds)
            //   N = Seve Sauvage            (3 PA, self, heal 130 +60 trap +30 voile)
            //   M = Evanescence             (4 PA, teleport ≤7 cases, HP<30%, heal 150 + Voile case quittee, 1/match)
            bool keyAzW = UnityEngine.Input.GetKeyDown(KeyCode.Z);         // 'W' AZERTY = scancode Z
            bool keyX   = UnityEngine.Input.GetKeyDown(KeyCode.X);
            bool keyC   = UnityEngine.Input.GetKeyDown(KeyCode.C);
            bool keyN   = UnityEngine.Input.GetKeyDown(KeyCode.N);
            bool keyAzM = UnityEngine.Input.GetKeyDown(KeyCode.Semicolon); // 'M' AZERTY = scancode Semicolon
            bool shiftHeld = UnityEngine.Input.GetKey(KeyCode.LeftShift) || UnityEngine.Input.GetKey(KeyCode.RightShift);

            bool anySpellKey = key1 || key2 || key3 || key4 || key5
                            || key6 || key7 || key8 || key9 || key0
                            || keyF1 || keyF2 || keyF3 || keyF4
                            || keyF5 || keyF6 || keyF7 || keyF8 || keyF9 // 3.3.c Colossar Survie
                            || keyF11 // 3.4 debug Necram apply venin
                            || keyF12 // 3.6 debug Ghostra spawn decoy
                            || keyB || keyT
                            || keyAzA || keyAzZ || keyE || keyR || keyV
                            || keyAzQ || keyS || keyD || keyF || keyG
                            || keyAzW || keyX || keyC || keyN || keyAzM
                            || keyP || keyO || keyU // 3.3.b.i Colossar tactiques (P=Pilier, O=Mur) + debug obstacle (U)
                            || keyY || keyComma || keyPeriod // 3.3.b.ii Colossar tactiques (Y=Ancrage, ,=Provoc, .=Brisure)
                            || keyH || keyJ // 3.3.a.i Colossar offensifs
                            || keyI || keyK || keyL; // 3.3.a.ii Colossar offensifs AoE
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

            // 3.3.c — Colossar SURVIE (F5-F9, tous self-target).
            if (keyF5)
            {
                if (TryGetCasterCell(game, senderPlayer, out int cx, out int cy))
                    SendSpellAt(game, senderPlayer, SpellId.ColossarStoicisme, cx, cy, 0);
                return;
            }
            if (keyF6)
            {
                if (TryGetCasterCell(game, senderPlayer, out int cx, out int cy))
                    SendSpellAt(game, senderPlayer, SpellId.ColossarGardeProtectrice, cx, cy, 0);
                return;
            }
            if (keyF7)
            {
                if (TryGetCasterCell(game, senderPlayer, out int cx, out int cy))
                    SendSpellAt(game, senderPlayer, SpellId.ColossarRessacVital, cx, cy, 0);
                return;
            }
            if (keyF8)
            {
                if (TryGetCasterCell(game, senderPlayer, out int cx, out int cy))
                    SendSpellAt(game, senderPlayer, SpellId.ColossarRenvoiDuBouclier, cx, cy, 0);
                return;
            }
            if (keyF9)
            {
                if (TryGetCasterCell(game, senderPlayer, out int cx, out int cy))
                    SendSpellAt(game, senderPlayer, SpellId.ColossarSoinLourd, cx, cy, 0);
                return;
            }

            // 2.11 / 2.16 / 3.3.d — touche B : SIGNATURE CONTEXTUELLE (depend de la classe du caster).
            //   Soulrender → Ame Laceree    (melee 1, 5 HG, 320 dgts + heal 50%, cooldown 4 tours)
            //   Nightseer  → Traquenard     (range 5, 4 PR, teleport adjacent + 280 dgts + Paralysie, cooldown 4 tours)
            //   Colossar   → Effondrement   (self, 3 FD, annonce 1 tour, AoE rayon 2 + Failles + buff 2T)
            //   Necram     → Virus Fatal    (range 5, 6 PT, tick venin x3 sur cible, transfert marques si kill, cooldown 4 tours)
            //   Phase 3 ajoutera Ghostra avec sa signature.
            if (keyB)
            {
                SpellId sigSpell = SpellId.SoulrenderAmeLaceree;
                if (TryGetCasterClass(game, senderPlayer, out Quantum.NymoraClass casterClass))
                {
                    if (casterClass == Quantum.NymoraClass.Nightseer) sigSpell = SpellId.NightseerTraquenard;
                    else if (casterClass == Quantum.NymoraClass.Colossar) sigSpell = SpellId.ColossarEffondrement;
                    else if (casterClass == Quantum.NymoraClass.Necram) sigSpell = SpellId.NecramVirusFatal;
                }
                // Effondrement est self-target : on redirige la case ciblee vers la case caster.
                if (sigSpell == SpellId.ColossarEffondrement
                    && TryGetCasterCell(game, senderPlayer, out int cxSig, out int cySig))
                {
                    SendSpellAt(game, senderPlayer, sigSpell, cxSig, cySig, 0);
                }
                else
                {
                    SendSpellAt(game, senderPlayer, sigSpell, gx, gy, 0);
                }
                return;
            }

            // 2.14 — touche T : DEBUG pose Voile Nightseer 2 tours sur case sous souris.
            // Le FogSystem cote sim accepte uniquement si senderPlayer == joueur actif.
            if (keyT)
            {
                var veilCmd = new DebugApplyVeilCommand { TargetX = gx, TargetY = gy };
                game.SendCommand(senderPlayer, veilCmd);
                Debug.Log($"[Nymora.CombatInput] Sent DEBUG ApplyVeil player={senderPlayer} target=({gx},{gy})");
                return;
            }

            // 3.4 — touche F11 : DEBUG applique +1 marque venin Necram sur la cible
            // ennemie sous la souris. NecramSystem cote sim accepte uniquement si
            // senderPlayer == joueur actif. Retire en 3.5.a (Crachat Acide / Inoculation).
            if (keyF11)
            {
                var veninCmd = new DebugApplyVeninCommand { TargetX = gx, TargetY = gy };
                game.SendCommand(senderPlayer, veninCmd);
                Debug.Log($"[Nymora.CombatInput] Sent DEBUG ApplyVenin player={senderPlayer} target=({gx},{gy})");
                return;
            }

            // 3.6 — touche F12 : DEBUG spawn un leurre Standard Ghostra sur la case sous
            // la souris. GhostraSystem cote sim accepte uniquement si senderPlayer == joueur
            // actif ET caster.Class == Ghostra. Retire en 3.7.a (Réplique Fantôme / Pas dans l'Ombre).
            if (keyF12)
            {
                var decoyCmd = new DebugSpawnDecoyCommand { TargetX = gx, TargetY = gy };
                game.SendCommand(senderPlayer, decoyCmd);
                Debug.Log($"[Nymora.CombatInput] Sent DEBUG SpawnDecoy player={senderPlayer} target=({gx},{gy})");
                return;
            }

            // 3.3.b.i / 3.6 — touche P AZERTY contextuelle :
            //   Colossar -> Pilier (sort tactique Colossar, remplace ancien debug spawn 3.1).
            //   Ghostra  -> Permutation (Angle 3 only, 0 PA, 1x/tour — swap Ghostra<->leurre).
            //   Autres   -> fallback Pilier (compat).
            // touche O : Mur de Pierre (3 cases perpendiculaires axe caster->cible).
            // touche U : DEBUG damage 50 sur obstacle (gardee pour test destruction Densite Inerte +30 HP).
            if (keyP)
            {
                if (TryGetCasterClass(game, senderPlayer, out Quantum.NymoraClass casterClsP)
                    && casterClsP == Quantum.NymoraClass.Ghostra)
                {
                    // 3.7.a.i.4 — Permutation cible la case sous la souris. Bible-strict :
                    // le joueur doit cliquer sur un de ses leurres pour le swap precis.
                    var permCmd = new PermutationCommand { TargetX = gx, TargetY = gy };
                    game.SendCommand(senderPlayer, permCmd);
                    Debug.Log($"[Nymora.CombatInput] Sent Permutation player={senderPlayer} target=({gx},{gy}) (Ghostra)");
                    return;
                }
                SendSpellAt(game, senderPlayer, SpellId.ColossarPilier, gx, gy, 0);
                return;
            }
            if (keyO)
            {
                // Shift+O : 1 FD optionnel -> 5 segments au lieu de 3 (Bible).
                byte murHg = (byte)(shiftHeld ? 1 : 0);
                SendSpellAt(game, senderPlayer, SpellId.ColossarMurDePierre, gx, gy, murHg);
                return;
            }
            if (keyU)
            {
                var dmgCmd = new DebugDamageObstacleCommand { TargetX = gx, TargetY = gy };
                game.SendCommand(senderPlayer, dmgCmd);
                Debug.Log($"[Nymora.CombatInput] Sent DEBUG DamageObstacle player={senderPlayer} target=({gx},{gy})");
                return;
            }

            // 3.3.b.iii — Colossar tactiques Bible-correct (target = case souris pour tous).
            if (keyY)
            {
                // Ancrage Bible : ENEMY range 4 (vs Self anciennement). Target = case souris.
                SendSpellAt(game, senderPlayer, SpellId.ColossarAncrage, gx, gy, 0);
                return;
            }
            if (keyComma)
            {
                SendSpellAt(game, senderPlayer, SpellId.ColossarProvocation, gx, gy, 0);
                return;
            }
            if (keyPeriod)
            {
                // Brisure Bible : ENEMY range 2 (vs Obstacle range 5 anciennement). Target = case souris.
                SendSpellAt(game, senderPlayer, SpellId.ColossarBrisure, gx, gy, 0);
                return;
            }

            // 3.3.a.i — Colossar offensifs (target = case sous souris, doit etre adjacente).
            if (keyH)
            {
                SendSpellAt(game, senderPlayer, SpellId.ColossarFrappeLourde, gx, gy, 0);
                return;
            }
            if (keyJ)
            {
                SendSpellAt(game, senderPlayer, SpellId.ColossarRepresailles, gx, gy, 0);
                return;
            }

            // 3.3.a.ii — Colossar offensifs AoE / longue portee.
            if (keyI)
            {
                // Onde de Choc : target = case caster (self-target AoE rayon 1).
                if (TryGetCasterCell(game, senderPlayer, out int cxOdC, out int cyOdC))
                    SendSpellAt(game, senderPlayer, SpellId.ColossarOndeDeChoc, cxOdC, cyOdC, 0);
                return;
            }
            if (keyK)
            {
                SendSpellAt(game, senderPlayer, SpellId.ColossarMarteauPunisseur, gx, gy, 0);
                return;
            }
            if (keyL)
            {
                SendSpellAt(game, senderPlayer, SpellId.ColossarChocSismique, gx, gy, 0);
                return;
            }

            // 2.15.a — sorts Nightseer offensifs (lettres AZERTY A/Z/E/R/V).
            // 2.15.a / 3.5.a.i / 3.7.a.i — Touche A AZERTY contextuelle :
            //   Nightseer -> Tir Precis (3 PA, range 6, 200 dgts +80 Traque)
            //   Necram    -> Crachat Acide (3 PA, range 4, 90 dgts + 2 marques venin)
            //   Ghostra   -> Lame Spectrale (3 PA, melee 1, 170 dgts + bonus dorsal + 60 PlaieOuverte)
            if (keyAzA)
            {
                SpellId azaSpell = SpellId.NightseerTirPrecis;
                if (TryGetCasterClass(game, senderPlayer, out Quantum.NymoraClass casterClsA))
                {
                    if (casterClsA == Quantum.NymoraClass.Necram) azaSpell = SpellId.NecramCrachatAcide;
                    else if (casterClsA == Quantum.NymoraClass.Ghostra) azaSpell = SpellId.GhostraLameSpectrale;
                }
                SendSpellAt(game, senderPlayer, azaSpell, gx, gy, 0);
                return;
            }
            // 2.15.a / 3.5.a.i / 3.7.a.i — Touche Z AZERTY contextuelle :
            //   Nightseer -> Volee d'Epines (ligne 5, 130 dgts + Filet derniere case)
            //   Necram    -> Morsure Putride (4 PA, melee 1, 110 + 22/marque, transfert marques au kill)
            //   Ghostra   -> Lame Vorace Spectrale (3 PA, melee 1, 130 dgts + 60 PlaieOuverte non consommee)
            if (keyAzZ)
            {
                SpellId azzSpell = SpellId.NightseerVoleeDEpines;
                if (TryGetCasterClass(game, senderPlayer, out Quantum.NymoraClass casterClsZ))
                {
                    if (casterClsZ == Quantum.NymoraClass.Necram) azzSpell = SpellId.NecramMorsurePutride;
                    else if (casterClsZ == Quantum.NymoraClass.Ghostra) azzSpell = SpellId.GhostraLameVoraceSpectrale;
                }
                SendSpellAt(game, senderPlayer, azzSpell, gx, gy, 0);
                return;
            }
            // 2.15.a / 3.5.a.ii — Touche E AZERTY contextuelle :
            //   Nightseer -> Detonation Onirique (4 PA, range 5, AoE 2x2, +80 Voile)
            //                 (Shift+E = 2 PR optionnel -> range 10)
            //   Necram    -> Detonation Virulente (4 PA, range 4, 80 + 50/marque consommee)
            if (keyE)
            {
                if (TryGetCasterClass(game, senderPlayer, out Quantum.NymoraClass casterClsE)
                    && casterClsE == Quantum.NymoraClass.Necram)
                {
                    SendSpellAt(game, senderPlayer, SpellId.NecramDetonationVirulente, gx, gy, 0);
                }
                else
                {
                    // Bible V7.1 : Shift+E = depense 2 PR (optionnel) -> portee passe de 5 a 10.
                    byte detoPr = (byte)(shiftHeld ? 2 : 0);
                    SendSpellAt(game, senderPlayer, SpellId.NightseerDetonationOnirique, gx, gy, detoPr);
                }
                return;
            }
            // 2.15.a / 3.5.a.ii — Touche R AZERTY contextuelle :
            //   Nightseer -> Frappe de l'Ombre (4 PA, range 3, 200 dgts +100 si target deplacee)
            //   Necram    -> Faux Decharnee (4 PA, AoE Square3x3 autour caster, 130/cible + heal/marque)
            //                Faux Decharnee est self-target : on redirige la case ciblee vers caster.
            if (keyR)
            {
                if (TryGetCasterClass(game, senderPlayer, out Quantum.NymoraClass casterClsR)
                    && casterClsR == Quantum.NymoraClass.Necram)
                {
                    if (TryGetCasterCell(game, senderPlayer, out int fxC, out int fyC))
                        SendSpellAt(game, senderPlayer, SpellId.NecramFauxDecharnee, fxC, fyC, 0);
                }
                else
                {
                    SendSpellAt(game, senderPlayer, SpellId.NightseerFrappeDeLOmbre, gx, gy, 0);
                }
                return;
            }
            // 3.5.a.iii — Touche V context-aware (AZERTY) :
            //   Nightseer -> Salve Mortelle (5 PA, range 6, AoE croix 5)
            //   Necram    -> Brume Toxique (4 PA, range 4, AoE 3x3 / 2 rounds)
            if (keyV)
            {
                if (TryGetCasterClass(game, senderPlayer, out Quantum.NymoraClass casterClsV)
                    && casterClsV == Quantum.NymoraClass.Necram)
                {
                    SendSpellAt(game, senderPlayer, SpellId.NecramBrumeToxique, gx, gy, 0);
                }
                else
                {
                    SendSpellAt(game, senderPlayer, SpellId.NightseerSalveMortelle, gx, gy, 0);
                }
                return;
            }

            // 2.15.b / 3.5.b.i — sorts tactiques (lettres AZERTY Q/S/D/F/G).
            // Touche Q context-aware : Nightseer -> Marque du Chasseur (range 5, Traque) / Necram -> Inoculation (1 PA, range 5, 2 marques venin).
            if (keyAzQ)
            {
                if (TryGetCasterClass(game, senderPlayer, out Quantum.NymoraClass casterClsQ)
                    && casterClsQ == Quantum.NymoraClass.Necram)
                {
                    SendSpellAt(game, senderPlayer, SpellId.NecramInoculation, gx, gy, 0);
                }
                else
                {
                    SendSpellAt(game, senderPlayer, SpellId.NightseerMarqueDuChasseur, gx, gy, 0);
                }
                return;
            }
            // Touche S context-aware : Nightseer -> Filet de Ronces (range 4, trap) / Necram -> Marque Sacrificielle (2 PA, range 5, +20 dmg/tick venin 3 rounds).
            if (keyS)
            {
                if (TryGetCasterClass(game, senderPlayer, out Quantum.NymoraClass casterClsS)
                    && casterClsS == Quantum.NymoraClass.Necram)
                {
                    SendSpellAt(game, senderPlayer, SpellId.NecramMarqueSacrificielle, gx, gy, 0);
                }
                else
                {
                    SendSpellAt(game, senderPlayer, SpellId.NightseerFiletDeRonces, gx, gy, 0);
                }
                return;
            }
            // Touche D context-aware : Nightseer -> Champ de Mines (range 3, AoE 3x3 / 3 mines voilees) / Necram -> Symbiose Morbide (3 PA, self, lifesteal DoT 2 rounds).
            if (keyD)
            {
                if (TryGetCasterClass(game, senderPlayer, out Quantum.NymoraClass casterClsD)
                    && casterClsD == Quantum.NymoraClass.Necram)
                {
                    // Self-cast : target = position du caster (resolu cote sim via Filter=Self range 0,
                    // mais on envoie quand meme la case caster pour rester coherent avec les autres self-casts).
                    if (TryGetCasterCell(game, senderPlayer, out int ncX, out int ncY))
                    {
                        SendSpellAt(game, senderPlayer, SpellId.NecramSymbioseMorbide, ncX, ncY, 0);
                    }
                }
                else
                {
                    SendSpellAt(game, senderPlayer, SpellId.NightseerChampDeMines, gx, gy, 0);
                }
                return;
            }
            // Touche F context-aware :
            //   Nightseer -> Bourrasque (push 3, shift = 1 PR -> push 5)
            //   Necram    -> Contagion (3 PA, range 5, propagation marques rayon 3 ; shift = 2 PT optionnel -> cap copie 3->4)
            if (keyF)
            {
                if (TryGetCasterClass(game, senderPlayer, out Quantum.NymoraClass casterClsF)
                    && casterClsF == Quantum.NymoraClass.Necram)
                {
                    byte ptSpend = (byte)(shiftHeld ? 2 : 0); // 2 PT pour cap boost 3->4
                    SendSpellAt(game, senderPlayer, SpellId.NecramContagion, gx, gy, ptSpend);
                }
                else
                {
                    // Shift+F = 1 PR depense -> push 5 cases (au lieu de 3).
                    byte pr = (byte)(shiftHeld ? 1 : 0);
                    SendSpellAt(game, senderPlayer, SpellId.NightseerBourrasque, gx, gy, pr);
                }
                return;
            }
            // Touche G context-aware :
            //   Nightseer -> Souffle Glacial (AoE croix 3 autour caster, 70 dgts + push 1 + -1 PM)
            //   Necram    -> Pas Spectral (3.5.b.iii — 2 PA self, +2 PM ce tour, traverse ennemis +1 marque par traverse)
            if (keyG)
            {
                if (TryGetCasterClass(game, senderPlayer, out Quantum.NymoraClass casterClsG)
                    && casterClsG == Quantum.NymoraClass.Necram)
                {
                    if (TryGetCasterCell(game, senderPlayer, out int psX, out int psY))
                        SendSpellAt(game, senderPlayer, SpellId.NecramPasSpectral, psX, psY, 0);
                }
                else
                {
                    // Souffle Glacial = self target. On envoie la case du caster (TryGetCasterCell).
                    if (TryGetCasterCell(game, senderPlayer, out int cx, out int cy))
                        SendSpellAt(game, senderPlayer, SpellId.NightseerSouffleGlacial, cx, cy, 0);
                }
                return;
            }

            // 2.15.c / 3.5.c — sorts survie (lettres AZERTY W/X/C/N/M).
            // Touche W context-aware :
            //   Nightseer -> Voile d'Ombre (Untargetable 1 round)
            //   Necram    -> Voile de Pestilence (aura 2 rounds : adjacence + riposte marque)
            if (keyAzW)
            {
                if (TryGetCasterClass(game, senderPlayer, out Quantum.NymoraClass casterClsW)
                    && casterClsW == Quantum.NymoraClass.Necram)
                {
                    if (TryGetCasterCell(game, senderPlayer, out int vpX, out int vpY))
                        SendSpellAt(game, senderPlayer, SpellId.NecramVoilePestilence, vpX, vpY, 0);
                }
                else
                {
                    if (TryGetCasterCell(game, senderPlayer, out int cx, out int cy))
                        SendSpellAt(game, senderPlayer, SpellId.NightseerVoileDOmbre, cx, cy, 0);
                }
                return;
            }
            // Touche X context-aware :
            //   Nightseer -> Pas Furtif (Shift+X = 1 PR -> case d'arrivee Voilee 2 tours)
            //   Necram    -> Pulse Sanguin Vert (Shift+X = 1 PT -> +30 HP additionnel)
            if (keyX)
            {
                if (TryGetCasterClass(game, senderPlayer, out Quantum.NymoraClass casterClsX)
                    && casterClsX == Quantum.NymoraClass.Necram)
                {
                    byte pt = (byte)(shiftHeld ? 1 : 0);
                    if (TryGetCasterCell(game, senderPlayer, out int psX, out int psY))
                        SendSpellAt(game, senderPlayer, SpellId.NecramPulseSanguinVert, psX, psY, pt);
                }
                else
                {
                    // Shift+X = 1 PR -> case d'arrivee Voilee 2 tours.
                    byte pr = (byte)(shiftHeld ? 1 : 0);
                    SendSpellAt(game, senderPlayer, SpellId.NightseerPasFurtif, gx, gy, pr);
                }
                return;
            }
            // Touche C context-aware :
            //   Nightseer -> Camouflage Ronces (ShieldActive 130 HP + RoncesAura 70 dgts adjacents)
            //   Necram    -> Carapace Visqueuse (ShieldActive 110 HP + flag riposte marque sur attaquant melee)
            if (keyC)
            {
                if (TryGetCasterClass(game, senderPlayer, out Quantum.NymoraClass casterClsC)
                    && casterClsC == Quantum.NymoraClass.Necram)
                {
                    if (TryGetCasterCell(game, senderPlayer, out int cvX, out int cvY))
                        SendSpellAt(game, senderPlayer, SpellId.NecramCarapaceVisqueuse, cvX, cvY, 0);
                }
                else
                {
                    if (TryGetCasterCell(game, senderPlayer, out int cx, out int cy))
                        SendSpellAt(game, senderPlayer, SpellId.NightseerCamouflageRonces, cx, cy, 0);
                }
                return;
            }
            // Touche N context-aware :
            //   Nightseer -> Seve Sauvage (self heal, target = caster cell)
            //   Necram    -> Drain Vital (60 dgts range 4 + heal Necram 30 ou 60 si target.marques>=3)
            if (keyN)
            {
                if (TryGetCasterClass(game, senderPlayer, out Quantum.NymoraClass casterClsN)
                    && casterClsN == Quantum.NymoraClass.Necram)
                {
                    SendSpellAt(game, senderPlayer, SpellId.NecramDrainVital, gx, gy, 0);
                }
                else
                {
                    if (TryGetCasterCell(game, senderPlayer, out int cx, out int cy))
                        SendSpellAt(game, senderPlayer, SpellId.NightseerSeveSauvage, cx, cy, 0);
                }
                return;
            }
            // Touche M (AZERTY) context-aware :
            //   Nightseer -> Evanescence (teleport invisible jusqu'a 4 cases)
            //   Necram    -> Cocon Putride (panic signature : heal 220 + marques venin AoE Manhattan<=4, gate HP<30%, 1x/match)
            if (keyAzM)
            {
                if (TryGetCasterClass(game, senderPlayer, out Quantum.NymoraClass casterClsM)
                    && casterClsM == Quantum.NymoraClass.Necram)
                {
                    if (TryGetCasterCell(game, senderPlayer, out int cmX, out int cmY))
                        SendSpellAt(game, senderPlayer, SpellId.NecramCoconPutride, cmX, cmY, 0);
                }
                else
                {
                    SendSpellAt(game, senderPlayer, SpellId.NightseerEvanescence, gx, gy, 0);
                }
                return;
            }

            // Clic gauche : 3 chemins possibles (priorite descendante).
            // 1) Sort arme via le HUD (2.13.a, option 2) : on cast au lieu de bouger.
            //    Si Filter=Self : la case cliquee est ignoree, target redirigee vers caster cell.
            //    Sinon : la case cliquee est utilisee telle quelle (Quantum validera la portee).
            // 2) Targeting preview debug (2.6) : bypass mouvement, l'apercu se charge du clic.
            // 3) Mouvement classique : MoveCommand.
            if (mouseDown)
            {
                if (_hudController != null && _hudController.ConsumeArmedSpell(out SpellId armedSpell))
                {
                    int tx = gx;
                    int ty = gy;
                    if (Quantum.SpellRegistry.TryGet(armedSpell, out Quantum.SpellDef def)
                        && def.Filter == TargetingFilter.Self
                        && TryGetCasterCell(game, senderPlayer, out int cx, out int cy))
                    {
                        tx = cx;
                        ty = cy;
                    }
                    SendSpellAt(game, senderPlayer, armedSpell, tx, ty, 0);
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

        /// <summary>
        /// 2.16 — Lit la classe du caster pour dispatcher la touche B (signature contextuelle).
        /// </summary>
        private static bool TryGetCasterClass(QuantumGame game, int playerIndex, out Quantum.NymoraClass cls)
        {
            cls = Quantum.NymoraClass.None;
            var frame = game.Frames.Verified;
            var filter = frame.Filter<Quantum.Combatant>();
            while (filter.Next(out Quantum.EntityRef _, out Quantum.Combatant c))
            {
                if (c.PlayerIndex == playerIndex)
                {
                    cls = c.Class;
                    return true;
                }
            }
            return false;
        }
    }
}
