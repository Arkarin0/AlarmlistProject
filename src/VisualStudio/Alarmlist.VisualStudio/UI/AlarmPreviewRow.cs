// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.IO;
using System.Linq;
using Alarmlist.VisualStudio.Analysis;

namespace Alarmlist.VisualStudio.UI
{
    internal sealed class AlarmPreviewRow : ObservableObject
    {
        internal AnalysisAlarm Alarm { get; }
        public string Name { get; }
        public string Code { get; }
        public string Category { get; }
        public string Description { get; }
        public string Identity => Alarm.Origin.FullyQualifiedName ?? "";
        public string Source => Alarm.Origin.Location == null ? "" : Path.GetFileName(Alarm.Origin.Location.FilePath) + ":" + Alarm.Origin.Location.LineNumber;
        public string SourcePath => Alarm.Origin.Location?.FilePath;
        public string Status => Alarm.HasEvaluatedValues ? "Resolved" : "Values unavailable; see live project diagnostics.";
        public string AutomationName => Identity + " — " + Name;
        public DelegateCommand GoToSourceCommand { get; }

        public AlarmPreviewRow(AnalysisAlarm alarm, DelegateCommand navigation)
        {
            Alarm = alarm;
            GoToSourceCommand = navigation;
            Name = Value("Name"); Code = Value("Code"); Category = Value("Category"); Description = Value("Description");
        }

        private string Value(string name) => !Alarm.HasEvaluatedValues ? "(unavailable)"
            : Alarm.Fields.FirstOrDefault(field => field.Name == name)?.EffectiveValue ?? "";

        internal string Column(string column)
        {
            switch (column)
            {
                case nameof(Name): return Name;
                case nameof(Code): return Code;
                case nameof(Category): return Category;
                case nameof(Description): return Description;
                case nameof(Identity): return Identity;
                case nameof(Source): return Source;
                default: throw new ArgumentException("Unknown preview column.", nameof(column));
            }
        }

        internal bool Matches(string filter) => new[] { Name, Code, Category, Description, Identity, SourcePath }
            .Any(value => value?.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0);
    }
}
