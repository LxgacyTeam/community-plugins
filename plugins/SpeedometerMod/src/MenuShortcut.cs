using System.Collections.Generic;
using BepInEx.Configuration;
using UnityEngine;

namespace SpeedometerMod
{
    internal static class MenuShortcut
    {
        private static readonly KeyCode[][] ModifierGroups =
        {
            new[] { KeyCode.LeftControl, KeyCode.RightControl },
            new[] { KeyCode.LeftAlt, KeyCode.RightAlt, KeyCode.AltGr },
            new[] { KeyCode.LeftShift, KeyCode.RightShift },
            new[] { KeyCode.LeftCommand, KeyCode.RightCommand, KeyCode.LeftWindows, KeyCode.RightWindows }
        };

        internal static bool IsModifier(KeyCode key)
        {
            foreach (var group in ModifierGroups)
                foreach (var variant in group) if (key == variant) return true;
            return false;
        }

        private static bool IsHeld(KeyCode[] group)
        {
            foreach (var key in group) if (Input.GetKey(key)) return true;
            return false;
        }

        internal static KeyboardShortcut Capture(KeyCode key)
        {
            var modifiers = new List<KeyCode>();
            foreach (var group in ModifierGroups) if (IsHeld(group)) modifiers.Add(group[0]);
            return new KeyboardShortcut(key, modifiers.ToArray());
        }

        internal static bool IsDown(KeyboardShortcut shortcut)
        {
            if (shortcut.MainKey == KeyCode.None || !Input.GetKeyDown(shortcut.MainKey)) return false;
            foreach (var group in ModifierGroups)
            {
                bool required = false;
                foreach (var modifier in shortcut.Modifiers)
                    foreach (var variant in group) if (modifier == variant) required = true;
                if (required != IsHeld(group)) return false;
            }
            return true;
        }
    }
}
