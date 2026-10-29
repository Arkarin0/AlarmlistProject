// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Linq;
using Alarmlist.Analysis;
using Alarmlist.Compiler;
using Alarmlist.Libraries;
using Alarmlist.Text;
using Xunit;

namespace Alarmlist.Core.UnitTests.Analysis
{
    public class ReferenceLookupTests
    {
        private const string LookupBody = "<Path Key='Panel'><Segment LabelKey='PanelLabel'/><Property Key='Value' LabelKey='FieldLabel'>"
            + "<Metadata Name='Unit' Value='literal $(P)'/></Property><Button Key='Apply' LabelKey='ButtonLabel'/></Path>"
            + "<List Key='Values'><Entry Key='One' Value='literal $(P)' DescriptionKey='EntryLabel'><Metadata Name='Unit' Value='entry literal $(P)'/></Entry></List>"
            + "<Template Key='Greeting'><Argument Name='Pos' List='Values'/></Template>";
        private static LookupTextSnapshot Lookup(string body = LookupBody) => new LookupTextSnapshot("<Lookup xmlns='urn:alarmlist:lookup:1'>" + body + "</Lookup>", "lookup.almlookup", 4);
        private static AlmxTextSnapshot Input(string text = "$(path:Panel) $(property:Panel#Value) $(button:Panel#Apply) $(meta:Panel#Value#Unit) $(list:Values#One) $(loc:Greeting, Pos=$(P))")
            => new AlmxTextSnapshot("<Alarmlist><Alarm><FullyQualifiedName>A</FullyQualifiedName><Properties><Property Name='P' Value='7'/></Properties><Name>" + text + "</Name></Alarm></Alarmlist>", "alarms.almx", 5);
        private static CompilationContext Context(string body = LookupBody) => new CompilationContext(new[]
        {
            new ResxTextSnapshot("<root><data name='Greeting'><value>Position {Pos} $(P)</value></data>"
                + "<data name='PanelLabel'><value>$(P)</value></data><data name='FieldLabel'><value>Field</value></data>"
                + "<data name='ButtonLabel'><value>Apply</value></data><data name='EntryLabel'><value>Entry</value></data></root>", "Text.en.resx", 6, "en"),
            new ResxTextSnapshot("<root><data name='Greeting'><value>Position {Pos}</value></data></root>", "Text.de-DE.resx", 7, "de-DE")
        }, new[] { Lookup(body) }, "en");

        [Theory]
        [InlineData("LookupPath", "Panel", 5)]
        [InlineData("LookupMember", "Value", 3)]
        [InlineData("LookupMetadata", "Unit", 2)]
        [InlineData("LookupList", "Values", 3)]
        [InlineData("LookupEntry", "One", 2)]
        [InlineData("Template", "Greeting", 4)]
        [InlineData("Argument", "Pos", 4)]
        [InlineData("Resource", "PanelLabel", 2)]
        public void PreviewCoordinatesOnlyItsBoundIdentityAndValidatesAllEditedFileKinds(string kind, string name, int count)
        {
            var inputs = new[] { Input() }; var context = Context();
            var target = ReferenceCapture.Capture(inputs, context).Symbols.First(symbol => symbol.Kind == kind && symbol.Name == name);
            var preview = ReferenceCapture.Preview(inputs, context, target.Id, "Next");
            Assert.True(preview.IsApplicable, preview.Failure); Assert.Equal(count, preview.Edits.Count);
            Assert.Empty(preview.Diagnostics); Assert.False(preview.RequiresAcknowledgement, string.Join("; ", preview.Skipped));
            if (kind == "Argument") Assert.Equal(2, preview.Edits.Count(edit => edit.Path.EndsWith(".resx", StringComparison.Ordinal)));
        }

