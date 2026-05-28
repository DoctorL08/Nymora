using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.U2D;

namespace Nymora.Editor.Generators
{
    /// <summary>
    /// Génère une normal map APPROXIMATIVE depuis le diffuse d'un sprite (luminance -> hauteur ->
    /// Sobel -> normale tangente) puis l'assigne en SECONDARY TEXTURE "_NormalMap" sur le sprite
    /// source. Résultat : les Light2D (torches + lumière globale) sculptent le relief de l'image
    /// plate -> profondeur de lumière sans découper la map ni dépendre du designer.
    ///
    /// "Approximative" : la hauteur est estimée depuis la luminance (le clair = en relief). Ce n'est
    /// pas physiquement exact mais ça donne un relief crédible qui réagit à la lumière. Sliders pour
    /// calibrer (force, lissage, inversions).
    ///
    /// Réutilisable sur N'IMPORTE QUEL sprite (map du hub, décor combat, perso).
    ///
    /// Pré-requis pour voir l'effet en jeu :
    /// 1. Le sprite doit utiliser un matériau 2D LIT (Sprite-Lit-Default) — c'est le défaut en URP 2D.
    /// 2. Les Light2D qui l'éclairent doivent avoir "Normal Maps > Quality" sur Fast/Accurate
    ///    (sélectionner la light -> section Normal Maps). Désactivé = la normal map est ignorée.
    ///
    /// Accès : Nymora > Generators > Normal Map Generator.
    /// </summary>
    public sealed class NormalMapGeneratorTool : EditorWindow
    {
        private Texture2D _source;
        private float _strength = 2.5f;
        private int _blur = 1;
        private bool _invertHeight;
        private bool _flipY;

        [MenuItem("Nymora/Generators/Normal Map Generator")]
        private static void Open()
        {
            var w = GetWindow<NormalMapGeneratorTool>("Normal Map Generator");
            w.minSize = new Vector2(360f, 320f);
            if (Selection.activeObject is Texture2D t) w._source = t;
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Générateur de normal map (relief 2D)", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Génère une normal map depuis le diffuse et l'assigne en secondary texture _NormalMap " +
                "sur le sprite. Les Light2D sculptent alors le relief.\n\n" +
                "Rappel : la light doit avoir 'Normal Maps > Quality' activé pour que l'effet soit visible.",
                MessageType.Info);

            EditorGUILayout.Space();
            _source = (Texture2D)EditorGUILayout.ObjectField("Sprite source", _source, typeof(Texture2D), false);

            EditorGUILayout.Space();
            _strength = EditorGUILayout.Slider(
                new GUIContent("Force du relief", "Amplitude des pentes (plus haut = relief plus marqué)."), _strength, 0.2f, 8f);
            _blur = EditorGUILayout.IntSlider(
                new GUIContent("Lissage", "Adoucit la hauteur avant le calcul (utile en pixel-art pour éviter un relief trop dur)."), _blur, 0, 5);
            _invertHeight = EditorGUILayout.Toggle(
                new GUIContent("Inverser hauteur", "Si le clair doit être 'en creux' plutôt qu'en relief."), _invertHeight);
            _flipY = EditorGUILayout.Toggle(
                new GUIContent("Flip Y", "Inverse le canal vert (convention OpenGL/DirectX). À cocher si l'éclairage paraît inversé verticalement."), _flipY);

            EditorGUILayout.Space();
            using (new EditorGUI.DisabledScope(_source == null))
            {
                if (GUILayout.Button("Générer + assigner", GUILayout.Height(34f)))
                    Generate();
            }

            if (_source != null)
            {
                string p = AssetDatabase.GetAssetPath(_source);
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Sortie", NormalPathFor(p), EditorStyles.miniLabel);
            }
        }

        private void Generate()
        {
            string srcPath = AssetDatabase.GetAssetPath(_source);
            if (string.IsNullOrEmpty(srcPath)) { Debug.LogError("[NormalMap] Source sans chemin asset."); return; }

            var srcImporter = AssetImporter.GetAtPath(srcPath) as TextureImporter;
            if (srcImporter == null) { Debug.LogError("[NormalMap] La source n'est pas une texture importée."); return; }
            if (srcImporter.textureType != TextureImporterType.Sprite)
                Debug.LogWarning("[NormalMap] La source n'est pas un Sprite : la secondary texture _NormalMap " +
                                 "ne sera prise en compte que sur un sprite lit. Continue quand même.");

            // Lecture des pixels SANS toucher l'import (on charge les octets du PNG du disque).
            string absSrc = ToAbsolute(srcPath);
            if (!File.Exists(absSrc)) { Debug.LogError($"[NormalMap] Fichier introuvable : {absSrc}"); return; }

            var tmp = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!tmp.LoadImage(File.ReadAllBytes(absSrc))) { Debug.LogError("[NormalMap] Échec lecture image."); Object.DestroyImmediate(tmp); return; }

            int w = tmp.width, h = tmp.height;
            Color32[] src = tmp.GetPixels32();
            Object.DestroyImmediate(tmp);

