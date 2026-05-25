using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;

namespace Nymora.Editor.Setup
{
    /// <summary>
    /// Diagnostic READ-ONLY (26 mai) : traque le "cadre noir transparent" qui depasse dans le
    /// coin BAS-GAUCHE des scenes combat. Ne modifie RIEN.
    ///
    /// Scanne tous les Graphic UI (Image + TMP_Text) effectivement visibles (GameObject actif,
    /// canvas actif, composant enabled, alpha > seuil), calcule leur rectangle en pixels ecran,
    /// et logue ceux qui chevauchent le coin bas-gauche (x &lt; 30% largeur, y &lt; 25% hauteur).
    /// Pour chaque suspect : chemin hierarchie complet, type, couleur+alpha, rect ecran, taille.
    ///
    /// A lancer EN PLAY MODE de preference : certains elements (ForfeitButtonView, overlays)
    /// sont crees par code au runtime et n'existent pas en edit mode.
    ///
    /// Menu : Nymora > Validation > Diagnose Bottom-Left UI.
    /// </summary>
    public static class DiagnoseBottomLeftUiTool
    {
        // Coin bas-gauche cible : 0..30% de la largeur, 0..25% de la hauteur ecran.
        private const float CornerWidthFrac = 0.30f;
        private const float CornerHeightFrac = 0.25f;
        private const float MinAlpha = 0.02f;

        [MenuItem("Nymora/Validation/Diagnose Bottom-Left UI", priority = 80)]
        private static void Diagnose()
        {
            float sw = Application.isPlaying ? Screen.width : 1920f;
            float sh = Application.isPlaying ? Screen.height : 1080f;
            var cornerRect = new Rect(0f, 0f, sw * CornerWidthFrac, sh * CornerHeightFrac);

            var sb = new StringBuilder();
            sb.AppendLine("=== DIAGNOSTIC UI BAS-GAUCHE ===");
            sb.AppendLine(Application.isPlaying
                ? $"Play Mode. Ecran {sw}x{sh}. Zone coin = x[0..{cornerRect.width:0}] y[0..{cornerRect.height:0}] (px, origine bas-gauche)."
                : "EDIT MODE (relance EN PLAY MODE pour voir les overlays crees par code). Ecran suppose 1920x1080.");
            sb.AppendLine();

            var hits = new List<(float area, string line)>();
            var corners = new Vector3[4];

            foreach (var g in Object.FindObjectsByType<Graphic>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (g == null || !g.isActiveAndEnabled) continue;

                var canvas = g.canvas;
                if (canvas == null || !canvas.isActiveAndEnabled) continue;

                // Alpha effectif = couleur du graphic * tous les CanvasGroup parents.
                float alpha = g.color.a * EffectiveCanvasGroupAlpha(g.transform);
                if (alpha <= MinAlpha) continue;

                var rt = g.rectTransform;
                rt.GetWorldCorners(corners); // [0]=bas-gauche, [2]=haut-droit (sens horaire depuis bas-gauche)

                Vector2 min, max;
                if (canvas.renderMode == RenderMode.ScreenSpaceOverlay)
                {
                    // Coins deja en pixels ecran.
                    min = new Vector2(corners[0].x, corners[0].y);
                    max = new Vector2(corners[2].x, corners[2].y);
                }
                else
                {
                    var cam = canvas.worldCamera != null ? canvas.worldCamera : Camera.main;
                    min = RectTransformUtility.WorldToScreenPoint(cam, corners[0]);
                    max = RectTransformUtility.WorldToScreenPoint(cam, corners[2]);
                }
                var screenRect = Rect.MinMaxRect(
                    Mathf.Min(min.x, max.x), Mathf.Min(min.y, max.y),
                    Mathf.Max(min.x, max.x), Mathf.Max(min.y, max.y));

                if (!screenRect.Overlaps(cornerRect)) continue;

                string kind = g is TMP_Text ? "TMP" : (g is Image img2 ? (img2.sprite != null ? "Image(sprite)" : "Image(uni)") : g.GetType().Name);
                var c = g.color;
                float area = screenRect.width * screenRect.height;
                hits.Add((area,
                    $"• {Path(g.transform)}\n" +
                    $"    type={kind}  enabled={g.enabled}  alpha_eff={alpha:0.00}  color=({c.r:0.00},{c.g:0.00},{c.b:0.00},{c.a:0.00})\n" +
                    $"    rect ecran x[{screenRect.xMin:0}..{screenRect.xMax:0}] y[{screenRect.yMin:0}..{screenRect.yMax:0}]  taille {screenRect.width:0}x{screenRect.height:0}\n" +
                    $"    canvas='{canvas.name}' (order {canvas.sortingOrder}, {canvas.renderMode})"));
            }

            if (hits.Count == 0)
            {
                sb.AppendLine("Aucun Graphic UI visible ne chevauche le coin bas-gauche.");
                sb.AppendLine("→ Si le cadre est tjrs la : ce n'est pas un Graphic uGUI (peut-etre un");
                sb.AppendLine("  SpriteRenderer, un effet post-process/vignette, ou une zone non couverte");
                sb.AppendLine("  par la map). Relance EN PLAY MODE si tu etais en edit mode.");
            }
            else
            {
                hits.Sort((a, b) => a.area.CompareTo(b.area)); // petits frames d'abord (le suspect)
                sb.AppendLine($"{hits.Count} element(s) UI dans le coin bas-gauche (du + petit au + grand) :");
                sb.AppendLine();
                foreach (var h in hits) sb.AppendLine(h.line);
            }

            Debug.Log(sb.ToString());
        }

        private static float EffectiveCanvasGroupAlpha(Transform t)
        {
            float a = 1f;
            var cur = t;
            while (cur != null)
            {
                if (cur.TryGetComponent<CanvasGroup>(out var cg))
                {
                    a *= cg.alpha;
                    if (cg.ignoreParentGroups) break;
                }
                cur = cur.parent;
            }
            return a;
        }

        private static string Path(Transform t)
        {
            var stack = new Stack<string>();
            var cur = t;
            while (cur != null) { stack.Push(cur.name); cur = cur.parent; }
            return string.Join("/", stack);
        }
    }
}
