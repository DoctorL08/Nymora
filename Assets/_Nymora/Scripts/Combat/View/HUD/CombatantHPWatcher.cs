using System.Collections.Generic;
using Nymora.Combat.Grid;
using Quantum;
using UnityEngine;

namespace Nymora.Combat.View.HUD
{
    /// <summary>
    /// Surveille les HP des combattants tick par tick. Quand un HP change, demande au
    /// FloatingTextManager de spawner un "-X" rouge (damage) ou "+X" vert (heal) au-dessus
    /// du sprite.
    ///
    /// Approche pollee (vs Signal Quantum) :
    ///   - Simple : aucune modif DSL Quantum, pas de regen code
    ///   - Suffisant pour 2.13.c : couvre tous les sorts (Pacte de Sang -80 HP self,
    ///     Sang Coagule -30 HP, Charge Brutale 180 dgts, Curee Heal 50%, etc.)
    ///   - Limite : ne distingue PAS shield absorb vs HP loss. Si Peau de Fer absorbe
    ///     200 dgts, le HP ne bouge pas, donc pas de texte (mais la status line montre
    ///     le shield qui decremente). Phase ulterieure : signal Quantum dedie.
    ///
    /// Position spawn : iso projection depuis GridX/GridY (+ offset Y au-dessus du sprite).
    /// Pas de couplage a CombatantRenderer.
    /// </summary>
    public class CombatantHPWatcher : MonoBehaviour
    {
        [Header("Configuration")]
        [SerializeField] private FloatingTextManager _manager;
        [SerializeField] private GridSettings _gridSettings;

        [Tooltip("Offset Y monde au-dessus de la case (1 = environ au-dessus de la tete du sprite).")]
        [SerializeField] private float _spawnYOffsetWorld = 1.1f;

        [Tooltip("Offset Y additionnel pour les chiffres de bouclier (au-dessus des degats, pour ne pas se chevaucher).")]
        [SerializeField] private float _shieldExtraYOffsetWorld = 0.35f;

        [Tooltip("Offset Y additionnel pour les flottants de perte PA/PM (au-dessus du bouclier).")]
        [SerializeField] private float _resourceExtraYOffsetWorld = 0.7f;

        private readonly Dictionary<EntityRef, int> _lastHP = new Dictionary<EntityRef, int>(4);
        // J5 — suit la Magnitude du status ShieldActive par entite (gain / absorption).
        private readonly Dictionary<EntityRef, int> _lastShield = new Dictionary<EntityRef, int>(4);
        // PATCH #4 — suit LastCastSequence par entite pour detecter les casts de signature
        // depuis la FRAME (deterministe -> arme le bridge sur les 2 clients, pas que le caster).
        private readonly Dictionary<EntityRef, int> _lastCastSeq = new Dictionary<EntityRef, int>(4);
        // Patch 5 juin — suit la magnitude des malus PM (MovementMalus) / PA (ActionMalus) par entite,
        // pour spawn un flottant "-N PM" / "-N PA" quand le malus est appliqué (forced drain, pas le
        // coût de sort normal).
        private readonly Dictionary<EntityRef, int> _lastPmMalus = new Dictionary<EntityRef, int>(4);
        private readonly Dictionary<EntityRef, int> _lastPaMalus = new Dictionary<EntityRef, int>(4);
        // Patch 8 juin — suit LastVeninTickOnTurn par entite pour detecter un tick venin CE frame.
        //   En miroir Necram, le tick (-X) et la regen Floraison (+Y) tombent dans la MEME frame :
        //   sans ce split, le poll HP fusionnerait en un net trompeur (-160 = -200 venin +40 regen).
        private readonly Dictionary<EntityRef, int> _lastVeninTickTurn = new Dictionary<EntityRef, int>(4);
        // 6 juin — HP des LEURRES Ghostra (clé (Ghostra, slot)). Les leurres ne sont pas des entités
        //   Combatant -> pas couverts par _lastHP. Sert au flottant de dégâts quand un leurre encaisse
        //   (notamment la Réplique Protectrice qui absorbe les dégâts redirigés du Ghostra).
        private readonly Dictionary<(EntityRef, int), int> _lastDecoyHP = new Dictionary<(EntityRef, int), int>(6);
        private Vector3 _centerOffset;
        private bool _gridReady;

