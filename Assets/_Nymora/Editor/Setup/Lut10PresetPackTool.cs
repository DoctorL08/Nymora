using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Nymora.Editor.Setup
{
    /// <summary>
    /// POLISH KYAMI — pack de 10 LUTs générées + fenêtre de sélection (x10) pour tester le grading
    /// du hub/combat en un clic. Les LUTs sont au format URP "Color Lookup" (strip 1024×32, sRGB OFF,
    /// non compressé). On NE pompe aucune LUT externe (0 licence) : tout est généré procéduralement.
    ///
    /// Fenêtre : choisis la cible (Hub / Combat / les deux), la contribution, puis clique un preset →
    /// il branche la LUT sur le Color Lookup du profil et sauve (visible direct en Game view, persiste).
    /// « Off » remet contribution 0 (look d'origine).
    ///
    /// Menu : Nymora > Setup > Polish Kyami > LUT Pack (x10) + Presets.
    /// </summary>
    public sealed class Lut10PresetPackTool : EditorWindow
    {
        private const int LutSize = 32; // -> 1024×32
        private const string OutDir = "Assets/_Nymora/Settings/PostProcessing/Pack10";
        private const string HubProfile = "Assets/_Nymora/Settings/PostProcessing/Hub_PostFX.asset";
        private const string CombatProfile = "Assets/_Nymora/Settings/PostProcessing/Combat_PostFX.asset";

        // (nom de fichier, libellé, fonction de grade)
        private static readonly (string file, string label)[] Presets =
        {
            ("LUT_01_Neutral",      "01 · Neutral (référence)"),
            ("LUT_02_Cinematic",    "02 · Cinematic (teal/orange)"),
            ("LUT_03_Cold",         "03 · Cold (lune froide)"),
            ("LUT_04_Warm",         "04 · Warm (couchant)"),
            ("LUT_05_Sepia",        "05 · Sépia (vintage)"),
            ("LUT_06_Noir",         "06 · Noir (désaturé contrasté)"),
            ("LUT_07_Vibrant",      "07 · Vibrant (couleurs punchy)"),
            ("LUT_08_DarkFantasy",  "08 · Dark Fantasy (sombre/froid)"),
            ("LUT_09_Faded",        "09 · Faded (pastel délavé)"),
            ("LUT_10_Toxic",        "10 · Toxic (vert fantasy)"),
        };

        private enum Target { Hub, Combat, Both }
        private Target _target = Target.Hub;
        private float _contribution = 1f;
        private string _status = "";

        [MenuItem("Nymora/Setup/Polish Kyami/LUT Pack (x10) + Presets", priority = 73)]
        private static void Open()
        {
            var w = GetWindow<Lut10PresetPackTool>("LUT Pack x10");
            w.minSize = new Vector2(360f, 470f);
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Pack 10 LUTs + sélection", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "1) Génère le pack (10 LUTs).  2) Choisis la cible + contribution.  3) Clique un preset.\n" +
                "Le grading s'applique au Color Lookup du Volume et est sauvegardé (visible en Game view).",
                MessageType.Info);

            EditorGUILayout.Space();
            if (GUILayout.Button(LutsExist() ? "Régénérer le pack (10 LUTs)" : "Générer le pack (10 LUTs)", GUILayout.Height(28)))
                GeneratePack();

            EditorGUILayout.Space();
            _target = (Target)EditorGUILayout.EnumPopup("Cible", _target);
            _contribution = EditorGUILayout.Slider("Contribution", _contribution, 0f, 1f);

            EditorGUILayout.Space();
            using (new EditorGUI.DisabledScope(!LutsExist()))
            {
                foreach (var p in Presets)
                {
                    if (GUILayout.Button(p.label, GUILayout.Height(24)))
                        Apply(p.file);
                }
                EditorGUILayout.Space();
                if (GUILayout.Button("Off (look d'origine, contribution 0)", GUILayout.Height(22)))
                    ApplyOff();
            }

            if (!string.IsNullOrEmpty(_status))
            {
                EditorGUILayout.Space();
                EditorGUILayout.HelpBox(_status, MessageType.None);
            }
        }

        private static bool LutsExist() => File.Exists($"{OutDir}/{Presets[0].file}.png");

        // ============================ GÉNÉRATION ============================
        private void GeneratePack()
        {
            EnsureFolder(OutDir);
            for (int i = 0; i < Presets.Length; i++)
                WriteLut($"{OutDir}/{Presets[i].file}.png", i);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            _status = $"Pack généré : {Presets.Length} LUTs dans {OutDir}";
            Debug.Log("[Lut10] " + _status);
        }

        private static void WriteLut(string path, int mode)
        {
            int w = LutSize * LutSize, h = LutSize;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false, true); // linéaire
            float inv = 1f / (LutSize - 1);
            for (int b = 0; b < LutSize; b++)
                for (int g = 0; g < LutSize; g++)
                    for (int r = 0; r < LutSize; r++)
                        tex.SetPixel(b * LutSize + r, g, Grade(mode, new Color(r * inv, g * inv, b * inv, 1f)));
            tex.Apply();

            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

            var ti = (TextureImporter)AssetImporter.GetAtPath(path);
            ti.textureType = TextureImporterType.Default;
            ti.sRGBTexture = false;
            ti.mipmapEnabled = false;
            ti.wrapMode = TextureWrapMode.Clamp;
            ti.filterMode = FilterMode.Bilinear;
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            ti.maxTextureSize = 2048;
            ti.SaveAndReimport();
        }

        // ============================ APPLICATION ============================
        private void Apply(string lutFile)
        {
            var lut = AssetDatabase.LoadAssetAtPath<Texture2D>($"{OutDir}/{lutFile}.png");
            if (lut == null) { _status = "⚠ LUT introuvable — génère le pack d'abord."; return; }

            int n = 0;
            foreach (var prof in TargetProfiles())
            {
                if (prof == null) continue;
                var lookup = GetOrAdd<ColorLookup>(prof);
                lookup.texture.overrideState = true;
                lookup.texture.value = lut;
                lookup.contribution.overrideState = true;
                lookup.contribution.value = _contribution;
                EditorUtility.SetDirty(prof);
                n++;
            }
            AssetDatabase.SaveAssets();
            _status = $"Appliqué : {lutFile} (contribution {_contribution:0.00}) sur {n} profil(s).";
            Debug.Log("[Lut10] " + _status);
        }

        private void ApplyOff()
        {
            int n = 0;
            foreach (var prof in TargetProfiles())
            {
                if (prof == null) continue;
                if (prof.TryGet<ColorLookup>(out var lookup))
                {
                    lookup.contribution.value = 0f;
                    EditorUtility.SetDirty(prof);
                    n++;
                }
            }
            AssetDatabase.SaveAssets();
            _status = $"Color Lookup désactivé (contribution 0) sur {n} profil(s).";
        }

        private System.Collections.Generic.IEnumerable<VolumeProfile> TargetProfiles()
        {
            if (_target == Target.Hub || _target == Target.Both)
                yield return AssetDatabase.LoadAssetAtPath<VolumeProfile>(HubProfile);
            if (_target == Target.Combat || _target == Target.Both)
                yield return AssetDatabase.LoadAssetAtPath<VolumeProfile>(CombatProfile);
        }

        private static T GetOrAdd<T>(VolumeProfile profile) where T : VolumeComponent
        {
            if (profile.TryGet<T>(out var comp)) return comp;
            comp = profile.Add<T>(false);
            AssetDatabase.AddObjectToAsset(comp, profile);
            return comp;
        }

        // ============================ 10 GRADES ============================
        private static float Lum(Color c) => 0.299f * c.r + 0.587f * c.g + 0.114f * c.b;
        private static Color Sat(Color c, float k) { float l = Lum(c); return new Color(Mathf.Lerp(l, c.r, k), Mathf.Lerp(l, c.g, k), Mathf.Lerp(l, c.b, k), 1f); }
        private static Color Contrast(Color c, float k) => new Color((c.r - 0.5f) * k + 0.5f, (c.g - 0.5f) * k + 0.5f, (c.b - 0.5f) * k + 0.5f, 1f);
        private static Color Clamp(Color c) => new Color(Mathf.Clamp01(c.r), Mathf.Clamp01(c.g), Mathf.Clamp01(c.b), 1f);

        private static Color Grade(int mode, Color c)
        {
            switch (mode)
            {
                case 0: return c; // Neutral

                case 1: // Cinematic teal/orange
                {
                    c = Contrast(c, 1.10f);
                    float lum = Lum(c);
                    float sh = 1f - Mathf.SmoothStep(0f, 0.5f, lum);
                    float hi = Mathf.SmoothStep(0.5f, 0.82f, lum) * (1f - Mathf.SmoothStep(0.85f, 1f, lum));
                    c.r += -0.02f * sh + 0.035f * hi;
                    c.g += 0.03f * sh + 0.02f * hi;
                    c.b += 0.08f * sh + -0.03f * hi;
                    return Clamp(Sat(c, 0.92f));
                }

                case 2: // Cold
                {
                    c = Contrast(c, 1.08f);
                    float lum = Lum(c);
                    float sh = 1f - Mathf.SmoothStep(0f, 0.5f, lum);
                    float hi = Mathf.SmoothStep(0.5f, 1f, lum);
                    c.r += -0.05f + -0.03f * sh + -0.03f * hi;
                    c.g += 0.02f * sh + 0.04f * hi;
                    c.b += 0.07f + 0.11f * sh + 0.08f * hi;
                    return Clamp(Sat(c, 0.88f));
                }

                case 3: // Warm
                {
                    c = Contrast(c, 1.06f);
                    float lum = Lum(c);
                    float sh = 1f - Mathf.SmoothStep(0f, 0.5f, lum);
                    float hi = Mathf.SmoothStep(0.5f, 1f, lum);
                    c.r += 0.04f + 0.02f * sh + 0.06f * hi;
                    c.g += 0.01f * sh + 0.03f * hi;
                    c.b += -0.03f + -0.02f * sh + -0.05f * hi;
                    return Clamp(Sat(c, 0.97f));
                }

                case 4: // Sepia
                {
                    float l = Lum(c);
                    var s = new Color(l + 0.13f, l + 0.02f, l - 0.10f, 1f);
                    return Clamp(Contrast(s, 1.04f));
                }

                case 5: // Noir (presque N&B, contrasté)
                {
                    c = Sat(c, 0.15f);
                    return Clamp(Contrast(c, 1.22f));
                }

                case 6: // Vibrant
                {
                    c = Sat(c, 1.35f);
                    return Clamp(Contrast(c, 1.08f));
                }

                case 7: // Dark Fantasy (crush blacks, froid, désaturé)
                {
                    c = new Color(Mathf.Pow(c.r, 1.35f), Mathf.Pow(c.g, 1.35f), Mathf.Pow(c.b, 1.35f), 1f);
                    c = Sat(c, 0.78f);
                    c.r += -0.02f; c.b += 0.05f;
                    return Clamp(Contrast(c, 1.1f));
                }

                case 8: // Faded (lifted blacks, low contrast, pastel chaud)
                {
                    c = Contrast(c, 0.88f);
                    c.r = c.r * 0.86f + 0.10f;
                    c.g = c.g * 0.86f + 0.095f;
                    c.b = c.b * 0.86f + 0.08f;
                    return Clamp(Sat(c, 0.85f));
                }

                case 9: // Toxic (vert fantasy, ombres magenta)
                {
                    c = Contrast(c, 1.06f);
                    float lum = Lum(c);
                    float mid = Mathf.SmoothStep(0.2f, 0.6f, lum) * (1f - Mathf.SmoothStep(0.6f, 0.95f, lum));
                    float sh = 1f - Mathf.SmoothStep(0f, 0.45f, lum);
                    c.g += 0.08f * mid;
                    c.r += -0.03f * mid + 0.04f * sh;
                    c.b += -0.04f * mid + 0.03f * sh;
                    return Clamp(Sat(c, 1.05f));
                }

                default: return c;
            }
        }

        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;
            string parent = Path.GetDirectoryName(folder)?.Replace('\\', '/');
            if (string.IsNullOrEmpty(parent)) return;
            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
        }
    }
}
