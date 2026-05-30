using System.Collections.Generic;

namespace Nymora.Core.Data
{
    /// <summary>
    /// Lobby pré-combat (B1) — Bridge static cross-scène pour transmettre au lobby de combat
    /// la LISTE des decks enregistrés de la classe jouée + le MMR local + l'id du deck par défaut
    /// (celui sélectionné dans le deck builder avant le lancement).
    ///
    /// Pattern miroir de <see cref="DeckBridge"/> / <see cref="MatchBridge"/> : placé dans
    /// Nymora.Core.Data (asmdef sans référence) pour être accessible côté Hub (set) ET côté
    /// Combat (read). Les champs sont static donc survivent au LoadScene. Clear() après consommation.
    ///
    /// Important : Nymora.Core n'a AUCUNE référence asmdef → impossible d'exposer DeckDto
    /// (Nymora.Network.Backend) ici. On expose une struct neutre <see cref="PreCombatDeckInfo"/>
    /// que le hub remplit depuis DeckDto.
    ///
    /// VIEW/NETWORK ONLY : jamais lu par la simulation Quantum → aucun impact déterministe,
    /// pas de bump CombatRulesVersion. Le deck final choisi dans le lobby repart par le canal
    /// existant (RuntimePlayer.SpellIdValues) au moment du Game.AddPlayer.
    /// </summary>
    public static class PreCombatBridge
    {
        /// <summary>Decks enregistrés de la classe locale (ceux affichés dans le picker du lobby).</summary>
        public static IReadOnlyList<PreCombatDeckInfo> AvailableDecks { get; private set; }

        /// <summary>
        /// Id du deck par défaut = deck sélectionné dans le deck builder avant le lancement
        /// (cf <see cref="DeckBridge"/>). Présélectionné dans le picker ; fallback si aucun
        /// choix lobby (timeout sans sélection).
        /// </summary>
        public static string DefaultDeckId { get; private set; }

        /// <summary>MMR du joueur local (lu côté hub depuis /profile/me). Diffusé en P2P à l'adversaire.</summary>
        public static int LocalMmr { get; private set; }

        public static bool HasData => AvailableDecks != null && AvailableDecks.Count > 0;

        public static void Set(IReadOnlyList<PreCombatDeckInfo> availableDecks, string defaultDeckId, int localMmr)
        {
            AvailableDecks = availableDecks;
            DefaultDeckId = defaultDeckId;
            LocalMmr = localMmr;
        }

        public static void Clear()
        {
            AvailableDecks = null;
            DefaultDeckId = null;
            LocalMmr = 0;
        }
    }

    /// <summary>
    /// Représentation neutre (sans dépendance Network) d'un deck enregistré, pour le picker du
    /// lobby pré-combat. Miroir minimal de DeckDto (Nymora.Network.Backend).
    /// </summary>
    public sealed class PreCombatDeckInfo
    {
        public string Id;
        public string ClassId;
        public string Name;
        /// <summary>Exactement 6 SpellIdTech (snake_case), comme DeckDto.spellIds.</summary>
        public string[] SpellIds;

        public PreCombatDeckInfo(string id, string classId, string name, string[] spellIds)
        {
            Id = id;
            ClassId = classId;
            Name = name;
            SpellIds = spellIds;
        }
    }
}
