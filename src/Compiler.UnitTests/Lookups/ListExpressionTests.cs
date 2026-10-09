// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using Alarmlist.Analysis;
using Alarmlist.Compiler;
using Alarmlist.Lookups;
using Alarmlist.Text;
using Xunit;

namespace Alarmlist.Core.UnitTests.Lookups
{
    public class ListExpressionTests
    {
        private const string Entries = """
            <List Key="Wires"><Entry Key="EC120W1" Value="=EC-120W1" DescriptionKey="Wire.Description">
              <Metadata Name="DrawingPage" Value="023"/>
            </Entry></List>
            <List Key="ConnectionPoints"><Entry Key="EC120K1_12" Value="=EC-120K1:12" Description="Contactor terminal"/></List>
            <Template Key="Disconnect"><Argument Name="wire" List="Wires"/><Argument Name="connection" List="ConnectionPoints"/></Template>
            """;
        private const string Call = "$(loc:Disconnect, wire=$(list:Wires#EC120W1), connection=$(list:ConnectionPoints#EC120K1_12))";

        [Fact]
        public void WireAndConnectionListsComposeOneReusableInstructionAndRetainProvenance()
        {
            AnalysisResult analysis = Analyze(Call);
            Assert.True(analysis.Success);
            Assert.Equal("Disconnect wire =EC-120W1 from =EC-120K1:12.", analysis.Alarms[0].Fields.Single(field => field.Name == "Name").EffectiveValue);
            Assert.Equal(2, analysis.ArgumentBindings.Count);
            Assert.All(analysis.ArgumentBindings, binding => Assert.True(binding.IsAvailable));
            Assert.Equal("wire", analysis.ArgumentBindings[0].ArgumentName);
            Assert.Equal("Wires", analysis.ArgumentBindings[0].ListKey);
            AnalyzedLookupListEntry entry = analysis.LookupLists[0].Entries[0];
            Assert.Equal("=EC-120W1", entry.Value);
            Assert.Equal("Outlet motor cable", entry.Description);
            Assert.Equal("en", entry.DescriptionTranslation.SupplyingCulture);
            Assert.Equal("023", Assert.Single(entry.Metadata).Value);
            Assert.Equal(7, entry.Location.SourceVersion);
            Assert.Equal("$(list:Wires#EC120W1)", entry.Expression);
            Assert.Equal(2, analysis.Alarms[0].LookupUses.Count);
            Assert.All(analysis.Alarms[0].LookupUses, use => { Assert.Equal(7, use.DefinitionLocation.SourceVersion); Assert.Equal(5, use.UsageLocation.SourceVersion); });
            Assert.Throws<NotSupportedException>(() => ((IList<AnalyzedLookupListEntry>)analysis.LookupLists[0].Entries).Clear());
            Assert.Throws<NotSupportedException>(() => ((IList<AnalyzedArgumentBinding>)analysis.ArgumentBindings).Clear());
        }

        [Theory]
        [InlineData("001")]
        [InlineData("=A1+CC1-M101")]
        [InlineData("Main.Outlets[1].VacuumReady")]
        [InlineData("$(loc:Missing) {wire} $$")]
        [InlineData("")]
        public void ValuesAreLiteralWithNoTranslationOrRecursiveExpansion(string value)
        {
            string list = new XElement(XName.Get("List", LookupReader.Namespace), new XAttribute("Key", "L"),
                new XElement(XName.Get("Entry", LookupReader.Namespace), new XAttribute("Key", "E"), new XAttribute("Value", value))).ToString();
            var context = new CompilationContext(Array.Empty<ResxTextSnapshot>(), new[] { Input(list) }, "fr");
            var result = new AlarmCompiler().CompileSnapshots(new[] { Source("$(list:L#E)") }, context);
            Assert.True(result.Success);
            Assert.Equal(value, Assert.Single(result.AlarmList).Name);
        }