        [Fact]
        public void LookupAndCaptionValuesAreLiteralWhileTheSelectedTranslationAndNestedArgumentBindParameters()
        {
            var inputs = new[] { Input() }; var context = Context();
            var target = ReferenceCapture.Capture(inputs, context).Symbols.Single(symbol => symbol.Kind == "Parameter");
            var preview = ReferenceCapture.Preview(inputs, context, target.Id, "Next");
            Assert.True(preview.IsApplicable, preview.Failure); Assert.Equal(3, preview.Edits.Count);
            Assert.DoesNotContain(preview.Edits, edit => edit.Path.EndsWith(".almlookup", StringComparison.Ordinal));
            var resourceEdit = preview.Edits.Single(edit => edit.Path.EndsWith(".resx", StringComparison.Ordinal));
            var result = resourceEdit.Apply(context.Resources[0].Text);
            Assert.Contains("<value>Position {Pos} $(Next)</value>", result);
            Assert.Contains("name='PanelLabel'><value>$(P)</value>", result);
        }

        [Fact]
        public void MemberNamesInUnrelatedPathsDoNotCollideEvenWhenUsedByTheSameAlarm()
        {
            string body = LookupBody + "<Path Key='Other'><Segment LabelKey='PanelLabel'/><Property Key='Existing' LabelKey='FieldLabel'/></Path>";
            var context = Context(body); var inputs = new[] { Input("$(property:Panel#Value) $(property:Other#Existing)") };
            var target = ReferenceCapture.Capture(inputs, context).Symbols.First(symbol => symbol.Kind == "LookupMember" && symbol.Name == "Value");
            var preview = ReferenceCapture.Preview(inputs, context, target.Id, "Existing");
            Assert.True(preview.IsApplicable, preview.Failure); Assert.Empty(preview.Diagnostics);
            Assert.Equal(2, preview.Edits.Count);
        }

        [Theory]
        [InlineData("LookupPath", "Panel", "bad name")]
        [InlineData("LookupMember", "Value", "Apply")]
        [InlineData("Argument", "Pos", "bad-name")]
        [InlineData("Template", "Greeting", "bad name")]
        public void InvalidNamesAndNamespaceCollisionsCannotApply(string kind, string name, string newName)
        {
            var inputs = new[] { Input() }; var context = Context();
            var target = ReferenceCapture.Capture(inputs, context).Symbols.First(symbol => symbol.Kind == kind && symbol.Name == name);
            Assert.False(ReferenceCapture.Preview(inputs, context, target.Id, newName).IsApplicable);
        }

        [Fact]
        public void AStaticPathPrefixRemainsProvenWhenItsMemberIsMissing()
        {
            var inputs = new[] { Input("$(property:Panel#Missing)") }; var context = Context();
            var target = ReferenceCapture.Capture(inputs, context).Symbols.Single(symbol => symbol.Kind == "LookupPath");
            var preview = ReferenceCapture.Preview(inputs, context, target.Id, "Next");
            Assert.True(preview.IsApplicable, preview.Failure); Assert.Equal(2, preview.Edits.Count);
            Assert.True(preview.RequiresAcknowledgement); Assert.Contains(preview.Skipped, message => message.Contains("missing member"));
        }

        [Fact]
        public void ActualPackagePathOverridesConnectFamiliesAndRetainReadOnlyFindings()
        {
            var identity = new LibraryIdentity("Example.Lookup", "1.0.0");
            var artifact = new LibraryArtifact(identity, null, null, null, null,
                new[] { Lookup("<Path Key='Panel'><Segment LabelKey='PanelLabel'/><Property Key='Value' LabelKey='FieldLabel'/></Path>") });
            var context = Context().WithLibraries(new LibraryReferenceSet(new[] { identity },
                new[] { new LibraryTextSnapshot(LibraryArtifactXml.Write(artifact), "lookup.almlib", 1) }));
            var inputs = new[] { Input() }; var data = ReferenceCapture.Capture(inputs, context);
            var paths = data.Symbols.Where(symbol => symbol.Kind == "LookupPath" && symbol.Name == "Panel").ToArray();
            Assert.Equal(2, paths.Length); Assert.Equal(paths[0].Family, paths[1].Family);
            var preview = ReferenceCapture.Preview(inputs, context, paths[0].Id, "Next");
            Assert.True(preview.IsApplicable, preview.Failure); Assert.Equal(5, preview.Edits.Count);
            Assert.True(preview.RequiresAcknowledgement); Assert.Contains(preview.Skipped, message => message.Contains("read-only"));
        }

