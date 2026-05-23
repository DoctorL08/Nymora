using System.Text;
using Nymora.Hub;
using UnityEngine;
using UnityEditor;

namespace Nymora.Editor.Setup
{
    /// <summary>
    /// Diagnostic READ-ONLY du tri iso du hub (23 mai). Ne modifie RIEN.
    /// Logue, pour chaque sprite "torche/lampadaire" : a-t-il IsoDepthSort ? sur quel sorting
    /// layer ? quel sortingOrder ? ou est son DepthPivot (world Y) ? a quelle case grille il
    /// inverse-projette (gx, gy) et donc quel gx+gy (= ce qui pilote l'order via 100-(gx+gy)).
    ///
    /// But : reperer (a) un lampadaire reste sur "Default" alors que le perso est sur
    /// "Personnages" -> perso toujours devant, et (b) un pivot mal place -> order aberrant.
    ///
    /// Detecte aussi les SpriteRenderer dont le nom evoque une torche/lampe mais SANS
    /// IsoDepthSort (ceux-la ne montent jamais sur "Personnages").
    ///
    /// Menu : Nymora > Setup > Diagnose Hub Depth Sort.
    /// </summary>
    public static class HubDepthSortDiagnosticTool
    {
        [MenuItem("Nymora/Setup/Diagnose Hub Depth Sort", priority = 70)]
        private static void Diagnose()
        {
            var grid = Object.FindFirstObjectByType<HubGridRenderer>();
            var sb = new StringBuilder();
            sb.AppendLine("=== DIAGNOSTIC TRI ISO HUB ===");
            sb.AppendLine(grid != null
                ? $"Grille: TileW={grid.TileWorldWidth} TileH={grid.TileWorldHeight} center={grid.CenterOffset}"
                : "AUCUN HubGridRenderer trouve (gx/gy non calculables).");
            sb.AppendLine("Perso (HubAvatar) : base order 100, order = 100-(gx+gy). " +
                          "Layer cible 'Personnages'. (Le perso n'existe qu'en Play Mode.)");
            sb.AppendLine("--- IsoDepthSort presents ---");

            int count = 0;
            foreach (var iso in Object.FindObjectsByType<IsoDepthSort>(FindObjectsSortMode.None))
            {
                count++;
                var sr = iso.GetComponent<SpriteRenderer>();
                var so = new SerializedObject(iso);
                var pivotProp = so.FindProperty("_depthPivot");
                var pivot = pivotProp != null ? pivotProp.objectReferenceValue as Transform : null;
                Vector3 pPos = pivot != null ? pivot.position : iso.transform.position;

                string layer = sr != null ? sr.sortingLayerName : "<no SR>";
                int order = sr != null ? sr.sortingOrder : 0;
                string sprite = sr != null && sr.sprite != null ? sr.sprite.name : "<none>";

                string gridInfo = "(grille absente)";
                if (grid != null)
                {
                    var (gx, gy) = IsoProjection.WorldToGrid(pPos, grid.TileWorldWidth, grid.TileWorldHeight, grid.CenterOffset);
                    gridInfo = $"gx={gx} gy={gy} gx+gy={gx + gy} -> orderTheorique={100 - (gx + gy)}";
                }

                sb.AppendLine($"[{iso.gameObject.name}] sprite='{sprite}' layer='{layer}' order={order} " +
                              $"pivot={(pivot != null ? pivot.name : "<self>")} pivotWorldY={pPos.y:F2} {gridInfo}");
            }
            sb.AppendLine($"--- total IsoDepthSort: {count} ---");

            // Tout SpriteRenderer dont un ancetre evoque torche/lampe/halo/sample/light, avec son
            // path complet + layer/order/sprite + presence d'IsoDepthSort. Revele les lampadaires
            // non tries (sur 'Default' ou sans IsoDepthSort).
            sb.AppendLine("--- SpriteRenderers 'sample/torch/light/halo/lamp' (ancetre) ---");
            int hits = 0;
            foreach (var sr in Object.FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None))
            {
                var anc = sr.transform;
                bool match = false;
                while (anc != null)
                {
                    string an = anc.name.ToLowerInvariant();
                    if (an.Contains("sample") || an.Contains("torch") || an.Contains("light")
                        || an.Contains("halo") || an.Contains("lamp")) { match = true; break; }
                    anc = anc.parent;
                }
                if (!match) continue;
                hits++;
                bool hasIso = sr.GetComponent<IsoDepthSort>() != null;
                sb.AppendLine($"  path='{FullPath(sr.transform)}' sprite='{(sr.sprite != null ? sr.sprite.name : "<none>")}' " +
                              $"layer='{sr.sortingLayerName}' order={sr.sortingOrder} iso={hasIso}");
            }
            if (hits == 0) sb.AppendLine("(aucun)");

            Debug.Log(sb.ToString());
            EditorUtility.DisplayDialog("Diagnose Hub Depth Sort",
                $"{count} IsoDepthSort + {hits} SpriteRenderers torche/lampe listes.\n" +
                "Detail complet dans la Console. Copie-colle-le moi.", "OK");
        }

        private static string FullPath(Transform t)
        {
            string path = t.name;
            for (var p = t.parent; p != null; p = p.parent) path = p.name + "/" + path;
            return path;
        }
    }
}
