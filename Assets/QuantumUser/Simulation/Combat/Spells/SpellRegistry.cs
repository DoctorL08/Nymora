namespace Quantum
{
    /// <summary>
    /// Definition statique d'un sort (cost + targeting + effets).
    ///
    /// SpellDef sert de catalogue immuable lu par SpellSystem. Les effets
    /// "exotiques" (statuses appliques, conditionnels, self-damage, etc.) sont
    /// implementes par switch SpellId dans SpellSystem.ApplySpellSpecificEffects.
    /// Ce design permet de garder SpellDef simple sans tomber dans un "Effect
    /// Composition Engine" prematurement complexe.
    ///
    /// Champs :
    ///   PACost           : cout PA base. Cout EFFECTIF via EffectiveStats.GetPACost
    ///                      (peut etre modifie par RageInsatiableActive : +1 PA).
    ///   Shape/Filter     : ciblage (cf TargetingResolver).
    ///   RangeMin/Max     : portee Manhattan depuis le caster.
    ///   DamageAmount     : dgts flat applique a chaque cible dans la zone (0 si pas offensif).
    ///   HGCostMandatory  : HG toujours consomme au cast (rejet si insuffisant).
    ///   HGCostMaxOptional: HG max que le joueur peut depenser EN PLUS (via Cmd.HGSpend).
    ///   OncePerMatchBit  : bit dans Combatant.OncePerMatchUsedFlags. 255 = pas concerne.
    ///   IsOffensive      : sert au pipeline buff +50% (Pacte) et regen PA Rage Insatiable.
    ///                      Convention : sort offensif = porte une intention de damage.
    ///                      Damage > 0 sufit comme proxy, mais on stocke explicitement
    ///                      pour les futurs sorts offensifs a damage conditionnel.
    /// </summary>
    public struct SpellDef
    {
        public int PACost;
        public TargetingShape Shape;
        public TargetingFilter Filter;
        public int RangeMin;
        public int RangeMax;
        public int DamageAmount;

        // 2.10.a — extensions
        public byte HGCostMandatory;
        public byte HGCostMaxOptional;
        public byte OncePerMatchBit;
        public byte IsOffensive;     // 0/1 (Byte pour serialisation/copy clean)
    }

    /// <summary>
    /// Catalogue statique des sorts par SpellId.
    /// Switch deterministe (pas de Dictionary heap-alloc).
    /// </summary>
    public static class SpellRegistry
    {
        public const byte OncePerMatchBitNone = 255;

        public const byte OncePerMatchBitPacteDeSang   = 0;
        public const byte OncePerMatchBitDernierSouffle = 1;

        // Constantes Bible V7.1 partagees par plusieurs sorts / systemes.
        public const int PeauDeFerShieldHP            = 200;
        public const int PeauDeFerShieldTurns         = 2;
        public const int PeauDeFerMeleeDmgBonus       = 30;
        public const int MarqueDeCarnageTurns         = 3;
        public const int SeveViveHealBase             = 100;
        public const int SeveViveHealBonusHG          = 60;  // +60 si 1 HG depense -> 160
        public const int SeveViveHealBonusBleed       = 50;  // +50 si BleedDoT actif
        public const int DernierSouffleHealAmount     = 200;
        public const int DernierSouffleHGGain         = 3;
        public const int DernierSouffleHPThresholdPct = 30;  // utilisable uniquement a < 30% HP

        public static bool TryGet(SpellId id, out SpellDef def)
        {
            switch (id)
            {
                // -------------------------------------------------------------
                // SOULRENDER — Bible V7.1
                // -------------------------------------------------------------

                // Tranche-Ame (2.8) : 3 PA, melee 1, 220 dgts. TODO 2.10.c : recul 2 cases si kill.
                case SpellId.SoulrenderTrancheAme:
                    def = new SpellDef
                    {
                        PACost = 3,
                        Shape = TargetingShape.SingleTile,
                        Filter = TargetingFilter.Enemy,
                        RangeMin = 1,
                        RangeMax = 1,
                        DamageAmount = 220,
                        HGCostMandatory = 0,
                        HGCostMaxOptional = 0,
                        OncePerMatchBit = OncePerMatchBitNone,
                        IsOffensive = 1,
                    };
                    return true;

                // Ouvre-Plaie (2.10.a) : 2 PA, range 1, 110 dgts.
                // Optionnel : 1 HG -> 230 dgts + cible AntiHealShield 2 tours.
                case SpellId.SoulrenderOuvrePlaie:
                    def = new SpellDef
                    {
                        PACost = 2,
                        Shape = TargetingShape.SingleTile,
                        Filter = TargetingFilter.Enemy,
                        RangeMin = 1,
                        RangeMax = 1,
                        DamageAmount = 110,
                        HGCostMandatory = 0,
                        HGCostMaxOptional = 1,
                        OncePerMatchBit = OncePerMatchBitNone,
                        IsOffensive = 1,
                    };
                    return true;

                // Pacte de Sang (2.10.a) : 1 PA, self, -80 HP self + +3 HG + BuffNextOffensiveDmgPercent.
                // 1 fois par match.
                case SpellId.SoulrenderPacteDeSang:
                    def = new SpellDef
                    {
                        PACost = 1,
                        Shape = TargetingShape.SingleTile,
                        Filter = TargetingFilter.Self,
                        RangeMin = 0,
                        RangeMax = 0,
                        DamageAmount = 0,
                        HGCostMandatory = 0,
                        HGCostMaxOptional = 0,
                        OncePerMatchBit = OncePerMatchBitPacteDeSang,
                        IsOffensive = 0,
                    };
                    return true;

                // Rugissement (2.10.a) : 3 PA, AoE rayon 3 autour du caster. Pas de dgts.
                // Tous les ennemis dans la zone subissent MovementMalus + AntiTeleport (1 tour).
                // Si cible <50% HP : MovementMalus magnitude = 2 (au lieu de 1).
                //
                // Note ciblage : "AoE 3 autour du Soulrender" = centre = case du caster.
                // RangeMin=0/RangeMax=0 + Filter=AnyTile permet de cliquer sa propre case.
                // Shape CircleMedium = rayon 2 dans TargetingResolver actuel — on declare
                // l'intent rayon 3 ici, la resolution AoE specifique sera faite manuellement
                // par SpellSystem (radius 3 hardcode pour ce sort).
                case SpellId.SoulrenderRugissement:
                    def = new SpellDef
                    {
                        PACost = 3,
                        Shape = TargetingShape.CircleLarge, // semantic intent rayon 3
                        Filter = TargetingFilter.Self,      // ciblage = case du caster
                        RangeMin = 0,
                        RangeMax = 0,
                        DamageAmount = 0,
                        HGCostMandatory = 0,
                        HGCostMaxOptional = 0,
                        OncePerMatchBit = OncePerMatchBitNone,
                        IsOffensive = 0,
                    };
                    return true;

                // Rage Insatiable (2.10.a) : 3 PA, self, applique RageInsatiableActive 2 tours.
                // Effets pendant la duree (gere par helpers / SpellSystem) :
                //   - Sorts coutent +1 PA (EffectiveStats.GetPACost).
                //   - Apres chaque cast offensif : caster regen 1 PA (max 1 par tour, tracker
                //     dans Status.Magnitude = LastTurnPAGained).
                case SpellId.SoulrenderRageInsatiable:
                    def = new SpellDef
                    {
                        PACost = 3,
                        Shape = TargetingShape.SingleTile,
                        Filter = TargetingFilter.Self,
                        RangeMin = 0,
                        RangeMax = 0,
                        DamageAmount = 0,
                        HGCostMandatory = 0,
                        HGCostMaxOptional = 0,
                        OncePerMatchBit = OncePerMatchBitNone,
                        IsOffensive = 0,
                    };
                    return true;

                // Riposte Carmin (2.10.a) : 2 PA, self, applique RipostMelee 1 tour.
                // Quand le Soulrender subit une attaque MELEE (range max == 1) pendant
                // ce status : l'attaquant prend 100 dgts ET gagne MovementMalus 1 (1 tour).
                case SpellId.SoulrenderRiposteCarmin:
                    def = new SpellDef
                    {
                        PACost = 2,
                        Shape = TargetingShape.SingleTile,
                        Filter = TargetingFilter.Self,
                        RangeMin = 0,
                        RangeMax = 0,
                        DamageAmount = 0,
                        HGCostMandatory = 0,
                        HGCostMaxOptional = 0,
                        OncePerMatchBit = OncePerMatchBitNone,
                        IsOffensive = 0,
                    };
                    return true;

                // -------------------------------------------------------------
                // SOULRENDER 2.10.b
                // -------------------------------------------------------------

                // Marque de Carnage (2.10.b) : 2 PA, range 5, marque cible 3 tours.
                // Effet : tous les casts Soulrender sur cible marquee genere +1 HG bonus
                // (en plus du +1 normal Soulrender qui inflige). Gere dans damage loop.
                case SpellId.SoulrenderMarqueDeCarnage:
                    def = new SpellDef
                    {
                        PACost = 2,
                        Shape = TargetingShape.SingleTile,
                        Filter = TargetingFilter.Enemy,
                        RangeMin = 1,
                        RangeMax = 5,
                        DamageAmount = 0,
                        HGCostMandatory = 0,
                        HGCostMaxOptional = 0,
                        OncePerMatchBit = OncePerMatchBitNone,
                        IsOffensive = 0,
                    };
                    return true;

                // Empoignade (2.10.b) : 3 PA, range 3, pull cible adjacent + AntiTeleport 1 tour.
                // Pas de dgts. Si pas de case adjacente libre : no-op silencieux (rare).
                case SpellId.SoulrenderEmpoignade:
                    def = new SpellDef
                    {
                        PACost = 3,
                        Shape = TargetingShape.SingleTile,
                        Filter = TargetingFilter.Enemy,
                        RangeMin = 1,
                        RangeMax = 3,
                        DamageAmount = 0,
                        HGCostMandatory = 0,
                        HGCostMaxOptional = 0,
                        OncePerMatchBit = OncePerMatchBitNone,
                        IsOffensive = 0,
                    };
                    return true;

                // Peau de Fer (2.10.b) : 3 PA, self, applique ShieldActive 200 HP / 2 tours.
                // Pendant la duree, sorts melee du caster gagnent +30 dgts (cf damage loop).
                case SpellId.SoulrenderPeauDeFer:
                    def = new SpellDef
                    {
                        PACost = 3,
                        Shape = TargetingShape.SingleTile,
                        Filter = TargetingFilter.Self,
                        RangeMin = 0,
                        RangeMax = 0,
                        DamageAmount = 0,
                        HGCostMandatory = 0,
                        HGCostMaxOptional = 0,
                        OncePerMatchBit = OncePerMatchBitNone,
                        IsOffensive = 0,
                    };
                    return true;

                // Seve Vive (2.10.b) : 2 PA, self, heal 100 (+60 si 1 HG, +50 si DoT actif).
                // HGCostMaxOptional = 1 : player choisit 0 ou 1 HG depense.
                case SpellId.SoulrenderSeveVive:
                    def = new SpellDef
                    {
                        PACost = 2,
                        Shape = TargetingShape.SingleTile,
                        Filter = TargetingFilter.Self,
                        RangeMin = 0,
                        RangeMax = 0,
                        DamageAmount = 0,
                        HGCostMandatory = 0,
                        HGCostMaxOptional = 1,
                        OncePerMatchBit = OncePerMatchBitNone,
                        IsOffensive = 0,
                    };
                    return true;

                // Dernier Souffle (2.10.b) : 4 PA, self, HP < 30% obligatoire.
                // Heal 200 HP + 3 HG. 1 fois par match. Check HP% dans SpellSystem amont.
                case SpellId.SoulrenderDernierSouffle:
                    def = new SpellDef
                    {
                        PACost = 4,
                        Shape = TargetingShape.SingleTile,
                        Filter = TargetingFilter.Self,
                        RangeMin = 0,
                        RangeMax = 0,
                        DamageAmount = 0,
                        HGCostMandatory = 0,
                        HGCostMaxOptional = 0,
                        OncePerMatchBit = OncePerMatchBitDernierSouffle,
                        IsOffensive = 0,
                    };
                    return true;

                default:
                    def = default;
                    return false;
            }
        }
    }

    /// <summary>
    /// Helpers de stats EFFECTIVES, qui prennent en compte les statuses actifs.
    /// Point d'extension central pour les modificateurs (Rage Insatiable, et plus tard
    /// passif "Appel du Sang" en 2.11 : -1 PA si cible <70% HP).
    /// </summary>
    public static unsafe class EffectiveStats
    {
        public static int GetPACost(in SpellDef def, Combatant* caster)
        {
            int cost = def.PACost;
            if (StatusHelper.Has(caster, StatusKind.RageInsatiableActive))
            {
                cost += 1; // Bible V7.1 Rage Insatiable : sorts coutent +1 PA pendant 2 tours
            }
            return cost;
        }
    }
}
