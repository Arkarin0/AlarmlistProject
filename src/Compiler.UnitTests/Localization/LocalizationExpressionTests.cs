// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Serialization;
using Alarmlist.Analysis;
using Alarmlist.Compiler;
using Alarmlist.Diagnostics;
using Alarmlist.Output;
using Alarmlist.Text;
using Xunit;

namespace Alarmlist.Core.UnitTests.Localization
{
    public class LocalizationExpressionTests
    {
        [Fact]
        public void KeywordIsTheDefaultAndPreservesUnknownTokensInEveryLocalizableField()
        {
            const string expression = "$(loc:Unknown.Key-1)";
            CompilationResult result = Compile("<Alarm><Name>" + expression + "</Name><Code>" + expression
                + "</Code><Description>" + expression + "</Description><Category>" + expression + "</Category>"
                + "<TestProcedure><Instructions><Step>" + expression + "</Step><Hint>" + expression
                + "</Hint><Warning>" + expression + "</Warning><Note>" + expression + "</Note></Instructions>"
                + "<Reset><Step>" + expression + "</Step></Reset></TestProcedure></Alarm>");
            Assert.True(result.Success);
            Alarm alarm = Assert.Single(result.AlarmList);
            Assert.Equal(expression, alarm.Name);
            Assert.Equal(expression, alarm.Code);
            Assert.Equal(expression, alarm.Description);
            Assert.Equal(expression, alarm.Category);
            Assert.All(alarm.TestProcedure.Instructions.Concat(alarm.TestProcedure.Reset), step => Assert.Equal(expression, step.Text));
            Assert.Contains(expression, new XmlAlarmlistOutputWriter().WriteToString(result.AlarmList));
        }

        [Theory]
        [InlineData("keyword", "$(loc:K)")]
        [InlineData("KEYWORD", "$(loc:K)")]
        [InlineData("de-DE", "Prüfen")]
        [InlineData("de", "Prüfen")]
        [InlineData("en", "Inspect")]
        public void SelectedCultureUsesOnlyItsOwnChain(string culture, string expected)
        {
            CompilationResult result = Compile("<Alarm><Name>$(loc:K)</Name></Alarm>", Context(culture,
                Resource("de", "K", "Prüfen"), Resource("en", "K", "Inspect"), Resource("", "K", "Wrong default")));
            Assert.True(result.Success);
            Assert.Equal(expected, Assert.Single(result.AlarmList).Name);
        }

        [Theory]
        [InlineData("exact", "parent", "exact")]
        [InlineData("", "parent", "parent")]
        [InlineData(" ", "parent", " ")]
        public void ExactAndEmptyTranslationsFollowCatalogueRules(string exact, string parent, string expected)
        {
            var context = Context("de-DE", Resource("de-DE", "K", exact), Resource("de", "K", parent));
            CompilationResult result = Compile("<Alarm><Name>$(loc:K)</Name></Alarm>", context);
            Assert.True(result.Success);
            Assert.Equal(expected, Assert.Single(result.AlarmList).Name);
        }

        [Theory]
        [InlineData("<Properties><Property Name='par1' Value='001'/></Properties>", "", "project")]
        [InlineData("", "<Properties><Property Name='par1' Value='001'/></Properties>", "project")]
        [InlineData("", "", "001")]
        public void TranslationsUseConsumingAlarmFileThenProjectAssignments(string alarmProperties, string fileProperties, string projectValue)
        {
            var snapshots = new[]
            {
                Source("<Properties><Property Name='par1' Value='base file'/></Properties>"
                    + "<Alarm abstract='true'><FullyQualifiedName>Base</FullyQualifiedName>"
                    + "<Properties><Property Name='par1' Value='base alarm'/></Properties>"
                    + "<Code>$(loc:Code)</Code><Description>$(loc:Inspection)</Description></Alarm>", "Base.almx", 1),
                Source(fileProperties + "<Alarm><FullyQualifiedName>Child</FullyQualifiedName><ReferenceName>Base</ReferenceName>"
                    + alarmProperties + "</Alarm>", "Child.almx", 2)
            };
            var context = new CompilationContext(new[] { Resource("de", "Code", "$(par1)"), Resource("de", "Inspection", "Inspect $(par1)") },
                "de-DE", properties: new[] { new PropertyDefinition("par1", projectValue) });
            var compiler = new AlarmCompiler();
            CompilationResult result = compiler.CompileSnapshots(snapshots, context);
            Assert.True(result.Success);
            Alarm alarm = Assert.Single(result.AlarmList);
            Assert.Equal("001", alarm.Code);
            Assert.Equal("Inspect 001", alarm.Description);
            Assert.Equal("Child", alarm.FullyQualifiedName);
            AnalysisResult analysis = compiler.AnalyzeSnapshots(snapshots, context, true);
            Assert.True(analysis.Success);
            AnalyzedField description = analysis.Alarms[1].Fields.Single(field => field.Name == "Description");
            Assert.Null(description.LocalValue);
            Assert.Equal("$(loc:Inspection)", description.EffectiveExpression);
            Assert.Equal("Inspect 001", description.EffectiveValue);
            Assert.True(description.IsInherited);
            Assert.Equal("Base.almx", description.ValueLocation.FilePath);
            Assert.Equal(new XmlAlarmlistOutputWriter().WriteToString(result.AlarmList), analysis.CompiledXml);
        }

