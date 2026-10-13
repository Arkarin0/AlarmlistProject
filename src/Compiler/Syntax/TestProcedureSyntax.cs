// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.Linq;
using Alarmlist.Text;

namespace Alarmlist.Syntax
{
    public sealed class ProcedureGroupSyntax
    {
        public string Name { get; }
        public string Title { get; set; }
        public SourceLocation Location { get; set; }
        public SourceLocation TitleLocation { get; set; }
        public ReferenceableCollection<ITestProcedureItem> Entries { get; } = new ReferenceableCollection<ITestProcedureItem>();

        public ProcedureGroupSyntax(string name) { Name = name; }

        public static bool IsValidName(string name)
            => !string.IsNullOrEmpty(name) && IsInitial(name[0])
                && name.All(c => IsInitial(c) || (c >= '0' && c <= '9') || c == '.' || c == '-');

        private static bool IsInitial(char c) => (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') || c == '_';
    }

    public sealed class TestProcedureSyntax
    {
        public List<ProcedureGroupSyntax> Groups { get; } = new List<ProcedureGroupSyntax>();

        public ProcedureGroupSyntax GetGroup(string name)
            => Groups.FirstOrDefault(group => string.Equals(group.Name, name, StringComparison.OrdinalIgnoreCase));

        public ProcedureGroupSyntax GetOrAddGroup(string name)
        {
            ProcedureGroupSyntax group = GetGroup(name);
            if (group == null) { group = new ProcedureGroupSyntax(name); Groups.Add(group); }
            return group;
        }

        public TestProcedureSyntax ResolveFrom(TestProcedureSyntax inheritedTestProcedure)
        {
            var result = new TestProcedureSyntax();
            IEnumerable<string> names = (inheritedTestProcedure?.Groups.AsEnumerable() ?? Enumerable.Empty<ProcedureGroupSyntax>())
                .Concat(Groups).Select(group => group.Name).Distinct(StringComparer.OrdinalIgnoreCase);
            foreach (string name in names)
            {
                ProcedureGroupSyntax local = GetGroup(name);
                ProcedureGroupSyntax inherited = inheritedTestProcedure?.GetGroup(name);
                ProcedureGroupSyntax title = local?.Title != null ? local : inherited;
                var group = new ProcedureGroupSyntax(name)
                {
                    Title = title?.Title,
                    TitleLocation = title?.TitleLocation,
                    Location = inherited?.Location ?? local?.Location
                };
                foreach (ITestProcedureItem item in (local?.Entries ?? new ReferenceableCollection<ITestProcedureItem>()).ResolveFrom(inherited?.Entries))
                    group.Entries.Add(item);
                result.Groups.Add(group);
            }
            return result;
        }
    }
}
