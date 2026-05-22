using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Nymora.Editor.Setup
{
    /// <summary>
    /// Brique 6.1 — Clone la scene 33_CombatCasual.unity vers 40_CombatRanked1v1.unity
    /// pour servir de base au combat CLASSE (ranked) 1v1.
    ///
    /// Le ranked 1v1 reutilise exactement le combat PvP online de la casual ; la seule
    /// difference (impact MMR) se gere VIEW-side (MatchBridge.IsRanked) + backend (6.3),
    /// PAS dans la simulation Quantum. Donc clone direct, zero bump CombatRulesVersion.
    ///
    /// NOTE QuantumMap : depuis le fix du 22 mai (AutoLoadSceneFromMap=0 dans
    /// QuantumDefaultConfigs), Quantum n'auto-charge plus la scene de la map en additif.
    /// La scene ranked peut donc REUTILISER la meme QuantumMap que la casual sans risque
    /// de scene fantome (la grille est procedurale). Pas de QuantumMap dediee necessaire.
    ///
    /// Apres le clone, la scene 40_CombatRanked1v1 contient encore un CombatBootstrapCasual
    /// dont la garde ExpectedSceneName="33_CombatCasual" le fait NO-OP dans la scene ranked.
    /// => en Play direct, la scene se charge proprement mais ne demarre pas de combat.
    /// Le bootstrap ranked (matchmaking -> room -> Quantum start) sera branche en BRIQUE 6.2.
    ///
    /// Menu : Nymora > Setup > Clone CombatCasual to Ranked1v1 Scene.
    /// </summary>
    public static class CloneCasualToRanked1v1SceneTool
    {
        private const string SRC_PATH = "Assets/_Nymora/Scenes/33_CombatCasual.unity";
        private const string DST_PATH = "Assets/_Nymora/Scenes/40_CombatRanked1v1.unity";

        [MenuItem("Nymora/Setup/Clone CombatCasual to Ranked1v1 Scene", priority = 21)]
        private static void Run()
        {
            if (!File.Exists(SRC_PATH))
            {
                Debug.LogError($"[Nymora.6.1] Source scene introuvable : {SRC_PATH}");
                return;
            }

            bool confirm = EditorUtility.DisplayDialog(
                "Clone CombatCasual → Ranked1v1",
                "Cette operation :\n" +
                "  1. Backup 40_CombatRanked1v1.unity actuel (si existe) en .BACKUP_<timestamp>.unity\n" +
                "  2. Copie 33_CombatCasual.unity vers 40_CombatRanked1v1.unity\n" +
                "  3. Ajoute la scene aux Build Settings\n\n" +
                "Le bootstrap ranked (matchmaking + Quantum online) sera branche en brique 6.2.\n\n" +
                "Continuer ?",
                "Cloner", "Annuler");
            if (!confirm) return;

            // 1. Backup l'existant si present
            if (File.Exists(DST_PATH))
            {
                string ts = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string backupPath = $"Assets/_Nymora/Scenes/40_CombatRanked1v1_BACKUP_{ts}.unity";
                string moveErr = AssetDatabase.MoveAsset(DST_PATH, backupPath);
                if (!string.IsNullOrEmpty(moveErr))
                {
                    Debug.LogError($"[Nymora.6.1] MoveAsset (backup) echec : {moveErr}");
                    return;
                }
                Debug.Log($"[Nymora.6.1] Backup 40_CombatRanked1v1.unity → {backupPath}");
            }

            // 2. Clone via CopyAsset (preserve les meta GUID, pas de casse de references)
            if (!AssetDatabase.CopyAsset(SRC_PATH, DST_PATH))
            {
                Debug.LogError($"[Nymora.6.1] CopyAsset echec : {SRC_PATH} → {DST_PATH}");
                return;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[Nymora.6.1] 33_CombatCasual.unity cloné vers {DST_PATH}.");

            // 3. Ajout aux build settings
            EnsureNymoraScenesInBuildTool_PublicRun();

            EditorUtility.DisplayDialog(
                "Clone OK",
                "40_CombatRanked1v1.unity creee depuis 33_CombatCasual.unity.\n\n" +
                "EN 6.1 : la scene se charge proprement en Play direct mais ne demarre pas\n" +
                "de combat (le CombatBootstrapCasual cloné no-op hors de sa scene).\n\n" +
                "PROCHAINE BRIQUE (6.2) : matchmaking backend + bootstrap ranked qui\n" +
                "appaire 2 joueurs et lance cette scene.",
                "OK");
        }

        // Trampoline reflection vers EnsureNymoraScenesInBuildTool.Run (private static),
        // meme pattern que CloneCombatIAToCasualSceneTool (4.14.b).
        private static void EnsureNymoraScenesInBuildTool_PublicRun()
        {
            try
            {
                var t = typeof(EnsureNymoraScenesInBuildTool);
                var m = t.GetMethod("Run", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
                if (m != null) m.Invoke(null, null);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Nymora.6.1] Auto-ensure-in-build skipped : {ex.Message}. Lance manuellement Nymora > Setup > Ensure Nymora Scenes In Build.");
            }
        }
    }
}