            // 1. Hauteur depuis la luminance.
            float[] height = new float[w * h];
            for (int i = 0; i < src.Length; i++)
            {
                float lum = (0.299f * src[i].r + 0.587f * src[i].g + 0.114f * src[i].b) / 255f;
                height[i] = _invertHeight ? 1f - lum : lum;
            }

            // 2. Lissage (box blur séparable) pour adoucir le pixel-art.
            for (int b = 0; b < _blur; b++) height = BoxBlur(height, w, h);

            // 3. Sobel -> normale tangente -> encodage RGB.
            var outPix = new Color32[w * h];
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float tl = H(height, w, h, x - 1, y + 1), t = H(height, w, h, x, y + 1), tr = H(height, w, h, x + 1, y + 1);
                    float l = H(height, w, h, x - 1, y), r = H(height, w, h, x + 1, y);
                    float bl = H(height, w, h, x - 1, y - 1), bo = H(height, w, h, x, y - 1), br = H(height, w, h, x + 1, y - 1);

                    float dx = (tr + 2f * r + br) - (tl + 2f * l + bl);
                    float dy = (tl + 2f * t + tr) - (bl + 2f * bo + br);

                    Vector3 n = new Vector3(-dx * _strength, -dy * _strength, 1f).normalized;
                    if (_flipY) n.y = -n.y;

                    outPix[y * w + x] = new Color32(
                        (byte)Mathf.Clamp(Mathf.RoundToInt((n.x * 0.5f + 0.5f) * 255f), 0, 255),
                        (byte)Mathf.Clamp(Mathf.RoundToInt((n.y * 0.5f + 0.5f) * 255f), 0, 255),
                        (byte)Mathf.Clamp(Mathf.RoundToInt((n.z * 0.5f + 0.5f) * 255f), 0, 255),
                        255);
                }
            }

            // 4. Écriture du PNG.
            var outTex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            outTex.SetPixels32(outPix);
            outTex.Apply();
            byte[] png = outTex.EncodeToPNG();
            Object.DestroyImmediate(outTex);

            string outRel = NormalPathFor(srcPath);
            File.WriteAllBytes(ToAbsolute(outRel), png);
            AssetDatabase.ImportAsset(outRel, ImportAssetOptions.ForceUpdate);

            // 5. Import de la normal map : linéaire (PAS sRGB), non compressée, clamp.
            var ni = AssetImporter.GetAtPath(outRel) as TextureImporter;
            if (ni != null)
            {
                ni.textureType = TextureImporterType.Default;
                ni.sRGBTexture = false;
                ni.wrapMode = TextureWrapMode.Clamp;
                ni.mipmapEnabled = false;
                ni.maxTextureSize = Mathf.Max(2048, Mathf.NextPowerOfTwo(Mathf.Max(w, h)));
                ni.textureCompression = TextureImporterCompression.Uncompressed;
                ni.SaveAndReimport();
            }

            // 6. Assignation en secondary texture _NormalMap sur le SPRITE source.
            var normalAsset = AssetDatabase.LoadAssetAtPath<Texture2D>(outRel);
            var list = new List<SecondarySpriteTexture>(srcImporter.secondarySpriteTextures ?? new SecondarySpriteTexture[0]);
            list.RemoveAll(s => s.name == "_NormalMap");
            list.Add(new SecondarySpriteTexture { name = "_NormalMap", texture = normalAsset });
            srcImporter.secondarySpriteTextures = list.ToArray();
            srcImporter.SaveAndReimport();

            AssetDatabase.Refresh();
            Debug.Log($"[NormalMap] OK : '{outRel}' généré ({w}x{h}) et assigné en _NormalMap sur '{Path.GetFileName(srcPath)}'. " +
                      "Pense à activer Normal Maps > Quality sur les Light2D de la scène.");
            EditorUtility.DisplayDialog("Normal Map",
                "Normal map générée et assignée.\n\n" +
                "Pour voir l'effet : sélectionne les Light2D de la scène (torches + lumière globale) et " +
                "mets 'Normal Maps > Quality' sur Accurate (ou Fast).", "OK");
        }

        private static float[] BoxBlur(float[] s, int w, int h)
        {
            var tmp = new float[s.Length];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    tmp[y * w + x] = (H(s, w, h, x - 1, y) + s[y * w + x] + H(s, w, h, x + 1, y)) / 3f;
            var o = new float[s.Length];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    o[y * w + x] = (H(tmp, w, h, x, y - 1) + tmp[y * w + x] + H(tmp, w, h, x, y + 1)) / 3f;
            return o;
        }

        private static float H(float[] a, int w, int h, int x, int y)
        {
            x = Mathf.Clamp(x, 0, w - 1);
            y = Mathf.Clamp(y, 0, h - 1);
            return a[y * w + x];
        }

        private static string NormalPathFor(string srcPath)
        {
            string dir = Path.GetDirectoryName(srcPath).Replace('\\', '/');
            string name = Path.GetFileNameWithoutExtension(srcPath);
            return $"{dir}/{name}_normal.png";
        }

        private static string ToAbsolute(string assetPath)
        {
            // Application.dataPath = ".../<projet>/Assets" ; assetPath commence par "Assets".
            return Application.dataPath + assetPath.Substring("Assets".Length);
        }
    }
}
