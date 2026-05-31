using System.Collections.Generic;
using Nymora.Core.Data;
using Quantum;
using UnityEngine;

namespace Nymora.Combat.View.HUD
{
    /// <summary>
    /// Journal de combat -> chat. Pole la frame Quantum (CallbackUpdateView) et pousse des lignes
    /// FR dans le chat via CombatLogRelay (relais Core). 100% View : aucune modif simulation,
    /// AUCUN bump CombatRulesVersion.
    ///
    /// Detection (meme approche pollee que CombatantHPWatcher / CombatVFXView, pas d'event bus
    /// Quantum custom dans ce projet) :
    ///   - changement de round (CombatState.TurnNumber)  -> separateur "Tour N"
    ///   - cast (Combatant.LastCastSequence)             -> nom du sort, UNIQUEMENT pour le joueur
    ///       LOCAL (anti-leak brouillard : on ne nomme jamais les sorts adverses).
    ///   - delta HP                                       -> degats / soin chiffres (deja publics).
    ///       Les degats infliges par MON sort sont ATTRIBUES au sort (correlation fenetre courte
    ///       cast->damage, comme SignatureCastBridge).
    ///   - delta bouclier (status ShieldActive)           -> gain / absorption.
    ///   - HP -> 0                                         -> KO.
    ///   - CombatPhase.MatchEnd                            -> resultat.
    ///
    /// Cycle de vie : singleton DontDestroyOnLoad cree une fois via RuntimeInitializeOnLoadMethod.
    /// QuantumCallback est un dispatcher global -> un seul abonne suffit quelle que soit la scene
    /// de combat. En dehors d'un combat (hub), aucun game ne tourne -> aucune ligne poussee.
    /// </summary>
    public sealed class CombatChatLogView : MonoBehaviour
    {
        private const string TurnColor = "#8a93a8";   // gris bleute (separateur)
        private const string CastColor = "#9bd6ff";   // bleu clair (mes sorts)
        private const string DamageColor = "#e06565"; // rouge
        private const string HealColor = "#5fd06a";   // vert
        private const string ShieldColor = "#6fb3ff"; // bleu bouclier
        private const string ControlColor = "#c9a0dc";// violet (debuffs PM/PA)
        private const string ResourceColor = "#caa64f";// or (ressource de classe)
        private const string KoColor = "#ffb000";     // ambre

        // Fenetre de correlation cast -> degats (delai Quantum command -> apply damage, qq ticks).
        private const float AttributionWindow = 1.0f;

        private static CombatChatLogView _instance;

        private readonly Dictionary<EntityRef, int> _lastHP = new Dictionary<EntityRef, int>(4);
        private readonly Dictionary<EntityRef, int> _lastShield = new Dictionary<EntityRef, int>(4);
        private readonly Dictionary<EntityRef, int> _lastMoveMalus = new Dictionary<EntityRef, int>(4);
        private readonly Dictionary<EntityRef, int> _lastPaMalus = new Dictionary<EntityRef, int>(4);
        private readonly Dictionary<EntityRef, int> _lastResource = new Dictionary<EntityRef, int>(4);
        private readonly Dictionary<EntityRef, int> _lastCastSeq = new Dictionary<EntityRef, int>(4);
        private int _lastTurnNumber = -1;
        private bool _matchEndLogged;
        private bool _ready;

        // Cast LOCAL en attente d'attribution (pour coller le nom du sort aux degats qui suivent).
        private SpellId _pendingSpell = SpellId.None;
        private float _pendingTime;
        private bool _pendingEmitted;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (_instance != null) return;
            var go = new GameObject("CombatChatLogView");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<CombatChatLogView>();
        }

        private void Awake()
        {
            if (_instance != null && _instance != this) { Destroy(gameObject); return; }
            _instance = this;
            QuantumCallback.Subscribe(this, (CallbackGameStarted c) => OnGameStarted(c.Game));
            QuantumCallback.Subscribe(this, (CallbackUpdateView c) => OnUpdateView(c.Game));
        }

        private void OnGameStarted(QuantumGame game)
        {
            _lastHP.Clear();
            _lastShield.Clear();
            _lastMoveMalus.Clear();
            _lastPaMalus.Clear();
            _lastCastSeq.Clear();
            _lastTurnNumber = -1;
            _matchEndLogged = false;
            _pendingSpell = SpellId.None;
            _pendingEmitted = false;
            _ready = false;

            var frame = game?.Frames?.Verified;
            if (frame == null) return;

            // Snapshot initial : evite de logger de faux casts / deltas au demarrage du match.
            var filter = frame.Filter<Combatant>();
            while (filter.Next(out EntityRef e, out Combatant c))
            {
                _lastHP[e] = c.HP;
                _lastShield[e] = ReadActiveShield(c);
                _lastMoveMalus[e] = ReadStatusMag(c, StatusKind.MovementMalus);
                _lastPaMalus[e] = ReadStatusMag(c, StatusKind.ActionMalus);
                _lastResource[e] = ResourceValue(c);
                _lastCastSeq[e] = c.LastCastSequence;
            }
            _ready = true;
        }

