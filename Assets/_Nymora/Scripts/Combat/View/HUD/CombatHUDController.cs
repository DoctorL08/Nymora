using System;
using Quantum;
using UnityEngine;
using UnityEngine.UI;
using Button = UnityEngine.UI.Button;
using Image = UnityEngine.UI.Image;

namespace Nymora.Combat.View.HUD
{
    /// <summary>
    /// HUD combat principal (2.13.a). Orchestre les widgets : ResourcePanel (x2), Timer,
    /// PassivePanel, Timeline, SpellSlots (6 deck + signature), bouton End Turn.
    ///
    /// Source de donnees : frame Quantum verifiee (CallbackUpdateView).
    ///
    /// Mode arme (Option 2 choisie par Lorenzo) : clic icone sort qui necessite une cible
    /// = passe le HUD en "armed", le prochain clic gauche sur la grille envoie le cast
    /// au lieu du mouvement. CombatInputController lit ArmedSpell via ConsumeArmedSpell.
    /// </summary>
    public class CombatHUDController : MonoBehaviour
    {
        [Header("Catalog")]
        [SerializeField] private SpellIconRegistry _iconRegistry;

        [Header("Local player")]
        [SerializeField] private int _localPlayerIndex = 0;

        [Tooltip("Si vrai (default Phase 2.x) : envoie les commands au joueur ACTIF courant " +
                 "au lieu de _localPlayerIndex. A desactiver en Phase 6 (vrai matchmaking).")]
        [SerializeField] private bool _debugAllPlayersControllable = true;

        [Header("Deck (configuration libre Inspector)")]
        [Tooltip("6 sorts visibles dans la barre de sorts bas-centre.")]
        [SerializeField] private SpellId[] _testDeck = new SpellId[6];

        [Tooltip("Sort de signature occupant le slot dedie a droite de la barre.")]
        [SerializeField] private SpellId _signatureSpell = SpellId.SoulrenderAmeLaceree;

        [Header("Widgets")]
        [SerializeField] private ResourcePanelView _p0Panel;
        [SerializeField] private ResourcePanelView _p1Panel;
        [SerializeField] private TimerView _timer;
        [SerializeField] private PassivePanelView _passive;
        [SerializeField] private TimelineView _timeline;
        [SerializeField] private SpellSlotView[] _spellSlots = new SpellSlotView[6];
        [SerializeField] private SpellSlotView _signatureSlot;
        [SerializeField] private Button _endTurnButton;
        [SerializeField] private SpellTooltipView _tooltip;
        [SerializeField] private MatchEndOverlay _matchEndOverlay;

        // Etat armed (Option 2). Consume via ConsumeArmedSpell() pour le CombatInputController.
        private SpellId? _armedSpell;
        public SpellId? ArmedSpell => _armedSpell;
        public event Action ArmedSpellChanged;

        private void Awake()
        {
            BindSlots();
            if (_endTurnButton != null)
            {
                _endTurnButton.onClick.RemoveAllListeners();
                _endTurnButton.onClick.AddListener(OnEndTurnClicked);
            }
            if (_passive != null)
            {
                _passive.Init(_iconRegistry);
            }
            // 2.13.e : portrait dans les ResourcePanels.
            if (_p0Panel != null) _p0Panel.Init(_iconRegistry);
            if (_p1Panel != null) _p1Panel.Init(_iconRegistry);
            QuantumCallback.Subscribe(this, (CallbackUpdateView c) => OnUpdateView(c.Game));
        }

        private void BindSlots()
        {
            // Deck (1-6)
            if (_spellSlots != null)
            {
                for (int i = 0; i < _spellSlots.Length; i++)
                {
                    var slot = _spellSlots[i];
                    if (slot == null) continue;
                    SpellId spell = i < _testDeck.Length ? _testDeck[i] : SpellId.None;
                    Sprite icon = _iconRegistry != null ? _iconRegistry.GetIcon(spell) : null;
                    slot.Bind(this, spell, icon, (i + 1).ToString());
                }
            }

            // Signature (touche B)
            if (_signatureSlot != null)
            {
                Sprite sigIcon = _iconRegistry != null ? _iconRegistry.GetIcon(_signatureSpell) : null;
                _signatureSlot.Bind(this, _signatureSpell, sigIcon, "B");
            }
        }

