// Created/modified by Arkarin0 under one ore more license(s).

namespace Alarmlist.Syntax
{
    public sealed class TestProcedureSyntax
    {
        public ReferenceableCollection<ITestProcedureItem> Instructions { get; } = new ReferenceableCollection<ITestProcedureItem>();

        public ReferenceableCollection<ITestProcedureItem> Reset { get; } = new ReferenceableCollection<ITestProcedureItem>();

        public TestProcedureSyntax ResolveFrom(TestProcedureSyntax inheritedTestProcedure)
        {
            var result = new TestProcedureSyntax();

            foreach (var item in Instructions.ResolveFrom(inheritedTestProcedure?.Instructions))
                result.Instructions.Add(item);

            foreach (var item in Reset.ResolveFrom(inheritedTestProcedure?.Reset))
                result.Reset.Add(item);

            return result;
        }
    }
}
