namespace Quantum
{
    /// <summary>
    /// Niveaux de difficulte IA (Bloc E Phase 2).
    /// 2.16.a — Easy : random pick + skip signature + cap 2 casts + HGSpend=0.
    /// 2.16.b — Medium : greedy max-score + signature autorisee + HG optionnel + cap 8.
    /// Hard (futur) : planification multi-tour + utilisation Pacte de Sang/Rugissement
    ///                + kite si HP critique + ciblage adaptatif par classe.
    /// </summary>
    public enum AIDifficulty
    {
        Easy = 0,
        Medium = 1,
    }

    /// <summary>
    /// Constantes deterministes pour l'IA Nymora (Bloc E Phase 2).
    ///
    /// Toutes les valeurs ici sont entieres (int) et compilent en dur dans la sim. Si
    /// on a besoin d'un parametre runtime (difficulte selectionnable, etc.), il devra
    /// passer par RuntimeConfig + SimulationConfig — pas par cette classe.
    /// </summary>
    public static class AIConstants
    {
        // 2.16.c.iv — Difficulte courante mutable runtime.
        //
        // Etait const en 2.16.b ; passe en static field pour que le View (boutons
        // Easy/Medium de l'overlay MatchEnd) puisse switcher entre 2 matches sans
        // recompile. La valeur survit aux reloads de scene (static dans le meme
        // domain Unity) — Lorenzo peut donc Rejouer Easy -> Rejouer Medium et le
        // matche se relance avec la bonne IA.
        //
        // Determinisme : offline IA = 1 client = 1 valeur partagee tout le match.
        // OK. Phase 6 multiplayer devra pousser la difficulte via un Command
        // initial ou via RuntimeConfig.AIDifficulty pour que tous les clients voient
        // la meme valeur (sinon desync).
        public static AIDifficulty CurrentDifficulty = AIDifficulty.Medium;


        // Phase 2 : P1 est le bot, Lorenzo joue P0. Hardcoded jusqu'a Phase 5/6 ou un
        // RuntimePlayer/RoomConfig permettra de configurer humain vs bot par slot.
        public const int BotPlayerIndex = 1;

        // Delai final apres la derniere action avant de declencher EndTurn. 30 ticks
        // = 0.5s a 60Hz. Permet au joueur de voir le dernier effet avant que le tour
        // passe.
        public const int BotEndTurnDelayTicks = 30;

        // 2.16.c.v — Intervalle entre actions du bot (move puis casts). 60 ticks = 1s
        // a 60Hz. Espace les casts dans le temps pour que le joueur voie chaque effet
        // (-dgts, push, tp...) sequentiellement, comme face a un vrai joueur.
        //
        // Calendrier d'un tour bot avec ce pacing :
        //   tick 0    : move
        //   tick 60   : cast 1
        //   tick 120  : cast 2
        //   ...
        //   tick (1+N)*60 + BotEndTurnDelayTicks : EndTurn (N = nb casts effectues)
        // Duree typique : Easy ~3s (move + 2 casts + delai), Medium ~5-9s.
        public const int ActionIntervalTicks = 60;

        // 2.16.a.iii — cap dur des casts par tour pour l'IA Easy.
        // Constate empiriquement : meme avec random pick + skip signature, un bot
        // Soulrender qui exploite le passif Appel du Sang (-1 PA cost <70% HP)
        // peut chainer 3-4 Tranche-Ame = 660-880 dgts/tour. Cap a 2 -> ~440 dgts
        // top, le joueur a 4-5 tours pour reagir.
        public const int MaxCastsPerTurnEasy = 2;

        // 2.16.b — pas de cap effectif pour Medium : le PA limite naturellement
        // (~3-5 casts/tour selon discount Appel du Sang). 8 = filet de securite
        // anti-infini si bug de cost 0.
        public const int MaxCastsPerTurnMedium = 8;

        // 2.16.b — DECKS PAR DIFFICULTE.
        //
        // Lorenzo : "les IA Easy/Medium vont devoiler les metas — il faut leur
        // donner des decks vraiment nuls". Donc chaque difficulte a un sous-ensemble
        // FIXE des 16 sorts de la classe. L'IA n'enumere que les sorts de son deck
        // (pas la plage complete SpellId 10-25). Effet :
        //   - Joueur regarde un combat Easy -> voit "OuvrePlaie + Curee + 4 utility",
        //     pas de TrancheAme ni signature. Aucune indication que TrancheAme est OP.
        //   - Joueur regarde un combat Medium -> voit "OuvrePlaie + ChargeBrutale +
        //     Curee + utility". Toujours pas de TrancheAme ni Pacte de Sang ni
        //     signature. Le vrai meta deck reste cache pour Hard / PvP.
        //
        // Decks Soulrender (Bible V7.1) — 6 sorts par deck (signature compte a part) :

        // EASY DECK : faible. 2 offensifs (les moins puissants) + 4 utility/self.
        // L'IA ne peut PAS one-shot puisqu'aucun sort >150 dgts base. Random pick
        // pioche souvent dans les 4 utility -> beaucoup de tours "loose" niveau dmg
        // (mais ces sorts sont filtres par IsOffensive donc cast=0 sur eux ; le bot
        // skip silencieusement et utilise OuvrePlaie/Curee). Visuellement le deck
        // affiche est un "deck de PvE / debutant" peu copiable.
        public static readonly SpellId[] SoulrenderEasyDeck =
        {
            SpellId.SoulrenderOuvrePlaie,        // 110 dgts base (230 avec 1 HG, mais Easy HGSpend=0)
            SpellId.SoulrenderCuree,             // 150 dgts, 2 HG mand
            SpellId.SoulrenderCauterisation,     // utility self (retire DoT, no-op tant qu'on en a pas)
            SpellId.SoulrenderSeveVive,          // heal 100 self
            SpellId.SoulrenderRiposteCarmin,     // niche reflect melee 100
            SpellId.SoulrenderMarqueDeCarnage,   // utility, marque +1 HG sur Soulrender (synergie faible solo)
        };

        // MEDIUM DECK : modere. 3 offensifs (sans le top-tier TrancheAme) + 3 utility.
        // Le bot greedy max-score pickera ChargeBrutale ou OuvrePlaie+HG, deal du dgts
        // mais moins violent qu'avec TrancheAme (220) ou signature (320). Sufficient
        // pour challenger le joueur sans reveler la combo meta.
        public static readonly SpellId[] SoulrenderMediumDeck =
        {
            SpellId.SoulrenderOuvrePlaie,        // 110-230 dgts (HG optionnel autorise en Medium)
            SpellId.SoulrenderChargeBrutale,     // 180 dgts + dash + Vapeur Carmin
            SpellId.SoulrenderCuree,             // 150 dgts + kill chain
            SpellId.SoulrenderEmpoignade,        // pull gap-close (utility, IsOffensive=0 -> bot le skip)
            SpellId.SoulrenderRugissement,       // AoE -1 PM (utility)
            SpellId.SoulrenderPeauDeFer,         // shield 200/2t (defensive)
        };

        // HARD DECK (futur) : META. Sera utilise quand IA Hard arrive.
        //   { TrancheAme, OuvrePlaie, ChargeBrutale, DetonationSanglante,
        //     PacteDeSang, PeauDeFer } + signature AmeLaceree.
    }
}
