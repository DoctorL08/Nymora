#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering.Universal;

namespace Nymora.Hub
{
    /// <summary>
    /// POLISH KYAMI — outil de DEBUG (éditeur uniquement, strippé du build) pour VÉRIFIER que les
    /// normal maps réagissent à la lumière. Spawné en Play Mode via
    /// « Nymora > Setup > Polish Kyami > Spawn Normal Map Debug Light (Play Mode) ».
    ///
    /// Crée un Point Light2D FORT et LARGE qui suit la souris (NormalMapDistance court → relief
    /// marqué). En le baladant sur un perso ou le sol, l'éclairage doit varier avec le relief des
    /// normals : c'est la preuve que le pipeline fonctionne. NE MODIFIE AUCUNE light existante :
    /// c'est un GameObject temporaire, détruit en sortie de Play Mode (rien n'est sauvegardé).
    ///
    /// Contrôles : la lumière suit la souris · [L] on/off (comparer avec/sans) · [K] supprimer.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class NormalMapDebugLight : MonoBehaviour
    {
        private Light2D _light;
        private Camera _cam;
        private bool _on = true;
        private bool _normalsOn = true;
        private bool _frozen = false;

        private void Start()
        {
            _cam = Camera.main;
            if (_cam == null) _cam = FindObjectOfType<Camera>();

            _light = gameObject.GetComponent<Light2D>();
            if (_light == null) _light = gameObject.AddComponent<Light2D>();

            // Configuré via SerializedObject (noms de champs fiables toutes versions URP 2022).
            var so = new SerializedObject(_light);
            SetEnum(so, "m_LightType", 3);          // Point
            SetFloat(so, "m_Intensity", 1.6f);       // fort (dépasse l'ambiance plate)
            SetColor(so, "m_Color", Color.white);
            SetFloat(so, "m_PointLightOuterRadius", 6f);
            SetFloat(so, "m_PointLightInnerRadius", 0.4f);
            SetFloat(so, "m_PointLightInnerAngle", 360f);
            SetFloat(so, "m_PointLightOuterAngle", 360f);
            SetFloat(so, "m_FalloffIntensity", 0.3f);
            SetEnum(so, "m_NormalMapQuality", 2);    // Accurate / High
            SetFloat(so, "m_NormalMapDistance", 0.6f); // court → relief marqué
            SetInt(so, "m_BlendStyleIndex", 1);      // Additive (révèle les normals)
            // CIBLE TOUS LES SORTING LAYERS (sinon la lumière n'éclaire que "Default" et ignore les
            // persos sur "Personnages" → toggler ses normals ne ferait rien sur eux).
            var layersProp = so.FindProperty("m_ApplyToSortingLayers");
            if (layersProp != null)
            {
                var all = SortingLayer.layers;
                layersProp.arraySize = all.Length;
                for (int i = 0; i < all.Length; i++)
                    layersProp.GetArrayElementAtIndex(i).intValue = all[i].id;
            }
            so.ApplyModifiedPropertiesWithoutUndo();

            transform.position = new Vector3(0f, 0f, 0f);
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.L) && _light != null)
            {
                _on = !_on;
                _light.enabled = _on;
            }
            // TEST DÉCISIF : active/désactive UNIQUEMENT la lecture des normals par cette lumière
            // (NormalMapQuality Accurate <-> Disabled). Même position, même intensité → si le perso
            // change d'éclairage en pressant [N], les normals agissent.
            if (Input.GetKeyDown(KeyCode.N) && _light != null)
            {
                _normalsOn = !_normalsOn;
                var so = new SerializedObject(_light);
                SetEnum(so, "m_NormalMapQuality", _normalsOn ? 2 : 0);
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            // Fige/défige le suivi souris pour pouvoir comparer tranquillement.
            if (Input.GetKeyDown(KeyCode.F)) _frozen = !_frozen;
            if (Input.GetKeyDown(KeyCode.K))
            {
                Destroy(gameObject);
                return;
            }

            if (!_frozen && _cam != null)
            {
                Vector3 m = Input.mousePosition;
                m.z = Mathf.Abs(_cam.transform.position.z);
                var w = _cam.ScreenToWorldPoint(m);
                w.z = 0f;
                transform.position = w;
            }
        }

        private void OnGUI()
        {
            var style = new GUIStyle(GUI.skin.box) { fontSize = 13, alignment = TextAnchor.MiddleLeft };
            GUI.color = Color.white;
            GUI.Box(new Rect(10, 10, 520, 86),
                $"  NORMAL MAP DEBUG LIGHT  ·  lumière {(_on ? "ON" : "OFF")}  ·  suivi {( _frozen ? "FIGÉ" : "souris")}\n" +
                $"  >>> NORMALS : {(_normalsOn ? "ON (relief)" : "OFF (plat)")}  <<<\n" +
                "  1) place la lumière sur un perso  ·  [F] fige  ·  2) presse [N] plusieurs fois\n" +
                "  Si l'éclairage du perso CHANGE entre N-ON et N-OFF → les normals agissent.\n" +
                "  [L] lumière on/off   ·   [K] supprimer", style);
        }

        private static void SetFloat(SerializedObject so, string p, float v) { var pr = so.FindProperty(p); if (pr != null) pr.floatValue = v; }
        private static void SetInt(SerializedObject so, string p, int v) { var pr = so.FindProperty(p); if (pr != null) pr.intValue = v; }
        private static void SetEnum(SerializedObject so, string p, int v) { var pr = so.FindProperty(p); if (pr != null) pr.enumValueIndex = v; }
        private static void SetColor(SerializedObject so, string p, Color v) { var pr = so.FindProperty(p); if (pr != null) pr.colorValue = v; }
    }
}
#endif
