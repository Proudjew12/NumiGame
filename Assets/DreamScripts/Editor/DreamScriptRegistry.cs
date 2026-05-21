using System;
using System.Collections.Generic;

namespace DreamScripts.EditorTools
{
    internal sealed class DreamToolbarAction
    {
        public DreamToolbarAction(string path, Action execute, int priority, Func<bool> isEnabled, bool isSeparator = false)
        {
            Path = path;
            Execute = execute;
            Priority = priority;
            IsEnabled = isEnabled;
            IsSeparator = isSeparator;
        }

        public string Path { get; }
        public Action Execute { get; }
        public int Priority { get; }
        public Func<bool> IsEnabled { get; }
        public bool IsSeparator { get; }
    }

    internal static class DreamScriptRegistry
    {
        private static readonly List<DreamToolbarAction> Actions = new List<DreamToolbarAction>();
        private static bool s_needsSort = true;

        public static void Register(string path, Action execute, int priority = 0, Func<bool> isEnabled = null)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException("Action path is required.", nameof(path));
            }

            if (execute == null)
            {
                throw new ArgumentNullException(nameof(execute));
            }

            Actions.RemoveAll(action => string.Equals(action.Path, path, StringComparison.Ordinal));
            Actions.Add(new DreamToolbarAction(path.Trim(), execute, priority, isEnabled));
            s_needsSort = true;
        }

        public static void RegisterSeparator(string id, int priority = 0)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentException("Separator id is required.", nameof(id));
            }

            var path = "__separator__/" + id.Trim();
            Actions.RemoveAll(action => string.Equals(action.Path, path, StringComparison.Ordinal));
            Actions.Add(new DreamToolbarAction(path, null, priority, null, isSeparator: true));
            s_needsSort = true;
        }

        public static IReadOnlyList<DreamToolbarAction> GetActions()
        {
            if (!s_needsSort)
            {
                return Actions;
            }

            Actions.Sort(CompareActions);
            s_needsSort = false;
            return Actions;
        }

        private static int CompareActions(DreamToolbarAction a, DreamToolbarAction b)
        {
            var priorityCompare = a.Priority.CompareTo(b.Priority);
            if (priorityCompare != 0)
            {
                return priorityCompare;
            }

            return string.Compare(a.Path, b.Path, StringComparison.OrdinalIgnoreCase);
        }
    }
}
