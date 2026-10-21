// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.Linq;

namespace Alarmlist.VisualStudio.UI
{
    internal sealed class ProcedureSearchMatch : ObservableObject
    {
        public string Key { get; }
        public string Label { get; }
        public ProcedureSectionViewModel Section { get; }
        public object Target { get; }
        public ProcedureSearchMatch(string key, string label, ProcedureSectionViewModel section, object target)
        { Key = key; Label = label; Section = section; Target = target; }
    }

    internal sealed partial class EditFileControlViewModel
    {
        private string _procedureSearch = string.Empty;
        private string _procedureSearchAlarm;
        private ProcedureSearchMatch _selectedProcedureMatch;
        internal bool IsRefreshingProcedureSearch { get; private set; }
        public string ProcedureSearch
        {
            get => _procedureSearch;
            set
            {
                if (IsRefreshingProcedureSearch) return;
                if (_procedureSearch == (value ?? string.Empty)) return;
                _procedureSearch = value ?? string.Empty;
                _selectedProcedureMatch = null;
                RefreshProcedureSearch();
                Notify();
            }
        }
        public IReadOnlyList<ProcedureSearchMatch> ProcedureMatches { get; private set; } = Array.Empty<ProcedureSearchMatch>();
        public bool HasProcedureSearch => ProcedureSearch.Length != 0;
        public string ProcedureSearchStatus => !HasProcedureSearch ? string.Empty
            : ProcedureMatches.Count == 0 ? "No procedure matches."
            : (SelectedProcedureMatch == null ? "" : (ProcedureMatches.ToList().IndexOf(SelectedProcedureMatch) + 1) + " of ")
                + ProcedureMatches.Count + " matches";
        public ProcedureSearchMatch SelectedProcedureMatch
        {
            get => _selectedProcedureMatch;
            set
            {
                if (IsRefreshingProcedureSearch) return;
                // ItemsSource replacement may briefly clear selection. Refresh
                // owns replacement; stale targets can never reveal another row.
                if (value != null && !ProcedureMatches.Contains(value)) return;
                if (ReferenceEquals(_selectedProcedureMatch, value)) return;
                _selectedProcedureMatch = value;
                if (value != null)
                {
                    value.Section.IsExpanded = true;
                    value.Section.ShowCleared = true;
                }
                Notify(); Notify(nameof(ProcedureSearchStatus));
            }
        }

        internal void MoveProcedureMatch(int direction)
        {
            if (ProcedureMatches.Count == 0) return;
            int index = SelectedProcedureMatch == null ? (direction > 0 ? -1 : 0)
                : ProcedureMatches.ToList().IndexOf(SelectedProcedureMatch);
            SelectedProcedureMatch = ProcedureMatches[(index + direction + ProcedureMatches.Count) % ProcedureMatches.Count];
            // Re-reveal even a single selected result after a manual collapse.
            SelectedProcedureMatch.Section.IsExpanded = true;
            SelectedProcedureMatch.Section.ShowCleared = true;
        }

        private void RefreshProcedureSearch()
        {
            string identity = Selected?.Source.Identity;
            string selectedKey = identity == _procedureSearchAlarm ? _selectedProcedureMatch?.Key : null;
            _procedureSearchAlarm = identity;
            var matches = new List<ProcedureSearchMatch>();
            if (HasProcedureSearch)
            {
                foreach (ProcedureSectionViewModel section in ProcedureSections)
                {
                    Add(section, "title", section.TitleField, "Title / ID", section.Name,
                        section.TitleField.EditValue, section.TitleField.CanShowResolved ? section.TitleField.ResolvedValue : null);
                    for (int index = 0; index < section.InheritedSequence.Count; index++)
                    {
                        ProcedureAnalysisRow row = section.InheritedSequence[index];
                        Add(section, "inherited:" + index, row, row.State + " " + row.Kind, row.Text, row.Entry.RawText);
                    }
                    foreach (ProcedureItemViewModel row in section.Items)
                        Add(section, "local:" + row.Index, row, row.State + " " + row.Kind + " " + row.Number,
                            row.IsClear ? "Clear preceding entries" : row.Text, row.CanShowResolved ? row.ResolvedText : null);
                }
            }
            ProcedureMatches = BindingCollection.Create(matches);
            _selectedProcedureMatch = matches.FirstOrDefault(match => match.Key == selectedKey);
            IsRefreshingProcedureSearch = true;
            try
            {
                Notify(nameof(ProcedureMatches)); Notify(nameof(SelectedProcedureMatch));
                Notify(nameof(HasProcedureSearch)); Notify(nameof(ProcedureSearchStatus));
            }
            finally { IsRefreshingProcedureSearch = false; }

            void Add(ProcedureSectionViewModel section, string key, object target, string kind, params string[] texts)
            {
                string[] values = texts.Where(text => !string.IsNullOrEmpty(text)).Distinct().ToArray();
                if (!values.Any(text => text.IndexOf(ProcedureSearch, StringComparison.OrdinalIgnoreCase) >= 0)) return;
                matches.Add(new ProcedureSearchMatch(section.Name + ":" + key,
                    section.Name + " · " + kind + ": " + string.Join(" / ", values), section, target));
            }
        }
    }
}
