// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using Alarmlist.Analysis;
using Alarmlist.Compiler;
using Alarmlist.Text;
using Xunit;

namespace Alarmlist.Core.UnitTests.Lookups
{
    public class LookupExpressionTests
    {
        private const string Catalogue = """
            <Lookup xmlns="urn:alarmlist:lookup:1">
              <Path Key="Service.Auslauf.真空">
                <Segment LabelKey="Hmi.Service"/>
                <Segment LabelKey="Hmi.Vacuum"/>
                <Property Key="Ein" LabelKey="Hmi.On">
                  <Metadata Name="Minimum" Value="0"/>
                  <Metadata Name="Maximum" Value="100"/>
                  <Metadata Name="Default" Value="060"/>
                  <Metadata Name="Literal" Value="$(missing) {other} $$"/>
                  <Metadata Name="Empty" Value=""/>
                </Property>
                <Button Key="Reset" LabelKey="Hmi.Reset"/>
              </Path>
            </Lookup>
            """;
        private const string Formatted = """
            $(loc:Set,
              path=$(path:Service.Auslauf.真空),
              property=$(property:Service.Auslauf.真空#Ein),
              value=$(meta:Service.Auslauf.真空#Ein#Default))
            """;

        [Fact]
        public void OneSentenceTemplateCombinesTranslatedSegmentsMemberAndLiteralMetadata()
        {
            CompilationResult result = Compile(Formatted, Context());
            Assert.True(result.Success, string.Join("; ", result.Diagnostics.Select(item => item.Message)));
            Assert.Equal("In \"Service -> Vacuum\", set \"ON\" to \"060\".", Assert.Single(result.AlarmList).Name);
            Assert.Equal("Reset", Assert.Single(Compile("$(button:Service.Auslauf.真空#Reset)", Context()).AlarmList).Name);
        }

        [Fact]
        public void ParentCultureSuppliesSentenceAndLabelsWithNoUnrelatedLanguageFallback()
        {
            var context = Context("en-GB");
            Assert.Equal("In \"Service -> Vacuum\", set \"ON\" to \"060\".", Assert.Single(Compile(Formatted, context).AlarmList).Name);
            CompilationResult missing = Compile(Formatted, Context("fr"));
            Assert.False(missing.Success);
            Assert.Contains(missing.Diagnostics, diagnostic => diagnostic.Id == "ALM0016");
            Assert.Empty(missing.AlarmList);
        }

        [Fact]
        public void NamedArgumentsCanReorderAndRepeatWithoutReparsingValues()
        {
            const string input = """$(loc:K, second="001", first="$(missing) {second} $$")""";
            var context = Context(template: "{second}|{first}|{second}|{{braces}}");
            Assert.Equal("001|$(missing) {second} $$|001|{braces}", Assert.Single(Compile(input, context).AlarmList).Name);
            Assert.Equal("literal {first}", Assert.Single(Compile("$(loc:K)", Context(template: "literal {first}")).AlarmList).Name);
        }

        [Fact]
        public void QuotedArgumentsContainEscapesSeparatorsAndParentheses()
        {
            const string input = """$(loc:K, value="a),\"b\"\\c\nnext\tline\rend")""";
            Assert.Equal("a),\"b\"\\c\nnext\tline\rend", Assert.Single(Compile(input, Context(template: "{value}")).AlarmList).Name);
        }

        [Fact]
        public void EscapedCallsAreLiteralIncludingNestedArgumentExpressions()
        {
            Assert.Equal(Formatted, Assert.Single(Compile("$" + Formatted, Context("fr")).AlarmList).Name);
        }

        [Theory]
        [InlineData("$(path:Service.Auslauf.真空)")]
        [InlineData("$(property:Service.Auslauf.真空#Ein)")]
        [InlineData("$(button:Service.Auslauf.真空#Reset)")]
        [InlineData("$(meta:Service.Auslauf.真空#Ein#Default)")]
        public void KeywordRetainsCompleteLookupTokensWithoutTranslations(string expression)
        {
            var context = new CompilationContext(Array.Empty<ResxTextSnapshot>(), Lookups(), "keyword");
            Assert.Equal(expression, Assert.Single(Compile(expression, context).AlarmList).Name);
            Assert.Equal(Formatted, Assert.Single(Compile(Formatted, context).AlarmList).Name);
            Assert.Equal("""$(loc:Unknown, value=$(unassigned))""",
                Assert.Single(Compile("""$(loc:Unknown, value=$(unassigned))""", context).AlarmList).Name);
        }

