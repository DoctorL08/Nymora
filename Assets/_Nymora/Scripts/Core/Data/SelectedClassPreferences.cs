using UnityEngine;

namespace Nymora.Core.Data
{
    /// <summary>
    /// Brique 5.3.g.bis — Persistance locale des preferences hub du joueur (classe
    /// selectionnee, dernier deck en edition par classe). Utilise par l'avatar hub
    /// et le Deck Builder pour restaurer l'etat post-deco/reco.
    ///
    /// PlayerPrefs local pour MVP, sera remonte au backend (User.selectedClassId +
    /// User.lastEditedDeckIds[]) en Phase 6 ranked.
    ///
    /// Extension 5.4 (18 mai 2026) : ajout last-edited-deck-per-class pour fix
    /// "Deck Builder revient toujours sur Soulrender apres reco" — l'avatar hub
    /// lisait deja Get() mais le DeckBuilder hardcodait "Soulrender" en field init.
    /// </summary>
    public static class SelectedClassPreferences
    {
        private const string PrefKey = "Nymora.SelectedClassId";
        private const string LastEditedDeckPrefPrefix = "Nymora.LastEditedDeck.";
        private const string DefaultClassId = "Soulrender";

        public static string Get()
        {
            return PlayerPrefs.GetString(PrefKey, DefaultClassId);
        }

        public static void Set(string classId)
        {
            if (string.IsNullOrEmpty(classId)) return;
            PlayerPrefs.SetString(PrefKey, classId);
            PlayerPrefs.Save();
        }

        /// <summary>
        /// Retourne l'ID du dernier deck edite dans le Deck Builder pour la classe
        /// donnee. Empty string si aucun deck memorise (premier launch ou Clear).
        /// </summary>
        public static string GetLastEditedDeckId(string classId)
        {
            if (string.IsNullOrEmpty(classId)) return string.Empty;
            return PlayerPrefs.GetString(LastEditedDeckPrefPrefix + classId, string.Empty);
        }

        public static void SetLastEditedDeckId(string classId, string deckId)
        {
            if (string.IsNullOrEmpty(classId) || string.IsNullOrEmpty(deckId)) return;
            PlayerPrefs.SetString(LastEditedDeckPrefPrefix + classId, deckId);
            PlayerPrefs.Save();
        }

        public static void ClearLastEditedDeckId(string classId)
        {
            if (string.IsNullOrEmpty(classId)) return;
            PlayerPrefs.DeleteKey(LastEditedDeckPrefPrefix + classId);
            PlayerPrefs.Save();
        }
    }
}