        [Fact]
        public void KeywordAndEscapesRetainWholeExpressions()
        {
            Assert.Equal(Call, Analyze(Call, culture: "keyword").Alarms[0].Fields.Single(field => field.Name == "Name").EffectiveValue);
            Assert.Equal("$(list:Wires#EC120W1)", Analyze("$(list:Wires#EC120W1)", culture: "keyword").Alarms[0].Fields.Single(field => field.Name == "Name").EffectiveValue);
            Assert.Equal(Call, Analyze("$" + Call).Alarms[0].Fields.Single(field => field.Name == "Name").EffectiveValue);
        }

        [Theory]
        [InlineData("$(list:Missing#EC120W1)")]
        [InlineData("$(list:Wires#missing)")]
        [InlineData("$(list:wires#EC120W1)")]
        [InlineData("$(list:Wires#ec120W1)")]
        public void MissingExplicitReferencesFailRealAndKeywordBuilds(string expression)
        {
            foreach (string culture in new[] { "en", "keyword" })
            {
                AnalysisResult analysis = Analyze(expression, culture: culture);
                Assert.False(analysis.Success);
                Assert.Equal("ALM0021", Assert.Single(analysis.Diagnostics).Id);
                Assert.Null(analysis.CompiledXml);
            }
        }

        [Fact]
        public void ListsMergeAcrossFilesAndDuplicateKeysNeverPickAWinner()
        {
            var first = Input("<List Key='L'><Entry Key='E1' Value='same'/></List>");
            var second = Input("<List Key='L'><Entry Key='E2' Value='same'/></List>", "second.almlookup");
            var catalogue = new LookupCatalogue(new[] { first, second });
            Assert.Empty(catalogue.Diagnostics);
            Assert.Equal(new[] { "E1", "E2" }, Assert.Single(catalogue.Lists).Entries.Select(entry => entry.Key));
            Assert.True(catalogue.TryGetEntry("L", "E2", out _));
            catalogue = new LookupCatalogue(new[] { first, second, first });
            Assert.Equal("ALM0023", Assert.Single(catalogue.Diagnostics).Id);
            Assert.False(catalogue.TryGetEntry("L", "E1", out _));
            Assert.True(catalogue.TryGetEntry("L", "E2", out _));
            AnalysisResult analysis = Analyze("$(list:L#E1)", lookups: new[] { first, first });
            Assert.All(Assert.Single(analysis.LookupLists).Entries, entry => Assert.True(entry.IsAmbiguous));
            Assert.False(analysis.Success);
        }

        [Fact]
        public void BindingsAreHintsAndNeverRestrictLiteralOrParameterArguments()
        {
            var analysis = Analyze("$(loc:Disconnect, wire=\"unlisted\", connection=\"manual terminal\")");
            Assert.True(analysis.Success);
            Assert.Contains("Disconnect wire unlisted from manual terminal.", analysis.CompiledXml);
            var context = Context("en", new[] { Input(Entries) }, new[] { new PropertyDefinition("p", "unlisted") });
            var result = new AlarmCompiler().CompileSnapshots(new[] { Source("$(loc:Disconnect, wire=$(p), connection=\"other\")") }, context);
            Assert.True(result.Success);
        }

        [Fact]
        public void BadBindingsReportLocationsAndNeverAdvertiseAmbiguousSuggestions()
        {
            var first = Input("<List Key='L'/><Template Key='K'><Argument Name='value' List='L'/></Template>");
            var second = Input("<Template Key='K'><Argument Name='value' List='L'/><Argument Name='other' List='Missing'/></Template>", "second.almlookup");
            AnalysisResult analysis = Analyze("literal", lookups: new[] { first, second });
            Assert.Equal(2, analysis.Diagnostics.Count);
            Assert.All(analysis.Diagnostics, diagnostic => Assert.Equal("ALM0024", diagnostic.Id));
            Assert.All(analysis.ArgumentBindings, binding => Assert.False(binding.IsAvailable));
            Assert.Equal("second.almlookup", analysis.Diagnostics[0].Location.FilePath);
            Assert.Equal("list.almlookup", Assert.Single(analysis.Diagnostics[0].RelatedLocations).FilePath);
        }

