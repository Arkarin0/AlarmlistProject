using System;
using System.Collections.Generic;
using System.Linq;

namespace Alarmlist.Compiler
{
    public sealed class ProcedureGroup
    {
        public string Name { get; }
        public string Title { get; }
        public IReadOnlyList<TestProcedureItem> Entries { get; }
        public Output.ExportText ExportTitle { get; }

        public ProcedureGroup(string name, string title, IEnumerable<TestProcedureItem> entries)
            : this(name, title, entries, null) { }

        internal ProcedureGroup(string name, string title, IEnumerable<TestProcedureItem> entries, Output.ExportText exportTitle)
        {
            Name = name;
            Title = string.IsNullOrEmpty(title) ? name : title;
            Entries = Array.AsReadOnly((entries ?? Enumerable.Empty<TestProcedureItem>()).ToArray());
            ExportTitle = exportTitle;
        }
    }

    public sealed class TestProcedure
    {
        public TestProcedure() : this(Array.Empty<ProcedureGroup>()) { }

        public TestProcedure(IEnumerable<ProcedureGroup> groups)
        {
            Groups = Array.AsReadOnly((groups ?? Enumerable.Empty<ProcedureGroup>()).ToArray());
        }

        public IReadOnlyList<ProcedureGroup> Groups { get; }
    }
}
