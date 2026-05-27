using System.Collections.Generic;
using Nymora.Core.ScriptableObjects;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Nymora.Editor.Tools
{
    /// <summary>
    /// Brique 5.11 — Extrait les frames de prévisu (tooltip boutique) depuis les AnimatorControllers
    /// SE d'un skin combat (stage 0/1/2). Les controllers ne sont pas lisibles au runtime ; on lit
    /// donc ici (éditeur) les keyframes de sprite (m_Sprite) de chaque clip et on les sérialise dans
    /// CosmeticSkinDefinition.CombatPreview, rejouables par UISpriteAnimator dans l'UI.
    ///
    /// Itère TOUS les CosmeticSkinDefinition du projet ayant des controllers combat. Idempotent
    /// (réécrit CombatPreview à chaque run).
    ///
    /// Menu : Nymora > Setup > Extract Skin Combat Preview.
    /// </summary>
    public static class ExtractSkinCombatPreviewTool
    {
        // Tags d'anim reconnus (substring du nom de clip), mêmes règles que BuildAshenSovereignCombatAnimator.
        private static readonly string[] AnimTags = { "idle", "walk", "attack", "cast", "hurt", "death" };

        [MenuItem("Nymora/Setup/Extract Skin Combat Preview")]
        public static void Extract()
        {
            var guids = AssetDatabase.FindAssets("t:CosmeticSkinDefinition");
            int skinsDone = 0, clipsDone = 0;

            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var def = AssetDatabase.LoadAssetAtPath<CosmeticSkinDefinition>(path);
                if (def == null || !def.HasCombatControllers) continue;

                var preview = new List<SkinPreviewClip>();
                var stageCtrls = new[]
                {
                    def.Stage0ControllerSE,
                    def.Stage1ControllerSE,
                    def.Stage2ControllerSE,
                };

                for (int stage = 0; stage < stageCtrls.Length; stage++)
                {
                    var ctrl = stageCtrls[stage];
                    if (ctrl == null) continue;

                    foreach (var clip in ctrl.animationClips)
                    {
                        if (clip == null) continue;
                        string anim = ClassifyAnim(clip.name);
                        if (anim == null) continue;
                        // Évite les doublons (un controller peut référencer le même clip 2x).
                        if (preview.Exists(p => p.Stage == stage && p.Anim == anim)) continue;

                        var frames = ExtractSprites(clip, out float fps);
                        if (frames == null || frames.Length == 0) continue;

                        preview.Add(new SkinPreviewClip { Stage = stage, Anim = anim, Frames = frames, Fps = fps });
                        clipsDone++;
                    }
                }

                def.CombatPreview = preview;
                EditorUtility.SetDirty(def);
                skinsDone++;
                Debug.Log($"[ExtractSkinPreview] {def.name} : {preview.Count} prévisu(s) extraites.");
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[ExtractSkinPreview] Terminé : {skinsDone} skin(s), {clipsDone} clip(s) extrait(s).");
        }

        private static string ClassifyAnim(string clipName)
        {
            string lower = clipName.ToLowerInvariant();
            foreach (var tag in AnimTags)
                if (lower.Contains(tag)) return tag;
            return null;
        }

        // Extrait les sprites (binding m_Sprite avec le plus de keyframes) + déduit le fps.
        private static Sprite[] ExtractSprites(AnimationClip clip, out float fps)
        {
            fps = 8f;
            ObjectReferenceKeyframe[] best = null;
            foreach (var binding in AnimationUtility.GetObjectReferenceCurveBindings(clip))
            {
                if (binding.propertyName != "m_Sprite") continue;
                var keys = AnimationUtility.GetObjectReferenceCurve(clip, binding);
                if (keys != null && (best == null || keys.Length > best.Length)) best = keys;
            }
            if (best == null || best.Length == 0) return null;

            var sprites = new List<Sprite>(best.Length);
            foreach (var k in best)
                if (k.value is Sprite s && s != null) sprites.Add(s);
            if (sprites.Count == 0) return null;

            // fps déduit de l'intervalle entre 2 keyframes (sinon clip.frameRate, sinon 8).
            if (best.Length >= 2)
            {
                float dt = best[1].time - best[0].time;
                if (dt > 0.0001f) fps = 1f / dt;
            }
            else if (clip.frameRate > 0f) fps = clip.frameRate;

            return sprites.ToArray();
        }
    }
}
