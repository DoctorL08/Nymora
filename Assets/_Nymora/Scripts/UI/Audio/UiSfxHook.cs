using System.Collections.Generic;
using Nymora.Core.Audio;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Nymora.UI.Audio
{
    /// <summary>
    /// Brique A3 — Hook SFX UI global. Joue automatiquement un son de clic / survol sur
    /// N'IMPORTE QUEL élément UI interactif (Button, Toggle, Slider, Dropdown, InputField…)
    /// sans avoir à toucher chaque bouton un par un. Détection via raycast EventSystem :
    ///   - survol : son quand le curseur ENTRE sur un nouvel élément interactif
    ///   - clic gauche : son si l'élément sous le curseur est interactif
    ///
    /// Singleton auto-bootstrappé + DontDestroyOnLoad → actif dans toutes les scènes
    /// (login, hub, combat, menus). 100% View. Les boutons non-interactables (grisés) ne
    /// sonnent pas (dégradation cohérente).
    /// </summary>
    public sealed class UiSfxHook : MonoBehaviour
    {
        private static UiSfxHook _instance;
        private static readonly List<RaycastResult> _hits = new List<RaycastResult>(16);

        private Selectable _lastHovered;
        private PointerEventData _ped;
        private EventSystem _pedOwner;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            if (_instance != null) return;
            var go = new GameObject("UiSfxHook");
            go.AddComponent<UiSfxHook>();
            DontDestroyOnLoad(go);
        }

        private void Awake()
        {
            if (_instance != null && _instance != this) { Destroy(gameObject); return; }
            _instance = this;
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        private void Update()
        {
            var es = EventSystem.current;
            if (es == null) { _lastHovered = null; return; }

            Selectable current = RaycastSelectable(es);

            // Survol : son uniquement à l'ENTRÉE sur un nouvel élément interactif.
            if (current != _lastHovered)
            {
                if (current != null && current.IsInteractable())
                    NymoraAudioManager.Instance?.PlaySfx(SoundId.UiHover);
                _lastHovered = current;
            }

            // Clic gauche sur un élément interactif.
            if (Input.GetMouseButtonDown(0) && current != null && current.IsInteractable())
                NymoraAudioManager.Instance?.PlaySfx(SoundId.UiClick);
        }

        private Selectable RaycastSelectable(EventSystem es)
        {
            // PointerEventData réutilisé (évite une alloc/frame) ; recréé si l'EventSystem change.
            if (_ped == null || _pedOwner != es) { _ped = new PointerEventData(es); _pedOwner = es; }
            _ped.position = Input.mousePosition;

            _hits.Clear();
            es.RaycastAll(_ped, _hits);
            for (int i = 0; i < _hits.Count; i++)
            {
                var go = _hits[i].gameObject;
                if (go == null) continue;
                var sel = go.GetComponentInParent<Selectable>();
                if (sel != null) return sel; // le 1er hit (topmost) qui porte un Selectable gagne
            }
            return null;
        }
    }
}
