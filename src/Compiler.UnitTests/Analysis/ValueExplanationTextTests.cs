// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Linq;
using System.Xml.Linq;
using Alarmlist.Analysis;
using Alarmlist.Binding;
using Alarmlist.Compiler;
using Alarmlist.Evaluation;
using Alarmlist.Text;
using Xunit;

namespace Alarmlist.Core.UnitTests.Analysis
{
    public class ValueExplanationTextTests
    {
        private static (ValueExplanationGraph Graph, CompilationResult Result) Explain(string expression,
            CompilationContext context, string code = null)
        {
            var source = new XElement("Alarmlist", new XElement("Alarm", new XElement("FullyQualifiedName", "A"),
                new XElement("Properties", new XElement("Property", new XAttribute("Name", "P"), new XAttribute("Value", "4"))),
                new XElement("Name", expression), code == null ? null : new XElement("Code", code)));
            var binder = new Binder();
            binder.AddSourceTexts(new[] { new AlmxTextSnapshot(source.ToString(), System.IO.Path.GetFullPath("input.almx"), 3).CreateSourceText() });
            var binding = binder.Bind();
            var capture = new ValueExplanationCapture(_ => true);
            var evaluation = new CompilationEvaluator(binding.SyntaxTree, context, explanations: capture);
            return (capture.GetGraph(binding.SyntaxTree.Alarms.Single()), evaluation.ToCompilationResult());
        }
        private static ResxTextSnapshot Resource(string culture, string value, string key = "A")
            => new ResxTextSnapshot(new XElement("root", new XElement("data", new XAttribute("name", key), new XElement("value", value))).ToString(), "text." + culture + ".resx", 20, culture);

        [Fact]
        public void FallbackRecordsOnlyTheSupplyingResourceAndItsChildContextDependencies()
        {
            var result = Explain("$(loc:A)", new CompilationContext(new[] { Resource("en-GB", ""), Resource("en", "Inspect $(P)") }, "en-GB"));
            Assert.True(result.Result.Success);
            var resource = Assert.Single(result.Graph.Nodes.Where(node => node.Kind == "Resource"));
            Assert.Equal("en-GB", resource.RequestedCulture);
            Assert.Equal("en", resource.SupplyingCulture);
            Assert.Equal("Inspect $(P)", resource.RawValue);
            Assert.Equal("Inspect 4", resource.Value);
            Assert.Equal("text.en.resx", resource.Location.FilePath);
            Assert.Equal(20, resource.Location.SourceVersion);
            Assert.Contains(result.Graph.Roots["Parameter:P"], resource.Inputs);
            Assert.DoesNotContain(result.Graph.Nodes, node => node.Location?.FilePath == "text.en-GB.resx");
        }

        [Fact]
        public void FormattedArgumentsKeepLiteralTextAndActualLookupOrigins()
        {
            const string lookup = "<Lookup xmlns='urn:alarmlist:lookup:1'><Path Key='Machine'><Segment LabelKey='Label'/><Property Key='Speed' LabelKey='Label'><Metadata Name='Default' Value='060'/></Property></Path><List Key='Devices'><Entry Key='E' Value='$(P)'/></List></Lookup>";
            var context = new CompilationContext(new[] { Resource("en", "Set {path} to {value}; {literal}."), Resource("en", "Machine", "Label") },
                new[] { new LookupTextSnapshot(lookup, "hmi.almlookup", 7) }, "en");
            var result = Explain("$(loc:A, path=$(path:Machine), value=$(meta:Machine#Speed#Default), literal=\"$(P)\")", context);
            Assert.True(result.Result.Success);
            Assert.Equal("Set Machine to 060; $(P).", result.Result.AlarmList.Single().Name);
            var args = result.Graph.Nodes.Where(node => node.Kind == "Argument").ToArray();
            Assert.Equal(3, args.Length);
            Assert.Equal("060", args.Single(node => node.Name == "value").Value);
            Assert.Equal("$(P)", args.Single(node => node.Name == "literal").Value);
            Assert.DoesNotContain(result.Graph.Nodes, node => node.Kind == "Parameter");
            Assert.Contains(result.Graph.Nodes, node => node.Kind == "LookupDefinition" && node.Value == "060" && node.Location.FilePath == "hmi.almlookup" && node.Location.SourceVersion == 7);
            Assert.Contains(result.Graph.Nodes, node => node.Kind == "Resource" && node.Name == "Label" && node.Value == "Machine");
            var list = Explain("$(list:Devices#E)", context);
            Assert.True(list.Result.Success);
            Assert.Equal("$(P)", list.Result.AlarmList.Single().Name);
            Assert.Contains(list.Graph.Nodes, node => node.Kind == "LookupDefinition" && node.Value == "$(P)");
            Assert.DoesNotContain(list.Graph.Nodes, node => node.Kind == "Parameter");
        }