        private void Awake()
        {
            QuantumCallback.Subscribe(this, (CallbackGameStarted c) => OnGameStarted(c.Game));
            QuantumCallback.Subscribe(this, (CallbackUpdateView c) => OnUpdateView(c.Game));
        }

        private void OnGameStarted(QuantumGame game)
        {
            _lastHP.Clear();
            _lastShield.Clear();
            _lastCastSeq.Clear();
            _lastPmMalus.Clear();
            _lastPaMalus.Clear();
            _lastVeninTickTurn.Clear();
            _lastDecoyHP.Clear();
            if (_gridSettings == null)
            {
                Debug.LogWarning("[CombatantHPWatcher] GridSettings manquant — cable dans l'Inspector.", this);
                _gridReady = false;
                return;
            }

            var frame = game.Frames.Verified;
            if (frame.TryGetSingleton<GridSingleton>(out var grid))
            {
                _centerOffset = _gridSettings.CenterGrid
                    ? IsoProjection.CenterOffset(grid.Width, grid.Height, _gridSettings.TileWorldWidth, _gridSettings.TileWorldHeight)
                    : Vector3.zero;
            }
            _gridReady = true;
        }

        private void OnUpdateView(QuantumGame game)
        {
            if (!_gridReady || _manager == null) return;

            var frame = game.Frames.Verified;
            if (frame == null) return;

            // PATCH #4 — 1re passe : detecte les casts de SIGNATURE depuis la frame (LastCastSequence
            // avance + LastCastSpellId signature) et arme SignatureCastBridge. Deterministe -> marche
            // sur les 2 clients, donc le flottant "SMASH!!" apparait AUSSI cote adverse (avant : seul
            // le caster armait le bridge via son input local). Passe separee = le bridge est arme avant
            // l'evaluation des deltas HP ci-dessous, quel que soit l'ordre d'iteration des entites.
            var sigFilter = frame.Filter<Combatant>();
            while (sigFilter.Next(out EntityRef sigEntity, out Combatant sigC))
            {
                if (_lastCastSeq.TryGetValue(sigEntity, out int prevSeq)
                    && sigC.LastCastSequence != prevSeq
                    && SignatureCastBridge.IsSignatureSpell(sigC.LastCastSpellId))
                {
                    SignatureCastBridge.NotifySpellCast(sigC.LastCastSpellId);
                }
                _lastCastSeq[sigEntity] = sigC.LastCastSequence;
            }

            var filter = frame.Filter<Combatant>();
            while (filter.Next(out EntityRef entity, out Combatant c))
            {
                Vector3 baseWorldPos = IsoProjection.GridToWorld(
                    c.GridX, c.GridY,
                    _gridSettings.TileWorldWidth, _gridSettings.TileWorldHeight) + _centerOffset;
                baseWorldPos.y += _spawnYOffsetWorld;

                // --- HP : degats / soin ---
                int currentHP = c.HP;

                // Patch 8 juin — detecte un tick venin CE frame (LastVeninTickOnTurn a avance). En miroir
                // Necram, tick (-X) et regen Floraison (+Y) tombent dans la meme frame -> on les separe en
                // 2 flottants au lieu d'un net fusionne trompeur (sinon "-160" pour "-200 venin +40 regen").
                bool veninTickedThisFrame = false;
                if (_lastVeninTickTurn.TryGetValue(entity, out int prevTickTurn))
                {
                    if (c.LastVeninTickOnTurn != prevTickTurn && c.LastVeninTickOnTurn > 0)
                        veninTickedThisFrame = true;
                }
                _lastVeninTickTurn[entity] = c.LastVeninTickOnTurn;

                if (_lastHP.TryGetValue(entity, out int prevHP))
                {
                    int delta = currentHP - prevHP;
                    if (delta != 0)
                    {
                        int veninTick = veninTickedThisFrame ? ComputeVeninTickAmount(frame, c) : 0;
                        if (veninTick > 0)
                        {
                            // Flottant venin dedie ("-X" rouge), recalcule comme la sim, puis le RESIDU
                            // (regen Floraison "+Y" vert, ou tout autre HP de la frame) en flottant separe.
                            float vEmph = c.MaxHP > 0 ? Mathf.Clamp01((float)veninTick / c.MaxHP) : 0f;
                            _manager.Spawn(baseWorldPos, -veninTick, vEmph);
                            int residual = delta + veninTick; // ex : -160 + 200 = +40 (regen Floraison)
                            if (residual != 0)
                            {
                                Vector3 resPos = baseWorldPos;
                                resPos.y += _shieldExtraYOffsetWorld; // decale pour ne pas chevaucher le venin
                                _manager.Spawn(resPos, residual, 0f);
                            }
                        }
                        // 19 mai POLISH-6h — Si un sort signature vient d'etre cast (dans la fenetre
                        // SignatureCastBridge ~1.5s), spawn le texte EPIQUE (gros or bounce). Sinon
                        // texte standard rouge/vert. Limite aux degats (delta < 0) — un signature ne
                        // soigne jamais en l'etat actuel mais defensif.
                        else if (delta < 0 && SignatureCastBridge.IsSignatureRecent())
                        {
                            _manager.SpawnSignatureHit(baseWorldPos, delta);
                        }
                        else
                        {
                            // J3 — emphase = fraction de PV perdue (gros coup = chiffre plus gros/orange).
                            float emphasis = (delta < 0 && c.MaxHP > 0)
                                ? Mathf.Clamp01((float)(-delta) / c.MaxHP)
                                : 0f;
                            _manager.Spawn(baseWorldPos, delta, emphasis);
                        }
                    }
                }
                _lastHP[entity] = currentHP;

                // --- J5 : Bouclier (gain / absorption) ---
                int currentShield = ReadActiveShield(c);
                if (_lastShield.TryGetValue(entity, out int prevShield))
                {
                    int sd = currentShield - prevShield;
                    Vector3 shieldPos = baseWorldPos;
                    shieldPos.y += _shieldExtraYOffsetWorld;
                    if (sd > 0)
                    {
                        // Bouclier gagne / rafraichi -> "+N" bleu.
                        _manager.SpawnShield(shieldPos, sd);
                    }
                    else if (sd < 0 && currentShield > 0)
                    {
                        // Bouclier qui ENCAISSE (reste > 0) -> "-N" bleu. On ignore la chute a 0
                        // (ambigu : absorption totale vs expiration de duree) pour ne pas faire
                        // pop un "-N" sur une simple fin de bouclier.
                        _manager.SpawnShield(shieldPos, sd);
                    }
                }
                _lastShield[entity] = currentShield;

                // --- Patch 5 juin : perte de PM (MovementMalus) / PA (ActionMalus) ---
                //   On spawn "-N PM" / "-N PA" quand le malus APPARAÎT ou AUGMENTE (= drain forcé par
                //   un effet : Rugissement, Ancrage, Provocation, Choc Sismique, Traquenard...). On ne
                //   suit PAS le coût de sort normal (qui n'est pas un malus -> pas de bruit visuel).
                int pmMalus = ReadStatusMagnitude(c, StatusKind.MovementMalus);
                if (_lastPmMalus.TryGetValue(entity, out int prevPmMalus) && pmMalus > prevPmMalus)
                {
                    Vector3 pmPos = baseWorldPos;
                    pmPos.y += _resourceExtraYOffsetWorld;
                    _manager.SpawnResourceDelta(pmPos, -(pmMalus - prevPmMalus), isPm: true);
                }
                _lastPmMalus[entity] = pmMalus;

                int paMalus = ReadStatusMagnitude(c, StatusKind.ActionMalus);
                if (_lastPaMalus.TryGetValue(entity, out int prevPaMalus) && paMalus > prevPaMalus)
                {
                    Vector3 paPos = baseWorldPos;
                    paPos.y += _resourceExtraYOffsetWorld + 0.3f; // légèrement au-dessus du flottant PM
                    _manager.SpawnResourceDelta(paPos, -(paMalus - prevPaMalus), isPm: false);
                }
                _lastPaMalus[entity] = paMalus;

                // --- 6 juin : HP des LEURRES Ghostra (flottant de dégâts sur le leurre) ---
                //   Couvre la Réplique Protectrice qui absorbe les dégâts redirigés du Ghostra, ET tout
                //   leurre qui encaisse un hit direct. Le flottant apparaît sur la CASE du leurre. On ne
                //   capture pas le coup létal (le slot passe à None le même tick) — un partiel suffit.
                if (c.Class == NymoraClass.Ghostra)
                {
                    for (int slot = 0; slot < DecoyHelpers.MaxDecoys; slot++)
                    {
                        var dkey = (entity, slot);
                        var d = c.Decoys[slot];
                        if (d.Kind == DecoyKind.None) { _lastDecoyHP.Remove(dkey); continue; }
                        int decoyHp = d.HP;
                        if (_lastDecoyHP.TryGetValue(dkey, out int prevDecoyHp))
                        {
                            int dd = decoyHp - prevDecoyHp;
                            if (dd < 0)
                            {
                                Vector3 dPos = IsoProjection.GridToWorld(
                                    d.PosX, d.PosY,
                                    _gridSettings.TileWorldWidth, _gridSettings.TileWorldHeight) + _centerOffset;
                                dPos.y += _spawnYOffsetWorld;
                                int maxHp = DecoyHelpers.GetDecoyMaxHp(d.Kind);
                                float emphasis = maxHp > 0 ? Mathf.Clamp01((float)(-dd) / maxHp) : 0f;
                                _manager.Spawn(dPos, dd, emphasis);
                            }
                        }
                        _lastDecoyHP[dkey] = decoyHp;
                    }
                }
            }
        }