        private void OnUpdateView(QuantumGame game)
        {
            if (!_ready) return;
            var frame = game?.Frames?.Verified;
            if (frame == null) return;
            if (!frame.TryGetSingleton<CombatState>(out var state)) return;

            // --- Separateur de round (uniquement en jeu actif, pas pendant l'intro pile-ou-face) ---
            if (state.CurrentPhase == CombatPhase.TurnActive && state.TurnNumber != _lastTurnNumber)
            {
                _lastTurnNumber = state.TurnNumber;
                Push($"<color={TurnColor}>--- Tour {state.TurnNumber} ---</color>");
            }

            // --- Passe 1 : detection des casts (set du pending AVANT les deltas, car le caster et
            //     la victime sont 2 entites distinctes et l'ordre d'iteration n'est pas garanti) ---
            var castFilter = frame.Filter<Combatant>();
            while (castFilter.Next(out EntityRef e, out Combatant c))
            {
                int prevSeq = _lastCastSeq.TryGetValue(e, out var ps) ? ps : c.LastCastSequence;
                if (c.LastCastSequence != prevSeq)
                {
                    // On ne nomme que MES sorts (brouillard). Un nouveau cast flush le precedent
                    // non-attribue (sort sans degats : buff / setup / heal-only) en ligne standalone.
                    if (LocalPlayerResolver.LocalOwns(game, c.PlayerIndex) && c.LastCastSpellId != SpellId.None)
                    {
                        FlushPendingCast();
                        _pendingSpell = c.LastCastSpellId;
                        _pendingTime = Time.unscaledTime;
                        _pendingEmitted = false;
                    }
                }
                _lastCastSeq[e] = c.LastCastSequence;
            }

            // --- Passe 2 : deltas HP / bouclier ---
            var filter = frame.Filter<Combatant>();
            while (filter.Next(out EntityRef e, out Combatant c))
            {
                bool mine = LocalPlayerResolver.LocalOwns(game, c.PlayerIndex);
                string who = mine ? LocalPseudo() : OppPseudo();

                // HP : degats / soin.
                int hp = c.HP;
                if (_lastHP.TryGetValue(e, out int prevHP))
                {
                    int delta = hp - prevHP;
                    if (delta < 0)
                    {
                        // Attribution au sort : seulement sur la VICTIME ENNEMIE (mon sort offensif).
                        if (!mine && TryConsumePending(out string spell))
                            Push($"<color={CastColor}>» {LocalPseudo()} : {spell}</color> <color={DamageColor}>({who} -{-delta})</color>");
                        else
                            Push($"<color={DamageColor}>{who} -{-delta}</color>");

                        if (prevHP > 0 && hp <= 0)
                            Push($"<color={KoColor}>{who} est éliminé !</color>");
                    }
                    else if (delta > 0)
                    {
                        // Soin attribue si c'est MON heal sur moi-meme (Seve Vive, etc.).
                        if (mine && TryConsumePending(out string spell))
                            Push($"<color={CastColor}>» {LocalPseudo()} : {spell}</color> <color={HealColor}>({who} +{delta} PV)</color>");
                        else
                            Push($"<color={HealColor}>{who} +{delta} PV</color>");
                    }
                }
                _lastHP[e] = hp;

                // Bouclier : gain / absorption (status ShieldActive, comme CombatantHPWatcher).
                int shield = ReadActiveShield(c);
                if (_lastShield.TryGetValue(e, out int prevSh))
                {
                    int sd = shield - prevSh;
                    if (sd > 0)
                        Push($"<color={ShieldColor}>{who} bouclier +{sd}</color>");
                    else if (sd < 0 && shield > 0)
                        Push($"<color={ShieldColor}>{who} bouclier -{-sd}</color>");
                }
                _lastShield[e] = shield;

                // PM / PA : debuffs de sorts uniquement (rising edge absent->present). Les statuts
                // sont publics (icones de statut affichees) -> fog-safe ; on ne nomme pas la source.
                int mm = ReadStatusMag(c, StatusKind.MovementMalus);
                if (_lastMoveMalus.TryGetValue(e, out int prevMm) && mm > 0 && prevMm == 0)
                    Push($"<color={ControlColor}>{who} -{mm} PM</color>");
                _lastMoveMalus[e] = mm;

                int pam = ReadStatusMag(c, StatusKind.ActionMalus);
                if (_lastPaMalus.TryGetValue(e, out int prevPa) && pam > 0 && prevPa == 0)
                    Push($"<color={ControlColor}>{who} -{pam} PA</color>");
                _lastPaMalus[e] = pam;

                // Progression de ressource de classe (HG / PR / FD / PT, ou leurres pour Ghostra).
                // Le nombre de leurres Ghostra est une info CACHEE cote adverse -> log local seulement.
                int res = ResourceValue(c);
                if (_lastResource.TryGetValue(e, out int prevRes) && res != prevRes
                    && c.Class != NymoraClass.None
                    && (mine || c.Class != NymoraClass.Ghostra))
                {
                    int max = ResourceMax(c.Class);
                    Push($"<color={ResourceColor}>{who} : {ResourceName(c.Class)} {res}/{max}</color>");
                    if (max > 0 && res >= max && prevRes < max)
                        Push($"<color={KoColor}>{who} : Signature débloquée !</color>");
                }
                _lastResource[e] = res;
            }

            // --- Cast local non suivi de degats (buff / setup / piege) : flush apres la fenetre ---
            if (_pendingSpell != SpellId.None && !_pendingEmitted
                && Time.unscaledTime - _pendingTime > AttributionWindow)
            {
                FlushPendingCast();
            }

            // --- Fin de match ---
            if (state.CurrentPhase == CombatPhase.MatchEnd && !_matchEndLogged)
            {
                _matchEndLogged = true;
                int local = LocalPlayerResolver.Resolve();
                string line;
                if (state.WinnerPlayerIndex < 0) line = $"<color={TurnColor}>--- MATCH NUL ---</color>";
                else if (state.WinnerPlayerIndex == local) line = $"<color={HealColor}>--- VICTOIRE ---</color>";
                else line = $"<color={DamageColor}>--- DÉFAITE ---</color>";
                Push(line);
            }
        }

