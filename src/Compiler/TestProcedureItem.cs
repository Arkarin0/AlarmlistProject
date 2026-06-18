namespace Alarmlist.Compiler
{
    public readonly struct TestProcedureItem
    {
        public TestProcedureItem(TestProcedureItemKind kind, string text = null)
        {
            Kind = kind;
            Text = text;
        }

        public TestProcedureItemKind Kind { get; }

        public string Text { get; }
    }
}
