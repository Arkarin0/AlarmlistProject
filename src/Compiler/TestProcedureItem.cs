namespace Alarmlist.Compiler
{
    public readonly struct TestProcedureItem : System.IEquatable<TestProcedureItem>
    {
        public TestProcedureItem(TestProcedureItemKind kind, string text = null)
            : this(kind, text, null) { }

        internal TestProcedureItem(TestProcedureItemKind kind, string text, Output.ExportText exportText)
        {
            Kind = kind;
            Text = text;
            ExportText = exportText;
        }

        public TestProcedureItemKind Kind { get; }

        public string Text { get; }
        public Output.ExportText ExportText { get; }

        // Export provenance does not change the established resolved item identity.
        public bool Equals(TestProcedureItem other) => Kind == other.Kind && Text == other.Text;
        public override bool Equals(object obj) => obj is TestProcedureItem other && Equals(other);
        public override int GetHashCode() => ((int)Kind * 397) ^ (Text?.GetHashCode() ?? 0);
    }
}
