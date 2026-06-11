// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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

        public AlarmSyntaxTree Update()
        {
            syntaxTree.Alarms.Clear();

            foreach (var item in sourceTexts)
            {
                var sourceTree = item.Read();
                if (sourceTree == null)
                    continue;

                sourceTree.Alarms.ToList().ForEach(alarm => syntaxTree.Alarms.Add(alarm));
            }

            ResolveReferences(syntaxTree);

            return syntaxTree;
        }

        private static void ResolveReferences(AlarmSyntaxTree tree)
        {
            var alarmsByName = new Dictionary<string, AlarmSyntaxNode>(StringComparer.Ordinal);

            foreach (var alarm in tree.Alarms)
            {
                AlarmSyntaxNode.ClearResolvedReference(alarm);

                if (string.IsNullOrWhiteSpace(alarm.FullyQualifiedName))
                    continue;

                if (alarmsByName.ContainsKey(alarm.FullyQualifiedName))
                    throw new InvalidOperationException($"Duplicate alarm name '{alarm.FullyQualifiedName}'.");

                alarmsByName.Add(alarm.FullyQualifiedName, alarm);
            }

            foreach (var alarm in tree.Alarms)
            {
                if (string.IsNullOrWhiteSpace(alarm.ReferenceName))
                    continue;

                if (alarm.ReferenceName == alarm.FullyQualifiedName)
                    throw new InvalidOperationException($"Alarm '{alarm.FullyQualifiedName}' references itself.");

                if (!alarmsByName.TryGetValue(alarm.ReferenceName, out var reference))
                    throw new InvalidOperationException($"Alarm '{alarm.FullyQualifiedName}' references missing alarm '{alarm.ReferenceName}'.");

                AlarmSyntaxNode.SetReference(alarm, reference);
            }

            DetectReferenceCycles(tree);
        }

        private static void DetectReferenceCycles(AlarmSyntaxTree tree)
        {
            var states = new Dictionary<AlarmSyntaxNode, int>();
            var path = new Stack<AlarmSyntaxNode>();

            foreach (var alarm in tree.Alarms)
                Visit(alarm, states, path);
        }

        private static void Visit(AlarmSyntaxNode alarm, Dictionary<AlarmSyntaxNode, int> states, Stack<AlarmSyntaxNode> path)
        {
            if (states.TryGetValue(alarm, out var state))
            {
                if (state == 1)
                {
                    var trace = path.Reverse()
                        .SkipWhile(item => !object.ReferenceEquals(item, alarm))
                        .Select(GetDisplayName)
                        .Concat(new[] { GetDisplayName(alarm) });

                    throw new InvalidOperationException($"Circular alarm reference detected: {string.Join(" -> ", trace)}.");
                }

                return;
            }

            states[alarm] = 1;
            path.Push(alarm);

            if (alarm.Reference != null)
                Visit(alarm.Reference, states, path);

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