        [Fact]
        public void WellKnownAndRepeatedFieldDependenciesUseCompilerValues()
        {
            var result = Explain("$(AlarmlistAlarmCode) $(AlarmlistAlarmCode) $(AlarmlistConfiguration) $(AlarmlistThisFileName)",
                new CompilationContext(configuration: "Debug"), "$(P)");
            Assert.True(result.Result.Success);
            Assert.Equal("4 4 Debug input", result.Result.AlarmList.Single().Name);
            var name = result.Graph.Nodes[result.Graph.Roots["Field:Name"]];
            Assert.Equal(1, name.Inputs.Count(id => id == result.Graph.Roots["Field:Code"]));
            Assert.Equal("Debug", result.Graph.Nodes[result.Graph.Roots["WellKnown:AlarmlistConfiguration"]].Value);
            Assert.Equal(System.IO.Path.GetFullPath("input.almx"), result.Graph.Nodes[result.Graph.Roots["WellKnown:AlarmlistThisFileName"]].Location.FilePath);
        }

        [Fact]
        public void KeywordLocalizationIsDeferredWithoutSelectingAnyResource()
        {
            var result = Explain("$(loc:A)", new CompilationContext(new[] { Resource("en", "Inspect $(P)") }, "keyword"));
            Assert.True(result.Result.Success);
            var localization = Assert.Single(result.Graph.Nodes.Where(node => node.Kind == "Localization"));
            Assert.Equal("Deferred", localization.State);
            Assert.Equal("$(loc:A)", localization.Value);
            Assert.Empty(localization.Inputs);
            Assert.DoesNotContain(result.Graph.Nodes, node => node.Kind == "Resource" || node.Kind == "Parameter");
        }

        [Theory]
        [InlineData("$(loc:Missing)", "Unavailable")]
        [InlineData("$(loc:A)", "Invalid")]
        public void FailedTranslationRetainsAvailableContributorsWithoutInventingAValue(string expression, string state)
        {
            var result = Explain(expression, new CompilationContext(new[] { Resource("en", "broken $(P") }, "en"));
            Assert.False(result.Result.Success);
            Assert.Equal("Invalid", result.Graph.Nodes[result.Graph.Roots["Field:Name"]].State);
            Assert.Null(result.Graph.Nodes[result.Graph.Roots["Field:Name"]].CanonicalValue);
            var contributor = result.Graph.Nodes.Last(node => node.Kind == (state == "Invalid" ? "Resource" : "Localization"));
            Assert.Equal(state, contributor.State);
            if (state == "Invalid") Assert.Equal("text.en.resx", contributor.Location.FilePath);
        }

        [Fact]
        public void FieldCycleThroughAResourceIsExplicitAndFinite()
        {
            var result = Explain("$(loc:A)", new CompilationContext(new[] { Resource("en", "$(AlarmlistAlarmName)") }, "en"));
            Assert.False(result.Result.Success);
            Assert.Equal("Invalid", result.Graph.Nodes[result.Graph.Roots["Field:Name"]].State);
            var cycle = Assert.Single(result.Graph.Nodes.Where(node => node.Kind == "Cycle"));
            Assert.Empty(cycle.Inputs);
            Assert.Contains(result.Graph.Nodes, node => node.Kind == "Resource" && node.Location.FilePath == "text.en.resx");
        }

        [Theory]
        [InlineData("$(path:Missing)")]
        [InlineData("$(list:Missing#Entry)")]
        public void MissingLookupReportsUnavailableWithoutInventingADefinition(string expression)
        {
            var result = Explain(expression, CompilationContext.Empty);
            Assert.False(result.Result.Success);
            Assert.Equal("Unavailable", Assert.Single(result.Graph.Nodes.Where(node => node.Kind == "Lookup")).State);
            Assert.DoesNotContain(result.Graph.Nodes, node => node.Kind == "LookupDefinition");
        }

        [Fact]
        public void LookupLabelTranslationsAreLiteralAndNeverRescanned()
        {
            const string lookup = "<Lookup xmlns='urn:alarmlist:lookup:1'><Path Key='M'><Segment LabelKey='A'/></Path></Lookup>";
            var result = Explain("$(path:M)", new CompilationContext(new[] { Resource("en", "$(P)") }, new[] { new LookupTextSnapshot(lookup, "hmi.almlookup", 1) }, "en"));
            Assert.True(result.Result.Success);
            Assert.Equal("$(P)", result.Result.AlarmList.Single().Name);
            Assert.Equal("$(P)", Assert.Single(result.Graph.Nodes.Where(node => node.Kind == "Resource")).Value);
            Assert.DoesNotContain(result.Graph.Nodes, node => node.Kind == "Parameter");
        }
    }
}
