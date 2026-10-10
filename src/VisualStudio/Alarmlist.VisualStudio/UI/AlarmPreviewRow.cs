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
        public string TestMethod { get; }
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
            TestMethod = !alarm.HasEvaluatedValues ? "(unavailable)" : string.Join("\r\n\r\n", new[]
            {
                ProcedureText("Instructions:", alarm.Instructions), ProcedureText("Reset alarm:", alarm.Reset)
            }.Where(section => section.Length != 0));
        }

        private static string ProcedureText(string heading, AnalysisSection section)
        {
            // The compiler owns inheritance, ordering and Clear exclusions.
            var entries = section?.EffectiveEntries.Where(entry => !entry.IsClear && entry.ExcludedBy == null).ToArray();
            return entries == null || entries.Length == 0 ? "" : heading + "\r\n" + string.Join("\r\n", entries.Select(entry =>
                (entry.Kind == AnalysisStepKind.Instruction ? "• " : entry.Kind + ": ") + (entry.IsValueAvailable ? entry.Text : "(unavailable)")));
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
                case nameof(TestMethod): return TestMethod;
                case nameof(Identity): return Identity;
                case nameof(Source): return Source;
                default: throw new ArgumentException("Unknown preview column.", nameof(column));
            }
        }

        internal bool Matches(string filter) => new[] { Name, Code, Category, Description, TestMethod, Identity, SourcePath }
            .Any(value => value?.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0);
    }
}
