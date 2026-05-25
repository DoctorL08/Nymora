using System;
using Nymora.Hub;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Nymora.Editor.Setup
{
    /// <summary>
    /// Nettoyage UI legacy (post-chantier menu Échap M0→M8). Le nouveau menu (HubMenuShell)
    /// remplace tous les points d'entrée épars du hub : on masque les 8 boutons legacy
    /// (Arène / Deck / Profil / Amis / Clan / Quêtes / Battle Pass / Boutique).
    ///
    /// IMPORTANT : on masque UNIQUEMENT les GameObjects portant un composant *Button (points
    /// d'entrée). Les *Panel (logique métier) restent intacts — le menu réutilise leurs
    /// Instance (HubArenaPanel.StartTraining, HubDeckBuilderPanel, HubFriendsPanel.IsFriendOnline,
    /// HubWalletWidget, etc.). Le chat, le wallet et les avatars ne sont PAS touchés.
    ///
    /// Réversible (Restore) + idempotent. Trouve aussi les objets déjà inactifs.
    /// Ouvre la scène 10_CommunityHub AVANT de lancer, puis sauvegarde (Ctrl+S).
    /// </summary>
    public static class HubLegacyUiCleanupTool
    {
        private static readonly Type[] LegacyButtonTypes =
        {
            typeof(HubArenaButton),
            typeof(HubDeckBuilderButton),
            typeof(HubProfileButton),
            typeof(HubFriendsButton),
            typeof(HubClanButton),
            typeof(HubQuestsButton),
            typeof(HubBattlePassButton),
            typeof(HubShopButton),
        };

        [MenuItem("Nymora/Setup/UI Menu/Hide Legacy Hub Buttons")]
        public static void HideLegacyButtons() => SetLegacyButtonsActive(false);

        [MenuItem("Nymora/Setup/UI Menu/Restore Legacy Hub Buttons")]
        public static void RestoreLegacyButtons() => SetLegacyButtonsActive(true);

        private static void SetLegacyButtonsActive(bool active)
        {
            int changed = 0, total = 0;
            foreach (var type in LegacyButtonTypes)
            {
                var comps = UnityEngine.Object.FindObjectsByType(type, FindObjectsInactive.Include, FindObjectsSortMode.None);
                foreach (var obj in comps)
                {
                    if (obj is not MonoBehaviour mb) continue;
                    total++;
                    var go = mb.gameObject;
                    if (go.activeSelf == active) continue;
                    Undo.RecordObject(go, active ? "Restore legacy hub button" : "Hide legacy hub button");
                    go.SetActive(active);
                    EditorUtility.SetDirty(go);
                    changed++;
                }
            }

            if (!Application.isPlaying)
                EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

            Debug.Log($"[HubLegacyUiCleanup] {(active ? "Restauré" : "Masqué")} {changed}/{total} bouton(s) legacy " +
                      $"({LegacyButtonTypes.Length} types ciblés). Pense à sauvegarder la scène (Ctrl+S).");
        }
    }
}
