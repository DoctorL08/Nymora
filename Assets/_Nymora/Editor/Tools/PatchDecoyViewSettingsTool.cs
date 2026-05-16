using System.Linq;
using Nymora.Combat.View;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Nymora.Editor.Tools
{
    /// <summary>
    /// 3.6.g — Force les valeurs Scale + Y Offset sur le composant DecoyView de la scene
    /// courante pour qu'elles correspondent au prefab Ghostra (RestructureGhostraPrefabTool).
    ///
    /// Pourquoi ce tool : Lorenzo a deja ajoute DecoyView dans 30_CombatIA avec les
    /// anciennes valeurs (Scale 1 / Y 0). Modifier les SerializeField defaults dans le
    /// code ne change pas les valeurs serialisees dans la scene. Ce tool force la
    /// resync proprement.
    ///
    /// Idempotent : peut etre relance, ecrase les valeurs courantes.
    /// Marque la scene comme dirty pour que Ctrl+S la sauve.
    /// </summary>
    public static class PatchDecoyViewSettingsTool
    {
        // Doit rester aligne avec RestructureGhostraPrefabTool (DefaultScale / DefaultYOffset).
        // Valeurs finales calibrees par Lorenzo en Play Mode (16 mai 2026).
        private static readonly Vector3 TargetScale = new Vector3(1.16f, 1.16f, 1f);
        private const float TargetYOffset = -0.22f;
        // 3.7.a.i.4 — tint bleu pâle + alpha 0.85 pour distinguer les leurres cote joueur.
        private const float TargetAlpha = 0.85f;
        private static readonly Color TargetTint = new Color(0.7f, 0.88f, 1.0f, 1.0f);

        // 3.7.a.i.4 — sprite idle Ghostra (statique) auto-bind sur _placeholderSprite.
        // Path du .aseprite source : Ghostra stage0 SE, premiere frame = idle.
        private const string GhostraIdleAsepritePath =
            "Assets/_Nymora/Art/Sprites/Ghostra/Base/sources/GHOSTRA_animation_stage0_SE.aseprite";
        // 3.7.a.i.4 — AnimatorController idle-only auto-bind sur _decoyIdleController.
        // Reutilise le controller stage0_SE existant : son default state est Idle, et comme
        // on ne trigger jamais Walk/Cast/Attack sur les leurres, il boucle en Idle.
        private const string GhostraIdleControllerPath =
            "Assets/_Nymora/Animations/Ghostra/GhostraStage0_SE.controller";

        [MenuItem("Nymora/Setup/Patch DecoyView Settings (Scale + Y Offset)")]
        public static void Run()
        {
            var allViews = Object.FindObjectsByType<DecoyView>(FindObjectsSortMode.None);
            if (allViews == null || allViews.Length == 0)
            {
                Debug.LogError("[PatchDecoyView] Aucun DecoyView trouve dans la scene courante. " +
                               "Ajoute le composant via Hierarchy avant de relancer ce tool.");
                return;
            }

            int patched = 0;
            foreach (var view in allViews)
            {
                var so = new SerializedObject(view);
                var scaleProp = so.FindProperty("_decoyScale");
                var yProp = so.FindProperty("_decoyYOffset");
                var alphaProp = so.FindProperty("_decoyAlpha");
                var tintProp = so.FindProperty("_decoyTint");
                var spriteProp = so.FindProperty("_placeholderSprite");
                if (scaleProp == null || yProp == null)
                {
                    Debug.LogWarning($"[PatchDecoyView] Champs _decoyScale / _decoyYOffset introuvables sur {view.name}. " +
                                     "Recompile DecoyView.cs et relance.");
                    continue;
                }

                Vector3 oldScale = scaleProp.vector3Value;
                float oldY = yProp.floatValue;
                scaleProp.vector3Value = TargetScale;
                yProp.floatValue = TargetYOffset;
                // 3.7.a.i.4 — applique aussi tint + alpha si les champs existent (recompile recent).
                if (alphaProp != null) alphaProp.floatValue = TargetAlpha;
                if (tintProp != null) tintProp.colorValue = TargetTint;
                // 3.7.a.i.4 — auto-bind sprite idle Ghostra stage0 SE (1er sprite du aseprite).
                if (spriteProp != null)
                {
                    Sprite idleSprite = LoadGhostraIdleSprite();
                    if (idleSprite != null)
                    {
                        spriteProp.objectReferenceValue = idleSprite;
                    }
                    else
                    {
                        Debug.LogWarning($"[PatchDecoyView] Sprite idle Ghostra introuvable a {GhostraIdleAsepritePath}. " +
                                         "Bind manuellement _placeholderSprite dans l'Inspector avec le 1er frame du idle stage0_SE.");
                    }
                }
                // 3.7.a.i.4 — auto-bind AnimatorController idle-only sur _decoyIdleController.
                // C'est lui qui drive l'animation idle (perso anime sur place) sur chaque leurre.
                var controllerProp = so.FindProperty("_decoyIdleController");
                if (controllerProp != null)
                {
                    var ctrl = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(GhostraIdleControllerPath);
                    if (ctrl != null)
                    {
                        controllerProp.objectReferenceValue = ctrl;
                    }
                    else
                    {
                        Debug.LogWarning($"[PatchDecoyView] AnimatorController idle Ghostra introuvable a {GhostraIdleControllerPath}. " +
                                         "Lance 'Nymora > Setup > Build Ghostra Animator' d'abord, puis re-relance ce tool.");
                    }
                }
                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(view);
                patched++;

                Debug.Log($"[PatchDecoyView] OK '{view.name}' : Scale {oldScale} -> {TargetScale}, Y {oldY} -> {TargetYOffset}, Alpha -> {TargetAlpha}, Tint -> {TargetTint}, IdleSprite auto-bind");
            }

            if (patched > 0)
            {
                EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
                Debug.Log($"[PatchDecoyView] {patched} composant(s) patche(s). Ctrl+S pour sauver la scene.");
            }
        }

        /// <summary>
        /// Charge le 1er sprite du .aseprite stage0 SE Ghostra (frame idle).
        /// Retourne null si le fichier n'existe pas ou ne contient pas de Sprite.
        /// </summary>
        private static Sprite LoadGhostraIdleSprite()
        {
            var assets = AssetDatabase.LoadAllAssetsAtPath(GhostraIdleAsepritePath);
            if (assets == null) return null;
            // Filtre les Sprites et prend le 1er (ordre lexicographique des sub-assets =
            // frames idle puis walk puis attack etc, donc idle frame 0 vient en premier).
            var sprites = assets.OfType<Sprite>().OrderBy(s => s.name).ToArray();
            if (sprites.Length == 0) return null;
            // Prefer un sprite avec "idle" dans le nom si dispo.
            foreach (var s in sprites)
            {
                if (s.name.ToLowerInvariant().Contains("idle")) return s;
            }
            return sprites[0];
        }
    }
}