        // Consomme le sort en attente s'il est encore dans la fenetre. Retourne false sinon.
        private bool TryConsumePending(out string spellName)
        {
            spellName = null;
            if (_pendingSpell == SpellId.None) return false;
            if (Time.unscaledTime - _pendingTime > AttributionWindow) return false;
            spellName = SpellDisplayInfo.GetDisplayName(_pendingSpell);
            _pendingEmitted = true;
            return true;
        }

        // Emet le cast en ligne standalone s'il n'a pas ete attribue a des degats / un soin.
        private void FlushPendingCast()
        {
            if (_pendingSpell != SpellId.None && !_pendingEmitted)
                Push($"<color={CastColor}>» {LocalPseudo()} : {SpellDisplayInfo.GetDisplayName(_pendingSpell)}</color>");
            _pendingSpell = SpellId.None;
            _pendingEmitted = false;
        }

        // Magnitude (PV) du bouclier actif (status ShieldActive). 0 si aucun. Cf CombatantHPWatcher.
        private static int ReadActiveShield(in Combatant c) => ReadStatusMag(c, StatusKind.ShieldActive);

        // Magnitude du 1er status actif d'un Kind donne (0 si absent). Meme iteration que les autres widgets.
        private static int ReadStatusMag(in Combatant c, StatusKind kind)
        {
            for (int s = 0; s < StatusHelper.SlotCount; s++)
            {
                if (c.Statuses[s].Kind == kind && c.Statuses[s].TurnsLeft > 0)
                    return c.Statuses[s].Magnitude;
            }
            return 0;
        }

        // Valeur de ressource "ressentie" : compte des leurres actifs pour Ghostra (sa Remanence
        // n'utilise pas Combatant.Resource), sinon la ressource generique. Cf CombatHUDController.
        private static int ResourceValue(in Combatant c)
        {
            if (c.Class == NymoraClass.Ghostra)
            {
                int active = 0;
                for (int i = 0; i < 3; i++)
                    if (c.Decoys[i].Kind != DecoyKind.None) active++;
                return active;
            }
            return c.Resource;
        }

        private static int ResourceMax(NymoraClass cls)
            => cls == NymoraClass.Ghostra ? 3 : CombatantStats.GetMaxResource(cls);

        private static string ResourceName(NymoraClass cls)
        {
            switch (cls)
            {
                case NymoraClass.Soulrender: return "Hémoglyphe";
                case NymoraClass.Nightseer: return "Prescience";
                case NymoraClass.Colossar: return "Fondation";
                case NymoraClass.Necram: return "Putréfaction";
                case NymoraClass.Ghostra: return "Leurres";
                default: return "Ressource";
            }
        }

        private static string LocalPseudo()
            => string.IsNullOrEmpty(MatchBridge.LocalDisplayName) ? "Toi" : MatchBridge.LocalDisplayName;

        private static string OppPseudo()
            => string.IsNullOrEmpty(MatchBridge.OpponentDisplayName) ? "Adversaire" : MatchBridge.OpponentDisplayName;

        private static void Push(string richLine) => CombatLogRelay.Push(richLine);
    }
}
