// Created/modified by Arkarin0 under one ore more license(s).

namespace Alarmlist.VisualStudio.Editing
{
    internal sealed class SourceEdit
    {
        public int Start { get; }
        public int Length { get; }
        public string Text { get; }
        public SourceEdit(int start, int length, string text) { Start = start; Length = length; Text = text; }
        public string Apply(string source) => source.Remove(Start, Length).Insert(Start, Text);
    }
}
