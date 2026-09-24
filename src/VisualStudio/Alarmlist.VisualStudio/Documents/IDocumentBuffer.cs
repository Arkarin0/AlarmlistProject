// Created/modified by Arkarin0 under one ore more license(s).

using System;
using Alarmlist.VisualStudio.Editing;

namespace Alarmlist.VisualStudio.Documents
{
    internal sealed class DocumentSnapshot
    {
        public string Text { get; }
        public int Version { get; }
        public DocumentSnapshot(string text, int version) { Text = text; Version = version; }
    }

    internal interface IDocumentBuffer
    {
        event EventHandler Changed;
        DocumentSnapshot Current { get; }
        bool TryApply(int expectedVersion, SourceEdit edit, string description, out string error);
    }
}
