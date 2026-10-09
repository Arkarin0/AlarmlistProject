// Created/modified by Arkarin0 under one ore more license(s).

using Alarmlist.Text;

namespace Alarmlist.Evaluation
{
    internal sealed class LookupEvaluation
    {
        internal LookupEvaluation(string expression, SourceLocation definition, SourceLocation usage, string value)
        { Expression = expression; Definition = definition; Usage = usage; Value = value; }
        internal string Expression { get; }
        internal SourceLocation Definition { get; }
        internal SourceLocation Usage { get; }
        internal string Value { get; }
    }
}