        [Fact]
        public void TranslatedBuiltInPropertiesUseTheConsumingFileAndAlarm()
        {
            var snapshots = new[]
            {
                Source("<Alarm abstract='true'><FullyQualifiedName>Base</FullyQualifiedName><Name>$(loc:K)</Name></Alarm>", Path.GetFullPath("Base.almx"), 1),
                Source("<Alarm><FullyQualifiedName>Child</FullyQualifiedName><ReferenceName>Base</ReferenceName></Alarm>", Path.GetFullPath("Child.almx"), 2)
            };
            var context = Context("de", Resource("de", "K", "$(AlarmlistThisFileName):$(AlarmlistAlarmFullyQualifiedName)"));
            CompilationResult result = new AlarmCompiler().CompileSnapshots(snapshots, context);
            Assert.True(result.Success);
            Assert.Equal("Child:Child", Assert.Single(result.AlarmList).Name);
        }

        [Fact]
        public void KeywordModeStillEvaluatesPropertiesAndDoesNotParseUnusedTranslationExpressions()
        {
            var context = new CompilationContext(new[] { Resource("de", "K", "$(malformed") },
                properties: new[] { new PropertyDefinition("p", "001") });
            CompilationResult result = Compile("<Alarm><Name>$(loc:K) $(p) $$(loc:Escaped)</Name></Alarm>", context);
            Assert.True(result.Success);
            Assert.Equal("$(loc:K) 001 $(loc:Escaped)", Assert.Single(result.AlarmList).Name);
        }

        [Fact]
        public void ParameterSubstitutionsRemainLiteralAndEscapesAreProcessedOnce()
        {
            var context = new CompilationContext(new[] { Resource("de", "K", "$(p) / $$(loc:NotNested) / $$(par1) / $$") },
                "de", properties: new[] { new PropertyDefinition("p", "$(loc:Literal) $(missing) $$") });
            CompilationResult result = Compile("<Alarm><Name>$(loc:K)</Name><Code>$$(loc:Unknown) / $$ / $$$$(loc:K)</Code></Alarm>", context);
            Assert.True(result.Success);
            Alarm alarm = Assert.Single(result.AlarmList);
            Assert.Equal("$(loc:Literal) $(missing) $$ / $(loc:NotNested) / $(par1) / $", alarm.Name);
            Assert.Equal("$(loc:Unknown) / $ / $$(loc:K)", alarm.Code);
        }

        [Fact]
        public void NonemptyTranslationEvaluatingToEmptyDoesNotRestartFallback()
        {
            var context = new CompilationContext(new[] { Resource("de-DE", "K", "$(p)"), Resource("de", "K", "wrong parent") },
                "de-DE", properties: new[] { new PropertyDefinition("p", "") });
            CompilationResult result = Compile("<Alarm><Name>$(loc:K)</Name></Alarm>", context);
            Assert.True(result.Success);
            Assert.Equal("", Assert.Single(result.AlarmList).Name);
        }

        [Fact]
        public void MissingTranslationReportsKeyCultureUsageAndEmptyResourceLocations()
        {
            var context = Context("de-DE", Resource("de-DE", "K", ""), Resource("de", "K", ""), Resource("", "K", "wrong default"));
            var source = Source("<Alarm><Name>$(loc:K)</Name></Alarm>");
            var compiler = new AlarmCompiler();
            CompilationResult result = compiler.CompileSnapshots(new[] { source }, context);
            Assert.False(result.Success);
            Assert.Empty(result.AlarmList);
            Diagnostic diagnostic = Assert.Single(result.Diagnostics);
            Assert.Equal("ALM0016", diagnostic.Id);
            Assert.Contains("'K'", diagnostic.Message);
            Assert.Contains("'de-DE'", diagnostic.Message);
            Assert.Equal("Input.almx", diagnostic.Location.FilePath);
            Assert.Equal(7, diagnostic.Location.SourceVersion);
            Assert.Equal(new[] { "de-DE-K.resx", "de-K.resx" }, diagnostic.RelatedLocations.Select(location => location.FilePath));
            AnalysisResult analysis = compiler.AnalyzeSnapshots(new[] { source }, context, true);
            Assert.Null(analysis.CompiledXml);
            AnalyzedField name = Assert.Single(analysis.Alarms).Fields.Single(field => field.Name == "Name");
            Assert.Equal("$(loc:K)", name.EffectiveExpression);
            Assert.False(name.IsValueAvailable);
            Assert.Null(name.EffectiveValue);
        }

