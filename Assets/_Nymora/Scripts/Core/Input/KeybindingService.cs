using System;
using UnityEngine;

namespace Nymora.Core.Input
{
    /// <summary>
    /// Actions rebindables du jeu. L'ordre sert au tri/UI : d'abord le COMBAT, puis le HUB
    /// (cf <see cref="KeybindingService.IsCombat"/>). NE PAS réordonner sans adapter IsCombat.
    /// </summary>
    public enum Keybind
    {
        // ----- COMBAT -----
        CombatEndTurn,
        CombatSpell1,
        CombatSpell2,
        CombatSpell3,
        CombatSpell4,
        CombatSpell5,
        CombatSpell6,
        CombatSpell7,

        // ----- HUB -----
        HubShop,
        HubCharacter,
        HubReplay,
        HubArena,
        HubBattlePass,
    }

    /// <summary>
    /// 5 juin 2026 — Service central de RACCOURCIS clavier rebindables.
    ///
    /// Source unique pour les inputs combat (passer le tour, slots de sorts) ET hub (ouvrir
    /// Boutique / Personnage / Replay / Arène / Battle Pass). Les handlers d'input lisent
    /// <see cref="GetKey"/> au lieu de KeyCode en dur ; l'onglet « Raccourcis » des Paramètres
    /// écrit via <see cref="SetKey"/>.
    ///
    /// REBINDABLE = règle le souci AZERTY (cf memory user_keyboard_azerty) : on ne devine plus
    /// la position QWERTY, le joueur CAPTURE la vraie touche pressée (on stocke son KeyCode).
    ///
    /// Persistance : PlayerPrefs (clé "keybind_&lt;Keybind&gt;" = (int)KeyCode). Absent = défaut.
    /// Static + sans état caché : chaque GetKey relit PlayerPrefs (cheap, appelé sur GetKeyDown).
    /// </summary>
    public static class KeybindingService
    {
        private const string PrefPrefix = "keybind_";

        /// <summary>Dernière action dont le binding a changé (l'UI peut s'y abonner pour rafraîchir).</summary>
        public static event Action<Keybind> OnBindingChanged;

        /// <summary>Binding par défaut (clavier d'usine). Combat : F1 + chiffres 1-7. Hub : B/P/R/A/K.</summary>
        public static KeyCode GetDefault(Keybind b)
        {
            switch (b)
            {
                case Keybind.CombatEndTurn:  return KeyCode.F1;
                case Keybind.CombatSpell1:   return KeyCode.Alpha1;
                case Keybind.CombatSpell2:   return KeyCode.Alpha2;
                case Keybind.CombatSpell3:   return KeyCode.Alpha3;
                case Keybind.CombatSpell4:   return KeyCode.Alpha4;
                case Keybind.CombatSpell5:   return KeyCode.Alpha5;
                case Keybind.CombatSpell6:   return KeyCode.Alpha6;
                case Keybind.CombatSpell7:   return KeyCode.Alpha7;
                case Keybind.HubShop:        return KeyCode.B; // Boutique
                case Keybind.HubCharacter:   return KeyCode.P; // Personnage
                case Keybind.HubReplay:      return KeyCode.R; // Replay
                case Keybind.HubArena:       return KeyCode.Q; // Arène — touche "A" AZERTY = position physique KeyCode.Q (A/Q décalés sur AZERTY ; B/P/R/K identiques)
                case Keybind.HubBattlePass:  return KeyCode.K; // Battle Pass
                default:                     return KeyCode.None;
            }
        }

        /// <summary>Touche ACTUELLE d'une action (custom si rebindée, sinon le défaut).</summary>
        public static KeyCode GetKey(Keybind b)
        {
            int v = PlayerPrefs.GetInt(PrefPrefix + b, (int)GetDefault(b));
            return (KeyCode)v;
        }

        /// <summary>Rebinde une action et persiste. KeyCode.None = action désactivée (aucun raccourci).</summary>
        public static void SetKey(Keybind b, KeyCode key)
        {
            PlayerPrefs.SetInt(PrefPrefix + b, (int)key);
            PlayerPrefs.Save();
            OnBindingChanged?.Invoke(b);
        }

        /// <summary>Restaure le défaut d'usine d'une action.</summary>
        public static void ResetToDefault(Keybind b)
        {
            PlayerPrefs.DeleteKey(PrefPrefix + b);
            PlayerPrefs.Save();
            OnBindingChanged?.Invoke(b);
        }