        /// <summary>
        /// Patch 8 juin — recompute EXACT du montant d'un tick venin, formule identique a
        /// <see cref="VeninHelpers.TryTick"/> (stacks * clock Floraison + Marque Sacrificielle +
        /// bonus Brume Toxique). Sert a afficher un flottant venin dedie en miroir Necram, ou le
        /// tick et la regen Floraison tombent dans la meme frame. La frame Verified est l'etat
        /// post-tick, mais le tick ne consomme PAS les marques et ne change pas la densite -> les
        /// valeurs lues sont celles du tick. Densite PAR-EQUIPE (fix miroir v141).
        /// </summary>
        private static int ComputeVeninTickAmount(Frame frame, in Combatant c)
        {
            if (c.VeninStacks <= 0) return 0;
            int density = VeninHelpers.GetDensityOnTeam(frame, c.PlayerIndex);
            int dmgPerMark = VeninHelpers.GetTickDmgPerMark(density);
            if (dmgPerMark <= 0) return 0;
            int total = c.VeninStacks * dmgPerMark;
            total += ReadStatusMagnitude(c, StatusKind.MarqueSacrificielle); // bonus flat sur le tick
            if (GridHelpers.GetTerrainKind(frame, c.GridX, c.GridY) == TerrainKind.BrumeToxique)
                total += c.VeninStacks * SpellRegistry.BrumeToxiqueTickBonusPerMark;
            return total;
        }

        /// <summary>
        /// Magnitude du status `kind` actif (TurnsLeft > 0) sur le snapshot, 0 sinon. Sert aux malus
        /// PM (MovementMalus) / PA (ActionMalus) pour le flottant de perte de ressource.
        /// </summary>
        private static int ReadStatusMagnitude(in Combatant c, StatusKind kind)
        {
            for (int s = 0; s < StatusHelper.SlotCount; s++)
            {
                if (c.Statuses[s].Kind == kind && c.Statuses[s].TurnsLeft > 0)
                {
                    return c.Statuses[s].Magnitude;
                }
            }
            return 0;
        }

        /// <summary>
        /// J5 — Lit la Magnitude (PV) du bouclier actif (status ShieldActive) sur un snapshot
        /// Combatant. 0 si aucun bouclier actif. Meme iteration que CombatantRenderer.
        /// </summary>
        private static int ReadActiveShield(in Combatant c)
        {
            for (int s = 0; s < StatusHelper.SlotCount; s++)
            {
                if (c.Statuses[s].Kind == StatusKind.ShieldActive && c.Statuses[s].TurnsLeft > 0)
                {
                    return c.Statuses[s].Magnitude;
                }
            }
            return 0;
        }
    }
}
