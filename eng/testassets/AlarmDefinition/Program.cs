using System;
using System.Globalization;
using System.Linq;
using Alarmlist.Compiler;
using Alarmlist.Definition;
using Alarmlist.Output;
using Alarmlist.Text;

internal static class Program
{
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }

    private static CompilationResult Compile(string fields, CompilationContext context = null)
        => new AlarmCompiler().CompileSnapshots(new[] { new AlmxTextSnapshot("<Alarmlist><Alarm><FullyQualifiedName>A</FullyQualifiedName>" + fields + "</Alarm></Alarmlist>", "fixture.almx", 1) }, context ?? CompilationContext.Empty);

    public static void Main()
    {
        if (AlarmDefinition.Fields.Single(field => field.Name == "Code").Default == null)
        {
            Check(!Compile("").Success, "required field without a default fails in applications");
            var inputs = new[] { new AlmxTextSnapshot("<Alarmlist><Alarm><FullyQualifiedName>A</FullyQualifiedName><Ratio>$(AlarmlistAlarmCode)</Ratio></Alarm></Alarmlist>", "required.almx", 1) };
            var analysis = new AlarmCompiler().AnalyzeLibrarySnapshots(inputs, CompilationContext.Empty);
            Check(analysis.Success && !analysis.Alarms.Single().HasEvaluatedValues, "library missing required field defers");
            var explanationTarget = new Alarmlist.Analysis.ValueExplanationTarget(analysis.Alarms.Single().Origin.Location, "Field", "Code");
            var requiredExplanation = new AlarmCompiler().ExplainSnapshots(inputs, CompilationContext.Empty, explanationTarget);
            Check(requiredExplanation.IsAvailable && requiredExplanation.Diagnostics.Any(diagnostic => diagnostic.Id == "ALM0029"), "Missing required field explanation retains its diagnostic without a value location");
            var libraryExplanation = new AlarmCompiler().ExplainSnapshots(inputs, CompilationContext.Empty, explanationTarget, allowDeferredValues: true);
            Check(libraryExplanation.Nodes.Single(node => node.Id == libraryExplanation.RootId).State == "Deferred", "Public library explanation retains required-field deferral");
            foreach (string name in new[] { "Code", "Ratio" })
            {
                var field = analysis.Alarms.Single().Fields.Single(item => item.Name == name);
                Check(field.State == AlarmFieldValueState.Deferred && !field.IsValueAvailable && field.TypedValue == null, "required deferral propagates: " + name);
            }
            Console.WriteLine("Required-field runtime checks passed.");
            return;
        }
        foreach (string culture in new[] { "en-US", "de-DE", "tr-TR" })
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
            var result = Compile("<Message>Exact text </Message><Count>+0042</Count><Price>12.500</Price><Ratio>1e+3</Ratio><Enabled>1</Enabled><Priority>High</Priority><Day>2024-02-29</Day><Clock>12:34:56.1200</Clock><Instant>2024-02-29T12:34:56.1200+02:00</Instant>");
            Check(result.Success, "all types compile");
            var alarm = result.AlarmList.Single();
            Check(alarm.Count == 42 && alarm.Price == 12.5m && alarm.Ratio == 1000 && alarm.Enabled == true && alarm.Priority == PriorityValue.High, "typed scalar accessors");
            Check(alarm.Day == new DateTime(2024, 2, 29) && alarm.Clock == new TimeSpan(0, 12, 34, 56, 120) && alarm.Instant.Value.Offset == TimeSpan.FromHours(2), "typed temporal accessors");
            Check(alarm.GetField("Count").RawText == "+0042" && alarm.GetField("Count").CanonicalText == "42", "raw/canonical separation");
            var document = ExportDocument.Create(result, "keyword");
            var exported = document.Alarms.Single();
            Check((long)exported.Fields.Single(field => field.Name == "Count").Value == 42L, "export numeric primitive");
            Check((string)exported.Fields.Single(field => field.Name == "Priority").Value == "High", "export enum is a detached token");
            Check(document.Fields.Single(field => field.Name == "Price").Type == "Decimal", "export generated metadata");
            var artifact = new XmlAlarmlistExportProvider().Export(new ExportRequest(document, "alarms.xml"), System.Threading.CancellationToken.None).Single();
            string providerXml = System.Text.Encoding.UTF8.GetString(artifact.GetContent()).TrimStart('\ufeff');
            Check(providerXml == new XmlAlarmlistOutputWriter().WriteToString(result.AlarmList), "alternate scalar XML provider parity");
            var json = ExportJsonChecks.Alarm(ExportJsonChecks.Read(result));
            var countJson = ExportJsonChecks.Field(json, "Count");
            Check((string)countJson.Element("value").Attribute("type") == "number" && countJson.Element("value").Value == "42", "JSON integer is a number");
            Check(ExportJsonChecks.Field(json, "Price").Element("value").Value == "12.5" && (string)ExportJsonChecks.Field(json, "Enabled").Element("value").Attribute("type") == "boolean", "JSON decimal and boolean types");
            Check(ExportJsonChecks.Field(json, "Priority").Element("value").Value == "High" && (string)ExportJsonChecks.Field(json, "Priority").Element("value").Attribute("type") == "string", "JSON enum token");
            Check(ExportJsonChecks.Field(json, "Day").Element("value").Value == "2024-02-29" && ExportJsonChecks.Field(json, "Clock").Element("value").Value == "12:34:56.12" && ExportJsonChecks.Field(json, "Instant").Element("value").Value == "2024-02-29T12:34:56.12+02:00", "JSON temporal strings keep compiler canonical values");
            Check(ExportJsonChecks.Field(json, "__type").Element("value").Value == "literal-name", "JSON field names never become reserved serializer members");
            var zenon = new Alarmlist.Exporters.Zenon.ZenonExportProvider().Export(new ExportRequest(document, "zenon.xml"), System.Threading.CancellationToken.None).Single();
            var zenonAlarm = System.Xml.Linq.XDocument.Parse(System.Text.Encoding.UTF8.GetString(zenon.GetContent()).TrimStart('\ufeff')).Root.Element("Alarm");
            Check(zenonAlarm.Element("Count").Value == "42" && zenonAlarm.Element("Price").Value == "12.5" && zenonAlarm.Element("Literal").Value == "$(NotAnExpression)", "Zenon generated scalar canonical values and literal defaults");
            string xml = new XmlAlarmlistOutputWriter().WriteToString(result.AlarmList);
            Check(xml.Contains("<Price>12.5</Price>") && xml.Contains("<Enabled>true</Enabled>") && xml.Contains("12:34:56.12+02:00"), "canonical output");
        }
        foreach (string fields in new[] { "<Count>9223372036854775808</Count>", "<Price>1,2</Price>", "<Ratio>NaN</Ratio>", "<Enabled>TRUE</Enabled>", "<Priority>high</Priority>", "<Day>2023-02-29</Day>", "<Clock>24:00:00</Clock>", "<Instant>2024-01-01T00:00:00</Instant>", "<Count> </Count>" })
            Check(!Compile(fields).Success, "invalid scalar rejected: " + fields);
        var absent = Compile("").AlarmList.Single();
        Check(Compile("", CompilationContext.Empty.WithAnalyzers(new[] { new Alarmlist.SampleAnalyzer.CodePrefixAnalyzer() })).Success,
            "sample analyzer tolerates a retyped Code field");
        Check(absent.Count == null && absent.GetField("Count").State == AlarmFieldValueState.Absent, "optional absence");
        Check(absent.Code == 7 && absent.GetField("Code").IsDefault && absent.GetField("Code").RawText == null, "typed literal default");
        Check(absent.Literal == "$(NotAnExpression)", "defaults are literal");
        var explanationBinder = new Alarmlist.Binding.Binder();
        explanationBinder.AddSourceTexts(new[] { new AlmxTextSnapshot("<Alarmlist><Alarm><FullyQualifiedName>A</FullyQualifiedName></Alarm></Alarmlist>", "fixture.almx", 1).CreateSourceText() });
        var explanationBinding = explanationBinder.Bind();
        var capture = new Alarmlist.Evaluation.ValueExplanationCapture(_ => true);
        new Alarmlist.Evaluation.CompilationEvaluator(explanationBinding.SyntaxTree, CompilationContext.Empty, explanations: capture);
        var graph = capture.GetGraph(explanationBinding.SyntaxTree.Alarms.Single());
        var explanationDefaultCode = graph.Nodes[graph.Roots["Field:Code"]];
        Check(explanationDefaultCode.RawValue == null && explanationDefaultCode.Scope == "Definition" && explanationDefaultCode.State == "Valid" && explanationDefaultCode.CanonicalValue == "7" && explanationDefaultCode.Type == "Int64", "Explanation typed definition default");
        Check(graph.Nodes[explanationDefaultCode.Inputs.Single(id => graph.Nodes[id].Kind == "Default")].Location == null, "Definition default has no forged source location");
        Check(graph.Nodes[explanationDefaultCode.Inputs.Single(id => graph.Nodes[id].Kind == "Conversion")].CanonicalValue == "7", "Explanation typed conversion uses compiler canonical value");
        Check(graph.Nodes[graph.Roots["Field:Literal"]].Value == "$(NotAnExpression)", "Explanation defaults stay literal");
        foreach (string countText in new[] { "+00042", "invalid" })
        {
            var conversionBinder = new Alarmlist.Binding.Binder();
            conversionBinder.AddSourceTexts(new[] { new AlmxTextSnapshot("<Alarmlist><Alarm><Count>" + countText + "</Count></Alarm></Alarmlist>", "fixture.almx", 1).CreateSourceText() });
            var conversionBinding = conversionBinder.Bind();
            var conversionCapture = new Alarmlist.Evaluation.ValueExplanationCapture(_ => true);
            new Alarmlist.Evaluation.CompilationEvaluator(conversionBinding.SyntaxTree, CompilationContext.Empty, explanations: conversionCapture);
            var conversionGraph = conversionCapture.GetGraph(conversionBinding.SyntaxTree.Alarms.Single());
            var countNode = conversionGraph.Nodes[conversionGraph.Roots["Field:Count"]];
            var conversionNode = conversionGraph.Nodes[countNode.Inputs.Single(id => conversionGraph.Nodes[id].Kind == "Conversion")];
            Check(conversionNode.RawValue == countText && conversionNode.CanonicalValue == (countText == "invalid" ? null : "42") && conversionNode.State == (countText == "invalid" ? "Invalid" : "Valid"), "Explanation conversion preserves compiler state and canonical value");
        }
        var absentJson = ExportJsonChecks.Alarm(ExportJsonChecks.Read(Compile("")));
        Check(ExportJsonChecks.Field(absentJson, "Count").Element("state").Value == "absent" && (string)ExportJsonChecks.Field(absentJson, "Count").Element("value").Attribute("type") == "null", "JSON optional absence");
        Check(ExportJsonChecks.Field(absentJson, "Code").Element("isDefault").Value == "true", "JSON default marker");
        var deferredJson = ExportJsonChecks.Alarm(ExportJsonChecks.Read(Compile("<Count>$(loc:Number)</Count>")));
        Check(ExportJsonChecks.Field(deferredJson, "Count").Element("state").Value == "deferred" && (string)ExportJsonChecks.Field(deferredJson, "Count").Element("value").Attribute("type") == "null" && ExportJsonChecks.Field(deferredJson, "Count").Element("canonicalText").Value == "$(loc:Number)", "JSON typed keyword deferral");
        var exactJson = ExportJsonChecks.Alarm(ExportJsonChecks.Read(Compile("<Count>9223372036854775807</Count><Price>79228162514264337593543950335</Price><Ratio>1e308</Ratio>")));
        Check(ExportJsonChecks.Field(exactJson, "Count").Element("value").Value == "9223372036854775807" && ExportJsonChecks.Field(exactJson, "Price").Element("value").Value == "79228162514264337593543950335" && ExportJsonChecks.Field(exactJson, "Ratio").Element("value").Value == "1E308", "JSON numbers retain full compiler precision and exponent");
        var defaultText = ExportDocument.Create(Compile(""), "keyword").Alarms.Single().Fields.Single(field => field.Name == "Literal").Text;
        Check(defaultText.Segments.Single().Kind == ExportTextSegmentKind.Literal && defaultText.ResolvedText == "$(NotAnExpression)", "Export generated scalar defaults stay literal");
        Check(absent.identity == "identifier", "lowercase field cannot shadow generated constructor parameters");
        Check(absent.Enum == "enum-name" && absent.DateTime == "date-name", "field names cannot shadow scalar helper types");
        Check(!Compile("<Code>11</Code>").Success && !Compile("<Code>-1</Code>").Success, "inclusive bounds enforced");
        Check(Compile("<Code>0</Code>").Success && Compile("<Code>10</Code>").Success, "inclusive endpoints accepted");
        Check(!Compile("<Properties><Property Name='Empty' Value=''/></Properties><Code>$(Empty)</Code>").Success, "empty expression does not use default");
        Check(!new XmlAlarmlistOutputWriter().WriteToString(Compile("").AlarmList).Contains("<Count"), "absent scalar omitted");
        var keyword = Compile("<Count>$(loc:Number)</Count><Ratio>$(AlarmlistAlarmCount)</Ratio>");
        Check(keyword.Success && keyword.AlarmList.Single().GetField("Count").State == AlarmFieldValueState.Deferred
            && keyword.AlarmList.Single().GetField("Ratio").State == AlarmFieldValueState.Deferred, "keyword deferral propagates");
        Check(!Compile("<Count>$(AlarmlistAlarmRatio)</Count><Ratio>$(AlarmlistAlarmCount)</Ratio>").Success, "cycles rejected");
        Check(!Compile("<Properties><Property Name='Literal' Value='$(loc:Number)'/></Properties><Count>$(Literal)</Count>").Success, "replacement tokens are not rescanned");
        var resources = new[] { new ResxTextSnapshot("<root><data name='Number'><value>42</value></data></root>", "values.en.resx", 1, "en") };
        var concrete = Compile("<Count>$(loc:Number)</Count>", new CompilationContext(resources, "en"));
        Check(concrete.Success && concrete.AlarmList.Single().Count == 42, "concrete localization is validated");
        Check(!Compile("<Code>$(loc:Number)</Code>", new CompilationContext(resources, "en")).Success, "concrete localization bounds fail");
        var lookups = new[] { new LookupTextSnapshot("<Lookup xmlns='urn:alarmlist:lookup:1'><List Key='Numbers'><Entry Key='Answer' Value='42'/></List></Lookup>", "numbers.almlookup", 1) };
        var lookupKeyword = Compile("<Count>$(list:Numbers#Answer)</Count>", new CompilationContext(resources, lookups, "keyword"));
        Check(lookupKeyword.Success && lookupKeyword.AlarmList.Single().GetField("Count").State == AlarmFieldValueState.Deferred, "keyword lookup deferral");
        Check(Compile("<Count>$(list:Numbers#Answer)</Count>", new CompilationContext(resources, lookups, "en")).AlarmList.Single().Count == 42, "concrete lookup validation");
        var snapshots = new[] { new AlmxTextSnapshot("<Alarmlist><Alarm abstract='true'><FullyQualifiedName>Base</FullyQualifiedName><Count>$(Value)</Count><Code>9</Code><Properties><Property Name='Value'/></Properties></Alarm><Alarm><FullyQualifiedName>Child</FullyQualifiedName><ReferenceName>Base</ReferenceName><Code/><Properties><Property Name='Value' Value='12'/></Properties></Alarm></Alarmlist>", "inheritance.almx", 1) };
        var inherited = new AlarmCompiler().CompileSnapshots(snapshots);
        Check(inherited.Success && inherited.AlarmList.Single().Count == 12 && inherited.AlarmList.Single().Code == 9, "abstract inheritance resolves in consumer context");
        var librarySources = new[] { new AlmxTextSnapshot("<Alarmlist><Alarm><FullyQualifiedName>Library</FullyQualifiedName><Count>$(Consumer)</Count></Alarm></Alarmlist>", "library.almx", 1) };
        Check(new AlarmCompiler().AnalyzeLibrarySnapshots(librarySources, CompilationContext.Empty).Success, "library consumer values defer");
        var libraryAnalysis = new AlarmCompiler().AnalyzeLibrarySnapshots(librarySources, CompilationContext.Empty);
        Check(libraryAnalysis.ContractVersion == 9 && libraryAnalysis.SchemaFingerprint == AlarmDefinition.SchemaFingerprint
            && libraryAnalysis.FieldDefinitions.Any(field => field.Name == "Code" && field.Type == "Int64"), "analysis schema metadata");
        Check(libraryAnalysis.Alarms.Single().Fields.Single(field => field.Name == "Count").State == AlarmFieldValueState.Deferred, "analysis consumer field defers");
        var defaultCode = libraryAnalysis.Alarms.Single().Fields.Single(field => field.Name == "Code");
        Check(defaultCode.IsDefault && defaultCode.CanonicalValue == "7" && (long)defaultCode.TypedValue == 7, "analysis typed default provenance");
        Check(!new AlarmCompiler().CompileSnapshots(librarySources).Success, "applications require concrete consumer values");
        var knownExpression = new[] { new AlmxTextSnapshot("<Alarmlist><Alarm><FullyQualifiedName>Invalid</FullyQualifiedName><Properties><Property Name='p' Value='not-a-number'/></Properties><Count>$(p)</Count></Alarm></Alarmlist>", "known.almx", 1) };
        Check(!new AlarmCompiler().AnalyzeLibrarySnapshots(knownExpression, CompilationContext.Empty).Success, "known-invalid evaluated library values do not defer");
        foreach (string abstractAttribute in new[] { "", " abstract='true'" })
        {
            var invalidLiteral = new[] { new AlmxTextSnapshot("<Alarmlist><Alarm" + abstractAttribute + "><FullyQualifiedName>Invalid</FullyQualifiedName><Count>not-a-number</Count></Alarm></Alarmlist>", "invalid.almx", 1) };
            Check(!new AlarmCompiler().AnalyzeLibrarySnapshots(invalidLiteral, CompilationContext.Empty).Success, "library known-invalid literals fail");
            Check(!new AlarmCompiler().CompileSnapshots(invalidLiteral).Success, "abstract known-invalid literals fail");
        }
        var node = new Alarmlist.Syntax.AlarmSyntaxNode { FullyQualifiedName = "Raw" };
        node.SetLocalFieldValue("Count", "+0042");
        node.SetLocalFieldValue("Message", "$(Unresolved)");
        using (var source = new System.IO.StringWriter())
        {
            using (var writer = System.Xml.XmlWriter.Create(source)) AlmxFile.WriteAlarmSyntaxNode(node, writer);
            string xml = source.ToString();
            Check(xml.Contains("<Count>+0042</Count>") && xml.Contains("$(Unresolved)") && !xml.Contains("<Code>7</Code>"), "source retains raw values and omits defaults");
        }
        var diagnostic = Compile("<Code>11</Code>").Diagnostics.Single(item => item.Id == "ALM0029");
        Check(Alarmlist.Diagnostics.DiagnosticPolicy.Apply(diagnostic, new Alarmlist.Diagnostics.DiagnosticOptions(noWarn: new[] { "ALM0029" })) != null, "typed validation is mandatory");
        Console.WriteLine("Alternate-definition runtime checks passed.");
    }
}