        /// <summary>Restaure tous les défauts.</summary>
        public static void ResetAll()
        {
            foreach (Keybind b in Enum.GetValues(typeof(Keybind)))
                PlayerPrefs.DeleteKey(PrefPrefix + b);
            PlayerPrefs.Save();
            foreach (Keybind b in Enum.GetValues(typeof(Keybind)))
                OnBindingChanged?.Invoke(b);
        }

        /// <summary>Raccourci pratique pour les handlers d'input : "est-ce que CETTE action est pressée ce frame ?".</summary>
        public static bool GetDown(Keybind b)
        {
            KeyCode k = GetKey(b);
            return k != KeyCode.None && UnityEngine.Input.GetKeyDown(k);
        }

        // ================= Métadonnées pour l'UI =================

        /// <summary>True si l'action appartient à la section COMBAT (sinon HUB). Repose sur l'ordre de l'enum.</summary>
        public static bool IsCombat(Keybind b) => b <= Keybind.CombatSpell7;

        /// <summary>Libellé lisible de l'action (affiché dans l'onglet Raccourcis).</summary>
        public static string DisplayName(Keybind b)
        {
            switch (b)
            {
                case Keybind.CombatEndTurn:  return "Passer le tour";
                case Keybind.CombatSpell1:   return "Sort 1";
                case Keybind.CombatSpell2:   return "Sort 2";
                case Keybind.CombatSpell3:   return "Sort 3";
                case Keybind.CombatSpell4:   return "Sort 4";
                case Keybind.CombatSpell5:   return "Sort 5";
                case Keybind.CombatSpell6:   return "Sort 6";
                case Keybind.CombatSpell7:   return "Signature";
                case Keybind.HubShop:        return "Boutique";
                case Keybind.HubCharacter:   return "Personnage";
                case Keybind.HubReplay:      return "Replay";
                case Keybind.HubArena:       return "Arène";
                case Keybind.HubBattlePass:  return "Battle Pass";
                default:                     return b.ToString();
            }
        }

        /// <summary>
        /// Libellé court d'une touche pour l'UI (ex : "F1", "1", "B", "Échap", "—").
        /// Les rangées de chiffres affichent le chiffre. Note AZERTY : le libellé reste la
        /// position QWERTY (Unity n'expose pas le label OS) — le rebind, lui, capture la vraie
        /// touche pressée, donc le raccourci FONCTIONNE quel que soit le clavier.
        /// </summary>
        public static string KeyLabel(KeyCode k)
        {
            if (k == KeyCode.None) return "—";
            if (k >= KeyCode.Alpha0 && k <= KeyCode.Alpha9) return ((int)k - (int)KeyCode.Alpha0).ToString();
            if (k >= KeyCode.Keypad0 && k <= KeyCode.Keypad9) return "Num" + ((int)k - (int)KeyCode.Keypad0);
            switch (k)
            {
                case KeyCode.Escape:    return "Échap";
                case KeyCode.Return:    return "Entrée";
                case KeyCode.Space:     return "Espace";
                case KeyCode.LeftShift: return "Maj G";
                case KeyCode.RightShift:return "Maj D";
                case KeyCode.LeftControl: return "Ctrl G";
                case KeyCode.RightControl:return "Ctrl D";
                case KeyCode.LeftAlt:   return "Alt G";
                case KeyCode.RightAlt:  return "Alt D";
                default:                return k.ToString();
            }
        }

        /// <summary>
        /// Libellé FR (AZERTY) d'une touche pour l'AFFICHAGE de l'onglet Raccourcis. Unity n'expose pas
        /// le label OS, donc on remappe à la main les touches décalées sur AZERTY (A/Q, Z/W) + la
        /// rangée de chiffres (&amp;é"'(-è_çà). Hypothèse : public FR/AZERTY (positionnement du jeu) ;
        /// le rebind, lui, reste layout-agnostique (on capture la VRAIE touche). Fallback = KeyLabel.
        /// </summary>
        public static string KeyLabelFr(KeyCode k)
        {
            switch (k)
            {
                case KeyCode.Q:      return "A";  // position physique QWERTY-Q = touche "A" AZERTY
                case KeyCode.A:      return "Q";
                case KeyCode.W:      return "Z";
                case KeyCode.Z:      return "W";
                case KeyCode.Alpha1: return "&";
                case KeyCode.Alpha2: return "é";
                case KeyCode.Alpha3: return "\"";
                case KeyCode.Alpha4: return "'";
                case KeyCode.Alpha5: return "(";
                case KeyCode.Alpha6: return "-";
                case KeyCode.Alpha7: return "è";
                case KeyCode.Alpha8: return "_";
                case KeyCode.Alpha9: return "ç";
                case KeyCode.Alpha0: return "à";
                default:             return KeyLabel(k);
            }
        }
    }
}
