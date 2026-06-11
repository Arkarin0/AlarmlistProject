// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Alarmlist.Diagnostics;
using Alarmlist.Syntax;
using Alarmlist.Text;

namespace Alarmlist.Binding
{
    public class Binder
    {
        protected readonly AlarmSyntaxTree syntaxTree;

        protected readonly List<SourceText> sourceTexts;

        public Binder() {
            syntaxTree = new AlarmSyntaxTree();
            sourceTexts = new List<SourceText>();
        }

        public BindingResult Bind()
        {
            var diagnostics = new DiagnosticBag();
            syntaxTree.Alarms.Clear();

            foreach (var item in sourceTexts)
            {
                var sourceTree = item.Read();
                if (sourceTree == null)
                    continue;

                sourceTree.Alarms.ToList().ForEach(alarm => syntaxTree.Alarms.Add(alarm));
            }

            ResolveReferences(syntaxTree, diagnostics);

            return new BindingResult(syntaxTree, diagnostics);
        }

        private static void ResolveReferences(AlarmSyntaxTree tree, DiagnosticBag diagnostics)
        {
            var alarmsByName = new Dictionary<string, AlarmSyntaxNode>(StringComparer.Ordinal);

            foreach (var alarm in tree.Alarms)
            {
                AlarmSyntaxNode.ClearResolvedReference(alarm);

                if (string.IsNullOrWhiteSpace(alarm.FullyQualifiedName))
                    continue;

                if (alarmsByName.ContainsKey(alarm.FullyQualifiedName))
                {
                    diagnostics.ReportDuplicateAlarmName(alarm.FullyQualifiedName);
                    continue;
                }

                alarmsByName.Add(alarm.FullyQualifiedName, alarm);
            }

            foreach (var alarm in tree.Alarms)
            {
                if (string.IsNullOrWhiteSpace(alarm.ReferenceName))
                    continue;

                if (alarm.ReferenceName == alarm.FullyQualifiedName)
                {
                    diagnostics.ReportSelfReference(alarm.FullyQualifiedName);
                    continue;
                }

                if (!alarmsByName.TryGetValue(alarm.ReferenceName, out var reference))
                {
                    diagnostics.ReportMissingReference(alarm.FullyQualifiedName, alarm.ReferenceName);
                    continue;
                }

                AlarmSyntaxNode.SetReference(alarm, reference);
            }

            DetectReferenceCycles(tree, diagnostics);
        }

        private static void DetectReferenceCycles(AlarmSyntaxTree tree, DiagnosticBag diagnostics)
        {
            var states = new Dictionary<AlarmSyntaxNode, int>();
            var path = new Stack<AlarmSyntaxNode>();

            foreach (var alarm in tree.Alarms)
                Visit(alarm, states, path, diagnostics);
        }

        private static void Visit(AlarmSyntaxNode alarm, Dictionary<AlarmSyntaxNode, int> states, Stack<AlarmSyntaxNode> path, DiagnosticBag diagnostics)
        {
            if (states.TryGetValue(alarm, out var state))
            {
                if (state == 1)
                {
                    var cycle = path.Reverse()
                        .SkipWhile(item => !object.ReferenceEquals(item, alarm))
                        .ToList();
                    var trace = cycle
                        .Select(GetDisplayName)
                        .Concat(new[] { GetDisplayName(alarm) })
                        .ToList();

                    diagnostics.ReportCircularReference(trace);

                    foreach (var item in cycle)
                        AlarmSyntaxNode.ClearResolvedReference(item);
                }

                return;
            }

            states[alarm] = 1;
            path.Push(alarm);

            if (alarm.Reference != null)
                Visit(alarm.Reference, states, path, diagnostics);

            path.Pop();
            states[alarm] = 2;
        }

        private static string GetDisplayName(AlarmSyntaxNode alarm)
        {
            return string.IsNullOrWhiteSpace(alarm.FullyQualifiedName)
                ? "<unnamed>"
                : alarm.FullyQualifiedName;
        }

        public void AddSourceText(SourceText sourceText)
        {
            sourceTexts.Add(sourceText);
        }

        public void AddSourceTexts(IEnumerable<SourceText> sourceTexts)
        {
            this.sourceTexts.AddRange(sourceTexts);
        }

        public void ClearSourceTexts()
        {
            sourceTexts.Clear();
        }

        public void RemoveSourceText(SourceText sourceText)
        {
            sourceTexts.Remove(sourceText);
        }
    }
}
