using System;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using Alarmlist.Compiler;
using Alarmlist.Definition;
using Alarmlist.Libraries;
using Alarmlist.Syntax;
using Alarmlist.Text;

internal static class Program
{
    private static int _checks;
    private static void Check(bool condition, string message)
    { if (!condition) throw new Exception(message); _checks++; }
    private static AlmxTextSnapshot Snapshot(string alarms, string path = "collections.almx")
        => new AlmxTextSnapshot("<Alarmlist>" + alarms + "</Alarmlist>", path, 42);
    private static string Alarm(string content, string name = "A", bool abstractAlarm = false)
        => "<Alarm" + (abstractAlarm ? " abstract='true'" : "") + "><FullyQualifiedName>" + name + "</FullyQualifiedName>" + content + "</Alarm>";
    private static CompilationResult Compile(string content, CompilationContext context = null)
        => new AlarmCompiler().CompileSnapshots(new[] { Snapshot(Alarm(content)) }, context ?? CompilationContext.Empty);
    private static string Record(string fields) => "<Checks><Item>" + fields + "</Item></Checks>";
    private static void Fails(string content, string id)
    {
        var result = Compile(content);
        Check(!result.Success && result.Diagnostics.Any(diagnostic => diagnostic.Id == id), "Expected " + id + " for " + content);
    }

    private static bool RejectsSource(string content)
    {
        try { return !Compile(content).Success; }
        catch (XmlException) { return true; }
    }

