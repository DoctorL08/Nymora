using System.Collections.Generic;
using System.IO;
using Nymora.Hub;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Nymora.Editor.Setup
{
    /// <summary>
    /// Injectable (23 mai) — l'art Kyami a livre TOUTES les torches dessinees sur UNE seule PNG
    /// (torch_frame1..8, 8 frames de flicker). Un seul sprite = un seul sortingOrder => le perso
    /// ne peut pas passer devant certaines torches et derriere d'autres.
    ///
    /// Ce tool decoupe la PNG combinee en torches individuelles ET les pose dans la scene :
    ///   1. Rend les 8 frames lisibles, lit les pixels.
    ///   2. Detecte chaque torche = region opaque connexe (8-connexite, UNION des 8 frames pour
    ///      capturer le flamme dans tous ses etats), filtre le bruit (aire mini).
    ///   3. Tranche les 8 PNG avec les MEMES rects (spriteImportMode=Multiple), pivot au PIED
    ///      (bas-centre de chaque torche).
    ///   4. Instancie 1 GameObject par torche, PILE a sa position actuelle (math PPU/pivot du
    ///      sprite source), avec SpriteRenderer + SpriteFlipbook (anim 8 frames desync) +
    ///      IsoDepthSort + DepthPivot au pied (layer 'Personnages', base 100).
    ///   5. Desactive (ne supprime pas) l'ancien GameObject au sprite combine.
    ///
    /// Usage : selectionne le GameObject 'Torch' (sprite combine, celui qui porte IsoDepthSort)
    /// puis lance le menu. Re-jouable : recree le conteneur 'Torches (split)'.
    ///
    /// Menu : Nymora > Setup > Slice & Place Torches.
    /// </summary>
    public static class HubTorchSlicerTool
    {
        private const string ArtDir = "Assets/_Nymora/Art/Hub/Torch";
        private const string ContainerName = "Torches (split)";
        private const byte AlphaThreshold = 24;   // pixel considere opaque au-dessus
        private const int MinComponentArea = 80;  // ignore les specks/antialiasing isoles
        private const float FlickerFps = 10f;

        [MenuItem("Nymora/Setup/Slice & Place Torches", priority = 67)]
        private static void Run()
        {
            if (Application.isPlaying)
            {
                EditorUtility.DisplayDialog("Slice & Place Torches", "Impossible pendant Play Mode.", "OK");
                return;
            }

            // --- Source : le GameObject au sprite combine (selection ou IsoDepthSort) ---
            var srcSR = ResolveSourceRenderer();
            if (srcSR == null || srcSR.sprite == null)
            {
                EditorUtility.DisplayDialog("Slice & Place Torches",
                    "Selectionne d'abord le GameObject 'Torch' (le sprite combine de toutes les torches), " +
                    "celui qui porte un SpriteRenderer + IsoDepthSort.", "OK");
                return;
            }
            Transform srcTransform = srcSR.transform;
            Material srcMaterial = srcSR.sharedMaterial;
            string srcSortingLayer = "Personnages";

            // --- Frames torch_frame1..N ---
            var framePaths = CollectFramePaths();
            if (framePaths.Count == 0)
            {
                EditorUtility.DisplayDialog("Slice & Place Torches",
                    $"Aucune frame 'torch_frameN.png' trouvee dans {ArtDir}.", "OK");
                return;
            }

            // PPU + pivot du sprite source (lus AVANT de passer en Multiple).
            var firstImporter = (TextureImporter)AssetImporter.GetAtPath(framePaths[0]);
            float ppu = firstImporter.spritePixelsToUnits;
            Vector2 srcPivotNorm = firstImporter.spritePivot; // (0.5, 0) attendu (bottom-center)

            // --- 1+2. Lire les pixels (readable) + union alpha + composantes connexes ---
            int w = 0, h = 0;
            bool[] mask = null;
            foreach (var path in framePaths)
            {
                SetReadable(path, true);
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if (tex == null) continue;
                if (mask == null) { w = tex.width; h = tex.height; mask = new bool[w * h]; }
                if (tex.width != w || tex.height != h)
                {
                    EditorUtility.DisplayDialog("Slice & Place Torches",
                        $"Frames de tailles differentes ({path} = {tex.width}x{tex.height}, attendu {w}x{h}). Abandon.", "OK");
                    return;
                }
                var px = tex.GetPixels32();
                for (int i = 0; i < px.Length; i++)
                    if (px[i].a >= AlphaThreshold) mask[i] = true;
            }
            if (mask == null)
            {
                EditorUtility.DisplayDialog("Slice & Place Torches", "Lecture des textures impossible.", "OK");
                return;
            }

            var comps = FindComponents(mask, w, h);
            if (comps.Count == 0)
            {
                EditorUtility.DisplayDialog("Slice & Place Torches",
                    "Aucune torche detectee (region opaque). Verifie le seuil alpha.", "OK");
                return;
            }
            // Tri stable : haut->bas puis gauche->droite (cohere le nommage entre runs).
            comps.Sort((a, b) =>
            {
                int byY = b.MaxY.CompareTo(a.MaxY);
                return byY != 0 ? byY : a.MinX.CompareTo(b.MinX);
            });

            // --- 3. Trancher les 8 frames avec les memes rects (pivot au pied) ---
            var metas = new SpriteMetaData[comps.Count];
            for (int i = 0; i < comps.Count; i++)
            {
                var c = comps[i];
                int rw = c.MaxX - c.MinX + 1;
                int rh = c.MaxY - c.MinY + 1;
                float pivotX = (c.BaseX - c.MinX + 0.5f) / rw; // centre horizontal du pied
                float pivotY = 0.0f;                            // bas de la torche
#pragma warning disable 618
                metas[i] = new SpriteMetaData
                {
                    name = $"torch{i}",
                    rect = new Rect(c.MinX, c.MinY, rw, rh),
                    alignment = (int)SpriteAlignment.Custom,
                    pivot = new Vector2(pivotX, pivotY),
                    border = Vector4.zero,
                };
#pragma warning restore 618
            }

            foreach (var path in framePaths)
            {
                var imp = (TextureImporter)AssetImporter.GetAtPath(path);
                imp.spriteImportMode = SpriteImportMode.Multiple;
                imp.isReadable = false; // plus besoin apres lecture
#pragma warning disable 618
                imp.spritesheet = metas;
#pragma warning restore 618
                EditorUtility.SetDirty(imp);
                imp.SaveAndReimport();
            }
            AssetDatabase.Refresh();

            // --- Charger les sous-sprites : par torche, [frame1_i, frame2_i, ...] ---
            var perTorchFrames = new Sprite[comps.Count][];
            for (int i = 0; i < comps.Count; i++) perTorchFrames[i] = new Sprite[framePaths.Count];
            for (int f = 0; f < framePaths.Count; f++)
            {
                var byName = new Dictionary<string, Sprite>();
                foreach (var a in AssetDatabase.LoadAllAssetRepresentationsAtPath(framePaths[f]))
                    if (a is Sprite s) byName[s.name] = s;
                for (int i = 0; i < comps.Count; i++)
                    byName.TryGetValue($"torch{i}", out perTorchFrames[i][f]);
            }

            // --- 4. Instancier 1 GameObject par torche pile a sa position ---
            var scene = srcTransform.gameObject.scene;
            var existing = GameObject.Find(ContainerName);
            if (existing != null) Object.DestroyImmediate(existing);
            var container = new GameObject(ContainerName);
            Undo.RegisterCreatedObjectUndo(container, "Create Torches container");
            container.transform.SetParent(srcTransform.parent, false);
            container.transform.localPosition = Vector3.zero;

            int placed = 0;
            for (int i = 0; i < comps.Count; i++)
            {
                var c = comps[i];
                // Position monde du pied (bas-centre) de la torche dans le sprite combine.
                float localX = (c.BaseX + 0.5f - srcPivotNorm.x * w) / ppu;
                float localY = (c.MinY - srcPivotNorm.y * h) / ppu;
                Vector3 world = srcTransform.TransformPoint(new Vector3(localX, localY, 0f));

                var go = new GameObject($"Torch_{i}");
                Undo.RegisterCreatedObjectUndo(go, "Create torch");
                go.transform.SetParent(container.transform, false);
                go.transform.localScale = srcTransform.localScale; // garder la taille du sprite combine
                go.transform.position = new Vector3(world.x, world.y, srcTransform.position.z);

                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = perTorchFrames[i][0];
                sr.sharedMaterial = srcMaterial;
                sr.sortingLayerName = srcSortingLayer;

                var flip = go.AddComponent<SpriteFlipbook>();
                flip.Configure(perTorchFrames[i], FlickerFps, i * 0.17f);

                // DepthPivot enfant au pied (= localPosition zero, car le GO EST deja pose au pied).
                var pivotGo = new GameObject("DepthPivot");
                pivotGo.transform.SetParent(go.transform, false);
                pivotGo.transform.localPosition = Vector3.zero;

                var iso = go.AddComponent<IsoDepthSort>();
                var so = new SerializedObject(iso);
                var pBase = so.FindProperty("_baseOrder");
                if (pBase != null) pBase.intValue = 100;
                var pLayer = so.FindProperty("_sortingLayerName");
                if (pLayer != null) pLayer.stringValue = srcSortingLayer;
                var pPivot = so.FindProperty("_depthPivot");
                if (pPivot != null) pPivot.objectReferenceValue = pivotGo.transform;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(iso);

                placed++;
            }

            // --- 5. Desactiver l'ancien sprite combine (sans le supprimer) ---
            srcTransform.gameObject.SetActive(false);

            if (scene.IsValid()) EditorSceneManager.MarkSceneDirty(scene);

            string summary =
                $"{comps.Count} torches detectees et tranchees ({framePaths.Count} frames).\n" +
                $"{placed} GameObjects poses sous '{ContainerName}' (Flipbook {FlickerFps}fps + IsoDepthSort + DepthPivot au pied).\n" +
                $"Ancien sprite combine '{srcTransform.name}' DESACTIVE (pas supprime).\n\n" +
                "Verifie en Play Mode : flicker + perso qui passe devant/derriere chaque torche. " +
                "Ajuste un DepthPivot a la main si une torche bascule un poil trop tot/tard, puis Ctrl+S.";
            EditorUtility.DisplayDialog("Slice & Place Torches", summary, "OK");
            Debug.Log("[HubTorchSlicerTool] " + summary);
        }

        // --- Resolution source ---
        private static SpriteRenderer ResolveSourceRenderer()
        {
            foreach (var go in Selection.gameObjects)
            {
                var sr = go.GetComponent<SpriteRenderer>();
                if (sr != null) return sr;
            }
            // Fallback : l'unique IsoDepthSort existant (le sprite combine actuel).
            var iso = Object.FindFirstObjectByType<IsoDepthSort>();
            return iso != null ? iso.GetComponent<SpriteRenderer>() : null;
        }

        private static List<string> CollectFramePaths()
        {
            var list = new List<string>();
            for (int n = 1; n <= 64; n++)
            {
                string p = $"{ArtDir}/torch_frame{n}.png";
                if (File.Exists(p)) list.Add(p);
                else if (n > 1) break;
            }
            return list;
        }

        private static void SetReadable(string path, bool readable)
        {
            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            if (imp == null || imp.isReadable == readable) return;
            imp.isReadable = readable;
            EditorUtility.SetDirty(imp);
            imp.SaveAndReimport();
        }

        // --- Composantes connexes 8-connexite (BFS sur pile) ---
        private struct Comp { public int MinX, MinY, MaxX, MaxY, BaseX; }

        private static List<Comp> FindComponents(bool[] mask, int w, int h)
        {
            var result = new List<Comp>();
            var visited = new bool[w * h];
            var stack = new Stack<int>();

            for (int start = 0; start < mask.Length; start++)
            {
                if (!mask[start] || visited[start]) continue;
                int minX = w, minY = h, maxX = 0, maxY = 0, area = 0;
                // bas de la composante : on memorise les x du/des pixel(s) sur la ligne la plus basse.
                int lowestY = h, lowestXSum = 0, lowestXCount = 0;

                stack.Push(start);
                visited[start] = true;
                while (stack.Count > 0)
                {
                    int idx = stack.Pop();
                    int x = idx % w;
                    int y = idx / w;
                    area++;
                    if (x < minX) minX = x;
                    if (x > maxX) maxX = x;
                    if (y < minY) minY = y;
                    if (y > maxY) maxY = y;
                    if (y < lowestY) { lowestY = y; lowestXSum = x; lowestXCount = 1; }
                    else if (y == lowestY) { lowestXSum += x; lowestXCount++; }

                    for (int dy = -1; dy <= 1; dy++)
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        if (dx == 0 && dy == 0) continue;
                        int nx = x + dx, ny = y + dy;
                        if (nx < 0 || nx >= w || ny < 0 || ny >= h) continue;
                        int nidx = ny * w + nx;
                        if (mask[nidx] && !visited[nidx])
                        {
                            visited[nidx] = true;
                            stack.Push(nidx);
                        }
                    }
                }

                if (area < MinComponentArea) continue;
                result.Add(new Comp
                {
                    MinX = minX, MinY = minY, MaxX = maxX, MaxY = maxY,
                    BaseX = lowestXCount > 0 ? lowestXSum / lowestXCount : (minX + maxX) / 2,
                });
            }
            return result;
        }
    }
}
