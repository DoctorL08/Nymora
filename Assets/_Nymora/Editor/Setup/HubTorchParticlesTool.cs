using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Nymora.Editor.Setup
{
    /// <summary>
    /// Outil opt-in — ajoute des particules discretes aux torches du hub (View only) :
    /// - Fumee grise qui monte doucement (haut).
    /// - Petits eclats orange sur les cotes.
    ///
    /// Cree un sprite de particule (point doux, 0 licence) + un materiau Sprites/Default
    /// (alpha, pas de blowout), puis ajoute un child "TorchParticles" (2 ParticleSystem) a
    /// chaque Point Light du groupe "Scene Lighting 2D" dont le nom contient "Torch".
    ///
    /// Debits volontairement faibles ("petit plus", pas too much). Idempotent.
    /// Ne touche pas aux lights elles-memes (cf feedback-dont-overwrite-light-values).
    ///
    /// Menu : Nymora > Setup > Add Torch Particles to Hub.
    /// </summary>
    public static class HubTorchParticlesTool
    {
        private const string ScenePath = "Assets/_Nymora/Scenes/10_CommunityHub.unity";
        private const string LightingRootName = "Scene Lighting 2D";
        private const string VfxDir = "Assets/_Nymora/Art/VFX";
        private const string DotPath = VfxDir + "/particle_dot.png";
        private const string MatPath = VfxDir + "/TorchParticle.mat";
        private const string ChildName = "TorchParticles";

        [MenuItem("Nymora/Setup/Add Torch Particles to Hub", priority = 67)]
        private static void Add()
        {
            if (Application.isPlaying)
            {
                EditorUtility.DisplayDialog("Torch Particles", "Impossible pendant Play Mode.", "OK");
                return;
            }

            var scene = EditorSceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.path.EndsWith("10_CommunityHub.unity"))
            {
                if (!EditorUtility.DisplayDialog("Torch Particles",
                        $"Scene active : {scene.path}\nAttendu : {ScenePath}\n\nOuvrir 10_CommunityHub ?",
                        "Ouvrir", "Annuler"))
                    return;
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }

            var root = GameObject.Find(LightingRootName);
            if (root == null)
            {
                EditorUtility.DisplayDialog("Torch Particles", "Groupe '" + LightingRootName + "' introuvable.", "OK");
                return;
            }

            var mat = EnsureMaterial();
            int sortingLayer = SortingLayer.NameToID("Default");

            int count = 0;
            foreach (var light in root.GetComponentsInChildren<Light2D>(true))
            {
                if (light.lightType != Light2D.LightType.Point) continue;
                if (!light.name.ToLowerInvariant().Contains("torch")) continue; // torches uniquement
                if (FindChild(light.transform, ChildName) != null) continue;

                BuildTorchParticles(light.transform, mat, sortingLayer);
                count++;
            }

            EditorSceneManager.MarkSceneDirty(scene);

            string summary = count == 0
                ? "Aucune torche sans particules (ou aucune light nommee 'Torch')."
                : $"Particules ajoutees a {count} torches (fumee + eclats). Ctrl+S pour sauver.\n" +
                  "Regle les debits (Emission > Rate over Time) si tu veux + ou - de particules.";
            EditorUtility.DisplayDialog("Torch Particles", summary, "OK");
            Debug.Log("[HubTorchParticlesTool] " + summary);
        }

        private static void BuildTorchParticles(Transform parent, Material mat, int sortingLayer)
        {
            var rootGo = new GameObject(ChildName);
            rootGo.transform.SetParent(parent, false);
            rootGo.transform.localPosition = Vector3.zero;

            BuildSmoke(rootGo.transform, mat, sortingLayer);
            BuildSparks(rootGo.transform, mat, sortingLayer);
        }

        private static void BuildSmoke(Transform parent, Material mat, int sortingLayer)
        {
            var go = new GameObject("Smoke");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0f, 0.3f, 0f); // haut de la flamme

            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop();

            var main = ps.main;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.8f, 2.6f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.05f, 0.12f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.14f, 0.24f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.45f, 0.45f, 0.47f, 0.22f));
            main.gravityModifier = new ParticleSystem.MinMaxCurve(0f);
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.maxParticles = 24;
            main.playOnAwake = true;

            var em = ps.emission;
            em.enabled = true;
            em.rateOverTime = 3f; // discret

            var sh = ps.shape;
            sh.enabled = true;
            sh.shapeType = ParticleSystemShapeType.Circle;
            sh.radius = 0.05f;
            sh.radiusThickness = 1f;

            var vol = ps.velocityOverLifetime;
            vol.enabled = true;
            vol.space = ParticleSystemSimulationSpace.Local;
            vol.y = new ParticleSystem.MinMaxCurve(0.35f, 0.55f);
            vol.x = new ParticleSystem.MinMaxCurve(-0.06f, 0.06f);

            var col = ps.colorOverLifetime;
            col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(FadeInOut());

            var sol = ps.sizeOverLifetime;
            sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                new Keyframe(0f, 0.6f), new Keyframe(1f, 1.5f)));

            ConfigureRenderer(ps, mat, sortingLayer, 200);
        }

        private static void BuildSparks(Transform parent, Material mat, int sortingLayer)
        {
            var go = new GameObject("Sparks");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0f, 0.12f, 0f);

            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop();

            var main = ps.main;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.9f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.4f, 0.9f); // ejecte sur les cotes
            main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.06f);
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(1f, 0.62f, 0.25f, 1f), new Color(1f, 0.78f, 0.4f, 1f));
            main.gravityModifier = new ParticleSystem.MinMaxCurve(0.25f); // retombent un peu
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.maxParticles = 30;
            main.playOnAwake = true;

            var em = ps.emission;
            em.enabled = true;
            em.rateOverTime = 4f; // discret

            var sh = ps.shape;
            sh.enabled = true;
            sh.shapeType = ParticleSystemShapeType.Circle; // emission radiale = cotes
            sh.radius = 0.06f;
            sh.radiusThickness = 1f;

            var vol = ps.velocityOverLifetime;
            vol.enabled = true;
            vol.space = ParticleSystemSimulationSpace.Local;
            vol.y = new ParticleSystem.MinMaxCurve(0.1f, 0.4f); // un peu vers le haut

            var col = ps.colorOverLifetime;
            col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(FadeOut());

            var sol = ps.sizeOverLifetime;
            sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                new Keyframe(0f, 1f), new Keyframe(1f, 0f)));

            ConfigureRenderer(ps, mat, sortingLayer, 201);
        }

        private static void ConfigureRenderer(ParticleSystem ps, Material mat, int sortingLayer, int order)
        {
            var r = ps.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Billboard;
            r.sharedMaterial = mat;
            r.sortingLayerID = sortingLayer;
            r.sortingOrder = order;
        }

        private static Gradient FadeInOut()
        {
            var g = new Gradient();
            g.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[]
                {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(1f, 0.18f),
                    new GradientAlphaKey(0.7f, 0.55f),
                    new GradientAlphaKey(0f, 1f),
                });
            return g;
        }

        private static Gradient FadeOut()
        {
            var g = new Gradient();
            g.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(1f, 0.4f, 0.15f), 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
            return g;
        }

        private static Material EnsureMaterial()
        {
            var existing = AssetDatabase.LoadAssetAtPath<Material>(MatPath);
            if (existing != null) return existing;

            var dot = EnsureDotTexture();
            var mat = new Material(Shader.Find("Sprites/Default"));
            if (dot != null) mat.mainTexture = dot;
            AssetDatabase.CreateAsset(mat, MatPath);
            return mat;
        }

        private static Texture2D EnsureDotTexture()
        {
            var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(DotPath);
            if (existing != null) return existing;

            if (!AssetDatabase.IsValidFolder(VfxDir))
                AssetDatabase.CreateFolder("Assets/_Nymora/Art", "VFX");

            const int size = 64;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            float c = (size - 1) / 2f;
            float rad = size / 2f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / rad;
                    float a = Mathf.SmoothStep(1f, 0f, Mathf.Clamp01(d));
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            }
            tex.Apply();
            File.WriteAllBytes(DotPath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(DotPath, ImportAssetOptions.ForceUpdate);

            var importer = (TextureImporter)AssetImporter.GetAtPath(DotPath);
            importer.textureType = TextureImporterType.Default;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();

            return AssetDatabase.LoadAssetAtPath<Texture2D>(DotPath);
        }

        private static Transform FindChild(Transform parent, string name)
        {
            foreach (Transform c in parent)
                if (c.name == name) return c;
            return null;
        }
    }
}