        private void OnUpdateView(QuantumGame game)
        {
            var frame = game.Frames.Verified;
            if (frame == null) return;
            if (!frame.TryGetSingleton<CombatState>(out var state)) return;

            int activePlayer = state.ActivePlayerIndex;
            int controlPlayer = ResolveControlPlayer(activePlayer);

            // Filter combatants une seule fois ; cache localement P0/P1 et le combatant local.
            Combatant p0 = default, p1 = default;
            bool hasP0 = false, hasP1 = false;
            Combatant local = default;
            bool hasLocal = false;
            var filter = frame.Filter<Combatant>();
            while (filter.Next(out EntityRef _, out Combatant c))
            {
                if (c.PlayerIndex == 0) { p0 = c; hasP0 = true; }
                if (c.PlayerIndex == 1) { p1 = c; hasP1 = true; }
                if (c.PlayerIndex == controlPlayer) { local = c; hasLocal = true; }
            }

            // ResourcePanel
            if (_p0Panel != null) { if (hasP0) _p0Panel.Refresh(p0, activePlayer == 0); else _p0Panel.Clear(); }
            if (_p1Panel != null) { if (hasP1) _p1Panel.Refresh(p1, activePlayer == 1); else _p1Panel.Clear(); }

            // Timer
            if (_timer != null)
            {
                int updateRate = frame.UpdateRate;
                float seconds = updateRate > 0 ? state.TurnTimerTicks / (float)updateRate : 0f;
                _timer.Refresh(seconds, state.TurnNumber);
            }

            // Passif (combattant qu'on controle)
            if (_passive != null) { if (hasLocal) _passive.Refresh(local); else _passive.Clear(); }

            // Timeline
            if (_timeline != null) _timeline.Refresh(activePlayer);

            // Slots : grisage selon PA / HG dispo du combattant qu'on controle, etat armed.
            // 2.13.c : passe aussi le turnNumber pour calcul du cooldown signature.
            RefreshSlots(hasLocal ? local : default, hasLocal, state.TurnNumber);

            // End Turn : seul le joueur actif peut le presser. (Si _debugAllPlayersControllable
            // est false et qu'on n'est pas le joueur actif, on grise le bouton.)
            if (_endTurnButton != null)
            {
                bool canEnd = state.CurrentPhase == CombatPhase.TurnActive
                              && controlPlayer == activePlayer;
                _endTurnButton.interactable = canEnd;
            }

            // 2.16.c.ii — Overlay Victory/Defeat affiche sur MatchEnd. Polled chaque frame
            // mais Refresh() est idempotent (no-op tant qu'on est deja dans le bon etat).
            if (_matchEndOverlay != null)
            {
                _matchEndOverlay.Refresh(state.CurrentPhase, state.WinnerPlayerIndex, _localPlayerIndex, state.TurnNumber);
            }
        }

        private void RefreshSlots(in Combatant c, bool valid, int turnNumber)
        {
            for (int i = 0; i < _spellSlots.Length; i++)
            {
                var slot = _spellSlots[i];
                if (slot == null) continue;
                slot.SetState(ResolveSlotState(slot.Spell, c, valid, turnNumber));
                slot.SetCooldownLabel(ResolveCooldownTurnsLeft(slot.Spell, c, valid, turnNumber));
            }
            if (_signatureSlot != null)
            {
                _signatureSlot.SetState(ResolveSlotState(_signatureSlot.Spell, c, valid, turnNumber));
                _signatureSlot.SetCooldownLabel(ResolveCooldownTurnsLeft(_signatureSlot.Spell, c, valid, turnNumber));
            }
        }

