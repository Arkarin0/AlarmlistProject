// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.Linq;
using Alarmlist.Analysis;
using Alarmlist.Compiler;
using Alarmlist.Output;
using Alarmlist.Text;
using Xunit;

namespace Alarmlist.Core.UnitTests.Analysis
{
    public sealed class LocalizationAnalysisTests
    {
        [Fact]
        public void ParentLookupKeepsRawValuesCommentsAndIndependentSnapshotVersions()
        {
            var source = Source("<Name>$(loc:Inspection)</Name>");
            var resource = Resource("Inspect $(par1)", "de", 19, "Operator hint");
            var empty = Resource("", "de-DE", 23);
            var context = new CompilationContext(new[] { resource, empty }, "DE-de", properties: new[] { new PropertyDefinition("par1", "001") });
            var compiler = new AlarmCompiler();
            AnalysisResult result = compiler.AnalyzeSnapshots(new[] { source }, context, true);
            Assert.True(result.Success);
            Assert.Equal(9, result.ContractVersion);
            Assert.Equal("de-DE", result.Culture);
            Assert.Equal(new[] { 19, 23 }, result.Resources.Select(item => item.Version));
            Assert.Equal(new[] { "de", "de-DE" }, result.Resources.Select(item => item.Culture));
            Assert.Equal(7, Assert.Single(result.Sources).Version);
            AnalyzedTranslation translation = Assert.Single(result.Translations);
            Assert.Equal("de-DE", translation.RequestedCulture);
            Assert.Equal("de", translation.SupplyingCulture);
            Assert.Equal("Inspect $(par1)", translation.RawValue);
            Assert.Equal("Operator hint", translation.Comment);
            Assert.Equal(resource.FilePath, translation.ValueLocation.FilePath);
            Assert.Equal(19, translation.ValueLocation.SourceVersion);
            AnalyzedLocalizationUse use = Assert.Single(Assert.Single(result.Alarms).LocalizationUses);
            Assert.Equal("Inspect 001", use.Value);
            Assert.True(use.IsValueAvailable);
            Assert.Equal(7, use.UsageLocation.SourceVersion);
            Assert.Equal("$(loc:Inspection)", result.Alarms[0].Fields.Single(field => field.Name == "Name").EffectiveExpression);
            Assert.Equal(new XmlAlarmlistOutputWriter().WriteToString(compiler.CompileSnapshots(new[] { source }, context).AlarmList), result.CompiledXml);
            Assert.Throws<NotSupportedException>(() => ((IList<AnalyzedResource>)result.Resources).Clear());
            Assert.Throws<NotSupportedException>(() => ((IList<AnalyzedResourceEntry>)result.ResourceEntries).Clear());
            Assert.Throws<NotSupportedException>(() => ((IList<AnalyzedTranslation>)result.Translations).Clear());
            Assert.Throws<NotSupportedException>(() => ((IList<AnalyzedLocalizationUse>)result.Alarms[0].LocalizationUses).Clear());
        }

        [Fact]
        public void InheritedUsesEvaluatePerConsumerAndClearRemovesExcludedUses()
        {
            var source = new AlmxTextSnapshot("<Alarmlist><Alarm abstract='true'><FullyQualifiedName>Base</FullyQualifiedName>"
                + "<Name>$(loc:Inspection)</Name><TestProcedure><Group Name='Instructions'><Step>$(loc:Missing)</Step></Group></TestProcedure></Alarm>"
                + "<Alarm><ReferenceName>Base</ReferenceName><Properties><Property Name='p' Value='001'/></Properties>"
                + "<TestProcedure><Group Name='Instructions'><Clear/><Step>$(loc:Inspection)</Step></Group></TestProcedure></Alarm>"
                + "<Alarm><ReferenceName>Base</ReferenceName><Properties><Property Name='p' Value='002'/></Properties>"
                + "<TestProcedure><Group Name='Instructions'><Clear/></Group></TestProcedure></Alarm></Alarmlist>", "Inherited.almx", 8);
            AnalysisResult result = new AlarmCompiler().AnalyzeSnapshots(new[] { source }, new CompilationContext(new[] { Resource("Inspect $(p)", "en") }, "en"), true);
            Assert.True(result.Success);
            Assert.Empty(result.Alarms[0].LocalizationUses);
            Assert.Equal(new[] { "Inspect 001", "Inspect 001" }, result.Alarms[1].LocalizationUses.Select(use => use.Value));
            Assert.Equal("Inspect 002", Assert.Single(result.Alarms[2].LocalizationUses).Value);
            Assert.All(result.Alarms.SelectMany(alarm => alarm.LocalizationUses), use => Assert.Equal("Inspection", use.Translation.Key));
            Assert.Single(result.Alarms[1].ProcedureSections.Single(group => group.Name == "Instructions").ExcludedEntries);
        }

