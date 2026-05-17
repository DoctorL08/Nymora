using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Nymora.Editor.Setup
{
    /// <summary>
    /// Brique 4.14.b — Clone la scene 30_CombatIA.unity vers 33_CombatCasual.unity
    /// pour servir de base au PvP casual online.
    ///
    /// Le stub actuel 33_CombatCasual.unity (3 boutons placeholder) est BACKUPED
    /// dans 33_CombatCasual_BACKUP_yyyymmdd_hhmmss.unity avant overwrite, pour que
    /// Lorenzo puisse revenir en arriere si necessaire.
    ///
    /// Apres le clone, Lorenzo doit :
    ///   1. Ouvrir 33_CombatCasual.unity
    ///   2. Verifier le GameObject QuantumRunnerLocalDebug -> Run "Multiplayer" sera
    ///      ajoute en brique 4.14.c (CombatBootstrapCasual) qui remplacera ce composant
    ///      par un Photon Realtime room join + Quantum online start.
    ///   3. Sur le RuntimeConfig asset utilise par 30_CombatIA : cocher IsBotMatch=true.
    ///      Default false = la nouvelle scene 33_CombatCasual est automatiquement PvP.
    ///   4. Lancer Tools > Quantum > CodeGen Compile pour regenerer le CombatState
    ///      (nouveau field Bool IsBotMatch).
    ///
    /// Menu : Nymora > Setup > Clone CombatIA to CombatCasual Scene.
    /// </summary>
    public static class CloneCombatIAToCasualSceneTool
    {
        private const string SRC_PATH = "Assets/_Nymora/Scenes/30_CombatIA.unity";
        private const string DST_PATH = "Assets/_Nymora/Scenes/33_CombatCasual.unity";

        [MenuItem("Nymora/Setup/Clone CombatIA to CombatCasual Scene", priority = 20)]
        private static void Run()
        {
            if (!File.Exists(SRC_PATH))
            {
                Debug.LogError($"[Nymora.4.14.b] Source scene introuvable : {SRC_PATH}");
                return;
            }

            bool confirm = EditorUtility.DisplayDialog(
                "Clone CombatIA → CombatCasual",
                "Cette operation :\n" +
                "  1. Backup 33_CombatCasual.unity actuel (si existe) en .BACKUP_<timestamp>.unity\n" +
                "  2. Copie 30_CombatIA.unity vers 33_CombatCasual.unity\n\n" +
                "Apres le clone, ouvre 33_CombatCasual et garde TOUT pour l'instant.\n" +
                "Le cleanup (retrait QuantumRunnerLocalDebug, ajout CombatBootstrapCasual) sera fait en brique 4.14.c.\n\n" +
                "Continuer ?",
                "Cloner", "Annuler");
            if (!confirm) return;

            // 1. Backup le stub current si existe
            if (File.Exists(DST_PATH))
            {
                string ts = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string backupPath = $"Assets/_Nymora/Scenes/33_CombatCasual_BACKUP_{ts}.unity";
                string moveErr = AssetDatabase.MoveAsset(DST_PATH, backupPath);
                if (!string.IsNullOrEmpty(moveErr))
                {
                    Debug.LogError($"[Nymora.4.14.b] MoveAsset (backup) echec : {moveErr}");
                    return;
                }
                Debug.Log($"[Nymora.4.14.b] Backup 33_CombatCasual.unity → {backupPath}");
            }

            // 2. Clone via CopyAsset (preserve les meta GUID donc pas de casse references)
            if (!AssetDatabase.CopyAsset(SRC_PATH, DST_PATH))
            {
                Debug.LogError($"[Nymora.4.14.b] CopyAsset echec : {SRC_PATH} → {DST_PATH}");
                return;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[Nymora.4.14.b] 30_CombatIA.unity cloné vers {DST_PATH}. " +
                      $"Ouvre la scene dans l'editor et garde-la telle quelle pour brique 4.14.c.");

            // Lance auto le tool EnsureScenesInBuild pour que 33_CombatCasual soit dans les build settings
            EnsureNymoraScenesInBuildTool_PublicRun();

            EditorUtility.DisplayDialog(
                "Clone OK",
                $"33_CombatCasual.unity creee depuis 30_CombatIA.unity.\n\n" +
                "PROCHAINES ETAPES (manip Lorenzo) :\n" +
                "  1. Lance Tools > Quantum > CodeGen Compile (regen Bool IsBotMatch)\n" +
                "  2. Trouve le RuntimeConfig asset utilise par 30_CombatIA -> coche IsBotMatch=true\n" +
                "  3. Test 30_CombatIA : combat avec bot doit fonctionner comme avant\n" +
                "  4. Brique 4.14.c : Claude ajoutera CombatBootstrapCasual + Photon Realtime online start",
                "OK");
        }

        // Trampoline pour ne pas dupliquer le code d'ajout aux build settings.
        // EnsureNymoraScenesInBuildTool.Run est private, on appelle via reflection.
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
                Debug.LogWarning($"[Nymora.4.14.b] Auto-ensure-in-build skipped : {ex.Message}. Lance manuellement Nymora > Setup > Ensure Nymora Scenes In Build.");
            }
        }
    }
}
