using System.Collections.Generic;
using UnityEngine;

namespace Nymora.Hub
{
    /// <summary>
    /// Brique 4.X — ScriptableObject listant les tiles bannées (unwalkable) du hub.
    /// Édité via `Nymora > Hub > Grid Ban Editor` et lu runtime par HubInputController.IsWalkable.
    /// </summary>
    [CreateAssetMenu(menuName = "Nymora/Hub/Grid Ban List", fileName = "HubGridBanList")]
    public sealed class HubGridBanList : ScriptableObject
    {
        [SerializeField] private int _gridWidth = 20;
        [SerializeField] private int _gridHeight = 20;
        [SerializeField] private List<Vector2Int> _bannedTiles = new List<Vector2Int>();

        public int GridWidth => _gridWidth;
        public int GridHeight => _gridHeight;
        public IReadOnlyList<Vector2Int> BannedTiles => _bannedTiles;

        public bool IsBanned(int gx, int gy)
        {
            for (int i = 0; i < _bannedTiles.Count; i++)
            {
                if (_bannedTiles[i].x == gx && _bannedTiles[i].y == gy) return true;
            }
            return false;
        }

#if UNITY_EDITOR
        public void EditorSetBanned(int gx, int gy, bool banned)
        {
            int idx = FindIndex(gx, gy);
            if (banned)
            {
                if (idx < 0) _bannedTiles.Add(new Vector2Int(gx, gy));
            }
            else
            {
                if (idx >= 0) _bannedTiles.RemoveAt(idx);
            }
            UnityEditor.EditorUtility.SetDirty(this);
        }

        public void EditorClearAll()
        {
            _bannedTiles.Clear();
            UnityEditor.EditorUtility.SetDirty(this);
        }

        public void EditorSetGridSize(int width, int height)
        {
            _gridWidth = Mathf.Max(1, width);
            _gridHeight = Mathf.Max(1, height);
            for (int i = _bannedTiles.Count - 1; i >= 0; i--)
            {
                var t = _bannedTiles[i];
                if (t.x < 0 || t.x >= _gridWidth || t.y < 0 || t.y >= _gridHeight)
                {
                    _bannedTiles.RemoveAt(i);
                }
            }
            UnityEditor.EditorUtility.SetDirty(this);
        }

        private int FindIndex(int gx, int gy)
        {
            for (int i = 0; i < _bannedTiles.Count; i++)
            {
                if (_bannedTiles[i].x == gx && _bannedTiles[i].y == gy) return i;
            }
            return -1;
        }
#endif
    }
}
