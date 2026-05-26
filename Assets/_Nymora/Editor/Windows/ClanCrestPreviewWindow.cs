using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Nymora.Editor.Windows
{
    /// <summary>
    /// Aperçu du pack SVG "blason de clan" (brique style-clan 1).
    ///
    /// Affiche toutes les FORMES et tous les EMBLÈMES du dossier
    /// Resources/UI/Icons/Clan/, + un aperçu COMPOSITE live (forme teintée couleur de fond
    /// + emblème teinté couleur d'emblème) — exactement le rendu visé en jeu (menu clan,
    /// tooltip avatar, pop-up d'invitation).
    ///
    /// Sert UNIQUEMENT à valider visuellement le pack (read-only). Accès :
    /// Nymora > Setup > UI Menu > Clan Crest Preview.
    /// </summary>
    public sealed class ClanCrestPreviewWindow : EditorWindow
    {
        private const string Folder = "Assets/_Nymora/Resources/UI/Icons/Clan";

        private readonly List<(string name, Sprite sprite)> _shapes = new List<(string, Sprite)>();
        private readonly List<(string name, Sprite sprite)> _emblems = new List<(string, Sprite)>();

        private int _shapeIdx;
        private int _emblemIdx;
        private Color _bgColor = new Color(0.36f, 0.48f, 0.65f, 1f);   // bleu ardoise (defaut clan actuel)
        private Color _fgColor = new Color(0.95f, 0.95f, 0.97f, 1f);   // emblème crème
        private float _emblemScale = 0.56f;                            // emblème = 56% de la forme
        private Vector2 _scroll;

        [MenuItem("Nymora/Setup/UI Menu/Clan Crest Preview")]
        public static void Open()
        {
            var w = GetWindow<ClanCrestPreviewWindow>("Clan Crest");
            w.minSize = new Vector2(560f, 620f);
            w.Reload();
        }

        private void OnEnable() => Reload();

        private void Reload()
        {
            _shapes.Clear();
            _emblems.Clear();
            if (!Directory.Exists(Folder))
            {
                Repaint();
                return;
            }
            var guids = AssetDatabase.FindAssets("t:Sprite", new[] { Folder });
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (sprite == null) continue;
                string name = Path.GetFileNameWithoutExtension(path);
                if (name.StartsWith("ui_clan_shape_")) _shapes.Add((name.Substring("ui_clan_shape_".Length), sprite));
                else if (name.StartsWith("ui_clan_emblem_")) _emblems.Add((name.Substring("ui_clan_emblem_".Length), sprite));
            }
            _shapes.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
            _emblems.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
            _shapeIdx = Mathf.Clamp(_shapeIdx, 0, Mathf.Max(0, _shapes.Count - 1));
            _emblemIdx = Mathf.Clamp(_emblemIdx, 0, Mathf.Max(0, _emblems.Count - 1));
            Repaint();
        }

        private void OnGUI()
        {
            if (_shapes.Count == 0 && _emblems.Count == 0)
            {
                EditorGUILayout.HelpBox(
                    "Aucun SVG trouvé dans " + Folder +
                    ".\nLance : python tools/generate_clan_svgs.py, puis Reimport du dossier dans Unity.",
                    MessageType.Warning);
                if (GUILayout.Button("Recharger")) Reload();
                return;
            }

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField($"Pack blason : {_shapes.Count} formes · {_emblems.Count} emblèmes",
                EditorStyles.boldLabel);

            // ===== Aperçu composite =====
            DrawCompositePreview();

            EditorGUILayout.Space(6);
            using (new EditorGUILayout.HorizontalScope())
            {
                _bgColor = EditorGUILayout.ColorField("Couleur fond", _bgColor);
                _fgColor = EditorGUILayout.ColorField("Couleur emblème", _fgColor);
            }
            _emblemScale = EditorGUILayout.Slider("Taille emblème", _emblemScale, 0.3f, 0.8f);
            if (GUILayout.Button("Couleurs aléatoires"))
            {
                _bgColor = Random.ColorHSV(0f, 1f, 0.3f, 0.7f, 0.3f, 0.7f);
                _fgColor = Random.ColorHSV(0f, 1f, 0f, 0.3f, 0.85f, 1f);
            }

            EditorGUILayout.Space(8);
            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            EditorGUILayout.LabelField("Formes (cliquer pour choisir)", EditorStyles.boldLabel);
            _shapeIdx = DrawGrid(_shapes, _shapeIdx, _bgColor);

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Emblèmes (cliquer pour choisir)", EditorStyles.boldLabel);
            _emblemIdx = DrawGrid(_emblems, _emblemIdx, _fgColor);

            EditorGUILayout.EndScrollView();
        }

        private void DrawCompositePreview()
        {
            const float box = 220f;
            Rect r = GUILayoutUtility.GetRect(box, box);
            r.x = (position.width - box) * 0.5f;
            r.width = box;

            EditorGUI.DrawRect(r, new Color(0.12f, 0.12f, 0.14f, 1f)); // fond sombre = lisibilité

            if (_shapes.Count > 0 && _shapeIdx < _shapes.Count)
            {
                var shape = _shapes[_shapeIdx].sprite;
                if (shape != null && shape.texture != null)
                {
                    GUI.color = _bgColor;
                    GUI.DrawTexture(Inset(r, 14f), shape.texture, ScaleMode.ScaleToFit, true);
                }
            }
            if (_emblems.Count > 0 && _emblemIdx < _emblems.Count)
            {
                var emblem = _emblems[_emblemIdx].sprite;
                if (emblem != null && emblem.texture != null)
                {
                    GUI.color = _fgColor;
                    float pad = box * (1f - _emblemScale) * 0.5f + 14f;
                    GUI.DrawTexture(Inset(r, pad), emblem.texture, ScaleMode.ScaleToFit, true);
                }
            }
            GUI.color = Color.white;

            string sName = _shapes.Count > 0 ? _shapes[_shapeIdx].name : "—";
            string eName = _emblems.Count > 0 ? _emblems[_emblemIdx].name : "—";
            var labelRect = new Rect(r.x, r.yMax + 2f, r.width, 18f);
            var centered = new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleCenter };
            GUI.Label(labelRect, $"{sName} + {eName}", centered);
        }

        private int DrawGrid(List<(string name, Sprite sprite)> list, int selected, Color tint)
        {
            const float cell = 76f;
            const float pad = 6f;
            int perRow = Mathf.Max(1, Mathf.FloorToInt((position.width - 20f) / (cell + pad)));
            int rows = Mathf.CeilToInt(list.Count / (float)perRow);

            Rect area = GUILayoutUtility.GetRect(position.width - 20f, rows * (cell + 20f) + 4f);
            for (int i = 0; i < list.Count; i++)
            {
                int col = i % perRow, row = i / perRow;
                var cellRect = new Rect(area.x + col * (cell + pad), area.y + row * (cell + 20f), cell, cell);

                bool isSel = i == selected;
                EditorGUI.DrawRect(cellRect, isSel ? new Color(0.25f, 0.4f, 0.6f, 1f) : new Color(0.14f, 0.14f, 0.16f, 1f));

                var sp = list[i].sprite;
                if (sp != null && sp.texture != null)
                {
                    GUI.color = tint;
                    GUI.DrawTexture(Inset(cellRect, 10f), sp.texture, ScaleMode.ScaleToFit, true);
                    GUI.color = Color.white;
                }

                var nameRect = new Rect(cellRect.x, cellRect.yMax, cell, 16f);
                var st = new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleCenter, fontSize = 9 };
                GUI.Label(nameRect, list[i].name, st);

                if (Event.current.type == EventType.MouseDown && cellRect.Contains(Event.current.mousePosition))
                {
                    selected = i;
                    Event.current.Use();
                    Repaint();
                }
            }
            return selected;
        }

        private static Rect Inset(Rect r, float by)
            => new Rect(r.x + by, r.y + by, r.width - by * 2f, r.height - by * 2f);
    }
}