    public static void Main()
    {
        if (AlarmDefinition.Collections.Single(collection => collection.Name == "Tags").MinCount == 1)
        {
            Fails("", "ALM0030");
            Fails("<Tags/>", "ALM0030");
            Fails("<Tags><Item>removed</Item><Clear/></Tags>", "ALM0030");
            Check(Compile("<Tags><Clear/><Item>one</Item></Tags>").Success, "MinCount validates final effective entries");
            var librarySources = new[] { Snapshot(Alarm("")) };
            Check(new AlarmCompiler().AnalyzeLibrarySnapshots(librarySources, CompilationContext.Empty).Success, "Library counts defer to consuming applications");
            Check(new AlarmCompiler().CompileSnapshots(new[] { Snapshot(Alarm("", "Abstract", true)) }).Success, "Abstract collections do not require consumer items");
            var options = new Alarmlist.Diagnostics.DiagnosticOptions(noWarn: new[] { "ALM0030" });
            Check(!Compile("", CompilationContext.Empty.WithDiagnosticOptions(options)).Success, "NoWarn cannot suppress cardinality failure");
            Console.WriteLine(_checks + " minimum-count checks passed");
            return;
        }
        var empty = Compile("");
        Check(empty.Success && empty.AlarmList.Single().Collections.All(collection => collection.Items.Count == 0), "Absent collections create no items");
        Check(Compile("<AlarmDefinition>ordinary</AlarmDefinition>").AlarmList.Single().AlarmDefinition == "ordinary", "Ordinary fields cannot shadow collection metadata helpers");
        if (!AlarmDefinition.HasTestProcedure)
        {
            var manual = Snapshot(Alarm("")).Parse();
            manual.Alarms.Single().TestProcedure.Groups.Add(new ProcedureGroupSyntax("Checks"));
            Check(!new AlarmCompiler().Compile(new[] { new AlmxFile(manual) }).Success, "Programmatic syntax cannot bypass disabled procedures");
            bool rejected = false;
            try { using (var text = new StringWriter()) using (var writer = XmlWriter.Create(text)) AlmxFile.WriteAlarmSyntaxNode(manual.Alarms.Single(), writer); }
            catch (InvalidOperationException) { rejected = true; }
            Check(rejected, "Source writing does not silently drop disabled procedure data");
        }
        var parsed = Snapshot(Alarm("<Checks><Item><Text></Text></Item></Checks><Tags><Item>x</Item><Clear/><Item>y</Item></Tags>")).Parse().Alarms.Single();
        var local = parsed.GetLocalCollection("Checks");
        Check(local.Location.SourceVersion == 42 && local.Entries.OfType<AlarmCollectionItemSyntax>().Single().GetFieldLocation("Text").SourceVersion == 42, "Collection and field source versions");
        string source;
        using (var text = new StringWriter())
        {
            using (var writer = XmlWriter.Create(text, new XmlWriterSettings { OmitXmlDeclaration = true })) AlmxFile.WriteAlarmSyntaxNode(parsed, writer);
            source = text.ToString();
        }
        var sourceXml = XElement.Parse(source);
        Check(sourceXml.Element("Checks").Element("Item").Elements().Count() == 1 && !source.Contains("$(Literal)"), "Source writing never materializes defaults");
        Check(sourceXml.Element("Numbers") == null && sourceXml.Element("Tags").Elements().Select(item => item.Name.LocalName).SequenceEqual(new[] { "Item", "Clear", "Item" }), "Source preserves absent containers and Clear order");
        Check(sourceXml.Element("TestProcedure") != null == AlarmDefinition.HasTestProcedure, "Source procedure capability");

        var defaults = Compile(Record(""));
        var item = defaults.AlarmList.Single().GetCollection("Checks").Items.Single();
        Check(defaults.Success && item.GetField("Text").CanonicalText == "$(Literal)" && item.GetField("Count").IsDefault && (long)item.GetField("Count").Value == 7, "Record defaults are literal and typed");
        Check(item.GetField("Enabled").State == AlarmFieldValueState.Absent, "Optional absent record field");
        var sibling = Compile("<Code>8</Code>" + Record("<Copy>$(item:Count)</Copy><Count>$(AlarmlistAlarmCode)</Count><Text>count=$(item:Copy)</Text>"));
        Check(sibling.Success && sibling.AlarmList.Single().GetCollection("Checks").Items.Single().GetField("Text").CanonicalText == "count=8", "Forward sibling and alarm field dependencies");
        Fails(Record("<Count>$(item:Copy)</Count><Copy>$(item:Count)</Copy>"), "ALM0010");
        Fails(Record("<Count>$(item:Count)</Count>"), "ALM0010");
        Fails(Record("<Text>$(item:Missing)</Text>"), "ALM0006");
        Fails("<Code>$(item:Count)</Code>", "ALM0006");
        Fails("<Tags><Item>$(item:Count)</Item></Tags>", "ALM0006");
        var escaped = Compile(Record("<Text>$$(item:Count)</Text>"));
        Check(escaped.Success && escaped.AlarmList.Single().GetCollection("Checks").Items.Single().GetField("Text").CanonicalText == "$(item:Count)", "Escaped sibling tokens remain literal");
        var parameterLiteral = Compile("<Properties><Property Name='P' Value='$(item:Count)'/></Properties>" + Record("<Text>$(P)</Text>"));
        Check(parameterLiteral.Success && parameterLiteral.AlarmList.Single().GetCollection("Checks").Items.Single().GetField("Text").CanonicalText == "$(item:Count)", "Parameter substitutions are never rescanned");

        var baseSource = Snapshot(Alarm("<Properties><Property Name='P'/></Properties><Tags><Item>base</Item><Item>same</Item></Tags>" + Record("<Count>$(P)</Count><Copy>$(item:Count)</Copy>"), "Base", true), "base.almx");
        var childSource = Snapshot(Alarm("<ReferenceName>Base</ReferenceName><Properties><Property Name='P' Value='4'/></Properties><Tags><Item>same</Item></Tags>", "Child"), "child.almx");
        var inherited = new AlarmCompiler().CompileSnapshots(new[] { baseSource, childSource });
        Check(inherited.Success && inherited.AlarmList.Single().GetCollection("Tags").Items.Select(value => value.GetField("Item").CanonicalText).SequenceEqual(new[] { "base", "same", "same" }), "Inherited-first order and duplicate values");
        Check((long)inherited.AlarmList.Single().GetCollection("Checks").Items.Single().GetField("Copy").Value == 4, "Inherited records evaluate in consuming alarm scope");
        var cleared = new AlarmCompiler().CompileSnapshots(new[] { baseSource, Snapshot(Alarm("<ReferenceName>Base</ReferenceName><Tags><Item>discard</Item><Clear/><Item>new</Item></Tags><Checks><Clear/></Checks>")) });
        Check(cleared.Success && cleared.AlarmList.Single().GetCollection("Checks").Items.Count == 0 && cleared.AlarmList.Single().GetCollection("Tags").Items.Count == 1, "Clear removes inherited and preceding local entries and defers excluded contextual evaluation");
        var emptyContainer = new AlarmCompiler().CompileSnapshots(new[] { baseSource, Snapshot(Alarm("<ReferenceName>Base</ReferenceName><Properties><Property Name='P' Value='3'/></Properties><Tags/>")) });
        Check(emptyContainer.Success && emptyContainer.AlarmList.Single().GetCollection("Tags").Items.Count == 2, "Empty container inherits");
        Check(Compile("<Tags><Item>removed</Item><Clear>\n  </Clear><Item>kept</Item></Tags>").AlarmList.Single().GetCollection("Tags").Items.Count == 1, "Clear permits formatting whitespace");
        Fails("<Tags><Item>a</Item><Item>b</Item><Item>c</Item><Item>d</Item></Tags>", "ALM0030");
        foreach (string value in new[] { "-1", "11", "", "1.5", "9223372036854775808" }) Fails("<Numbers><Item>" + value + "</Item></Numbers>", "ALM0029");
        foreach (string field in new[] { "<Count>11</Count>", "<Enabled>yes</Enabled>", "<Day>2026-02-30</Day>", "<Clock>25:00:00</Clock>", "<Instant>2026-10-23T12:00:00</Instant>", "<Ratio>NaN</Ratio>", "<Priority>Other</Priority>" }) Fails(Record(field), "ALM0029");
        var scalars = Compile(Record("<Count>10</Count><Enabled>true</Enabled><Day>2026-10-23</Day><Clock>12:34:56</Clock><Instant>2026-10-23T12:34:56+02:00</Instant><Price>1.25</Price><Ratio>2.5</Ratio><Priority>High</Priority>"));
        Check(scalars.Success && (decimal)scalars.AlarmList.Single().GetCollection("Checks").Items.Single().GetField("Price").Value == 1.25m, "All scalar shapes compile in records");

        foreach (string malformed in new[] { "<Tags><Other/></Tags>", "<Tags><Item><Nested/></Item></Tags>", "<Tags><Clear>x</Clear></Tags>", "<Tags/><Tags/>", "<Tags bogus='x'/>", "<Checks><Item><Text>a</Text><Text>b</Text></Item></Checks>", "<Checks><Item><Unknown/></Item></Checks>", "<Checks><Item>text</Item></Checks>", "<Checks><Item><Text attribute='x'/></Item></Checks>" })
            Check(RejectsSource(malformed), "Malformed collection fails: " + malformed);
        Check(Compile("<Unknown><Anything/></Unknown>").Success, "Unknown alarm elements remain forward compatible");
        Check(RejectsSource("<TestProcedure/>") != AlarmDefinition.HasTestProcedure, "Disabled procedures rejected");
        string compiled = new Alarmlist.Output.XmlAlarmlistOutputWriter().WriteToString(scalars.AlarmList);
        var output = XDocument.Parse(compiled).Root.Element("Alarm");
        Check((output.Element("TestProcedure") != null) == AlarmDefinition.HasTestProcedure && output.Element("Checks").Element("Item").Element("Count").Value == "10", "Compiled XML has typed collection values and optional procedures");

        var keyword = Compile(Record("<Count>$(loc:Number)</Count><Copy>$(item:Count)</Copy>"));
        Check(keyword.Success && keyword.AlarmList.Single().GetCollection("Checks").Items.Single().Fields.Where(value => value.Name == "Count" || value.Name == "Copy").All(value => value.State == AlarmFieldValueState.Deferred), "Keyword deferral propagates through sibling fields");
        var resources = new[] { new ResxTextSnapshot("<root><data name='Number'><value>5</value></data><data name='Record'><value>$(item:Count)</value></data><data name='Formatted'><value>{value}</value></data></root>", "values.en.resx", 1, "en") };
        var context = new CompilationContext(resources, "en");
        var localized = Compile(Record("<Count>$(loc:Number)</Count><Copy>$(loc:Record)</Copy><Text>$(loc:Formatted, value=$(item:Copy))</Text>"), context);
        Check(localized.Success && localized.AlarmList.Single().GetCollection("Checks").Items.Single().GetField("Text").CanonicalText == "5", "Concrete localization and named arguments use record context");
        Check(!Compile("<Code>$(loc:Record)</Code>", context).Success, "Localized sibling reference outside record is rejected");
        var deferred = new[] { Snapshot(Alarm(Record("<Count>$(Consumer)</Count><Copy>$(item:Count)</Copy>"))) };
        Check(new AlarmCompiler().AnalyzeLibrarySnapshots(deferred, CompilationContext.Empty).Success, "Unassigned library consumer values defer through sibling dependencies");
        Check(!new AlarmCompiler().CompileSnapshots(deferred).Success, "Applications require collection consumer values");
        var abstractBad = new AlarmCompiler().CompileSnapshots(new[] { Snapshot(Alarm(Record("<Count>bad</Count>"), "Base", true)) });
        Check(!abstractBad.Success, "Known invalid abstract literals fail");
        Check(!Alarmlist.Diagnostics.DiagnosticDescriptors.InvalidCollectionCount.IsConfigurable, "Collection count errors are mandatory");
        AnalyzeCollections(baseSource, childSource);
        Console.WriteLine(_checks + " collection checks passed; TestProcedure=" + AlarmDefinition.HasTestProcedure);
    }

