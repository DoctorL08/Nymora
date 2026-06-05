using System;
using UnityEngine;

namespace Nymora.Core.Input
{
    /// <summary>
    /// 5 juin 2026 — Capture de touche pour le REBIND (onglet « Raccourcis » des Paramètres).
    ///
    /// HubMenuSettings n'est pas un MonoBehaviour : la boucle de capture est pilotée par
    /// <c>HubMenuShell.Update</c> (MonoBehaviour) qui appelle <see cref="Tick"/> chaque frame.
    ///   - <see cref="Begin"/> arme la capture pour une action (le bouton de la ligne affiche « … »).
    ///   - Le prochain GetKeyDown clavier (hors Échap) est stocké via KeybindingService.SetKey, puis
    ///     <c>onDone</c> est rappelé pour rafraîchir l'UI.
    ///   - Échap = ANNULE la capture sans changer le binding.
    /// Souris et manettes sont ignorées (raccourcis clavier uniquement).
    /// </summary>
    public static class KeyRebindCapture
    {
        public static bool IsCapturing { get; private set; }
        public static Keybind Capturing { get; private set; }
        private static Action _onDone;

        public static void Begin(Keybind b, Action onDone)
        {
            Capturing = b;
            _onDone = onDone;
            IsCapturing = true;
        }

        public static void Cancel()
        {
            IsCapturing = false;
            _onDone = null;
        }

        /// <summary>À appeler chaque frame depuis le hub tant que <see cref="IsCapturing"/>.</summary>
        public static void Tick()
        {
            if (!IsCapturing) return;
            if (!UnityEngine.Input.anyKeyDown) return;

            // Échap = annule (ne touche pas au binding).
            if (UnityEngine.Input.GetKeyDown(KeyCode.Escape)) { Finish(); return; }

            foreach (KeyCode k in Enum.GetValues(typeof(KeyCode)))
            {
                if (k == KeyCode.None) continue;
                if (k >= KeyCode.Mouse0 && k <= KeyCode.Mouse6) continue; // pas la souris
                if (k >= KeyCode.JoystickButton0) continue;               // pas les manettes
                if (UnityEngine.Input.GetKeyDown(k))
                {
                    KeybindingService.SetKey(Capturing, k);
                    Finish();
                    return;
                }
            }
        }

        private static void Finish()
        {
            Action cb = _onDone;
            Cancel();
            cb?.Invoke();
        }
    }
}
