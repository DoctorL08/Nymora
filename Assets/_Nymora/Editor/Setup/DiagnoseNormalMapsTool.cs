using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Nymora.Editor.Setup
{
    /// <summary>
    /// POLISH KYAMI — diagnostic RUNTIME (Play Mode) pour trancher : les sprites affichés portent-ils
    /// vraiment une texture secondaire « _NormalMap » ? Quel shader ? Quel sorting layer ?
    ///
    /// Interroge directement chaque SpriteRenderer actif via l'API runtime Sprite.GetSecondaryTextures
    /// — c'est la vérité du moteur, pas une lecture de .meta. Cible en priorité les combattants.
    ///
    /// Menu : Nymora > Setup > Polish Kyami > Diagnose Normal Maps (Play Mode).
    /// </summary>
    public static class DiagnoseNormalMapsTool
    {
        [MenuItem("Nymora/Setup/Polish Kyami/Diagnose Normal Maps (Play Mode)", priority = 71)]
        public static void Run()
        {
            if (!Application.isPlaying)
            {
                EditorUtility.DisplayDialog("Diagnose Normal Maps",
                    "Entre d'abord en Play Mode (un combat), puis relance ce menu.", "OK");
                return;
            }

            var renderers = Object.FindObjectsOfType<SpriteRenderer>(false);
            var sb = new StringBuilder();
            sb.AppendLine($"=== DIAGNOSE NORMAL MAPS — {renderers.Length} SpriteRenderer actifs ===");

            var buf = new List<UnityEngine.SecondarySpriteTexture>();
            int combatants = 0;

            foreach (var sr in renderers)
            {
                if (sr.sprite == null) continue;

                bool looksCombatant = sr.transform.root != null &&
                    (sr.transform.root.name.Contains("Combatant") || sr.transform.root.name.Contains("P0_") || sr.transform.root.name.Contains("P1_"));

                var sprite = sr.sprite;
                int secCount = sprite.GetSecondaryTextureCount();
                bool hasNormal = false;
                string secNames = "";
                if (secCount > 0)
                {
                    var arr = new UnityEngine.SecondarySpriteTexture[secCount];
                    sprite.GetSecondaryTextures(arr);
                    foreach (var s in arr)
                    {
                        secNames += s.name + " ";
                        if (s.name == "_NormalMap") hasNormal = true;
                    }
                }

                string shader = sr.sharedMaterial != null && sr.sharedMaterial.shader != null
                    ? sr.sharedMaterial.shader.name : "(null)";
                bool lit = shader.ToLowerInvariant().Contains("lit");
                string layer = SortingLayer.IDToName(sr.sortingLayerID);

                // Log détaillé pour les combattants (+ une poignée d'autres pour contexte).
                if (looksCombatant)
                {
                    combatants++;
                    sb.AppendLine(
                        $"[COMBATANT] {sr.transform.root.name} → sprite '{sprite.name}'\n" +
                        $"    secondaryTextures={secCount} [{secNames.Trim()}]  _NormalMap={(hasNormal ? "OUI" : "NON")}\n" +
                        $"    shader='{shader}'  lit={(lit ? "OUI" : "NON")}  layer='{layer}'");
                }
            }

            if (combatants == 0)
                sb.AppendLine("⚠ Aucun combattant trouvé (lance un combat avant le diagnostic).");

            // Un échantillon non-combattant pour comparaison (sol/map).
            foreach (var sr in renderers)
            {
                if (sr.sprite == null) continue;
                string n = sr.gameObject.name.ToLowerInvariant();
                if (!(n.Contains("map") || n.Contains("sol") || n.Contains("arene") || n.Contains("floor"))) continue;
                int secCount = sr.sprite.GetSecondaryTextureCount();
                bool hasNormal = false;
                if (secCount > 0)
                {
                    var arr = new UnityEngine.SecondarySpriteTexture[secCount];
                    sr.sprite.GetSecondaryTextures(arr);
                    foreach (var s in arr) if (s.name == "_NormalMap") hasNormal = true;
                }
                sb.AppendLine($"[MAP] {sr.gameObject.name} → '{sr.sprite.name}' _NormalMap={(hasNormal ? "OUI" : "NON")} layer='{SortingLayer.IDToName(sr.sortingLayerID)}'");
                break;
            }

            Debug.Log(sb.ToString());
            EditorUtility.DisplayDialog("Diagnose Normal Maps",
                "Résultat écrit dans la Console (détail par combattant).\n\n" +
                "Lis surtout les lignes [COMBATANT] : _NormalMap=OUI/NON, lit=OUI/NON, layer=?.\n" +
                "Copie-moi ces lignes.", "OK");
        }
    }
}
