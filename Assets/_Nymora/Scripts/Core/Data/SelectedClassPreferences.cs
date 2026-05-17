using UnityEngine;

namespace Nymora.Core.Data
{
    /// <summary>
    /// Brique 5.3.g.bis — Persistance locale de la classe selectionnee par le joueur
    /// (utilise pour l'avatar hub + le combat). PlayerPrefs local pour MVP, sera
    /// remonte au backend (User.selectedClassId) en Phase 6 ranked.
    /// </summary>
    public static class SelectedClassPreferences
    {
        private const string PrefKey = "Nymora.SelectedClassId";
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
    }
}
