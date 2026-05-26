using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Nymora.Editor.Windows
{
    /// <summary>
    /// Aperçu ASSEMBLÉ du bandeau ornemental de tooltip "ruban" (brique style-clan banner).
    ///
    /// Compose en live : bout de ruban gauche + plaque centrale (pseudo) + bout droit (miroir),
    /// avec le NOM DE CLAN encadré par une fioriture à gauche et son miroir à droite
    /// (— ◆  CLAN  ◆ —). Plus d'emblème ni de médaillon central. Tout est teintable
    /// (couleur ornement / fond plaque) et re-skinnable plus tard par les bannières boutique.
    ///
    /// Read-only (validation visuelle). Accès : Nymora > Setup > UI Menu > Clan Banner Preview.
    /// </summary>
    public sealed class ClanBannerPreviewWindow : EditorWindow
    {
        private const string BannerFolder = "Assets/_Nymora/Resources/UI/Icons/Banner";

        private readonly List<(string name, Sprite sprite)> _ends = new List<(string, Sprite)>();
        private readonly List<(string name, Sprite sprite)> _flourishes = new List<(string, Sprite)>();

        private int _endIdx, _flourishIdx;
        private Color _ornamentColor = new Color(0.83f, 0.69f, 0.36f, 1f);  // or
        private Color _plateColor = new Color(0.10f, 0.10f, 0.13f, 0.96f);  // plaque sombre
        private string _pseudo = "Nocturn";
        private string _clanTag = "Les Sans-Visage";
        private string _title = "l'Inébranlable";

        [MenuItem("Nymora/Setup/UI Menu/Clan Banner Preview")]
        public static void Open()
        {
            var w = GetWindow<ClanBannerPreviewWindow>("Clan Banner");
            w.minSize = new Vector2(560f, 520f);
            w.Reload();
        }

        private void OnEnable() => Reload();

        private void Reload()
        {
            Gather("ui_banner_end_", _ends);
            Gather("ui_banner_flourish_", _flourishes);
            _endIdx = Mathf.Clamp(_endIdx, 0, Mathf.Max(0, _ends.Count - 1));
            _flourishIdx = Mathf.Clamp(_flourishIdx, 0, Mathf.Max(0, _flourishes.Count - 1));
            Repaint();
        }

        private static void Gather(string prefix, List<(string, Sprite)> dst)
        {
            dst.Clear();
            if (!Directory.Exists(BannerFolder)) return;
            foreach (var guid in AssetDatabase.FindAssets("t:Sprite", new[] { BannerFolder }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                string name = Path.GetFileNameWithoutExtension(path);
                if (!name.StartsWith(prefix)) continue;
                var sp = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (sp != null) dst.Add((name.Substring(prefix.Length), sp));
            }
            dst.Sort((a, b) => string.CompareOrdinal(a.Item1, b.Item1));
        }

        private void OnGUI()
        {
            if (_ends.Count == 0 || _flourishes.Count == 0)
            {
                EditorGUILayout.HelpBox(
                    "Pièces de bandeau introuvables dans " + BannerFolder +
                    ".\nLance python tools/generate_clan_svgs.py puis Reimport.", MessageType.Warning);
                if (GUILayout.Button("Recharger")) Reload();
                return;
            }

            EditorGUILayout.Space(6);
            DrawBanner();

            EditorGUILayout.Space(10);
            _endIdx = Popup("Bout de ruban", _ends, _endIdx);
            _flourishIdx = Popup("Fioriture nom", _flourishes, _flourishIdx);

            EditorGUILayout.Space(4);
            _ornamentColor = EditorGUILayout.ColorField("Couleur ornement", _ornamentColor);
            _plateColor = EditorGUILayout.ColorField("Couleur plaque", _plateColor);

            EditorGUILayout.Space(4);
            _clanTag = EditorGUILayout.TextField("Clan", _clanTag);
            _pseudo = EditorGUILayout.TextField("Pseudo", _pseudo);
            _title = EditorGUILayout.TextField("Titre", _title);

            if (GUILayout.Button("Couleur ornement aléatoire"))
                _ornamentColor = Random.ColorHSV(0f, 1f, 0.3f, 0.7f, 0.55f, 0.95f);
        }

        private void DrawBanner()
        {
            const float h = 200f;
            Rect area = GUILayoutUtility.GetRect(position.width, h);
            EditorGUI.DrawRect(area, new Color(0.16f, 0.17f, 0.20f, 1f)); // fond hub simulé

            float cx = area.center.x;

            // ===== Ligne CLAN (haut) : fioriture gauche + nom + fioriture droite (miroir) =====
            bool hasClan = !string.IsNullOrEmpty(_clanTag);
            float clanY = area.y + 28f;
            if (hasClan)
            {
                var clanStyle = Centered(13, new Color(0.95f, 0.82f, 0.55f), bold: true);
                Vector2 sz = clanStyle.CalcSize(new GUIContent(_clanTag));
                float flW = 58f, flH = 18f, gap = 10f;
                float half = sz.x * 0.5f;
                var clanRect = new Rect(cx - half, clanY - sz.y * 0.5f, sz.x, sz.y);

                var flTex = _flourishes[_flourishIdx].sprite.texture;
                var lFl = new Rect(clanRect.x - gap - flW, clanY - flH * 0.5f, flW, flH);
                var rFl = new Rect(clanRect.xMax + gap, clanY - flH * 0.5f, flW, flH);
                GUI.color = _ornamentColor;
                GUI.DrawTexture(lFl, flTex, ScaleMode.ScaleToFit, true);
                DrawFlipped(rFl, flTex, _ornamentColor);
                GUI.color = Color.white;

                DrawOutlined(clanRect, _clanTag, 13, new Color(0.96f, 0.84f, 0.58f), bold: true);
            }

            // ===== Plaque centrale (pseudo) + bouts de ruban =====
            float plateH = 54f;
            float plateY = area.y + 44f; // resserré sous la ligne de clan (moins de vide)
            float plateW = Mathf.Clamp(_pseudo.Length * 14f + 70f, 170f, area.width - 200f);
            var plate = new Rect(cx - plateW * 0.5f, plateY, plateW, plateH);

            EditorGUI.DrawRect(plate, _plateColor);
            EditorGUI.DrawRect(new Rect(plate.x, plate.y, plate.width, 2f), _ornamentColor);
            EditorGUI.DrawRect(new Rect(plate.x, plate.yMax - 2f, plate.width, 2f), _ornamentColor);

            float endSize = 84f;
            var endTex = _ends[_endIdx].sprite.texture;
            var lRect = new Rect(plate.x - endSize + 14f, plate.center.y - endSize * 0.5f, endSize, endSize);
            var rRect = new Rect(plate.xMax - 14f, plate.center.y - endSize * 0.5f, endSize, endSize);
            GUI.color = _ornamentColor;
            GUI.DrawTexture(lRect, endTex, ScaleMode.ScaleToFit, true);
            DrawFlipped(rRect, endTex, _ornamentColor);
            GUI.color = Color.white;

            // ===== Pseudo (plaque) + titre (sous la plaque), en avant + contour noir =====
            DrawOutlined(plate, _pseudo, 18, new Color(1f, 0.97f, 0.9f), bold: true);
            if (!string.IsNullOrEmpty(_title))
                DrawOutlined(new Rect(plate.x, plate.yMax + 4f, plate.width, 18f), _title,
                    12, new Color(1f, 0.85f, 0.2f), italic: true);
        }

        private static void DrawFlipped(Rect r, Texture tex, Color tint)
        {
            var m = GUI.matrix;
            GUIUtility.ScaleAroundPivot(new Vector2(-1f, 1f), r.center);
            GUI.color = tint;
            GUI.DrawTexture(r, tex, ScaleMode.ScaleToFit, true);
            GUI.matrix = m;
        }

        /// <summary>Label centré avec contour noir (8 passes décalées + couleur par-dessus).</summary>
        private static void DrawOutlined(Rect r, string text, int size, Color color, bool bold = false, bool italic = false)
        {
            var outline = Centered(size, Color.black, bold, italic);
            for (int dx = -1; dx <= 1; dx++)
                for (int dy = -1; dy <= 1; dy++)
                {
                    if (dx == 0 && dy == 0) continue;
                    GUI.Label(new Rect(r.x + dx, r.y + dy, r.width, r.height), text, outline);
                }
            GUI.Label(r, text, Centered(size, color, bold, italic));
        }

        private static GUIStyle Centered(int size, Color c, bool bold = false, bool italic = false)
        {
            return new GUIStyle(EditorStyles.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = size,
                fontStyle = bold ? FontStyle.Bold : (italic ? FontStyle.Italic : FontStyle.Normal),
                normal = { textColor = c }
            };
        }

        private static int Popup(string label, List<(string name, Sprite sprite)> list, int idx)
        {
            var names = new string[list.Count];
            for (int i = 0; i < list.Count; i++) names[i] = list[i].name;
            return list.Count == 0 ? idx : EditorGUILayout.Popup(label, Mathf.Clamp(idx, 0, list.Count - 1), names);
        }
    }
}
