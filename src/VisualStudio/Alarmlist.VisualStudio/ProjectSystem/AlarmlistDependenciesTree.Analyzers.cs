// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Linq;
using Alarmlist.VisualStudio.Analysis;
using Microsoft.VisualStudio.Imaging;
using Microsoft.VisualStudio.ProjectSystem;

namespace Alarmlist.VisualStudio.ProjectSystem
{
    internal sealed partial class AlarmlistDependenciesTree
    {
        private IProjectTree UpdateAnalyzers(IProjectTree root, ProjectAnalysisState state)
        {
            var entries = AnalyzerTreeEntry.Create(_treeProject, state);
            IProjectTree group = root.Children.FirstOrDefault(child => child.Flags.Contains("AlarmlistAnalyzers"));
            if (entries.Count == 0) return group == null ? root : root.Remove(group);
            var analyzerIcon = new ProjectImageMoniker(KnownMonikers.CodeAnalysisWindow.Guid, KnownMonikers.CodeAnalysisWindow.Id);
            group = group ?? root.Add(NewTree(DependencyTreeLabels.Analyzers, icon: analyzerIcon,
                flags: ProjectTreeFlags.VirtualFolder | ProjectTreeFlags.Create("AlarmlistAnalyzers")));
            string Identity(string path) => "AlarmlistAnalyzer=" + path.ToUpperInvariant();
            foreach (var removed in group.Children.Where(child => !entries.Any(entry => child.Flags.Contains(Identity(entry.Path)))).ToArray())
                group = group.Remove(removed);
            foreach (var entry in entries)
            {
                var icon = entry.Available ? analyzerIcon : new ProjectImageMoniker(KnownMonikers.ReferenceWarning.Guid, KnownMonikers.ReferenceWarning.Id);
                var flags = ProjectTreeFlags.VirtualFolder | ProjectTreeFlags.Reference | ProjectTreeFlags.Create(Identity(entry.Path));
                var existing = group.Children.FirstOrDefault(child => child.Flags.Contains(Identity(entry.Path)));
                var node = existing == null ? group.Add(NewTree(entry.Caption, icon: icon, flags: flags))
                    : existing.SetProperties(caption: entry.Caption, icon: icon, flags: flags);
                string RuleIdentity(string id) => "AlarmlistAnalyzerRule=" + id.ToUpperInvariant();
                foreach (var removed in node.Children.Where(child => !entry.Rules.Any(rule => child.Flags.Contains(RuleIdentity(rule.Id)))).ToArray())
                    node = node.Remove(removed);
                foreach (var rule in entry.Rules)
                {
                    var severity = rule.DefaultSeverity == "Error" ? KnownMonikers.CodeErrorRule
                        : rule.DefaultSeverity == "Warning" ? KnownMonikers.CodeWarningRule
                        : rule.DefaultSeverity == "Hidden" ? KnownMonikers.CodeHiddenRule : KnownMonikers.CodeInformationRule;
                    var ruleIcon = new ProjectImageMoniker(severity.Guid, severity.Id);
                    var ruleFlags = ProjectTreeFlags.Create(RuleIdentity(rule.Id));
                    var previous = node.Children.FirstOrDefault(child => child.Flags.Contains(RuleIdentity(rule.Id)));
                    var caption = rule.Id + ": " + rule.Title;
                    node = (previous == null ? node.Add(NewTree(caption, icon: ruleIcon, flags: ruleFlags))
                        : previous.SetProperties(caption: caption, icon: ruleIcon, flags: ruleFlags)).Parent;
                }
                group = node.Parent;
            }
            return group.Parent;
        }
    }
}
