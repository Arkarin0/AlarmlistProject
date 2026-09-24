// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace Alarmlist.VisualStudio.Editing
{
    internal sealed class AlmxProjection
    {
        public string Text { get; }
        public string Error { get; }
        public IReadOnlyList<AlarmSource> Alarms { get; }
        internal XmlSourceElement Root { get; }
        public bool IsValid => Error == null;

        internal AlmxProjection(string text, XmlSourceElement root, string error)
        {
            Text = text;
            Root = root;
            Error = error;
            Alarms = root == null ? Array.Empty<AlarmSource>() : root.Children
                .Where(element => element.Element.Name.LocalName == "Alarm")
                .Select(element => new AlarmSource(element)).ToArray();
        }
    }

    internal sealed class AlarmSource
    {
        public static readonly string[] FieldNames = { "FullyQualifiedName", "Name", "Code", "Category", "Description", "ReferenceName" };
        internal XmlSourceElement Source { get; }
        public int Start => Source.Start;
        public string Identity => GetValue("FullyQualifiedName");
        internal AlarmSource(XmlSourceElement source) { Source = source; }
        public string GetValue(string field) => Source.Element.Elements().LastOrDefault(element => element.Name.LocalName == field)?.Value;
        public bool HasField(string field) => Source.Element.Elements().Any(element => element.Name.LocalName == field);

        public ProcedureSectionSource GetProcedureSection(string name)
        {
            if (name != "Instructions" && name != "Reset") throw new ArgumentException("Unknown procedure section.", nameof(name));
            XmlSourceElement[] procedures = Source.Children.Where(element => element.Element.Name.LocalName == "TestProcedure").ToArray();
            XmlSourceElement[] sections = procedures.SelectMany(element => element.Children).Where(element => element.Element.Name.LocalName == name).ToArray();
            string error = procedures.Length > 1 || sections.Length > 1
                ? "Repeated TestProcedure or section elements must be edited in XML." : null;
            return new ProcedureSectionSource(name, procedures.FirstOrDefault(), sections.FirstOrDefault(), error);
        }
    }

    internal sealed class ProcedureSectionSource
    {
        public string Name { get; }
        public string Error { get; }
        public IReadOnlyList<ProcedureItemSource> Items { get; }
        internal XmlSourceElement Procedure { get; }
        internal XmlSourceElement Source { get; }

        internal ProcedureSectionSource(string name, XmlSourceElement procedure, XmlSourceElement source, string error)
        {
            Name = name;
            Procedure = procedure;
            Source = source;
            Error = error;
            Items = source == null ? Array.Empty<ProcedureItemSource>() : source.Children
                .Where(element => ProcedureItemSource.Kinds.Contains(element.Element.Name.LocalName))
                .Select(element => new ProcedureItemSource(element)).ToArray();
        }

        public string GetText(AlmxProjection projection) => Source == null ? null
            : projection.Text.Substring(Source.Start, Source.End - Source.Start);
    }

    internal sealed class ProcedureItemSource
    {
        public static readonly string[] Kinds = { "Step", "Hint", "Warning", "Note", "Clear" };
        internal XmlSourceElement Source { get; }
        public string Kind => Source.Element.Name.LocalName;
        public string Text => Kind == "Clear" ? string.Empty : Source.Element.Value;
        public bool CanEdit => !Source.Element.Nodes().Any(node => !(node is XText))
            && (Kind != "Clear" || string.IsNullOrWhiteSpace(Source.Element.Value));

        internal ProcedureItemSource(XmlSourceElement source) { Source = source; }
    }

    internal sealed class ProcedureValueChange
    {
        public string Section { get; }
        public int Index { get; }
        public string Kind { get; }
        public string Text { get; }

        public ProcedureValueChange(string section, int index, string kind, string text)
        {
            Section = section;
            Index = index;
            Kind = kind;
            Text = text;
        }
    }

    internal sealed class XmlSourceElement
    {
        public XElement Element { get; set; }
        public int Start { get; set; }
        public int StartTagEnd { get; set; }
        public int CloseStart { get; set; }
        public int End { get; set; }
        public bool IsEmpty { get; set; }
        public List<XmlSourceElement> Children { get; } = new List<XmlSourceElement>();
    }

    internal sealed class SourceEdit
    {
        public int Start { get; }
        public int Length { get; }
        public string Text { get; }
        public SourceEdit(int start, int length, string text) { Start = start; Length = length; Text = text; }
        public string Apply(string source) => source.Remove(Start, Length).Insert(Start, Text);
    }
}
