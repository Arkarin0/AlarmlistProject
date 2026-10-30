// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using Alarmlist.Analysis;
using Alarmlist.Compiler;
using Alarmlist.Text;
using Xunit;

namespace Alarmlist.Core.UnitTests.Analysis
{
    public class ReferenceSelectionTests
    {
        private static AlmxTextSnapshot Source(string name) => new AlmxTextSnapshot("<Alarmlist><Alarm><FullyQualifiedName>A</FullyQualifiedName><Name>" + name + "</Name></Alarm></Alarmlist>", "source.almx", 4);

        [Fact]
        public void ProjectUpdateBindingsHaveCompilerMappedSpansAndInvalidateChangedProvenance()
        {
            string text = "<Project><ItemGroup><AlarmlistProperty Include='P' Value='one'/><AlarmlistProperty Update='p' Value='two'/><AlarmlistProperty Update='P*' Value='three'/></ItemGroup></Project>";
            var doc = XDocument.Parse(text, LoadOptions.SetLineInfo);
            SourceLocation Location(XAttribute attribute)
            { var info = (IXmlLineInfo)attribute; return new SourceLocation("app.almproj", info.LineNumber, info.LinePosition, attribute.Name.LocalName.Length, 0); }
            var declaration = Location(doc.Descendants("AlarmlistProperty").First().Attribute("Include"));
            var updates = doc.Descendants("AlarmlistProperty").Skip(1).Select(item => Location(item.Attribute("Update"))).ToArray();
            var source = Source("$(P)");
            var context = new CompilationContext(projectPath: "app.almproj", properties: new[] { new PropertyDefinition("P", "three", declaration, updates, false) });
            var request = new ReferenceCompilationSnapshot(new[] { source }, context, new[] { new ReferenceTextSnapshot(text, "app.almproj", 0) });
            var target = request.TargetAtLocation("Parameter", declaration, "P");
            var found = new AlarmCompiler().FindReferencesSnapshots(request, target);
            Assert.Equal(2, found.Occurrences.Count(use => use.Role == "ProjectBinding"));
            Assert.Contains(found.Occurrences, use => use.Role == "ProjectBinding" && use.Spelling == "p" && use.Unavailable == null);
            Assert.Contains(found.Occurrences, use => use.Role == "ProjectBinding" && use.Spelling == "P*" && use.Unavailable != null && use.Start.HasValue);
            var preview = new AlarmCompiler().PreviewRenameSnapshots(request, target, "Next");
            Assert.Equal(3, preview.Edits.Count); Assert.True(preview.RequiresProjectEvaluation); Assert.True(preview.RequiresAcknowledgement);
            Assert.Contains(preview.Edits, edit => edit.Path == "app.almproj" && text.Substring(edit.Start, edit.Length) == "p");
            var changed = new ReferenceCompilationSnapshot(new[] { source }, new CompilationContext(projectPath: "app.almproj", properties: new[] { new PropertyDefinition("P", "three", declaration) }), request.ProjectDocuments);
            Assert.NotEqual(request.SnapshotId, changed.SnapshotId); Assert.False(new AlarmCompiler().FindReferencesSnapshots(changed, target).IsAvailable);
        }

        [Fact]
        public void ExcludedGroupsRevalidateTheActualSelectionAndRequireAcknowledgementOfNewDiagnostics()
        {
            var source = Source("$(loc:Greeting)");
            var resources = new[] { new ResxTextSnapshot("<root><data name='Greeting'><value>Hello</value></data></root>", "text.en.resx", 1, "en"),
                new ResxTextSnapshot("<root><data name='Greeting'><value>Hallo</value></data></root>", "text.de-DE.resx", 2, "de-DE") };
            var request = new ReferenceCompilationSnapshot(new[] { source }, new CompilationContext(resources, "en"));
            var target = request.TargetAtToken(source.FilePath, source.Version, source.Text.IndexOf("Greeting", StringComparison.Ordinal));
            var full = ReferenceCapture.Preview(request, target, "Next");
            Assert.True(full.IsApplicable); Assert.Equal(3, full.Groups.Count); Assert.Empty(full.Diagnostics);
            var included = full.Groups.Where(group => group.Path != source.FilePath).Select(group => group.Id).ToArray();
            var partial = ReferenceCapture.Preview(request, target, "Next", includedGroups: included);
            Assert.True(partial.IsApplicable, partial.Failure); Assert.Equal(2, partial.Edits.Count);
            Assert.True(partial.RequiresAcknowledgement); Assert.Contains(partial.Skipped, message => message.Contains("Excluded change group"));
            Assert.Contains(partial.Skipped, message => message.Contains("Expected diagnostic")); Assert.NotEmpty(partial.Diagnostics);
            Assert.Single(partial.Groups.Where(group => !group.IsIncluded));
            var empty = ReferenceCapture.Preview(request, target, "Next", includedGroups: Array.Empty<string>());
            Assert.False(empty.IsApplicable); Assert.Empty(empty.Edits); Assert.True(empty.RequiresAcknowledgement);
            Assert.Equal("Greeting", XDocument.Parse(resources[0].Text).Root.Element("data").Attribute("name").Value);
        }

