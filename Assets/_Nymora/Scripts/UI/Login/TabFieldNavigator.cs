using System.Collections;
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
    ///
    /// IMPORTANT (TMP) : on differe la prise de focus d'une frame (coroutine). Sinon le TMP_InputField
    /// encore focus traite SA propre touche Tab dans la meme frame (OnUpdateSelected) et se re-empare
    /// du focus / annule notre ActivateInputField -> visuellement rien ne bouge.
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
                    // Coupe le focus du champ courant tout de suite, puis active la cible la frame d'apres.
                    if (idx >= 0 && _fields[idx] is TMP_InputField cur) cur.DeactivateInputField();
                    StartCoroutine(FocusNextFrame(sel));
                    return;
                }
                next = Step(next, back);
            }
        }

        // Differe d'une frame : laisse le TMP_InputField sortant finir son traitement clavier
        // avant de poser le focus sur la cible (sinon la course de frame annule l'activation).
        private IEnumerator FocusNextFrame(Selectable sel)
        {
            yield return null;
            if (sel == null || !sel.gameObject.activeInHierarchy || !sel.interactable) yield break;

            sel.Select();
            if (sel is TMP_InputField tmp)
            {
                tmp.ActivateInputField();
                tmp.caretPosition = tmp.text.Length; // caret en fin de texte (pseudo pre-rempli)
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