        [Fact]
        public void KeysStayCaseSensitiveInExpressions()
        {
            var context = Context("de", Resource("de", "Pump", "large"), Resource("de", "pump", "small"));
            CompilationResult result = Compile("<Alarm><Name>$(loc:Pump) / $(loc:pump)</Name></Alarm>", context);
            Assert.Equal("large / small", Assert.Single(result.AlarmList).Name);
            Assert.Equal("ALM0016", Assert.Single(Compile("<Alarm><Name>$(loc:PUMP)</Name></Alarm>", context).Diagnostics).Id);
        }

        [Theory]
        [InlineData("$(loc:K)")]
        [InlineData("$(loc:Other)")]
        [InlineData("before $$(loc:Literal) $(loc:Other)")]
        public void DirectNestedLocalizationIsRejectedWithResourceAndUsageLocations(string translation)
        {
            CompilationResult result = Compile("<Alarm><Name>$(loc:K)</Name></Alarm>", Context("de-DE", Resource("de", "K", translation)));
            Diagnostic diagnostic = Assert.Single(result.Diagnostics);
            Assert.Equal("ALM0017", diagnostic.Id);
            Assert.Contains("'K'", diagnostic.Message);
            Assert.Contains("'de-DE'", diagnostic.Message);
            Assert.Equal("Input.almx", diagnostic.Location.FilePath);
            Assert.Equal("de-K.resx", Assert.Single(diagnostic.RelatedLocations).FilePath);
            Assert.Empty(result.AlarmList);
        }

        [Theory]
        [InlineData("$(par1)", "ALM0007")]
        [InlineData("$(AlarmlistProjectName)", "ALM0009")]
        [InlineData("$(bad.property)", "ALM0006")]
        public void TranslatedPropertyErrorsKeepExistingIdsAndRelatedResourceLocations(string translation, string id)
        {
            CompilationResult result = Compile("<Alarm><Name>$(loc:K)</Name></Alarm>", Context("de", Resource("de", "K", translation)));
            Diagnostic diagnostic = Assert.Single(result.Diagnostics);
            Assert.Equal(id, diagnostic.Id);
            Assert.Equal("Input.almx", diagnostic.Location.FilePath);
            SourceLocation resource = Assert.Single(diagnostic.RelatedLocations);
            Assert.Equal("de-K.resx", resource.FilePath);
            Assert.Equal(8, resource.SourceVersion);
        }

        [Fact]
        public void UnassignedInheritedParameterIsNotTakenFromTheTemplate()
        {
            CompilationResult result = Compile("<Alarm abstract='true'><FullyQualifiedName>A</FullyQualifiedName>"
                + "<Properties><Property Name='p' Value='template'/></Properties><Name>$(loc:K)</Name></Alarm>"
                + "<Alarm><ReferenceName>A</ReferenceName></Alarm>", Context("de", Resource("de", "K", "$(p)")));
            Assert.Equal("ALM0008", Assert.Single(result.Diagnostics).Id);
        }

        [Theory]
        [InlineData("$(loc:)")]
        [InlineData("$(loc:has space)")]
        [InlineData("$(LOC:Key)")]
        [InlineData("$(loc:9key)")]
        [InlineData("$(loc:key")]
        public void MalformedTokensFailEvenOnUnusedTemplates(string expression)
        {
            CompilationResult result = Compile("<Alarm abstract='true'><Name>" + expression + "</Name></Alarm>");
            Assert.Equal("ALM0006", Assert.Single(result.Diagnostics).Id);
        }

