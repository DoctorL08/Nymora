using UnityEngine;
using UnityEngine.EventSystems;

namespace Nymora.Hub
{
    /// <summary>
    /// Brique 4.3.b + 4.3.c — Clic gauche souris -> WorldToGrid -> A* path -> assign au MovementController.
    /// Brique 4.8.a — Clic sur un avatar remote (HasStateAuthority=false) -> ouvre ChallengePopup au lieu de pathfind.
    /// Ignore les clics sur UI.
    /// </summary>
    public sealed class HubInputController : MonoBehaviour
    {
        [SerializeField] private Camera _camera;
        [SerializeField] private HubGridRenderer _grid;
        [SerializeField] private ChallengePopup _challengePopup;
        [SerializeField] private HubGridBanList _banList;
        [SerializeField] private bool _logClicks = true;

        private void Reset()
        {
            _camera = Camera.main;
        }

        private void Update()
        {
            if (!Input.GetMouseButtonDown(0)) return;
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;
            if (_camera == null || _grid == null) return;

            var localAvatar = HubAvatar.Local;
            if (localAvatar == null) return; // pas encore spawn

            var movement = localAvatar.GetComponent<HubMovementController>();
            if (movement == null) return;

            Vector3 mouseWorld = _camera.ScreenToWorldPoint(Input.mousePosition);
            mouseWorld.z = 0f;

            (int gx, int gy) = IsoProjection.WorldToGrid(mouseWorld, _grid.TileWorldWidth, _grid.TileWorldHeight, _grid.CenterOffset);

            if (gx < 0 || gy < 0 || gx >= _grid.Width || gy >= _grid.Height)
            {
                if (_logClicks) Debug.Log($"[HubInput] Clic hors grille (gx={gx}, gy={gy}) — ignore");
                return;
            }

            // 4.8.a — Si la tile cliquée contient un avatar REMOTE, ouvre la popup de défi.
            var remoteAvatar = FindRemoteAvatarAt(gx, gy, localAvatar);
            if (remoteAvatar != null)
            {
                if (_logClicks) Debug.Log($"[HubInput] Clic sur avatar remote PlayerRef={remoteAvatar.Object.InputAuthority} -> popup défi");
                if (_challengePopup != null) _challengePopup.Show(remoteAvatar);
                return;
            }

            var path = HubPathfinder.FindPath(
                localAvatar.GridX, localAvatar.GridY,
                gx, gy,
                _grid.Width, _grid.Height,
                IsWalkable);

            if (path == null || path.Count == 0)
            {
                if (_logClicks) Debug.Log($"[HubInput] Pas de chemin de ({localAvatar.GridX},{localAvatar.GridY}) vers ({gx},{gy})");
                return;
            }

            if (_logClicks) Debug.Log($"[HubInput] Path {localAvatar.GridX},{localAvatar.GridY} -> {gx},{gy} ({path.Count} steps)");
            movement.Follow(path);
        }

        private static HubAvatar FindRemoteAvatarAt(int gx, int gy, HubAvatar localExclude)
        {
            var avatars = FindObjectsByType<HubAvatar>(FindObjectsSortMode.None);
            foreach (var av in avatars)
            {
                if (av == localExclude) continue;
                if (av.Object == null || av.Object.HasStateAuthority) continue;
                if (av.GridX == gx && av.GridY == gy) return av;
            }
            return null;
        }

        // 4.3.b/c : grille walkable par défaut, sauf tiles bannées dans le HubGridBanList SO.
        private bool IsWalkable(int gx, int gy)
        {
            if (_banList != null && _banList.IsBanned(gx, gy)) return false;
            return true;
        }
    }
}