        [Theory]
        [InlineData("$(path:Missing)")]
        [InlineData("$(property:Service.Auslauf.真空#Missing)")]
        [InlineData("$(button:Service.Auslauf.真空#Ein)")]
        [InlineData("$(meta:Service.Auslauf.真空#Ein#Missing)")]
        [InlineData("$(meta:Service.Auslauf.真空#Ein#default)")]
        public void MissingEntitiesAndWrongKindsFailCompilationInBothRealAndKeywordModes(string expression)
        {
            foreach (string culture in new[] { "en", "keyword" })
            {
                var result = Compile(expression, Context(culture));
                Assert.False(result.Success);
                Assert.Equal("ALM0021", Assert.Single(result.Diagnostics).Id);
                Assert.Empty(result.AlarmList);
                AnalysisResult analysis = new AlarmCompiler().AnalyzeSnapshots(new[] { Source(expression) }, Context(culture), true);
                Assert.Null(analysis.CompiledXml);
            }
        }

        [Theory]
        [InlineData("""$(loc:K, value=60)""")]
        [InlineData("""$(loc:K, value="1", value="2")""")]
        [InlineData("""$(loc:K, value=$(loc:Other))""")]
        [InlineData("""$(loc:K, value="bad\q")""")]
        [InlineData("""$(loc:K, value="unterminated)""")]
        [InlineData("""$(loc:K, value="1",)""")]
        [InlineData("""$(loc:K, 0="1")""")]
        [InlineData("""$(path:P, value="1")""")]
        [InlineData("$(parameter:foo)")]
        [InlineData("$(meta:P#M)")]
        [InlineData("$(path:has space)")]
        public void MalformedArgumentAndLookupSyntaxFailsEvenInKeywordMode(string expression)
        {
            var result = Compile(expression, Context("keyword"));
            Assert.False(result.Success);
            Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "ALM0006");
        }

        [Theory]
        [InlineData("{missing}", """$(loc:K, value="1")""")]
        [InlineData("{value}", """$(loc:K, Value="1")""")]
        [InlineData("{value}", """$(loc:K, value="1", extra="2")""")]
        [InlineData("{0}", """$(loc:K, value="1")""")]
        [InlineData("{value:N2}", """$(loc:K, value="1")""")]
        [InlineData("unclosed {value", """$(loc:K, value="1")""")]
        [InlineData("stray }", """$(loc:K, value="1")""")]
        public void PlaceholderErrorsIdentifyBothUsageAndResource(string template, string expression)
        {
            var result = Compile(expression, Context(template: template));
            var diagnostic = Assert.Single(result.Diagnostics);
            Assert.Equal("ALM0022", diagnostic.Id);
            Assert.Equal("alarm.almx", diagnostic.Location.FilePath);
            Assert.Equal("labels.en.resx", Assert.Single(diagnostic.RelatedLocations).FilePath);
            Assert.Empty(result.AlarmList);
        }

        [Fact]
        public void ParametersKeepScopeEmptyAssignmentsAndFieldCycleDetection()
        {
            var compiler = new AlarmCompiler();
            var sources = new[]
            {
                new AlmxTextSnapshot("""
                    <Alarmlist><Alarm abstract="true"><FullyQualifiedName>Base</FullyQualifiedName>
                    <Properties><Property Name="p" Value="base"/></Properties>
                    <Name>$(loc:K, value=$(p))</Name></Alarm></Alarmlist>
                    """, "base.almx", 1),
                new AlmxTextSnapshot("""
                    <Alarmlist><Properties><Property Name="p" Value="001"/></Properties>
                    <Alarm><ReferenceName>Base</ReferenceName></Alarm>
                    <Alarm><ReferenceName>Base</ReferenceName><Properties><Property Name="p" Value=""/></Properties></Alarm>
                    </Alarmlist>
                    """, "child.almx", 2)
            };
            var result = compiler.CompileSnapshots(sources, Context(template: "[{value}]"));
            Assert.True(result.Success);
            Assert.Equal(new[] { "[001]", "[]" }, result.AlarmList.Select(alarm => alarm.Name));
            var cycle = Compile("$(loc:K, value=$(AlarmlistAlarmName))", Context(template: "{value}"));
            Assert.Contains(cycle.Diagnostics, diagnostic => diagnostic.Id == "ALM0010"
                && diagnostic.RelatedLocations.Any(location => location.FilePath == "labels.en.resx"));
        }

