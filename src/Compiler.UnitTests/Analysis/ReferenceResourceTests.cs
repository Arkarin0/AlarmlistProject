// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Linq;
using Alarmlist.Analysis;
using Alarmlist.Compiler;
using Alarmlist.Evaluation;
using Alarmlist.Libraries;
using Alarmlist.Text;
using Xunit;

namespace Alarmlist.Core.UnitTests.Analysis
{
    public class ReferenceResourceTests
    {
        private static AlmxTextSnapshot Input(string value) => new AlmxTextSnapshot("<Alarmlist><Alarm><FullyQualifiedName>A</FullyQualifiedName>"
            + "<Properties><Property Name='P' Value='1'/></Properties><Name>" + value + "</Name></Alarm></Alarmlist>", "source.almx", 2);
        private static ResxTextSnapshot Resource(string body, string culture) => new ResxTextSnapshot("<root>" + body + "</root>", "Text." + culture + ".resx", 3, culture);

        [Theory]
        [InlineData("en")]
        [InlineData("de-DE")]
        [InlineData("keyword")]
        public void ResourceRenameCoordinatesCultureDeclarationsAndAlmxAndValidatesResourceOverlay(string culture)
        {
            var inputs = new[] { Input("$(loc:Greeting)") };
            var resources = new[] { Resource("<data name='Greeting'><value>Hello</value></data>", "en"), Resource("<data name='Greeting'><value>Hallo</value></data>", "de-DE") };
            var context = new CompilationContext(resources, culture);
            var data = ReferenceCapture.Capture(inputs, context);
            var symbols = data.Symbols.Where(symbol => symbol.Kind == "Resource").ToArray();
            Assert.Equal(2, symbols.Length); Assert.Single(symbols.Select(symbol => symbol.Family).Distinct());
            var preview = ReferenceCapture.Preview(inputs, context, symbols[0].Id, "Welcome");
            Assert.True(preview.IsApplicable, preview.Failure); Assert.Equal(3, preview.Edits.Count);
            Assert.Empty(preview.Diagnostics); Assert.False(preview.RequiresAcknowledgement);
            Assert.Contains("name='Greeting'", resources[0].Text);
        }

        [Fact]
        public void TranslatedExpressionUsesItsActualParameterBindingAndParentCultureFallback()
        {
            var inputs = new[] { Input("$(loc:Greeting)") };
            var resources = new[] { Resource("<data name='Greeting'><value>Position $(P), $$(P)</value><comment>$(P)</comment></data>", "de") };
            var context = new CompilationContext(resources, "de-DE");
            var data = ReferenceCapture.Capture(inputs, context);
            var parameter = data.Symbols.Single(symbol => symbol.Kind == "Parameter");
            var preview = ReferenceCapture.Preview(inputs, context, parameter.Id, "Next");
            Assert.True(preview.IsApplicable, preview.Failure); Assert.Equal(2, preview.Edits.Count);
            var resourceEdit = preview.Edits.Single(edit => edit.Path.EndsWith(".resx", StringComparison.Ordinal));
            Assert.Contains("Position $(Next), $$(P)", resourceEdit.Apply(resources[0].Text));
            Assert.Contains("<comment>$(P)</comment>", resourceEdit.Apply(resources[0].Text)); Assert.Empty(preview.Diagnostics);
        }

        [Theory]
        [InlineData("bad key", false)]
        [InlineData("Existing", false)]
        [InlineData("existing", true)]
        public void ResourceNamesUseTheExistingCaseSensitiveGrammarAndRejectCollisions(string newName, bool applicable)
        {
            var inputs = new[] { Input("$(loc:Greeting)") };
            var context = new CompilationContext(new[] { Resource("<data name='Greeting'><value>Hello</value></data><data name='Existing'><value>Existing</value></data>", "en") }, "en");
            var target = ReferenceCapture.Capture(inputs, context).Symbols.First(symbol => symbol.Kind == "Resource");
            Assert.Equal(applicable, ReferenceCapture.Preview(inputs, context, target.Id, newName).IsApplicable);
        }

        [Fact]
        public void ParserOwnsExactPlaceholderSpansAndExcludesEscapedBraces()
        {
            const string raw = "{{Literal}} {Position} {Position} $(P)";
            var expression = PropertyExpression.Parse(raw, placeholders: true);
            Assert.Equal(2, expression.Placeholders.Count());
            Assert.All(expression.Placeholders, span => Assert.Equal(span.Name, raw.Substring(span.Start, span.Length)));
            Assert.All(expression.Placeholders, span => Assert.Equal("Position", span.Name));
        }

        [Fact]
        public void ResourceOverridesConnectOnlyActualProvidersAndKeepPackageDeclarationsReadOnly()
        {
            var identity = new LibraryIdentity("Example.Resources", "1.0.0");
            var artifact = new LibraryArtifact(identity, null, null, null,
                new[] { Resource("<data name='Greeting'><value>Library</value></data>", "en") }, null);
            var context = new CompilationContext(new[] { Resource("<data name='Greeting'><value>Project</value></data>", "en") }, "en")
                .WithLibraries(new LibraryReferenceSet(new[] { identity }, new[] { new LibraryTextSnapshot(LibraryArtifactXml.Write(artifact), "resources.almlib", 1) }));
            var inputs = new[] { Input("$(loc:Greeting)") };
            var resources = ReferenceCapture.Capture(inputs, context).Symbols.Where(symbol => symbol.Kind == "Resource").ToArray();
            Assert.Equal(2, resources.Length); Assert.Equal(resources[0].Family, resources[1].Family);
            var preview = ReferenceCapture.Preview(inputs, context, resources[0].Id, "Next");
            Assert.True(preview.IsApplicable, preview.Failure); Assert.Equal(2, preview.Edits.Count);
            Assert.True(preview.RequiresAcknowledgement); Assert.Empty(preview.Diagnostics);
        }

        [Fact]
        public void UnrelatedResourceProvidersStaySeparateAndAmbiguousUsesReceiveNoGuessedEdits()
        {
            var identities = new[] { new LibraryIdentity("One", "1.0.0"), new LibraryIdentity("Two", "1.0.0") };
            var artifacts = identities.Select(identity => new LibraryTextSnapshot(LibraryArtifactXml.Write(new LibraryArtifact(identity, null, null, null,
                new[] { Resource("<data name='Greeting'><value>" + identity.Id + "</value></data>", "en") }, null)), identity.Id + ".almlib", 1));
            var context = new CompilationContext(Array.Empty<ResxTextSnapshot>(), "en").WithLibraries(new LibraryReferenceSet(identities, artifacts));
            var inputs = new[] { Input("$(loc:Greeting)") };
            var data = ReferenceCapture.Capture(inputs, context);
            var symbols = data.Symbols.Where(symbol => symbol.Kind == "Resource").ToArray();
            Assert.Equal(2, symbols.Length); Assert.NotEqual(symbols[0].Family, symbols[1].Family);
            Assert.DoesNotContain(data.Occurrences, use => use.Role == "Localization");
            Assert.False(ReferenceCapture.Preview(inputs, context, symbols[0].Id, "Next").IsApplicable);
            Assert.Contains(data.Coverage, message => message.Contains("ambiguous localization"));
        }
    }
}