        private SpellSlotView.SlotState ResolveSlotState(SpellId spell, in Combatant c, bool valid, int turnNumber)
        {
            if (_armedSpell.HasValue && _armedSpell.Value == spell)
            {
                return SpellSlotView.SlotState.Armed;
            }
            if (!valid || spell == SpellId.None) return SpellSlotView.SlotState.Disabled;
            if (!SpellRegistry.TryGet(spell, out SpellDef def)) return SpellSlotView.SlotState.Disabled;

            // Cout PA effectif approxime (base + RageInsatiable). Le bonus -1 PA du passif
            // Soulrender depend de la cible visee donc on l'ignore pour le grisage initial.
            int paCost = def.PACost;
            if (HasStatus(c, StatusKind.RageInsatiableActive)) paCost += 1;
            if (paCost < 1) paCost = 1;

            if (c.PA < paCost) return SpellSlotView.SlotState.Disabled;
            if (c.Resource < def.HGCostMandatory) return SpellSlotView.SlotState.Disabled;

            // 2.13.c : cooldown (signature Ame Laceree) et 1/match (Pacte de Sang, Dernier Souffle).
            if (ResolveCooldownTurnsLeft(spell, c, valid: true, turnNumber) > 0) return SpellSlotView.SlotState.Disabled;
            if (def.OncePerMatchBit != SpellRegistry.OncePerMatchBitNone
                && (c.OncePerMatchUsedFlags & (1 << def.OncePerMatchBit)) != 0)
            {
                return SpellSlotView.SlotState.Disabled;
            }

            return SpellSlotView.SlotState.Normal;
        }

        /// <summary>
        /// Tours de cooldown restants pour un sort, ou 0 s'il est dispo. Pour 2.13.c,
        /// seule la signature Ame Laceree a un cooldown de 4 tours. Les autres retournent 0.
        /// </summary>
        private static int ResolveCooldownTurnsLeft(SpellId spell, in Combatant c, bool valid, int turnNumber)
        {
            if (!valid || spell != SpellId.SoulrenderAmeLaceree) return 0;
            int sinceLast = turnNumber - c.LastAmeLaceeUsedOnTurn;
            int remaining = SpellRegistry.AmeLaceeCooldownTurns - sinceLast;
            return remaining > 0 ? remaining : 0;
        }

        private static bool HasStatus(in Combatant c, StatusKind kind)
        {
            for (int i = 0; i < 8; i++)
            {
                var s = c.Statuses[i];
                if (s.Kind == kind && s.TurnsLeft > 0) return true;
            }
            return false;
        }

        /// <summary>
        /// Click sur un slot : passe le HUD en mode armed. Le prochain clic grille (gere
        /// par CombatInputController) envoie le CastSpellCommand. Pour les sorts Filter=Self,
        /// l'input controller redirige la cible vers la case du caster, donc Lorenzo peut
        /// cliquer n'importe ou pour confirmer.
        ///
        /// Re-clic sur le sort deja arme = annulation.
        /// </summary>
        public void OnSlotClicked(SpellId spell)
        {
            if (spell == SpellId.None) return;

            if (_armedSpell.HasValue && _armedSpell.Value == spell)
            {
                Disarm();
                return;
            }

            _armedSpell = spell;
            ArmedSpellChanged?.Invoke();
            Debug.Log($"[Nymora.HUD] Armed {spell} (cliquez sur une case pour lancer)");
        }

        /// <summary>
        /// Appele par CombatInputController quand le joueur clique sur la grille.
        /// Si un sort est arme : retourne true + le SpellId, et clear l'etat armed.
        /// Sinon : false (laisser passer le MoveCommand).
        /// </summary>
        public bool ConsumeArmedSpell(out SpellId spell)
        {
            if (_armedSpell.HasValue)
            {
                spell = _armedSpell.Value;
                Disarm();
                return true;
            }
            spell = SpellId.None;
            return false;
        }

        public void Disarm()
        {
            if (_armedSpell.HasValue)
            {
                _armedSpell = null;
                ArmedSpellChanged?.Invoke();
            }
        }

        // -- Tooltip API (2.13.c) --

        public void ShowTooltip(SpellId spell, RectTransform anchor)
        {
            if (_tooltip != null) _tooltip.Show(spell, anchor);
        }

        public void HideTooltip()
        {
            if (_tooltip != null) _tooltip.Hide();
        }

        private void OnEndTurnClicked()
        {
            var game = QuantumRunner.Default?.Game;
            if (game == null) return;
            var frame = game.Frames.Verified;
            if (frame == null) return;
            if (!frame.TryGetSingleton<CombatState>(out var state)) return;

            int senderPlayer = ResolveControlPlayer(state.ActivePlayerIndex);
            game.SendCommand(senderPlayer, new EndTurnCommand());
            Debug.Log($"[Nymora.HUD] EndTurnCommand sent player={senderPlayer}");
            Disarm(); // securite : passer le tour annule un eventuel armement
        }

        private int ResolveControlPlayer(int activePlayer)
        {
            return _debugAllPlayersControllable ? activePlayer : _localPlayerIndex;
        }
    }
}