        [Fact]
        public void InsertedParametersAndMetadataAreLiteralAndNoBoundsValidationOccurs()
        {
            var context = new CompilationContext(Context(template: "{value} / $(p)").Resources, Lookups(), "en",
                properties: new[] { new PropertyDefinition("p", "{value} $(unknown)") });
            Assert.Equal("$(missing) {other} $$ / {value} $(unknown)",
                Assert.Single(Compile("$(loc:K, value=$(meta:Service.Auslauf.真空#Ein#Literal))", context).AlarmList).Name);
            Assert.Equal("prefix / {value} $(unknown)", Assert.Single(Compile(
                "prefix$(loc:K, value=$(meta:Service.Auslauf.真空#Ein#Empty))", context).AlarmList).Name);
            Assert.Equal("999", Assert.Single(Compile("""$(loc:K, value="999")""", Context(template: "{value}")).AlarmList).Name);
        }

        [Fact]
        public void UnusedAbstractAndClearedInstructionsDoNotResolveMissingLookups()
        {
            var source = new AlmxTextSnapshot("""
                <Alarmlist>
                  <Alarm abstract="true"><FullyQualifiedName>Base</FullyQualifiedName>
                    <TestProcedure><Instructions><Step>$(path:Missing)</Step></Instructions></TestProcedure>
                  </Alarm>
                  <Alarm><ReferenceName>Base</ReferenceName>
                    <TestProcedure><Instructions><Clear/><Step>done</Step></Instructions></TestProcedure>
                  </Alarm>
                </Alarmlist>
                """, "abstract.almx", 1);
            var result = new AlarmCompiler().CompileSnapshots(new[] { source }, Context());
            Assert.True(result.Success);
            Assert.Equal("done", Assert.Single(Assert.Single(result.AlarmList).TestProcedure.Instructions).Text);
        }

        [Fact]
        public void AnalysisDetachesInputsAndRetainsLookupAndTranslationOrigins()
        {
            var inputs = Lookups().ToList();
            var context = new CompilationContext(Context().Resources, inputs, "en-GB");
            inputs.Clear();
            AnalysisResult analysis = new AlarmCompiler().AnalyzeSnapshots(new[] { Source(Formatted) }, context, true);
            Assert.True(analysis.Success);
            Assert.NotNull(analysis.CompiledXml);
            Assert.Equal(23, Assert.Single(analysis.LookupFiles).Version);
            AnalyzedLookupPath path = Assert.Single(analysis.LookupPaths);
            Assert.Equal("Service -> Vacuum", path.Value);
            Assert.All(path.Segments, label => Assert.Equal("en", label.SupplyingCulture));
            Assert.Equal("060", path.Members[0].Metadata.Single(value => value.Name == "Default").Value);
            Assert.Equal(3, Assert.Single(analysis.Alarms).LookupUses.Count);
            Assert.All(Assert.Single(analysis.Alarms).LookupUses, use =>
            {
                Assert.Equal("hmi.almlookup", use.DefinitionLocation.FilePath);
                Assert.Equal(23, use.DefinitionLocation.SourceVersion);
                Assert.Equal("alarm.almx", use.UsageLocation.FilePath);
                Assert.True(use.IsValueAvailable);
            });
            Assert.Equal(new[] { "path", "property", "value" }, analysis.Translations.Single(value => value.Key == "Set").PlaceholderNames);
            Assert.Throws<NotSupportedException>(() => ((IList<AnalyzedLookupPath>)analysis.LookupPaths).Clear());
        }

        private static CompilationResult Compile(string expression, CompilationContext context)
            => new AlarmCompiler().CompileSnapshots(new[] { Source(expression) }, context);

        private static AlmxTextSnapshot Source(string expression)
            => new AlmxTextSnapshot(new XElement("Alarmlist", new XElement("Alarm", new XElement("Name", expression))).ToString(), "alarm.almx", 7);

        private static LookupTextSnapshot[] Lookups() => new[] { new LookupTextSnapshot(Catalogue, "hmi.almlookup", 23) };

        private static CompilationContext Context(string culture = "en", string template = "{value}")
        {
            var values = new Dictionary<string, string>
            {
                ["Set"] = "In \"{path}\", set \"{property}\" to \"{value}\".",
                ["K"] = template, ["Hmi.Service"] = "Service", ["Hmi.Vacuum"] = "Vacuum",
                ["Hmi.On"] = "ON", ["Hmi.Reset"] = "Reset"
            };
            var resource = new XElement("root", values.Select(pair => new XElement("data", new XAttribute("name", pair.Key), new XElement("value", pair.Value))));
            return new CompilationContext(new[] { new ResxTextSnapshot(resource.ToString(), "labels.en.resx", 14, "en") }, Lookups(), culture);
        }
    }
}