        [Fact]
        public void KeywordUsesRemainLiteralWithoutInventingResourceOrigins()
        {
            AnalysisResult result = new AlarmCompiler().AnalyzeSnapshots(new[] { Source("<Name>$(loc:Undeclared)</Name>") },
                new CompilationContext(new[] { Resource("never a fallback", "") }), true);
            Assert.True(result.Success);
            Assert.Equal("keyword", result.Culture);
            AnalyzedTranslation declared = Assert.Single(result.Translations);
            Assert.Equal("$(loc:Inspection)", declared.RawValue);
            Assert.Null(declared.Location);
            AnalyzedLocalizationUse use = Assert.Single(result.Alarms[0].LocalizationUses);
            Assert.Equal("$(loc:Undeclared)", use.Value);
            Assert.True(use.Translation.IsKeyword);
            Assert.True(use.Translation.IsAvailable);
            Assert.Null(use.Translation.SupplyingCulture);
            Assert.Null(use.Translation.ValueLocation);
        }

        [Fact]
        public void DuplicateDeclarationsAreAllAmbiguousAndCannotSupplyProvenance()
        {
            var resource = Resource("First", "en");
            AnalysisResult result = new AlarmCompiler().AnalyzeSnapshots(new[] { Source("<Name>$(loc:Inspection)</Name>") },
                new CompilationContext(new[] { resource, resource }, "en"), true);
            Assert.False(result.Success);
            Assert.Null(result.CompiledXml);
            Assert.Equal(2, result.Resources.Count);
            Assert.All(result.ResourceEntries, entry => Assert.True(entry.IsAmbiguous));
            Assert.False(Assert.Single(result.Translations).IsAvailable);
            AnalyzedLocalizationUse use = Assert.Single(result.Alarms[0].LocalizationUses);
            Assert.False(use.IsValueAvailable);
            Assert.Null(use.Translation.ValueLocation);
            Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "ALM0015");
        }

        [Theory]
        [InlineData("$(missing)", "ALM0007")]
        [InlineData("$(loc:Nested)", "ALM0017")]
        [InlineData("$(", "ALM0006")]
        public void LookupAvailabilityDoesNotClaimThatTranslationExpressionsEvaluated(string value, string diagnosticId)
        {
            AnalysisResult result = new AlarmCompiler().AnalyzeSnapshots(new[] { Source("<Name>$(loc:Inspection)</Name>") },
                new CompilationContext(new[] { Resource(value, "en") }, "en"), true);
            Assert.False(result.Success);
            Assert.True(Assert.Single(result.Translations).IsAvailable);
            AnalyzedLocalizationUse use = Assert.Single(result.Alarms[0].LocalizationUses);
            Assert.False(use.IsValueAvailable);
            Assert.Null(use.Value);
            Assert.Equal(value, use.Translation.RawValue);
            Assert.Null(result.CompiledXml);
            Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == diagnosticId && diagnostic.RelatedLocations.Any(location => location.FilePath == "Labels.en.resx"));
        }

        [Fact]
        public void LaterResourcesAndCultureSelectionsCannotMutateAnEarlierDetachedResult()
        {
            var compiler = new AlarmCompiler();
            var source = Source("<Name>$(loc:Inspection)</Name>");
            AnalysisResult first = compiler.AnalyzeSnapshots(new[] { source }, new CompilationContext(new[] { Resource("First", "en", 1) }, "en"), true);
            AnalysisResult next = compiler.AnalyzeSnapshots(new[] { source }, new CompilationContext(new[] { Resource("Next", "en", 2) }, "de"), true);
            Assert.False(next.Success);
            Assert.Null(next.CompiledXml);
            Assert.False(Assert.Single(next.Translations).IsAvailable);
            Assert.Equal("First", Assert.Single(first.Translations).RawValue);
            Assert.Equal(1, Assert.Single(first.Resources).Version);
            Assert.Equal("First", Assert.Single(first.Alarms[0].LocalizationUses).Value);
            Assert.Contains("<Name>First</Name>", first.CompiledXml);
        }

        private static AlmxTextSnapshot Source(string content) => new AlmxTextSnapshot("<Alarmlist><Alarm>" + content + "</Alarm></Alarmlist>", "Alarm.almx", 7);
        private static ResxTextSnapshot Resource(string value, string culture, int version = 1, string comment = null) =>
            new ResxTextSnapshot("<root><data name='Inspection'><value>" + value + "</value><comment>" + comment + "</comment></data></root>",
                "Labels." + culture + ".resx", version, culture);
    }
}
