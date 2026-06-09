using System.Collections.Generic;
using Quantum;          // NymoraCombatMap, MapSpawn, GridConstants
using UnityEditor;
using UnityEngine;

namespace Nymora.Editor.Windows
{
    /// <summary>
    /// 5.4c (2v2/3v3) — Éditeur de maps de combat. Permet de PEINDRE la forme jouable (masque
    /// Walkable irrégulier, bord découpé) et de PLACER les points de spawn groupés par équipe,
    /// puis d'enregistrer un asset <see cref="NymoraCombatMap"/> (AssetObject Quantum) consommé en
    /// jeu par GridSystem (forme) + CombatantSystem (spawns), via RuntimeConfig.CombatMap (5.4b).
    ///
    /// Le 1v1 garde sa zone 10x10 hardcodée (pas de map) ; cet éditeur sert aux maps 2v2 (12x12) /
    /// 3v3 (15x15). La grille MAX du jeu est 15x15 (GridConstants), l'index est y*15+x (stride 15).
    ///
    /// Accès : menu Unity « Nymora > Combat > Map Editor ».
    /// </summary>
    public class NymoraMapEditorWindow : EditorWindow
    {
        [MenuItem("Nymora/Combat/Map Editor")]
        public static void Open()
        {
            var w = GetWindow<NymoraMapEditorWindow>("Nymora Map Editor");
            w.minSize = new Vector2(520, 620);
        }

        private NymoraCombatMap _map;

        // État d'édition (chargé depuis l'asset, écrit au save).
        private int _logicalW = 12;
        private int _logicalH = 12;
        private byte[] _walk = new byte[GridConstants.Count];           // 1 = jouable, indexé y*15+x
        private readonly Vector2Int[] _spawns = new Vector2Int[6];      // index = team*3 + rank ; (-1,-1) = non posé

        private enum Tool { Walkable, Spawn }
        private Tool _tool = Tool.Walkable;
        private int _spawnTeam = 0;   // 0 ou 1
        private int _spawnRank = 0;   // 0..2

        private const int CellPx = 26;

        private void OnEnable()
        {
            if (_map == null) ResetToBlank(_logicalW, _logicalH);
        }

        private static int Idx(int x, int y) => y * GridConstants.Width + x; // stride 15 (= GridHelpers.Index)

        private void ResetToBlank(int w, int h)
        {
            _logicalW = Mathf.Clamp(w, 1, GridConstants.Width);
            _logicalH = Mathf.Clamp(h, 1, GridConstants.Height);
            _walk = new byte[GridConstants.Count];
            for (int y = 0; y < _logicalH; y++)
                for (int x = 0; x < _logicalW; x++)
                    _walk[Idx(x, y)] = 1; // tout jouable par défaut, on carve ensuite
            for (int i = 0; i < _spawns.Length; i++) _spawns[i] = new Vector2Int(-1, -1);
        }

        private void LoadFromAsset()
        {
            if (_map == null) return;
            _logicalW = _map.Width > 0 ? Mathf.Clamp(_map.Width, 1, GridConstants.Width) : 12;
            _logicalH = _map.Height > 0 ? Mathf.Clamp(_map.Height, 1, GridConstants.Height) : 12;

            _walk = new byte[GridConstants.Count];
            if (_map.Walkable != null && _map.Walkable.Length == GridConstants.Count)
            {
                System.Array.Copy(_map.Walkable, _walk, GridConstants.Count);
            }
            else
            {
                for (int y = 0; y < _logicalH; y++)
                    for (int x = 0; x < _logicalW; x++)
                        _walk[Idx(x, y)] = 1;
            }

            for (int i = 0; i < _spawns.Length; i++) _spawns[i] = new Vector2Int(-1, -1);
            if (_map.Spawns != null)
            {
                foreach (var s in _map.Spawns)
                {
                    if (s.Team < 0 || s.Team > 1 || s.Rank < 0 || s.Rank > 2) continue;
                    _spawns[s.Team * 3 + s.Rank] = new Vector2Int(s.X, s.Y);
                }
            }
        }

