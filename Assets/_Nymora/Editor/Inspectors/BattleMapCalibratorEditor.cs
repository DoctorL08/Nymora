using Nymora.Combat.View;
using UnityEditor;
using UnityEngine;

namespace Nymora.Editor.Inspectors
{
    /// <summary>
    /// POLISH-5e (17 mai) — Custom Editor pour BattleMapCalibrator avec aide visuelle :
    /// - Default Inspector (sliders Y/X/Scale serialized).
    /// - HelpBox d'instructions Play -> Edit persistance.
    /// - Bouton "Copy from Transform" : recharge les sliders depuis la position/scale courante.
    /// - Bouton "Apply Now" : force un push vers le Transform (utile si autoApply off).
    /// - Bouton "Reset to defaults" : remet Y=0, X=0, Scale=1.
    /// </summary>
    [CustomEditor(typeof(BattleMapCalibrator))]
    public class BattleMapCalibratorEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            EditorGUILayout.Space(8);
            EditorGUILayout.HelpBox(
                "Pour caler la map sur la grille :\n" +
                "  1. Active Gizmos en Scene View (bouton 'Gizmos' haut-droite).\n" +
                "  2. Selectionne le GameObject GridRenderer pour voir le losange jaune.\n" +
                "  3. Ajuste Y/X/Scale jusqu'a aligner la map sur les Gizmos.\n\n" +
                "Pour persister un calage fait pendant Play :\n" +
                "  Click-droit sur le header 'Battle Map Calibrator' -> 'Copy Component'.\n" +
                "  Stop Play. Click-droit -> 'Paste Component Values'. Sauve la scene.",
                MessageType.Info);

            EditorGUILayout.Space(4);

            var calibrator = (BattleMapCalibrator)target;

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Apply Now", GUILayout.Height(28)))
                {
                    Undo.RecordObject(calibrator.transform, "Apply Calibrator");
                    calibrator.ApplyToTransform();
                    EditorUtility.SetDirty(calibrator.transform);
                }
                if (GUILayout.Button("Copy from Transform", GUILayout.Height(28)))
                {
                    Undo.RecordObject(calibrator, "Copy From Transform");
                    calibrator.CopyFromTransform();
                    EditorUtility.SetDirty(calibrator);
                }
            }

            if (GUILayout.Button("Reset (Y=0, X=0, Scale=1)", GUILayout.Height(22)))
            {
                Undo.RecordObject(calibrator, "Reset Calibrator");
                var so = new SerializedObject(calibrator);
                so.FindProperty("_yOffset").floatValue = 0f;
                so.FindProperty("_xOffset").floatValue = 0f;
                so.FindProperty("_scale").floatValue = 1f;
                so.ApplyModifiedProperties();
                calibrator.ApplyToTransform();
                EditorUtility.SetDirty(calibrator);
            }
        }
    }
}
