using Nymora.Combat.Grid;
using Nymora.Combat.View;
using UnityEditor;
using UnityEngine;

namespace Nymora.Editor.Tools
{
    /// <summary>
    /// Outil de validation Phase 2.1 : spawn une grille iso hors Play pour
    /// verifier la visu rapidement (sans devoir lancer Quantum).
    ///
    /// La logique d'instanciation est INDEPENDANTE de Quantum — on copie juste
    /// la formule IsoProjection. Le GridRenderer reste la source de verite en runtime.
    ///
    /// Menu : Nymora > Validation > Grid Previewer
    /// </summary>
    public class GridPreviewerWindow : EditorWindow
    {
        // POLISH-5e (17 mai) : lit GridConstants au lieu d'hardcoder, pour rester aligne
        // si la dimension grille change sans avoir a re-modifier le previewer.
        private static int Width => Quantum.GridConstants.Width;
        private static int Height => Quantum.GridConstants.Height;
        private const string PreviewRootName = "__GridPreview (Editor Only)";

        private GridSettings _settings;
        private GameObject _tilePrefab;
        private Color _evenColor = new Color(0.85f, 0.85f, 0.85f, 1f);
        private Color _oddColor = new Color(0.75f, 0.75f, 0.75f, 1f);

        [MenuItem("Nymora/Validation/Grid Previewer")]
        public static void Open()
        {
            var win = GetWindow<GridPreviewerWindow>("Grid Previewer");
            win.minSize = new Vector2(320, 280);
            win.Show();
        }

        private void OnEnable()
        {
            if (_settings == null)
            {
                _settings = AssetDatabase.LoadAssetAtPath<GridSettings>("Assets/_Nymora/Settings/GridSettings.asset");
            }
            if (_tilePrefab == null)
            {
                _tilePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Nymora/Prefabs/Combat/TileView.prefab");
            }
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Grid Previewer (Phase 2.1)", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            _settings = (GridSettings)EditorGUILayout.ObjectField("Grid Settings", _settings, typeof(GridSettings), false);
            _tilePrefab = (GameObject)EditorGUILayout.ObjectField("Tile Prefab", _tilePrefab, typeof(GameObject), false);
            _evenColor = EditorGUILayout.ColorField("Tile color even", _evenColor);
            _oddColor = EditorGUILayout.ColorField("Tile color odd", _oddColor);

            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(
                $"Grille verrouillee {Width}x{Height} (Bible V7.1).\n" +
                "Iso 2:1. La preview est temporaire — supprime-la avant de commit la scene.",
                MessageType.Info);

            EditorGUILayout.Space();

            using (new EditorGUI.DisabledScope(_settings == null || _tilePrefab == null))
            {
                if (GUILayout.Button("Spawn Preview", GUILayout.Height(30)))
                {
                    SpawnPreview();
                }
            }

            if (GUILayout.Button("Clear Preview", GUILayout.Height(24)))
            {
                ClearPreview();
            }
        }

        private void SpawnPreview()
        {
            ClearPreview();

            var root = new GameObject(PreviewRootName);
            root.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;

            Vector3 centerOffset = _settings.CenterGrid
                ? IsoProjection.CenterOffset(Width, Height, _settings.TileWorldWidth, _settings.TileWorldHeight)
                : Vector3.zero;

            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    Vector3 pos = IsoProjection.GridToWorld(
                        x, y, _settings.TileWorldWidth, _settings.TileWorldHeight) + centerOffset;

                    var go = (GameObject)PrefabUtility.InstantiatePrefab(_tilePrefab, root.transform);
                    go.transform.position = pos;
                    go.name = $"Tile_{x}_{y}";
                    go.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;

                    bool even = ((x + y) & 1) == 0;
                    Color color = even ? _evenColor : _oddColor;

                    var view = go.GetComponent<TileView>();
                    if (view != null)
                    {
                        view.Setup(x, y, color);
                        view.SetSortingOrder(
                            string.IsNullOrEmpty(_settings.SortingLayer) ? "Default" : _settings.SortingLayer,
                            IsoProjection.SortingOrderFor(x, y, _settings.BaseSortingOrder));
                    }
                }
            }

            Selection.activeGameObject = root;
            SceneView.lastActiveSceneView?.FrameSelected();

            Debug.Log($"[Nymora.GridPreviewer] Preview {Width}x{Height} spawnee (root: {PreviewRootName}).");
        }

        private void ClearPreview()
        {
            var existing = GameObject.Find(PreviewRootName);
            if (existing != null)
            {
                DestroyImmediate(existing);
            }
        }
    }
}
