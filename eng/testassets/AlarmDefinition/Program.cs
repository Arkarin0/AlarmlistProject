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
        foreach (string culture in new[] { "en-US", "de-DE", "tr-TR" })
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
            var result = Compile("<Message>Exact text </Message><Count>+0042</Count><Price>12.500</Price><Ratio>1e+3</Ratio><Enabled>1</Enabled><Priority>High</Priority><Day>2024-02-29</Day><Clock>12:34:56.1200</Clock><Instant>2024-02-29T12:34:56.1200+02:00</Instant>");
            Check(result.Success, "all types compile");
            var alarm = result.AlarmList.Single();
            Check(alarm.Count == 42 && alarm.Price == 12.5m && alarm.Ratio == 1000 && alarm.Enabled == true && alarm.Priority == PriorityValue.High, "typed scalar accessors");
            Check(alarm.Day == new DateTime(2024, 2, 29) && alarm.Clock == new TimeSpan(0, 12, 34, 56, 120) && alarm.Instant.Value.Offset == TimeSpan.FromHours(2), "typed temporal accessors");
            Check(alarm.GetField("Count").RawText == "+0042" && alarm.GetField("Count").CanonicalText == "42", "raw/canonical separation");
            string xml = new XmlAlarmlistOutputWriter().WriteToString(result.AlarmList);
            Check(xml.Contains("<Price>12.5</Price>") && xml.Contains("<Enabled>true</Enabled>") && xml.Contains("12:34:56.12+02:00"), "canonical output");
        }
        foreach (string fields in new[] { "<Count>9223372036854775808</Count>", "<Price>1,2</Price>", "<Ratio>NaN</Ratio>", "<Enabled>TRUE</Enabled>", "<Priority>high</Priority>", "<Day>2023-02-29</Day>", "<Clock>24:00:00</Clock>", "<Instant>2024-01-01T00:00:00</Instant>", "<Count> </Count>" })
            Check(!Compile(fields).Success, "invalid scalar rejected: " + fields);
        var absent = Compile("").AlarmList.Single();
        Check(absent.Count == null && absent.GetField("Count").State == AlarmFieldValueState.Absent, "optional absence");
        Check(absent.Code == 7 && absent.GetField("Code").IsDefault && absent.GetField("Code").RawText == null, "typed literal default");
        Check(absent.Literal == "$(NotAnExpression)", "defaults are literal");
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
        Check(!new AlarmCompiler().CompileSnapshots(librarySources).Success, "applications require concrete consumer values");
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
