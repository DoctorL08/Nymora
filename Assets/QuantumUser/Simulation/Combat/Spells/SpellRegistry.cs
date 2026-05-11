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

        // 2.10.c constants
        public const int ChargeBrutaleRange           = 5;
        public const int ChargeBrutaleDamage          = 180;
        public const int VapeurCarminTurns            = 1;
        public const int DetonationBaseDamage         = 60;
        public const int DetonationDamagePerHG        = 40;
        public const int SangCoaguleTurns             = 2;
        public const int CureeDamage                  = 150;
        public const int CureeBonusPANextTurn         = 4;
        public const int CureeMissSelfDamage          = 60;
        public const int CauterisationHealMin         = 60;   // toujours applique (min)
        public const int CauterisationHealPerDoT      = 60;   // chaque DoT retire ajoute 60
        public const int CauterisationHealMax         = 180;  // cap 3 DoTs retires
        public const int TrancheAmeKillRecul          = 2;    // 2 cases de recul si kill

        // 2.11 — Signature Ame Laceree + Passif Appel du Sang.
        public const int AmeLaceeDamage               = 320;  // dgts de base
        public const int AmeLaceeHealPercentOfPassed  = 50;   // heal caster = X% des dgts qui passent (post-shield)
        public const int AmeLaceeCooldownTurns        = 4;    // 4 tours de cooldown apres usage
        public const int AppelDuSangPalierMarquage    = 70;   // cible <70% HP -> -1 PA cost
        public const int AppelDuSangPalierRageOuverte = 40;   // cible <40% HP -> +1 PM Soulrender + 50% shield bypass melee
        public const int AppelDuSangPalierLeCri       = 20;   // cible <20% HP -> Sang Coagule croix 5 autour caster
        public const int AppelDuSangShieldBypassPct   = 50;   // 50% des dgts mêlée ignorent le shield si target <40%

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

                // -------------------------------------------------------------
                // SOULRENDER 2.10.c
                // -------------------------------------------------------------

                // Charge Brutale (2.10.c) : 4 PA, ligne range 5. Fonce en ligne droite jusqu'a la
                // 1ere unite ou case bloquante. Inflige 180 dgts a la cible touchee. Toutes les
                // cases foulees deviennent Vapeur Carmin 1 tour. Gestion specifique dans
                // ApplySpellSpecificEffects (mvt + dgts + terrain).
                // Shape Line existe deja (TargetingResolver) mais on gere l'effet specifique
                // ici car on a besoin du chemin precis + du mouvement caster.
                case SpellId.SoulrenderChargeBrutale:
                    def = new SpellDef
                    {
                        PACost = 4,
                        Shape = TargetingShape.SingleTile, // on gere la ligne nous-memes
                        Filter = TargetingFilter.AnyTile,  // peut viser une case vide ou ennemi
                        RangeMin = 1,
                        RangeMax = ChargeBrutaleRange,
                        DamageAmount = 0, // applique manuellement dans le branche specifique
                        HGCostMandatory = 0,
                        HGCostMaxOptional = 0,
                        OncePerMatchBit = OncePerMatchBitNone,
                        IsOffensive = 1,
                    };
                    return true;

                // Detonation Sanglante (2.10.c) : 4 PA, range 4, AoE croix 3.
                // Damage de base 60 + 40 par HG total consomme (mandatory 2 + optional max 3 = 5).
                // Sang Coagule pose sous la case centre 2 tours.
                // ATTENTION 2.11 : si on consomme 5 HG ici, ca interdit Ame Laceree et reset son cooldown.
                case SpellId.SoulrenderDetonationSanglante:
                    def = new SpellDef
                    {
                        PACost = 4,
                        Shape = TargetingShape.CrossSmall, // croix 3 cases (centre + 4 cardinales)
                        Filter = TargetingFilter.AnyTile,
                        RangeMin = 1,
                        RangeMax = 4,
                        DamageAmount = 0, // calcule dynamiquement (60 + 40*totalHG)
                        HGCostMandatory = 2,
                        HGCostMaxOptional = 3,
                        OncePerMatchBit = OncePerMatchBitNone,
                        IsOffensive = 1,
                    };
                    return true;

                // Curee (2.10.c) : 2 PA, range 2, 2 HG (mandatory), 150 dgts.
                // Kill chain : heal 50% HP manquants + 4 PA next turn.
                // Miss (target encore vivante) : caster prend 60 dgts self.
                case SpellId.SoulrenderCuree:
                    def = new SpellDef
                    {
                        PACost = 2,
                        Shape = TargetingShape.SingleTile,
                        Filter = TargetingFilter.Enemy,
                        RangeMin = 1,
                        RangeMax = 2,
                        DamageAmount = CureeDamage,
                        HGCostMandatory = 2,
                        HGCostMaxOptional = 0,
                        OncePerMatchBit = OncePerMatchBitNone,
                        IsOffensive = 1,
                    };
                    return true;

                // Cauterisation (2.10.c, stub) : 2 PA, self, retire tous DoT + heal 60 par DoT
                // retire (min 60 toujours, max 180 cap 3 DoTs). Pour 2.10.c : aucun DoT actuel,
                // donc heal = 60 (min). Structure prete pour Phase 3 Necram.
                case SpellId.SoulrenderCauterisation:
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
                // SOULRENDER 2.11 — SIGNATURE
                // -------------------------------------------------------------

                // Ame Laceree (2.11) : signature Soulrender.
                // 2 PA, range 1 (melee), 5 HG (consomme toute la jauge), 320 dgts.
                // Le Soulrender heal 50% des dgts qui passent (post-shield).
                // Si kill : Sang Coagule en croix 5 cases (caster centre + 4 cardinales).
                // Cooldown 4 tours apres usage. Re-castable si HG remonte a 5.
                // Slot separe du deck de 6 (touche dediee en View).
                case SpellId.SoulrenderAmeLaceree:
                    def = new SpellDef
                    {
                        PACost = 2,
                        Shape = TargetingShape.SingleTile,
                        Filter = TargetingFilter.Enemy,
                        RangeMin = 1,
                        RangeMax = 1,
                        DamageAmount = AmeLaceeDamage,
                        HGCostMandatory = 5, // 5/5 HG obligatoire
                        HGCostMaxOptional = 0,
                        OncePerMatchBit = OncePerMatchBitNone,
                        IsOffensive = 1,
                    };
                    return true;

                default:
                    def = default;
                    return false;
            }
        }
    }

    /// <summary>
    /// Helpers de stats EFFECTIVES, qui prennent en compte les statuses actifs et le passif.
    /// Point d'extension central pour les modificateurs.
    ///
    /// 2.10.a : Rage Insatiable Active +1 PA cost.
    /// 2.11 : Passif Appel du Sang -1 PA si cible visee <70% HP (caster Soulrender uniquement).
    /// </summary>
    public static unsafe class EffectiveStats
    {
        /// <summary>
        /// Cout PA effectif pour le caster sur ce sort.
        ///
        /// <paramref name="targetHPRatio"/> : ratio HP de la cible visee (HP * 100 / MaxHP),
        /// ou 100 si pas de cible ennemie (sorts self/AnyTile sans ennemi). Sert au passif
        /// Appel du Sang : -1 PA si caster Soulrender et target < 70% HP. Min 1 PA.
        /// </summary>
        public static int GetPACost(in SpellDef def, Combatant* caster, int targetHPRatio)
        {
            int cost = def.PACost;
            if (StatusHelper.Has(caster, StatusKind.RageInsatiableActive))
            {
                cost += 1; // Rage Insatiable : sorts coutent +1 PA pendant 2 tours
            }
            // Passif Appel du Sang : caster Soulrender + cible <70% HP -> -1 PA (min 1).
            if (caster->Class == NymoraClass.Soulrender
                && targetHPRatio < SpellRegistry.AppelDuSangPalierMarquage)
            {
                cost -= 1;
                if (cost < 1) cost = 1;
            }
            return cost;
        }

        /// <summary>
        /// Calcule le ratio HP de la cible ennemie a (targetX, targetY), ou 100 si pas d'ennemi.
        /// Utilise par GetPACost pour le passif Appel du Sang.
        /// </summary>
        public static int ResolveTargetHPRatio(Frame f, int targetX, int targetY, int casterPlayerIndex)
        {
            EntityRef occ = GridHelpers.GetOccupant(f, targetX, targetY);
            if (occ == EntityRef.None) return 100;
            if (!f.Unsafe.TryGetPointer<Combatant>(occ, out Combatant* c)) return 100;
            if (c->PlayerIndex == casterPlayerIndex) return 100; // pas un ennemi
            if (c->MaxHP <= 0) return 100;
            return c->HP * 100 / c->MaxHP;
        }
    }
}
