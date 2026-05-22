using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Nymora.UI.Login
{
    /// <summary>
    /// Navigation au clavier entre des champs via Tab (et Shift+Tab pour revenir en arriere).
    ///
    /// A poser SUR le panneau qui contient les champs : comme un GameObject inactif ne tourne pas
    /// son Update, seul le panneau actif (Connexion OU Inscription) reagit a Tab.
    /// La liste _fields est ordonnee ; le focus cycle (wrap) en sautant les champs inactifs/non-interactables.
    /// </summary>
    public class TabFieldNavigator : MonoBehaviour
    {
        [SerializeField] private List<Selectable> _fields = new List<Selectable>();

        private void Update()
        {
            if (!Input.GetKeyDown(KeyCode.Tab)) return;
            if (_fields == null || _fields.Count == 0) return;

            var es = EventSystem.current;
            if (es == null) return;

            bool back = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

            // Index du champ actuellement selectionne (-1 si aucun de la liste).
            var current = es.currentSelectedGameObject;
            int idx = -1;
            for (int i = 0; i < _fields.Count; i++)
            {
                if (_fields[i] != null && _fields[i].gameObject == current) { idx = i; break; }
            }

            int next = idx < 0 ? 0 : Step(idx, back);
            for (int tries = 0; tries < _fields.Count; tries++)
            {
                var sel = _fields[next];
                if (sel != null && sel.gameObject.activeInHierarchy && sel.interactable)
                {
                    sel.Select();
                    if (sel is TMP_InputField tmp) tmp.ActivateInputField();
                    return;
                }
                next = Step(next, back);
            }
        }

        private int Step(int i, bool back)
        {
            int n = back ? i - 1 : i + 1;
            if (n < 0) n = _fields.Count - 1;
            if (n >= _fields.Count) n = 0;
            return n;
        }
    }
}