        [Fact]
        public void GroupIdsCannotBeReusedForAnotherNameOrInventedToSilentlyChangeTheSelection()
        {
            var source = Source("literal"); var request = new ReferenceCompilationSnapshot(new[] { source }, CompilationContext.Empty);
            var target = request.TargetAtLocation("Alarm", source.Parse().Alarms.Single().Location, "A");
            var first = ReferenceCapture.Preview(request, target, "B");
            Assert.True(first.IsApplicable);
            Assert.NotNull(ReferenceCapture.Preview(request, target, "C", includedGroups: first.Groups.Select(group => group.Id).ToArray()).Failure);
            Assert.NotNull(ReferenceCapture.Preview(request, target, "B", includedGroups: new[] { "invented" }).Failure);
            Assert.NotNull(ReferenceCapture.Preview(request, target, "B", includedGroups: new[] { first.Groups[0].Id, first.Groups[0].Id }).Failure);
            Assert.False(ReferenceCapture.Preview(request, target, "A").IsApplicable);
        }

        [Theory]
        [InlineData("valid", true)]
        [InlineData("original-source", false)]
        [InlineData("extra-edit", false)]
        [InlineData("missing-project", false)]
        [InlineData("other-context", false)]
        public void ProjectEditsBecomeApplicableOnlyAfterAnExactCapturedOverlayIsValidated(string variant, bool applicable)
        {
            string projectText = "<Project><ItemGroup><AlarmlistProperty Include='P' Value='one'/></ItemGroup></Project>";
            var attribute = XDocument.Parse(projectText, LoadOptions.SetLineInfo).Descendants("AlarmlistProperty").Single().Attribute("Include");
            var info = (IXmlLineInfo)attribute;
            var origin = new SourceLocation("project.almproj", info.LineNumber, info.LinePosition, 7, 0);
            var source = Source("$(P)");
            var context = new CompilationContext(projectPath: origin.FilePath, properties: new[] { new PropertyDefinition("P", "one", origin) });
            var before = new ReferenceCompilationSnapshot(new[] { source }, context, new[] { new ReferenceTextSnapshot(projectText, origin.FilePath, 0) });
            var target = before.TargetAtLocation("Parameter", origin, "P");
            var pending = ReferenceCapture.Preview(before, target, "Next");
            Assert.True(pending.RequiresProjectEvaluation); Assert.False(pending.IsApplicable); Assert.Equal(2, pending.Edits.Count);
            string Apply(string text, string path)
            {
                foreach (var edit in pending.Edits.Where(edit => edit.Path == path).OrderByDescending(edit => edit.Start)) text = edit.Apply(text);
                return text;
            }
            var projectedSource = new AlmxTextSnapshot(variant == "original-source" ? source.Text : Apply(source.Text, source.FilePath)
                + (variant == "extra-edit" ? "<!--unapproved-->" : ""), source.FilePath, source.Version);
            var projectedContext = new CompilationContext(projectPath: variant == "other-context" ? "other.almproj" : origin.FilePath,
                properties: new[] { new PropertyDefinition("Next", "reevaluated value", origin) });
            var projected = new ReferenceCompilationSnapshot(new[] { projectedSource }, projectedContext,
                variant == "missing-project" ? Array.Empty<ReferenceTextSnapshot>() : new[] { new ReferenceTextSnapshot(Apply(projectText, origin.FilePath), origin.FilePath, 0) });
            var result = ReferenceCapture.Preview(before, target, "Next", validationInputs: projected);
            Assert.Equal(applicable, result.IsApplicable);
            if (applicable) { Assert.False(result.RequiresProjectEvaluation); Assert.Empty(result.Diagnostics); }
            else { Assert.NotNull(result.Failure); Assert.Empty(result.Edits); }
        }
    }
}
