using System;
using System.Collections.Generic;
using System.Linq;

namespace Alarmlist.Compiler
{
    public sealed class TestProcedure
    {
        public TestProcedure()
            : this(Array.Empty<TestProcedureItem>(), Array.Empty<TestProcedureItem>())
        {
        }

        public TestProcedure(IEnumerable<TestProcedureItem> instructions, IEnumerable<TestProcedureItem> reset)
        {
            Instructions = (instructions ?? Enumerable.Empty<TestProcedureItem>()).ToArray();
            Reset = (reset ?? Enumerable.Empty<TestProcedureItem>()).ToArray();
        }

        public IReadOnlyList<TestProcedureItem> Instructions { get; }

        public IReadOnlyList<TestProcedureItem> Reset { get; }
    }
}
