using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Nymora.Editor.Setup
{
    /// <summary>
    /// Brique VP.2 — Color grading par LUT du hub (View-side only, aucun bump CombatRulesVersion).
    ///
    /// URP "Color Lookup" attend une LUT au format strip (N tall, N*N wide) en NON-sRGB,
    /// non compressee. Plutot que pomper un .cube filmmaker (licence douteuse + conversion),
    /// l'outil GENERE les LUT au bon format (0 licence) :
    /// - LUT_Neutral  : identite (aucun changement -> valide le pipeline).
    /// - LUT_Cinematic: teal-orange leger (contraste + ombres froides / lumieres chaudes).
    /// Puis branche le Color Lookup sur le profil Hub_PostFX (texture = cinematic, contribution
    /// modeste car ca s'empile sur le grading existant VP.1).
    ///
    /// Pour utiliser ta propre LUT CC0 : remplace la texture du Color Lookup par un strip
    /// 1024x32, import Default + sRGB OFF + Compression None + Mipmaps OFF + Bilinear/Clamp.
    ///
    /// Menu : Nymora > Setup > Setup Hub Color LUT (VP2).
    /// </summary>
    public static class HubLutTool
    {
        private const string Dir = "Assets/_Nymora/Settings/PostProcessing";
        private const string ProfilePath = Dir + "/Hub_PostFX.asset";
        private const string NeutralPath = Dir + "/LUT_Neutral.png";
        private const string CinematicPath = Dir + "/LUT_Cinematic.png";
        private const string ColdPath = Dir + "/LUT_Cold.png";
        private const string WarmPath = Dir + "/LUT_Warm.png";

        private const int LutSize = 32; // -> texture 1024x32

        private enum GradeMode { Neutral, Cinematic, Cold, Warm }

        [MenuItem("Nymora/Setup/Setup Hub Color LUT (VP2)", priority = 63)]
        private static void Setup()
        {
            if (Application.isPlaying)
            {
                EditorUtility.DisplayDialog("Hub Color LUT", "Impossible pendant Play Mode.", "OK");
                return;
            }

            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath);
            if (profile == null)
            {
                EditorUtility.DisplayDialog("Hub Color LUT",
                    "Profil introuvable : " + ProfilePath + "\nLance d'abord VP.1 (Setup Hub Visual Polish).", "OK");
                return;
            }

            var actions = new List<string>();

            EnsureLut(NeutralPath, GradeMode.Neutral, actions);
            var cinematic = EnsureLut(CinematicPath, GradeMode.Cinematic, actions);
            EnsureLut(ColdPath, GradeMode.Cold, actions);
            EnsureLut(WarmPath, GradeMode.Warm, actions);

            var lookup = GetOrAdd<ColorLookup>(profile);
            lookup.texture.overrideState = true;
            lookup.texture.value = cinematic;
            lookup.contribution.overrideState = true;
            lookup.contribution.value = 0.4f;
            actions.Add("- Color Lookup branche sur Hub_PostFX (LUT_Cinematic, contribution 0.4)");

            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();

            string summary = "VP.2 applique :\n\n" + string.Join("\n", actions) +
                "\n\nAjuste 'Contribution' du Color Lookup (0 = off, 1 = plein) dans Hub_PostFX,\nou swap la texture par LUT_Neutral / ta propre LUT CC0.";
            EditorUtility.DisplayDialog("Hub Color LUT (VP2)", summary, "OK");
            Debug.Log("[HubLutTool] " + summary);
        }

        private static Texture2D EnsureLut(string path, GradeMode mode, List<string> actions)
        {
            int w = LutSize * LutSize;
            int h = LutSize;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false, true); // linear

            float inv = 1f / (LutSize - 1);
            for (int b = 0; b < LutSize; b++)
            {
                for (int g = 0; g < LutSize; g++)
                {
                    for (int r = 0; r < LutSize; r++)
                    {
                        int x = b * LutSize + r; // tile par bleu, x=rouge dans la tile
                        int y = g;               // y=vert
                        var c = new Color(r * inv, g * inv, b * inv, 1f);
                        if (mode == GradeMode.Cinematic) c = GradeCinematic(c);
                        else if (mode == GradeMode.Cold) c = GradeCold(c);
                        else if (mode == GradeMode.Warm) c = GradeWarm(c);
                        tex.SetPixel(x, y, c);
                    }
                }
            }
            tex.Apply();

            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = false;                 // donnees LUT brutes, pas une couleur sRGB
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = 2048;               // ne pas downscaler le strip 1024
            importer.SaveAndReimport();

            actions.Add("- LUT generee : " + path + " (" + mode + ")");
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        /// <summary>Grade cinema leger : contraste + split tone teal/orange + legere desaturation.</summary>
        private static Color GradeCinematic(Color c)
        {
            const float contrast = 1.1f;
            c.r = (c.r - 0.5f) * contrast + 0.5f;
            c.g = (c.g - 0.5f) * contrast + 0.5f;
            c.b = (c.b - 0.5f) * contrast + 0.5f;

            float lum = 0.299f * c.r + 0.587f * c.g + 0.114f * c.b;
            float shadow = 1f - Mathf.SmoothStep(0f, 0.5f, lum);   // poids ombres
            // poids lumieres qui RETOMBE a 0 pres du blanc pur -> blancs restent blancs
            float high = Mathf.SmoothStep(0.5f, 0.82f, lum) * (1f - Mathf.SmoothStep(0.85f, 1f, lum));

            // ombres -> teal, lumieres -> orange (orange adouci)
            c.r += (-0.02f) * shadow + (0.035f) * high;
            c.g += (0.03f) * shadow + (0.02f) * high;
            c.b += (0.08f) * shadow + (-0.03f) * high;

            // legere desaturation vers la luminance
            c.r = Mathf.Lerp(lum, c.r, 0.92f);
            c.g = Mathf.Lerp(lum, c.g, 0.92f);
            c.b = Mathf.Lerp(lum, c.b, 0.92f);

            c.r = Mathf.Clamp01(c.r);
            c.g = Mathf.Clamp01(c.g);
            c.b = Mathf.Clamp01(c.b);
            c.a = 1f;
            return c;
        }

        /// <summary>Grade froid : balance globale cool + ombres bleu profond + lumieres cyan.</summary>
        private static Color GradeCold(Color c)
        {
            const float contrast = 1.08f;
            c.r = (c.r - 0.5f) * contrast + 0.5f;
            c.g = (c.g - 0.5f) * contrast + 0.5f;
            c.b = (c.b - 0.5f) * contrast + 0.5f;

            float lum = 0.299f * c.r + 0.587f * c.g + 0.114f * c.b;
            float shadow = 1f - Mathf.SmoothStep(0f, 0.5f, lum);
            float high = Mathf.SmoothStep(0.5f, 1f, lum);

            // refroidissement global (moins de rouge, plus de bleu)
            c.r += -0.05f;
            c.b += 0.07f;

            // ombres -> bleu profond, lumieres -> cyan froid (pas d'orange)
            c.r += (-0.03f) * shadow + (-0.03f) * high;
            c.g += (0.02f) * shadow + (0.04f) * high;
            c.b += (0.11f) * shadow + (0.08f) * high;

            // desaturation un peu plus marquee pour un rendu froid/sobre
            c.r = Mathf.Lerp(lum, c.r, 0.88f);
            c.g = Mathf.Lerp(lum, c.g, 0.88f);
            c.b = Mathf.Lerp(lum, c.b, 0.88f);

            c.r = Mathf.Clamp01(c.r);
            c.g = Mathf.Clamp01(c.g);
            c.b = Mathf.Clamp01(c.b);
            c.a = 1f;
            return c;
        }

        /// <summary>Grade chaud : blancs propres + lumieres orangees, peu desature (pas de teal en ombre).</summary>
        private static Color GradeWarm(Color c)
        {
            const float contrast = 1.06f;
            c.r = (c.r - 0.5f) * contrast + 0.5f;
            c.g = (c.g - 0.5f) * contrast + 0.5f;
            c.b = (c.b - 0.5f) * contrast + 0.5f;

            float lum = 0.299f * c.r + 0.587f * c.g + 0.114f * c.b;
            float shadow = 1f - Mathf.SmoothStep(0f, 0.5f, lum);
            float high = Mathf.SmoothStep(0.5f, 1f, lum);

            // rechauffement global leger (+rouge, -bleu)
            c.r += 0.04f;
            c.b += -0.03f;

            // lumieres -> orange, ombres a peine chaudes (pas de teal)
            c.r += (0.02f) * shadow + (0.06f) * high;
            c.g += (0.01f) * shadow + (0.03f) * high;
            c.b += (-0.02f) * shadow + (-0.05f) * high;

            // desaturation minime : on garde des couleurs propres
            c.r = Mathf.Lerp(lum, c.r, 0.97f);
            c.g = Mathf.Lerp(lum, c.g, 0.97f);
            c.b = Mathf.Lerp(lum, c.b, 0.97f);

            c.r = Mathf.Clamp01(c.r);
            c.g = Mathf.Clamp01(c.g);
            c.b = Mathf.Clamp01(c.b);
            c.a = 1f;
            return c;
        }

        private static T GetOrAdd<T>(VolumeProfile profile) where T : VolumeComponent
        {
            if (profile.TryGet<T>(out var comp)) return comp;
            comp = profile.Add<T>(false);
            AssetDatabase.AddObjectToAsset(comp, profile);
            return comp;
        }
    }
}
