// Created/modified by Arkarin0 under one ore more license(s).

namespace Alarmlist.Syntax
{
    public sealed class TestProcedureStepSyntax : ITestProcedureItem
    {
        public TestProcedureStepSyntax()
        {
        }

        public TestProcedureStepSyntax(TestProcedureStepKind kind, string text)
        {
            Kind = kind;
            Text = text;
        }

        public TestProcedureStepKind Kind { get; set; }

        public string Text { get; set; }
    }
}