    private static void AnalyzeCollections(AlmxTextSnapshot baseSource, AlmxTextSnapshot childSource)
    {
        var compiler = new AlarmCompiler();
        var analysis = compiler.AnalyzeSnapshots(new[] { baseSource, childSource }, includeCompiledXml: true);
        Check(analysis.ContractVersion == 7 && analysis.CollectionDefinitions.Count == 3 && analysis.HasTestProcedure == AlarmDefinition.HasTestProcedure, "Analysis metadata and capability");
        Check(analysis.Sources.All(source => (source.ProcedureCreation.Groups.Count != 0) == AlarmDefinition.HasTestProcedure), "Creation defaults honor optional procedures");
        var child = analysis.Alarms.Single(alarm => alarm.Origin.FullyQualifiedName == "Child");
        var checks = child.Collections.Single(collection => collection.Name == "Checks");
        var entry = checks.EffectiveEntries.Single();
        Check(!checks.IsLocallyDeclared && checks.LocalEntries.Count == 0 && entry.IsInherited && entry.Origin.Alarm.FullyQualifiedName == "Base" && entry.Origin.Location.FilePath == "base.almx", "Inherited collection provenance");
        var count = entry.Fields.Single(field => field.Name == "Count");
        Check(count.EffectiveExpression == "$(P)" && count.LocalValue == null && (long)count.TypedValue == 4 && count.ValueLocation.SourceVersion == 42, "Detached typed record field and source version");
        var clearSource = Snapshot(Alarm("<ReferenceName>Base</ReferenceName><Tags><Clear/><Item>local</Item></Tags><Checks><Clear/></Checks>"));
        var cleared = compiler.AnalyzeSnapshots(new[] { baseSource, clearSource });
        var tags = cleared.Alarms.Single(alarm => alarm.Origin.FullyQualifiedName == "A").Collections.Single(collection => collection.Name == "Tags");
        Check(tags.LocalEntries[0].IsClear && tags.ExcludedEntries.Count == 2 && tags.ExcludedEntries.All(value => value.ExcludedBy.Index == 0 && value.ExcludedBy.Alarm.FullyQualifiedName == "A"), "Clear exclusions retain directive origins");
        var invalid = compiler.AnalyzeSnapshots(new[] { Snapshot(Alarm("<ReferenceName>Missing</ReferenceName><Tags><Item>local</Item></Tags>")) });
        Check(!invalid.Alarms.Single().IsResolved && invalid.Alarms.Single().Collections.All(collection => collection.EffectiveEntries.Count == 0), "Invalid chain never advertises effective collections");
        var copied = new Alarmlist.VisualStudio.Analysis.AnalysisContractReader(typeof(AlarmCompiler).Assembly).Read(analysis);
        var copiedEntry = copied.Alarms.Single(alarm => alarm.Origin.FullyQualifiedName == "Child").Collections.Single(collection => collection.Name == "Checks").EffectiveEntries.Single();
        Check(copied.CollectionDefinitions.Count == 3 && copied.HasTestProcedure == AlarmDefinition.HasTestProcedure && (long)copiedEntry.Fields.Single(field => field.Name == "Copy").TypedValue == 4, "Bridge copies descriptors and primitive typed record values");
        var presentation = copied.CollectionDefinitions.Single(collection => collection.Name == "Tags");
        Check(presentation.DisplayType == "Concatenated" && presentation.Separator == ", " && presentation.PreviewVisible
            && copied.CollectionDefinitions.Single(collection => collection.Name == "Numbers").Symbol == "Number", "Bridge retains collection presentation metadata");
        Check(!ReferenceEquals(copiedEntry.Origin, entry.Origin) && !ReferenceEquals(copiedEntry.Fields[0], entry.Fields[0]), "Bridge owns detached objects");

        var identity = new Alarmlist.Libraries.LibraryIdentity("Records", "1.0.0");
        var built = compiler.CreateLibrary(identity, new[] { baseSource }, CompilationContext.Empty);
        Check(built.Success && built.ArtifactText.Contains("FormatVersion=\"3\""), "Collection library format");
        Check(built.ArtifactText.Contains("$(item:Count)") && !built.ArtifactText.Contains("$(Literal)"), "Libraries retain raw expressions without default materialization");
        var context = CompilationContext.Empty.WithLibraries(new Alarmlist.Libraries.LibraryReferenceSet(new[] { identity },
            new[] { new LibraryTextSnapshot(built.ArtifactText, "records.almlib", 1) }));
        var consumer = compiler.AnalyzeSnapshots(new[] { childSource }, context, includeCompiledXml: true);
        var libraryEntry = consumer.Alarms.Single(alarm => alarm.Origin.FullyQualifiedName == "Child").Collections.Single(collection => collection.Name == "Checks").EffectiveEntries.Single();
        Check(consumer.Success && libraryEntry.Origin.Alarm.Library.Id == "Records" && libraryEntry.Origin.Location.FilePath.StartsWith("library://Records/1.0.0/"), "Library record ownership restored");
        Check((long)libraryEntry.Fields.Single(field => field.Name == "Copy").TypedValue == 4 && libraryEntry.Origin.Location.LineNumber == entry.Origin.Location.LineNumber, "Library expressions and original locations round trip");
        Check(consumer.Libraries.Single().Sources.Single().Origins.Any(origin => origin.Key.Contains("/collections/Checks/entries/0/fields/Count")), "Library navigation maps record fields");
        var libraryXml = XDocument.Parse(built.ArtifactText);
        var originNode = libraryXml.Descendants().First(element => element.Name.LocalName == "Origin" && ((string)element.Attribute("Key"))?.Contains("/collections/Checks") == true);
        originNode.SetAttributeValue("Key", "alarms/0/collections/Unknown");
        var invalidContext = CompilationContext.Empty.WithLibraries(new Alarmlist.Libraries.LibraryReferenceSet(new[] { identity },
            new[] { new LibraryTextSnapshot(libraryXml.ToString(), "bad.almlib", 1) }));
        Check(!compiler.CompileSnapshots(new[] { childSource }, invalidContext).Success, "Forged collection navigation keys fail");
    }
}
