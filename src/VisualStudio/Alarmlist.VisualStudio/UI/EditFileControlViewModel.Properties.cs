// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Alarmlist.VisualStudio.Analysis;
using Alarmlist.VisualStudio.Editing;

namespace Alarmlist.VisualStudio.UI
{
    internal sealed partial class EditFileControlViewModel
    {
        private string _parameterScope = "Alarm";
        public IReadOnlyList<string> ParameterScopes { get; } = BindingCollection.Create(new[] { "Alarm", "File" });
        public ObservableCollection<ParameterViewModel> Parameters { get; } = new ObservableCollection<ParameterViewModel>();
        public IReadOnlyList<PropertyCompletion> CompletionProperties { get; private set; } = Array.Empty<PropertyCompletion>();
        public string ParameterError { get; private set; }
        public string NewParameterName { get; set; } = "par1";
        public bool CanEditParameters => IsValid && (FileParameters || CanEdit) && ParameterError == null;
        private bool FileParameters => ParameterScope == "File";
        public string ParameterScope
        {
            get => _parameterScope;
            set
            {
                if (value == _parameterScope || (value != "File" && value != "Alarm")) return;
                if (!CommitPending()) { Notify(); return; }
                _parameterScope = value;
                Parameters.Clear();
                RefreshAnalysis();
                Notify();
            }
        }
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
        public DelegateCommand AddParameterCommand { get; private set; }

        private void InitializeProperties()
        {
            AddParameterCommand = new DelegateCommand(() =>
            {
                if (!CommitPending() || !CanEditParameters) return;
                _model.SetProperty(_baseline, _original, new PropertyValueChange(null, NewParameterName, null), FileParameters, out _error);
                Notify(nameof(Error));
            }, () => CanEditParameters);
        }

        private void EditParameterDeclaration(ParameterViewModel row, bool delete)
        {
            string name = row.Name;
            if (!CommitPending() || !CanEditParameters) return;
            _model.SetProperty(_baseline, _original, new PropertyValueChange(delete ? name : null, delete ? null : name, null), FileParameters, out _error);
            Notify(nameof(Error));
        }

        private void RefreshProperties(ProjectAnalysisState state, AnalysisAlarm alarm)
        {
            PropertyScopeSource scope = FileParameters ? _model.Projection.Properties : _original?.Properties;
            ParameterError = scope?.Error;
            bool current = state?.Status == ProjectAnalysisStatus.Available && state.Response?.Snapshot != null && DiagnosticDocumentMatches(state);
            IReadOnlyList<AnalysisProperty> properties = current ? FileParameters
                ? state.Response.Snapshot.Sources.FirstOrDefault(source => string.Equals(source.FilePath, _analysis.DocumentPath, StringComparison.OrdinalIgnoreCase))?.Properties
                : alarm?.Properties : null;
            PropertySource[] local = scope?.Properties.ToArray() ?? Array.Empty<PropertySource>();
            if (!Parameters.Any(row => row.HasChanges))
            {
                string[] names = local.Select(property => property.Name).Concat((properties ?? Array.Empty<AnalysisProperty>())
                    .Where(property => !property.IsWellKnown).Select(property => property.Name)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
                if (!Parameters.Select(row => row.Name).SequenceEqual(names, StringComparer.OrdinalIgnoreCase))
                {
                    Parameters.Clear();
                    foreach (string name in names) Parameters.Add(new ParameterViewModel(name, OnDraftChanged, EditParameterDeclaration));
                }
                foreach (ParameterViewModel row in Parameters)
                    row.Load(local.FirstOrDefault(property => string.Equals(property.Name, row.Name, StringComparison.OrdinalIgnoreCase)), scope?.Error == null);
            }
            foreach (ParameterViewModel row in Parameters)
                row.SetAnalysis(properties?.FirstOrDefault(property => string.Equals(property.Name, row.OriginalName ?? row.Name, StringComparison.OrdinalIgnoreCase)));
            bool identityDraft = Fields.Any(field => !field.IsScalar && field.HasChanges);
            CompletionProperties = BindingCollection.Create(current && alarm?.IsResolved == true && !identityDraft && !Parameters.Any(row => row.HasChanges)
                ? alarm.Properties.Select(property => new PropertyCompletion(property, !HasPending)) : Enumerable.Empty<PropertyCompletion>());
            Notify(nameof(CompletionProperties)); Notify(nameof(ParameterError)); Notify(nameof(CanEditParameters)); Notify(nameof(IsAbstract));
            AddParameterCommand?.Refresh();
        }
    }
}