        [Fact]
        public void ListEntryOverridesJoinTheirOwnIdentityWithoutJoiningUnrelatedEntries()
        {
            var identity = new LibraryIdentity("Example.List", "1.0.0");
            var artifact = new LibraryArtifact(identity, null, null, null, null,
                new[] { Lookup("<List Key='Values'><Entry Key='One' Value='library'/><Entry Key='Two' Value='other'/></List>") });
            var context = Context().WithLibraries(new LibraryReferenceSet(new[] { identity },
                new[] { new LibraryTextSnapshot(LibraryArtifactXml.Write(artifact), "list.almlib", 1) }));
            var inputs = new[] { Input("$(list:Values#One)") }; var data = ReferenceCapture.Capture(inputs, context);
            var entries = data.Symbols.Where(symbol => symbol.Kind == "LookupEntry" && symbol.Name == "One").ToArray();
            Assert.Equal(2, entries.Length); Assert.Equal(entries[0].Family, entries[1].Family);
            Assert.NotEqual(entries[0].Family, data.Symbols.Single(symbol => symbol.Kind == "LookupEntry" && symbol.Name == "Two").Family);
            var preview = ReferenceCapture.Preview(inputs, context, entries[0].Id, "Next");
            Assert.True(preview.IsApplicable, preview.Failure); Assert.Equal(2, preview.Edits.Count); Assert.True(preview.RequiresAcknowledgement);
        }

        [Fact]
        public void DeferredTemplatesUseTheMergedDeclaredBindingNamespaceWithoutInventingResourceDeclarations()
        {
            var identities = new[] { new LibraryIdentity("One", "1.0.0"), new LibraryIdentity("Two", "1.0.0") };
            var artifacts = identities.Select(identity => new LibraryTextSnapshot(LibraryArtifactXml.Write(new LibraryArtifact(identity, null, null, null, null,
                new[] { Lookup("<List Key='Values'/><Template Key='Greeting'><Argument Name='" + identity.Id + "' List='Values'/></Template>") })), identity.Id + ".almlib", 1));
            var context = new CompilationContext(Array.Empty<ResxTextSnapshot>(), new[] { Lookup("<Template Key='Greeting'><Argument Name='Local' List='Values'/></Template>") }, "keyword")
                .WithLibraries(new LibraryReferenceSet(identities, artifacts));
            var inputs = new[] { Input("$(loc:Greeting, Local=&quot;literal&quot;)") };
            var data = ReferenceCapture.Capture(inputs, context);
            var templates = data.Symbols.Where(symbol => symbol.Kind == "Template").ToArray();
            Assert.Equal(3, templates.Length); Assert.Single(templates.Select(symbol => symbol.Family).Distinct());
            Assert.DoesNotContain(data.Symbols, symbol => symbol.Kind == "Resource");
            var preview = ReferenceCapture.Preview(inputs, context, templates[0].Id, "Next");
            Assert.True(preview.IsApplicable, preview.Failure); Assert.Equal(2, preview.Edits.Count); Assert.True(preview.RequiresAcknowledgement);
        }

        [Fact]
        public void AmbiguousPathOwnersDoNotMakeTheirMemberDeclarationsRenameable()
        {
            const string path = "<Path Key='Panel'><Segment LabelKey='PanelLabel'/><Property Key='Value' LabelKey='FieldLabel'/></Path>";
            var resources = Context().Resources;
            var context = new CompilationContext(resources, new[] { Lookup(path), new LookupTextSnapshot("<Lookup xmlns='urn:alarmlist:lookup:1'>" + path + "</Lookup>", "duplicate.almlookup", 2) }, "en");
            var inputs = new[] { Input("$(property:Panel#Value)") };
            var data = ReferenceCapture.Capture(inputs, context);
            foreach (var target in data.Symbols.Where(symbol => symbol.Kind == "LookupMember"))
            {
                var preview = ReferenceCapture.Preview(inputs, context, target.Id, "Next");
                Assert.False(preview.IsApplicable); Assert.Empty(preview.Edits);
            }
        }
    }
}