        private void SaveToAsset()
        {
            if (_map == null) return;

            _map.Width = _logicalW;
            _map.Height = _logicalH;

            // Masque : on ne garde walkable que DANS la zone logique (hors zone -> 0).
            var mask = new byte[GridConstants.Count];
            for (int y = 0; y < _logicalH; y++)
                for (int x = 0; x < _logicalW; x++)
                    mask[Idx(x, y)] = _walk[Idx(x, y)];
            _map.Walkable = mask;

            // Spawns posés.
            var list = new List<MapSpawn>();
            for (int team = 0; team < 2; team++)
                for (int rank = 0; rank < 3; rank++)
                {
                    var p = _spawns[team * 3 + rank];
                    if (p.x < 0) continue;
                    list.Add(new MapSpawn { Team = team, Rank = rank, X = p.x, Y = p.y });
                }
            _map.Spawns = list.ToArray();

            EditorUtility.SetDirty(_map);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[NymoraMapEditor] Map sauvegardée : {_logicalW}x{_logicalH}, {list.Count} spawn(s).", _map);
        }

        private void CreateNewAsset()
        {
            string path = EditorUtility.SaveFilePanelInProject(
                "Nouvelle map de combat", "CombatMap_2v2", "asset",
                "Choisis l'emplacement de la map (ex: Assets/_Nymora/ScriptableObjects/Maps/).");
            if (string.IsNullOrEmpty(path)) return;

            var map = ScriptableObject.CreateInstance<NymoraCombatMap>();
            AssetDatabase.CreateAsset(map, path);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            _map = map;
            ResetToBlank(12, 12);
            SaveToAsset(); // écrit l'état initial (Quantum assigne l'Identifier/Guid à l'import)
        }

        private void OnGUI()
        {
            DrawToolbar();
            EditorGUILayout.Space(6);

            if (_map == null)
            {
                EditorGUILayout.HelpBox(
                    "Crée une nouvelle map ou glisse un asset NymoraCombatMap existant ci-dessus.",
                    MessageType.Info);
                return;
            }

            DrawDimsAndTools();
            EditorGUILayout.Space(6);
            DrawGrid();
            EditorGUILayout.Space(6);
            DrawLegendAndSave();
        }

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            if (GUILayout.Button("Nouvelle map", EditorStyles.toolbarButton, GUILayout.Width(110)))
                CreateNewAsset();

            EditorGUI.BeginChangeCheck();
            var picked = (NymoraCombatMap)EditorGUILayout.ObjectField(
                _map, typeof(NymoraCombatMap), false);
            if (EditorGUI.EndChangeCheck())
            {
                _map = picked;
                LoadFromAsset();
            }
            EditorGUILayout.EndHorizontal();
        }

        private void DrawDimsAndTools()
        {
            EditorGUI.BeginChangeCheck();
            int w = EditorGUILayout.IntSlider("Largeur logique", _logicalW, 1, GridConstants.Width);
            int h = EditorGUILayout.IntSlider("Hauteur logique", _logicalH, 1, GridConstants.Height);
            if (EditorGUI.EndChangeCheck())
            {
                // Étend/réduit la zone : nouvelles cases -> jouables ; on conserve l'existant.
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                        if (x >= _logicalW || y >= _logicalH) _walk[Idx(x, y)] = 1;
                _logicalW = w; _logicalH = h;
            }

            EditorGUILayout.BeginHorizontal();
            _tool = (Tool)GUILayout.Toolbar((int)_tool, new[] { "Peindre la forme", "Placer un spawn" });
            EditorGUILayout.EndHorizontal();

            if (_tool == Tool.Spawn)
            {
                EditorGUILayout.BeginHorizontal();
                _spawnTeam = EditorGUILayout.IntPopup("Équipe", _spawnTeam,
                    new[] { "Équipe 0", "Équipe 1" }, new[] { 0, 1 });
                _spawnRank = EditorGUILayout.IntPopup("Rang", _spawnRank,
                    new[] { "1er (rang 0)", "2e (rang 1)", "3e (rang 2)" }, new[] { 0, 1, 2 });
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.HelpBox(
                    "Clique une case JOUABLE pour y poser le spawn (Équipe/Rang sélectionné). " +
                    "2v2 = rangs 0-1, 3v3 = rangs 0-1-2.", MessageType.None);
            }
            else
            {
                EditorGUILayout.HelpBox(
                    "Clique une case pour activer/désactiver le sol. Carve les bords pour une forme " +
                    "irrégulière (non carrée).", MessageType.None);
            }
        }