        [Theory]
        [InlineData("<List Key='L'><Entry Key='E'/></List>")]
        [InlineData("<List Key='L'><Entry Key='E' Value='v' Description='literal' DescriptionKey='K'/></List>")]
        [InlineData("<List Key='L'><Entry Key='E' Value='v'/><Entry Key='E' Value='other'/></List>")]
        [InlineData("<List Key='L'><Entry Key='E' Value='v'><Metadata Name='M' Value='1'/><Metadata Name='M' Value='2'/></Entry></List>")]
        [InlineData("<List Key='L'><Entry Key='E' Value='v'><Relationship/></Entry></List>")]
        [InlineData("<Template Key='K'><Argument Name='bad-name' List='L'/></Template>")]
        [InlineData("<Template Key='K'><Argument Name='a' List='L'/><Argument Name='a' List='L'/></Template>")]
        public void SchemaAndDescriptionConstraintsRejectWholeMalformedDocuments(string content)
        {
            var catalogue = new LookupCatalogue(new[] { Input(content) });
            Assert.Empty(catalogue.Lists);
            Assert.Empty(catalogue.Bindings);
            Assert.NotEmpty(catalogue.Diagnostics);
            Assert.All(catalogue.Diagnostics, diagnostic => Assert.Equal("ALM0019", diagnostic.Id));
        }

        [Fact]
        public void MissingDescriptionTranslationDoesNotChangeTheIdentifierOrUseAnEnglishFallback()
        {
            AnalysisResult analysis = Analyze("$(list:Wires#EC120W1)", culture: "fr");
            Assert.True(analysis.Success);
            var entry = analysis.LookupLists[0].Entries[0];
            Assert.Null(entry.Description);
            Assert.False(entry.DescriptionTranslation.IsAvailable);
            Assert.Equal("=EC-120W1", entry.Value);
        }

        [Fact]
        public void AbstractAndClearedStepsDeferMissingListReferences()
        {
            var source = new AlmxTextSnapshot("<Alarmlist><Alarm abstract='true'><FullyQualifiedName>Base</FullyQualifiedName>"
                + "<TestProcedure><Instructions><Step>$(list:Missing#E)</Step></Instructions></TestProcedure></Alarm>"
                + "<Alarm><ReferenceName>Base</ReferenceName><TestProcedure><Instructions><Clear/><Step>done</Step></Instructions></TestProcedure></Alarm></Alarmlist>", "A.almx", 1);
            Assert.True(new AlarmCompiler().CompileSnapshots(new[] { source }, Context("en", new[] { Input(Entries) })).Success);
        }

        private static LookupTextSnapshot Input(string content, string path = "list.almlookup")
            => new LookupTextSnapshot("<Lookup xmlns='urn:alarmlist:lookup:1'>" + content + "</Lookup>", path, 7);
        private static AlmxTextSnapshot Source(string expression) => new AlmxTextSnapshot(
            new XElement("Alarmlist", new XElement("Alarm", new XElement("Name", expression))).ToString(), "A.almx", 5);
        private static CompilationContext Context(string culture, IEnumerable<LookupTextSnapshot> lookups, IEnumerable<PropertyDefinition> properties = null)
            => new CompilationContext(new[] { new ResxTextSnapshot("<root><data name='Disconnect'><value>Disconnect wire {wire} from {connection}.</value></data>"
                + "<data name='Wire.Description'><value>Outlet motor cable</value></data></root>", "labels.en.resx", 8, "en") }, lookups, culture, properties: properties);
        private static AnalysisResult Analyze(string expression, string culture = "en-GB", IEnumerable<LookupTextSnapshot> lookups = null)
            => new AlarmCompiler().AnalyzeSnapshots(new[] { Source(expression) }, Context(culture, lookups ?? new[] { Input(Entries) }), true);
    }
}
