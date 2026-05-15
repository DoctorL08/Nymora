using System.IO;
using Nymora.Hub;
using UnityEditor;
using UnityEngine;

namespace Nymora.Editor.Windows
{
    /// <summary>
    /// Editor Window pour banner/débanner des tiles du hub.
    /// Affiche la grille 20×20 (ou autre taille) en boutons cliquables :
    ///   - Gris = walkable
    ///   - Rouge = banni
    /// Save direct dans le ScriptableObject HubGridBanList assigné.
    ///
    /// Menu : Nymora > Hub > Grid Ban Editor
    /// </summary>
    public sealed class HubGridBanEditor : EditorWindow
    {
        private const string DefaultAssetFolder = "Assets/_Nymora/ScriptableObjects/Hub";
        private const string DefaultAssetPath = DefaultAssetFolder + "/HubGridBanList.asset";

        private HubGridBanList _banList;
        private Vector2 _scroll;
        private int _editWidth = 20;
        private int _editHeight = 20;
        private const float CellWidth = 38f;
        private const float CellHeight = 22f;

        [MenuItem("Nymora/Hub/Grid Ban Editor", priority = 50)]
        private static void OpenWindow()
        {
            var window = GetWindow<HubGridBanEditor>("Grid Ban Editor");
            window.minSize = new Vector2(900f, 700f);
            window.TryLoadDefaultAsset();
        }

        private void TryLoadDefaultAsset()
        {
            if (_banList != null) return;
            _banList = AssetDatabase.LoadAssetAtPath<HubGridBanList>(DefaultAssetPath);
            if (_banList != null)
            {
                _editWidth = _banList.GridWidth;
                _editHeight = _banList.GridHeight;
            }
        }

        private void OnGUI()
        {
            DrawHeader();
            if (_banList == null)
            {
                EditorGUILayout.HelpBox(
                    "Aucun HubGridBanList assigné.\n" +
                    "Clique 'Create New' pour en créer un (sera sauvé dans Assets/_Nymora/ScriptableObjects/Hub/).",
                    MessageType.Info);
                return;
            }

            EditorGUILayout.Space();
            DrawGridSizeControls();
            EditorGUILayout.Space();
            EditorGUILayout.LabelField($"Tiles bannies : {_banList.BannedTiles.Count}", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Clic gauche sur une case = toggle ban. Les tiles bannies (rouge) sont unwalkable pour le pathfinding A*.", MessageType.None);

            EditorGUILayout.Space();
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            DrawGridButtons();
            EditorGUILayout.EndScrollView();
        }

        private void DrawHeader()
        {
            EditorGUILayout.BeginHorizontal();
            _banList = (HubGridBanList)EditorGUILayout.ObjectField("Ban List Asset", _banList, typeof(HubGridBanList), false);
            if (GUILayout.Button("Create New", GUILayout.Width(100)))
            {
                CreateNewAsset();
            }
            EditorGUILayout.EndHorizontal();
        }

        private void DrawGridSizeControls()
        {
            EditorGUILayout.BeginHorizontal();
            _editWidth = EditorGUILayout.IntField("Grid Width", _editWidth);
            _editHeight = EditorGUILayout.IntField("Grid Height", _editHeight);
            if (GUILayout.Button("Apply Size", GUILayout.Width(90)))
            {
                _banList.EditorSetGridSize(_editWidth, _editHeight);
            }
            GUI.backgroundColor = new Color(1f, 0.7f, 0.7f);
            if (GUILayout.Button("Clear All Bans", GUILayout.Width(110)))
            {
                if (EditorUtility.DisplayDialog("Clear Bans", "Effacer TOUS les bans ?", "Oui", "Annuler"))
                {
                    _banList.EditorClearAll();
                }
            }
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();
        }

        private void DrawGridButtons()
        {
            // Affiche du haut (gy max) vers le bas (gy=0) pour matcher l'intuition spatiale
            for (int gy = _banList.GridHeight - 1; gy >= 0; gy--)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"y={gy:D2}", GUILayout.Width(38f));
                for (int gx = 0; gx < _banList.GridWidth; gx++)
                {
                    bool banned = _banList.IsBanned(gx, gy);
                    var prevBg = GUI.backgroundColor;
                    GUI.backgroundColor = banned ? new Color(0.9f, 0.3f, 0.3f) : new Color(0.5f, 0.5f, 0.55f);
                    string label = banned ? "✕" : $"{gx},{gy}";
                    if (GUILayout.Button(label, GUILayout.Width(CellWidth), GUILayout.Height(CellHeight)))
                    {
                        _banList.EditorSetBanned(gx, gy, !banned);
                    }
                    GUI.backgroundColor = prevBg;
                }
                EditorGUILayout.EndHorizontal();
            }
            // Footer : label x
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("", GUILayout.Width(38f));
            for (int gx = 0; gx < _banList.GridWidth; gx++)
            {
                EditorGUILayout.LabelField($"x={gx}", GUILayout.Width(CellWidth));
            }
            EditorGUILayout.EndHorizontal();
        }

        private void CreateNewAsset()
        {
            if (!AssetDatabase.IsValidFolder(DefaultAssetFolder))
            {
                Directory.CreateDirectory(DefaultAssetFolder);
                AssetDatabase.Refresh();
            }
            if (AssetDatabase.LoadAssetAtPath<HubGridBanList>(DefaultAssetPath) != null)
            {
                if (!EditorUtility.DisplayDialog("Create",
                    $"{DefaultAssetPath} existe déjà. L'écraser ?", "Écraser", "Annuler"))
                {
                    _banList = AssetDatabase.LoadAssetAtPath<HubGridBanList>(DefaultAssetPath);
                    return;
                }
                AssetDatabase.DeleteAsset(DefaultAssetPath);
            }
            var asset = ScriptableObject.CreateInstance<HubGridBanList>();
            AssetDatabase.CreateAsset(asset, DefaultAssetPath);
            AssetDatabase.SaveAssets();
            _banList = asset;
            _editWidth = asset.GridWidth;
            _editHeight = asset.GridHeight;
        }
    }
}