        private void DrawGrid()
        {
            // Rendu Cartésien : y croît vers le HAUT (ligne du haut = y le plus grand). (0,0) en bas-gauche.
            for (int y = _logicalH - 1; y >= 0; y--)
            {
                EditorGUILayout.BeginHorizontal();
                for (int x = 0; x < _logicalW; x++)
                {
                    DrawCell(x, y);
                }
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();
            }
        }

        private void DrawCell(int x, int y)
        {
            int idx = Idx(x, y);
            bool walkable = _walk[idx] != 0;

            // Un spawn est-il sur cette case ?
            int spawnTeam = -1, spawnRank = -1;
            for (int i = 0; i < _spawns.Length; i++)
            {
                if (_spawns[i].x == x && _spawns[i].y == y) { spawnTeam = i / 3; spawnRank = i % 3; break; }
            }

            Color prev = GUI.backgroundColor;
            if (!walkable) GUI.backgroundColor = new Color(0.18f, 0.18f, 0.20f);
            else if (spawnTeam == 0) GUI.backgroundColor = new Color(0.35f, 0.55f, 0.95f); // bleu
            else if (spawnTeam == 1) GUI.backgroundColor = new Color(0.95f, 0.45f, 0.40f); // rouge
            else GUI.backgroundColor = new Color(0.45f, 0.70f, 0.45f);                      // vert sol

            string label = spawnTeam >= 0 ? $"{spawnTeam}.{spawnRank}" : (walkable ? "" : "·");
            if (GUILayout.Button(label, GUILayout.Width(CellPx), GUILayout.Height(CellPx)))
            {
                OnCellClicked(x, y);
            }
            GUI.backgroundColor = prev;
        }

        private void OnCellClicked(int x, int y)
        {
            int idx = Idx(x, y);
            if (_tool == Tool.Walkable)
            {
                _walk[idx] = (byte)(_walk[idx] != 0 ? 0 : 1);
                // Si on enlève le sol sous un spawn, on retire le spawn.
                if (_walk[idx] == 0)
                    for (int i = 0; i < _spawns.Length; i++)
                        if (_spawns[i].x == x && _spawns[i].y == y) _spawns[i] = new Vector2Int(-1, -1);
            }
            else // Spawn
            {
                if (_walk[idx] == 0) { ShowNotification(new GUIContent("Spawn impossible : case non jouable")); return; }
                // Une case n'héberge qu'un spawn : retire tout spawn déjà présent ici.
                for (int i = 0; i < _spawns.Length; i++)
                    if (_spawns[i].x == x && _spawns[i].y == y) _spawns[i] = new Vector2Int(-1, -1);
                _spawns[_spawnTeam * 3 + _spawnRank] = new Vector2Int(x, y);
            }
        }

        private void DrawLegendAndSave()
        {
            EditorGUILayout.LabelField("Légende : vert = sol · bleu = spawn équipe 0 · rouge = équipe 1 · gris = hors-forme",
                EditorStyles.miniLabel);

            // Récap des spawns posés.
            int posed = 0;
            for (int i = 0; i < _spawns.Length; i++) if (_spawns[i].x >= 0) posed++;
            EditorGUILayout.LabelField($"Spawns posés : {posed}", EditorStyles.miniLabel);

            EditorGUILayout.Space(4);
            using (new EditorGUI.DisabledScope(_map == null))
            {
                if (GUILayout.Button("Enregistrer la map", GUILayout.Height(30)))
                    SaveToAsset();
            }
        }
    }
}
