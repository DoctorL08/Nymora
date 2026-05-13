using System.Collections.Generic;
using System.Linq;
using Nymora.Combat.View.Animation;
using UnityEditor;
using UnityEngine;

namespace Nymora.Editor.Tools
{
    /// <summary>
    /// Polish 3.3.d — Auto-bind les sub-sprites des assets Nightseer / Colossar dans les
    /// ScriptableObject Libraries (MarkSpriteLibrary, VFXSpriteLibrary).
    ///
    /// Hypothese : les sheets sont deja slicees (run "Nymora > Setup > Auto-slice Frame Sheets"
    /// AVANT ce tool). Si les sub-sprites n'existent pas (sheet pas sliced ou .gif animes
    /// mal importes), on log un warning par asset manquant.
    ///
    /// Tool simplement ergonomique : evite a Lorenzo de drag-and-drop 4+4+12 sub-sprites
    /// manuellement dans les .asset.
    ///
    /// Idempotent : re-run sans risque (ecrase les arrays existants).
    /// </summary>
    public static class BindClassVisualsTool
    {
        // Chemins durs Nymora — coherent avec AutoSliceFrameSheetsTool.
        private const string MarkLibAssetPath  = "Assets/_Nymora/ScriptableObjects/Combat/MarkSpriteLibrary.asset";
        private const string VfxLibAssetPath   = "Assets/_Nymora/ScriptableObjects/Combat/VFXSpriteLibrary.asset";

        // Assets-sources (sheets a slicer en amont).
        private const string TraqueSheet    = "Assets/_Nymora/Art/Sprites/Nightseer/Marks/marque_traque_oeil_pulsant_4frame.gif";
        private const string EmpreinteSheet = "Assets/_Nymora/Art/Sprites/Nightseer/Marks/marque_empreinte_4frame.gif";
        private const string EffondrementSheet = "Assets/_Nymora/Art/Sprites/Colossar/VFX/VFX_strates_qui_sempilent_12frame.gif";

        [MenuItem("Nymora/Setup/Bind NS+CO Visuals")]
        public static void Run()
        {
            int bound = 0;

            // === MarkSpriteLibrary : Traque + Empreinte (Nightseer) ===
            var markLib = AssetDatabase.LoadAssetAtPath<MarkSpriteLibrary>(MarkLibAssetPath);
            if (markLib == null)
            {
                Debug.LogError($"[Nymora.BindVisuals] MarkSpriteLibrary introuvable a {MarkLibAssetPath}");
            }
            else
            {
                var so = new SerializedObject(markLib);
                if (BindFramesToProperty(so, "_traqueFrames", TraqueSheet))    bound++;
                if (BindFramesToProperty(so, "_empreinteFrames", EmpreinteSheet)) bound++;
                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(markLib);
            }

            // === VFXSpriteLibrary : Effondrement (Colossar) ===
            var vfxLib = AssetDatabase.LoadAssetAtPath<VFXSpriteLibrary>(VfxLibAssetPath);
            if (vfxLib == null)
            {
                Debug.LogError($"[Nymora.BindVisuals] VFXSpriteLibrary introuvable a {VfxLibAssetPath}");
            }
            else
            {
                var so = new SerializedObject(vfxLib);
                if (BindFramesToProperty(so, "_effondrementFrames", EffondrementSheet)) bound++;
                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(vfxLib);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[Nymora.BindVisuals] Termine : {bound} arrays bound. " +
                      "Lance d'abord 'Auto-slice Frame Sheets' si les sub-sprites n'existent pas encore.");
        }

        /// <summary>
        /// Trouve tous les Sprites a l'interieur d'une texture/gif slicee, les trie par
        /// position X (left-to-right), et les ecrit dans la propriete Sprite[] cible.
        /// </summary>
        private static bool BindFramesToProperty(SerializedObject so, string propName, string sheetPath)
        {
            // Tous les sub-assets de la texture (sub-sprites apres slicing) sont chargeables via
            // LoadAllAssetsAtPath. On filtre uniquement les Sprite et on trie par rect.x.
            Object[] subAssets = AssetDatabase.LoadAllAssetsAtPath(sheetPath);
            if (subAssets == null || subAssets.Length == 0)
            {
                Debug.LogWarning($"[Nymora.BindVisuals] {propName}: aucun asset trouve a {sheetPath}");
                return false;
            }

            var sprites = subAssets.OfType<Sprite>()
                                   .OrderBy(s => s.rect.x)
                                   .ToArray();

            if (sprites.Length == 0)
            {
                Debug.LogWarning($"[Nymora.BindVisuals] {propName}: {sheetPath} n'a pas de Sprite (slicing fait ?)");
                return false;
            }

            var prop = so.FindProperty(propName);
            if (prop == null)
            {
                Debug.LogError($"[Nymora.BindVisuals] Property '{propName}' introuvable sur {so.targetObject.name}");
                return false;
            }

            prop.arraySize = sprites.Length;
            for (int i = 0; i < sprites.Length; i++)
            {
                prop.GetArrayElementAtIndex(i).objectReferenceValue = sprites[i];
            }

            Debug.Log($"[Nymora.BindVisuals] {propName}: {sprites.Length} sprites bound depuis {System.IO.Path.GetFileName(sheetPath)}");
            return true;
        }
    }
}
