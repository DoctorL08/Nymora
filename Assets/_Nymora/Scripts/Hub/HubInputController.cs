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

            // POLISH-7 polish (20 mai) — detection click avatar par BOUNDS DU SPRITE en priorite
            // (avant le mapping grille). Permet de cliquer sur un sprite avatar meme s'il deborde
            // de sa tile iso (cas frequent : sprites scale > 1, pivot non centre, animations).
            // Le pattern miroir le FindAvatarAtMouse de HubAvatarHoverTooltip.
            var remoteAvatar = FindRemoteAvatarBySpriteBounds(mouseWorld, localAvatar);
            if (remoteAvatar != null)
            {
                if (_logClicks) Debug.Log($"[HubInput] Clic sur sprite remote PlayerRef={remoteAvatar.Object.InputAuthority} -> popup");
                if (_challengePopup != null) _challengePopup.Show(remoteAvatar);
                return;
            }

            (int gx, int gy) = IsoProjection.WorldToGrid(mouseWorld, _grid.TileWorldWidth, _grid.TileWorldHeight, _grid.CenterOffset);

            if (gx < 0 || gy < 0 || gx >= _grid.Width || gy >= _grid.Height)
            {
                if (_logClicks) Debug.Log($"[HubInput] Clic hors grille (gx={gx}, gy={gy}) — ignore");
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
            // 5.3.g.bis polish multi (17 mai nuit) — push facing reseau IMMEDIATEMENT au clic,
            // avant que BeginNextStep ne se declenche frame suivante. Gain visible cote remote :
            // NetFacing peut rattraper le prochain tick Fusion meme s'il part juste apres
            // (sinon on perdait 1 frame + potentiellement 33ms d'attente du tick suivant).
            var firstStep = path[0];
            localAvatar.PrimeFacingForNextStep(firstStep.gx, firstStep.gy);
            movement.Follow(path);
        }

        /// <summary>
        /// POLISH-7 polish (20 mai) — Detection click avatar par bounds du SpriteRenderer.
        /// Pattern miroir HubAvatarHoverTooltip.FindAvatarAtMouse : iterate tous les avatars
        /// remote actifs, check si mouseWorld est dans les bounds du SpriteRenderer du child
        /// Visual. Si plusieurs avatars chevauchent visuellement, le plus haut sortingOrder gagne.
        /// </summary>
        private static HubAvatar FindRemoteAvatarBySpriteBounds(Vector3 mouseWorld, HubAvatar localExclude)
        {
            var avatars = FindObjectsByType<HubAvatar>(FindObjectsSortMode.None);
            HubAvatar best = null;
            int bestSortingOrder = int.MinValue;
            foreach (var av in avatars)
            {
                if (av == localExclude) continue;
                if (av.Object == null || av.Object.HasStateAuthority) continue;
                if (!av.isActiveAndEnabled) continue;
                var sr = av.GetComponentInChildren<SpriteRenderer>();
                if (sr == null || !sr.enabled || sr.sprite == null) continue;
                Bounds b = sr.bounds;
                if (mouseWorld.x < b.min.x || mouseWorld.x > b.max.x) continue;
                if (mouseWorld.y < b.min.y || mouseWorld.y > b.max.y) continue;
                if (sr.sortingOrder > bestSortingOrder)
                {
                    bestSortingOrder = sr.sortingOrder;
                    best = av;
                }
            }
            return best;
        }

        // 4.3.b/c : grille walkable par défaut, sauf tiles bannées dans le HubGridBanList SO.
        private bool IsWalkable(int gx, int gy)
        {
            if (_banList != null && _banList.IsBanned(gx, gy)) return false;
            return true;
        }
    }
}
