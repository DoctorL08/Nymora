#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Nymora.Hub
{
    /// <summary>
    /// POLISH KYAMI — tuner de lights LIVE en Play Mode (éditeur uniquement, strippé du build).
    /// Permet de doser, en voyant les vrais persos, le « fill » Global, l'intensité + le rayon des
    /// Point lights, et la Normal Map Distance (force du relief). Spawné via
    /// « Nymora > Setup > Polish Kyami > Spawn Light Tuner (Play Mode) ».
    ///
    /// Persistance : [S] mémorise les valeurs absolues de CHAQUE light (par chemin de hiérarchie)
    /// dans SessionState ; <see cref="Nymora.Editor.Setup"/> les réapplique automatiquement à la
    /// scène à la sortie du Play Mode et sauve. Aucune valeur n'est écrasée sans ton SAVE.
    ///
    /// Sliders : Global · Point intensité (×) · Point rayon (×) · Normal Distance.
    /// [S] sauver · [K] retirer.
    /// </summary>
    public sealed class LightTuner : MonoBehaviour
    {
        public const string PendingKey = "Nymora.LightTuner.PendingJson";

        [System.Serializable] public struct Entry { public string path; public float intensity; public float radius; public float normalDist; public bool isGlobal; }
        [System.Serializable] public class Buffer { public List<Entry> items = new List<Entry>(); }

        private readonly List<Light2D> _points = new List<Light2D>();
        private readonly List<Light2D> _globals = new List<Light2D>();
        private readonly Dictionary<Light2D, float> _baseIntensity = new Dictionary<Light2D, float>();
        private readonly Dictionary<Light2D, float> _baseRadius = new Dictionary<Light2D, float>();

        private float _globalIntensity = 0.4f;
        private float _pointIntensityMul = 1f;
        private float _pointRadiusMul = 1f;
        private float _normalDist = 1.5f;
        private string _status = "";

        private void Start()
        {
            foreach (var l in FindObjectsOfType<Light2D>(true))
            {
                if (l.lightType == Light2D.LightType.Global) { _globals.Add(l); }
                else if (l.lightType == Light2D.LightType.Point)
                {
                    _points.Add(l);
                    _baseIntensity[l] = l.intensity;
                    _baseRadius[l] = l.pointLightOuterRadius;
                }
            }
            if (_globals.Count > 0) _globalIntensity = _globals[0].intensity;
            if (_points.Count > 0) _normalDist = GetNormalDist(_points[0]);
        }

        private void Update()
        {
            // Applique en live.
            foreach (var g in _globals) if (g != null) g.intensity = _globalIntensity;
            foreach (var p in _points)
            {
                if (p == null) continue;
                p.intensity = _baseIntensity[p] * _pointIntensityMul;
                p.pointLightOuterRadius = _baseRadius[p] * _pointRadiusMul;
                SetNormalDist(p, _normalDist);
            }

            if (Input.GetKeyDown(KeyCode.S)) SaveToSession();
            if (Input.GetKeyDown(KeyCode.K)) Destroy(gameObject);
        }

        private void OnGUI()
        {
            const float w = 460f;
            GUILayout.BeginArea(new Rect(10, 10, w, 220), GUI.skin.box);
            GUILayout.Label("<b>LIGHT TUNER</b>  ·  [S] sauver  ·  [K] retirer");
            Row($"Global (fill plat)  {_globalIntensity:0.00}", ref _globalIntensity, 0f, 1f);
            Row($"Point intensité  ×{_pointIntensityMul:0.00}", ref _pointIntensityMul, 0.2f, 5f);
            Row($"Point rayon  ×{_pointRadiusMul:0.00}", ref _pointRadiusMul, 0.5f, 8f);
            Row($"Normal Distance (relief)  {_normalDist:0.00}", ref _normalDist, 0.3f, 6f);
            GUILayout.Space(4);
            GUILayout.Label($"{_points.Count} point · {_globals.Count} global   {_status}");
            if (GUILayout.Button("SAUVER (appliqué à la sortie du Play)")) SaveToSession();
            GUILayout.EndArea();
        }

        private static void Row(string label, ref float val, float min, float max)
        {
            GUILayout.Label(label);
            val = GUILayout.HorizontalSlider(val, min, max);
        }

        private void SaveToSession()
        {
            var buf = new Buffer();
            foreach (var g in _globals)
                if (g != null) buf.items.Add(new Entry { path = PathOf(g.transform), intensity = g.intensity, radius = -1f, normalDist = GetNormalDist(g), isGlobal = true });
            foreach (var p in _points)
                if (p != null) buf.items.Add(new Entry { path = PathOf(p.transform), intensity = p.intensity, radius = p.pointLightOuterRadius, normalDist = GetNormalDist(p), isGlobal = false });

            SessionState.SetString(PendingKey, JsonUtility.ToJson(buf));
            _status = $"✓ sauvé ({buf.items.Count} lights) — sors du Play";
            Debug.Log($"[LightTuner] {buf.items.Count} lights mémorisées — appliquées à la sortie du Play Mode.");
        }

        public static string PathOf(Transform t)
        {
            var stack = new Stack<string>();
            while (t != null) { stack.Push(t.name); t = t.parent; }
            return string.Join("/", stack);
        }

        public static float GetNormalDist(Light2D l)
        {
            var p = new SerializedObject(l).FindProperty("m_NormalMapDistance");
            return p != null ? p.floatValue : 3f;
        }

        public static void SetNormalDist(Light2D l, float v)
        {
            var so = new SerializedObject(l);
            var p = so.FindProperty("m_NormalMapDistance");
            if (p != null) { p.floatValue = v; so.ApplyModifiedPropertiesWithoutUndo(); }
        }
    }
}
#endif
