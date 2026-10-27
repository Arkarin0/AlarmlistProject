// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.Linq;

namespace Alarmlist.VisualStudio.Editor
{
    // Document lifetime, independent of a designer/XML view and of analysis
    // generations. Never turns enumeration order into a consumer preference.
    internal sealed class DocumentProjectSelection
    {
        private readonly object _gate = new object();
        private string _selected;
        private string _opening;
        private string _implicitOwner;
        private bool _decided;
        private bool _requireChoice;
        public event EventHandler Changed;

        public void SetOpeningProject(string path)
        {
            bool changed = false;
            lock (_gate)
                if (!_decided && _opening == null && !string.IsNullOrEmpty(path)) { _opening = path; changed = true; }
            if (changed) Changed?.Invoke(this, EventArgs.Empty);
        }

        public string Resolve(IReadOnlyList<string> owners)
        {
            bool changed = false;
            string result;
            lock (_gate)
            {
                if (_implicitOwner != null && !owners.Contains(_implicitOwner, StringComparer.OrdinalIgnoreCase))
                { _requireChoice = true; _decided = true; _opening = null; changed = true; }
                _implicitOwner = null;
                if (_selected != null && !owners.Contains(_selected, StringComparer.OrdinalIgnoreCase))
                { _selected = null; _opening = null; _requireChoice = true; changed = true; }
                if (!_decided && _selected == null && _opening != null && owners.Contains(_opening, StringComparer.OrdinalIgnoreCase))
                { _selected = _opening; _opening = null; _decided = true; changed = true; }
                result = _selected ?? (_opening == null && !_requireChoice && owners.Count == 1 ? owners[0] : null);
                if (_selected == null) _implicitOwner = result;
            }
            if (changed) Changed?.Invoke(this, EventArgs.Empty);
            return result;
        }

        public void Select(string path, IReadOnlyList<string> owners)
        {
            lock (_gate)
            {
                string selected = owners.FirstOrDefault(owner => string.Equals(owner, path, StringComparison.OrdinalIgnoreCase));
                if (_decided && string.Equals(_selected, selected, StringComparison.OrdinalIgnoreCase)) return;
                _selected = selected; _opening = null; _decided = true; _requireChoice = selected == null;
            }
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }
}
