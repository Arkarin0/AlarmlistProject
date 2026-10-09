// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.Linq;
using Alarmlist.VisualStudio.Analysis;
using Alarmlist.VisualStudio.Editing;

namespace Alarmlist.VisualStudio.UI
{
    internal sealed partial class EditFileControlViewModel
    {
        public ParameterListViewModel FileParameters { get; private set; }
        public ParameterListViewModel AlarmParameters { get; private set; }
        private IEnumerable<ParameterViewModel> AllParameters => FileParameters.Rows.Concat(AlarmParameters.Rows);
        public IReadOnlyList<PropertyCompletion> CompletionProperties { get; private set; } = Array.Empty<PropertyCompletion>();
        public IReadOnlyList<ArgumentCompletionBinding> CompletionBindings { get; private set; } = Array.Empty<ArgumentCompletionBinding>();
        public bool IsAbstract
        {
            get => _original?.IsAbstract == true;
            set
            {
                if (value == IsAbstract) return;
                if (CommitPending() && CanEdit) _model.SetAbstract(_baseline, _original, value, out _error);
                Notify(); Notify(nameof(Error));
            }
        }

        private void InitializeProperties()
        {
            FileParameters = new ParameterListViewModel(true, CommitPending, OnDraftChanged, EditParameter);
            AlarmParameters = new ParameterListViewModel(false, CommitPending, OnDraftChanged, EditParameter);
        }

        private void EditParameter(ParameterListViewModel list, ParameterViewModel row, ParameterAction action)
        {
            string name = row?.Name;
            if (!CommitPending() || !list.CanEdit) return;
            if (action == ParameterAction.Add)
            {
                int index = 1;
                do { name = "par" + index++; } while (list.Rows.Any(item => string.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase)));
                _model.SetProperty(_baseline, _original, new PropertyValueChange(null, name, null), list.IsFile, out _error);
            }
            else
            {
                row = list.Rows.FirstOrDefault(item => string.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase));
                if (row == null || (row.IsLocal && !row.CanEdit)) return;
                if (action == ParameterAction.Declare && row.IsLocal) return;
                if (action == ParameterAction.Remove && !row.IsLocal) return;
                string value = action == ParameterAction.AssignEmpty ? ""
                    : action == ParameterAction.Assign ? row.HasValue ? row.Value : "" : null;
                _model.SetProperty(_baseline, _original, new PropertyValueChange(row.OriginalName,
                    action == ParameterAction.Remove ? null : name, value), list.IsFile, out _error);
            }
            list.SelectName(name);
            Notify(nameof(Error));
        }

        private static PropertyValueChange[] ParameterChanges(ParameterListViewModel list) => list.Rows.Where(row => row.HasChanges)
            .Select(row => new PropertyValueChange(row.OriginalName, row.Name, row.HasValue ? row.Value : null)).ToArray();

        private void RefreshProperties(ProjectAnalysisState state, AnalysisAlarm alarm)
        {
            bool current = state?.Status == ProjectAnalysisStatus.Available && state.Response?.Snapshot != null && DiagnosticDocumentMatches(state);
            bool parameterDraft = AllParameters.Any(row => row.HasChanges);
            IReadOnlyList<AnalysisProperty> fileProperties = current ? state.Response.Snapshot.Sources
                .FirstOrDefault(source => string.Equals(source.FilePath, _analysis.DocumentPath, StringComparison.OrdinalIgnoreCase))?.Properties : null;
            FileParameters.Refresh(_model.Projection.Properties, fileProperties, IsValid, current && !parameterDraft);
            AlarmParameters.Refresh(_original?.Properties, current ? alarm?.Properties : null, CanEdit, current && !parameterDraft);
            bool identityDraft = Fields.Any(field => !field.IsScalar && field.HasChanges);
            CompletionBindings = BindingCollection.Create(current && !identityDraft && !parameterDraft
                ? state.Response.Snapshot.ArgumentBindings.Select(binding => new ArgumentCompletionBinding(binding))
                : Enumerable.Empty<ArgumentCompletionBinding>());
            Notify(nameof(CompletionBindings));
            CompletionProperties = BindingCollection.Create(current && alarm?.IsResolved == true && !identityDraft && !parameterDraft
                ? alarm.Properties.Select(property => new PropertyCompletion(property, !HasPending))
                    .Concat(state.Response.Snapshot.Translations.OrderBy(translation => translation.Key, StringComparer.Ordinal)
                        .Select(translation => new PropertyCompletion(translation,
                            alarm.LocalizationUses.FirstOrDefault(use => use.Translation.Key == translation.Key), !HasPending)))
                    .Concat(TemplateCompletions(state.Response.Snapshot))
                    .Concat(LookupCompletions(state.Response.Snapshot))
                    .Concat(ListCompletions(state.Response.Snapshot))
                : Enumerable.Empty<PropertyCompletion>());
            Notify(nameof(CompletionProperties)); Notify(nameof(IsAbstract));
        }

        private static IEnumerable<PropertyCompletion> TemplateCompletions(AnalysisSnapshot snapshot)
        {
            foreach (AnalysisTranslation translation in snapshot.Translations.OrderBy(item => item.Key, StringComparer.Ordinal))
            {
                // Keyword mode has no supplying language. Offer each distinct signature
                // explicitly, using compiler-parsed names without interpreting RESX here.
                IEnumerable<IReadOnlyList<string>> signatures = translation.IsKeyword
                    ? snapshot.ResourceEntries.Where(entry => entry.Key == translation.Key && entry.IsValid && !entry.IsAmbiguous)
                        .Select(entry => entry.PlaceholderNames)
                    : new[] { translation.PlaceholderNames };
                foreach (var signature in signatures.Where(names => names.Count != 0)
                    .GroupBy(names => string.Join("\0", names.OrderBy(name => name, StringComparer.Ordinal)), StringComparer.Ordinal))
                    yield return new PropertyCompletion(translation.Key, signature.First());
            }
        }

        private static IEnumerable<PropertyCompletion> LookupCompletions(AnalysisSnapshot snapshot)
        {
            foreach (AnalysisLookupPath path in snapshot.LookupPaths.Where(item => !item.IsAmbiguous).OrderBy(item => item.Key, StringComparer.Ordinal))
            {
                yield return new PropertyCompletion(path.Expression, path.Value, "path");
                foreach (AnalysisLookupMember member in path.Members)
                {
                    string metadata = string.Join("; ", member.Metadata.Select(item => item.Name + "=" + item.Value));
                    yield return new PropertyCompletion(member.Expression, member.Value, member.Kind + (metadata.Length == 0 ? "" : "; " + metadata));
                    foreach (AnalysisLookupMetadata item in member.Metadata)
                        yield return new PropertyCompletion(item.Expression, item.Value, "stored metadata; inserted only when selected");
                }
            }
        }

        private static IEnumerable<PropertyCompletion> ListCompletions(AnalysisSnapshot snapshot)
        {
            foreach (AnalysisLookupList list in snapshot.LookupLists.OrderBy(item => item.Key, StringComparer.Ordinal))
            {
                AnalysisArgumentBinding[] bindings = snapshot.ArgumentBindings.Where(binding => binding.IsAvailable && binding.ListKey == list.Key).ToArray();
                foreach (AnalysisLookupListEntry entry in list.Entries.Where(item => !item.IsAmbiguous).OrderBy(item => item.Key, StringComparer.Ordinal))
                    yield return new PropertyCompletion(entry, bindings);
            }
        }
    }
}