        [Fact]
        public void UnusedAndExcludedTranslationsDoNotRequireParametersOrTranslationValues()
        {
            const string template = "<Alarm abstract='true'><FullyQualifiedName>A</FullyQualifiedName><Name>$(loc:Missing)</Name>"
                + "<TestProcedure><Instructions><Step>$(loc:Nested)</Step><Hint>$(loc:Malformed)</Hint></Instructions>"
                + "<Reset><Note>$(loc:Reset)</Note></Reset></TestProcedure></Alarm>";
            var context = Context("de", Resource("de", "Nested", "$(loc:Another)"), Resource("de", "Malformed", "$(bad.name)"),
                Resource("de", "Reset", "Reset"), Resource("de", "Step", "Inspect $(AlarmlistAlarmCode)"));
            Assert.True(Compile(template, context).Success);
            CompilationResult result = Compile(template + "<Alarm><ReferenceName>A</ReferenceName><Name>Local</Name><Code>001</Code>"
                + "<TestProcedure><Instructions><Clear/><Warning>$(loc:Step)</Warning><Step>$(loc:Step)</Step></Instructions>"
                + "</TestProcedure></Alarm>", context);
            Assert.True(result.Success);
            Alarm alarm = Assert.Single(result.AlarmList);
            Assert.Equal(new[] { "Inspect 001", "Inspect 001" }, alarm.TestProcedure.Instructions.Select(step => step.Text));
            Assert.Equal(new[] { TestProcedureItemKind.Warning, TestProcedureItemKind.Instruction }, alarm.TestProcedure.Instructions.Select(step => step.Kind));
            Assert.Equal("Reset", Assert.Single(alarm.TestProcedure.Reset).Text);
        }

        [Fact]
        public void TranslationFieldDependenciesShareCycleDetectionAndCanBeOverridden()
        {
            var context = Context("de", Resource("de", "Name", "Inspect $(AlarmlistAlarmCode)"), Resource("de", "Code", "$(AlarmlistAlarmName)"));
            const string template = "<Alarm abstract='true'><FullyQualifiedName>A</FullyQualifiedName><Name>$(loc:Name)</Name><Code>$(loc:Code)</Code></Alarm>";
            CompilationResult cycle = Compile(template + "<Alarm><ReferenceName>A</ReferenceName></Alarm>", context);
            Diagnostic diagnostic = Assert.Single(cycle.Diagnostics);
            Assert.Equal("ALM0010", diagnostic.Id);
            Assert.Contains("Name -> Code -> Name", diagnostic.Message);
            Assert.Equal(new[] { "de-Name.resx", "de-Code.resx" }, diagnostic.RelatedLocations.Select(location => location.FilePath));
            Assert.Empty(cycle.AlarmList);
            CompilationResult corrected = Compile(template + "<Alarm><ReferenceName>A</ReferenceName><Code>001</Code></Alarm>", context);
            Assert.True(corrected.Success);
            Assert.Equal("Inspect 001", Assert.Single(corrected.AlarmList).Name);
        }

        [Fact]
        public void IdentityReferencesAndSourceSerializationRemainLiteral()
        {
            var source = Source("<Alarm abstract='true'><FullyQualifiedName>$(loc:Identity)</FullyQualifiedName><Name>$(loc:K)</Name></Alarm>"
                + "<Alarm><FullyQualifiedName>$(loc:Child)</FullyQualifiedName><ReferenceName>$(loc:Identity)</ReferenceName></Alarm>");
            var compiler = new AlarmCompiler();
            CompilationResult result = compiler.CompileSnapshots(new[] { source }, Context("de", Resource("de", "K", "<&> Überdruck")));
            Assert.True(result.Success);
            Assert.Equal("$(loc:Child)", Assert.Single(result.AlarmList).FullyQualifiedName);
            string xml = new XmlAlarmlistOutputWriter().WriteToString(result.AlarmList);
            Assert.Contains("&lt;&amp;&gt; Überdruck", xml);
            var serializer = new XmlSerializer(typeof(AlmxFile), new XmlRootAttribute("Alarmlist"));
            var text = new StringBuilder();
            using (var writer = new StringWriter(text)) serializer.Serialize(writer, new AlmxFile(source.Parse()));
            Assert.Contains("$(loc:Identity)", text.ToString());
            Assert.Contains("$(loc:K)", text.ToString());
            Assert.DoesNotContain("Überdruck", text.ToString());
        }

        private static CompilationResult Compile(string alarms, CompilationContext context = null)
            => new AlarmCompiler().CompileSnapshots(new[] { Source(alarms) }, context ?? CompilationContext.Empty);
        private static AlmxTextSnapshot Source(string content, string path = "Input.almx", int version = 7)
            => new AlmxTextSnapshot("<Alarmlist>" + content + "</Alarmlist>", path, version);
        private static CompilationContext Context(string culture, params ResxTextSnapshot[] resources) => new CompilationContext(resources, culture);
        private static ResxTextSnapshot Resource(string culture, string key, string value)
            => new ResxTextSnapshot(new XElement("root", new XElement("data", new XAttribute("name", key), new XElement("value", value))).ToString(),
                culture + "-" + key + ".resx", 8, culture);
    }
}
