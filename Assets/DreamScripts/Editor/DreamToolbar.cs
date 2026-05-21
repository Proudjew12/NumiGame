using UnityEditor;
using UnityEditor.Toolbars;
using UnityEngine;

namespace DreamScripts.EditorTools
{
    internal static class DreamToolbar
    {
        [MainToolbarElement(
            "DreamScripts/Dropdown",
            defaultDockPosition = MainToolbarDockPosition.Right,
            defaultDockIndex = 1000)]
        private static MainToolbarElement CreateDropdown()
        {
            var content = new MainToolbarContent("DreamScripts", "Open DreamScripts tools");
            return new MainToolbarDropdown(content, OpenDropdown);
        }

        private static void OpenDropdown(Rect buttonRect)
        {
            var menu = new GenericMenu();
            var actions = DreamScriptRegistry.GetActions();

            if (actions.Count == 0)
            {
                menu.AddDisabledItem(new GUIContent("No actions registered"));
                menu.DropDown(buttonRect);
                return;
            }

            foreach (var action in actions)
            {
                if (action.IsSeparator)
                {
                    menu.AddSeparator(string.Empty);
                    continue;
                }

                var isEnabled = action.IsEnabled == null || action.IsEnabled();
                if (isEnabled)
                {
                    menu.AddItem(new GUIContent(action.Path), false, () => action.Execute());
                }
                else
                {
                    menu.AddDisabledItem(new GUIContent(action.Path));
                }
            }

            menu.DropDown(buttonRect);
        }
    }
}
