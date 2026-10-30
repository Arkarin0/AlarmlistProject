// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Linq;
using System.Threading;
using Alarmlist.Analysis;
using Alarmlist.Compiler;
using Alarmlist.Text;
using Xunit;

namespace Alarmlist.Core.UnitTests.Analysis
{
    public class ReferenceSolutionTests
    {
        private static ResxTextSnapshot Resource(string culture, string folder = "shared")
            => new ResxTextSnapshot("<root><data name='Greeting'><value>Hello</value></data></root>", folder + "/text." + culture + ".resx", 2, culture);

        private static ReferenceCompilationSnapshot Context(string project, string culture, params ResxTextSnapshot[] resources)
            => new ReferenceCompilationSnapshot(new[] { new AlmxTextSnapshot("<Alarmlist><Alarm><FullyQualifiedName>A</FullyQualifiedName><Name>$(loc:Greeting)</Name></Alarm></Alarmlist>", project + ".almx", 1) },
                new CompilationContext(resources, culture, projectPath: project));

        [Fact]
        public void ConnectedDeclarationFamiliesExpandTransitivelyAndRetainEveryConsumerCulture()
        {
            var english = Resource("en"); var german = Resource("de-DE"); var french = Resource("fr"); var italian = Resource("it");
            var north = Context("North", "en", english, german);
            var south = Context("South", "de-DE", german, french);
            var west = Context("West", "fr", french, italian);
            var unrelated = Context("Unrelated", "en", Resource("en", "other"));
            var northGerman = Context("North", "de-DE", english, german);
            var target = north.TargetAtToken(english.FilePath, english.Version, english.Text.IndexOf("Greeting", StringComparison.Ordinal));
            // Reverse graph order forces traversal beyond a single forward pass.
            var found = ReferenceCapture.FindSolution(new[] { unrelated, west, south, northGerman, north }, north, target,
                new[] { "Unavailable project: Missing", "Excluded configuration: North/Release" });
            Assert.True(found.IsAvailable, found.Failure); Assert.Equal(5, found.Contexts.Count); Assert.Equal(2, found.Coverage.Count);
            Assert.Empty(found.Contexts.Single(context => context.ProjectPath == "Unrelated").Symbols);
            foreach (var context in found.Contexts.Where(context => context.ProjectPath != "Unrelated"))
            {
                Assert.Equal(2, context.Symbols.Count); Assert.Contains(context.Occurrences, use => use.Role == "Localization");
                Assert.Equal(2, context.Occurrences.Count(use => use.Role == "Declaration"));
            }
            Assert.Equal(2, found.Contexts.Count(context => context.ProjectPath == "North"));
            Assert.Equal(4, found.Contexts.SelectMany(context => context.Symbols).Select(symbol => symbol.DeclarationId).Distinct().Count());
            Assert.Throws<NotSupportedException>(() => ((System.Collections.Generic.IList<ReferenceContextResult>)found.Contexts).Clear());
        }

        [Fact]
        public void UnavailableInputsRemainVisibleWithoutMakingEqualNamesIntoReferences()
        {
            var resource = Resource("en"); var origin = Context("North", "en", resource);
            var invalid = new ReferenceCompilationSnapshot(new[] { new AlmxTextSnapshot("<broken", "bad.almx", 1) }, new CompilationContext(projectPath: "Bad"));
            var missing = Context("Missing", "en");
            var target = origin.TargetAtToken(resource.FilePath, resource.Version, resource.Text.IndexOf("Greeting", StringComparison.Ordinal));
            var result = ReferenceCapture.FindSolution(new[] { origin, invalid, missing }, origin, target);
            Assert.True(result.IsAvailable, result.Failure);
            Assert.Contains(result.Contexts.Single(context => context.ProjectPath == "Bad").Coverage, message => message.StartsWith("References unavailable:"));
            Assert.Empty(result.Contexts.Single(context => context.ProjectPath == "Missing").Occurrences);
            Assert.NotEmpty(result.Contexts.Single(context => context.ProjectPath == "Missing").Diagnostics);
            Assert.False(ReferenceCapture.FindSolution(new[] { invalid }, invalid, invalid.TargetAtToken("bad.almx", 1, 1)).IsAvailable);
        }

        [Fact]
        public void SharedTokenWithIndependentProjectBindingsIsReportedWithoutGuessingAnEdit()
        {
            var shared = new AlmxTextSnapshot("<Alarmlist><Alarm><FullyQualifiedName>A</FullyQualifiedName><Code>$(P)</Code></Alarm></Alarmlist>", "shared.almx", 3);
            var north = new ReferenceCompilationSnapshot(new[] { shared }, new CompilationContext(projectPath: "North", properties: new[] { new PropertyDefinition("P", "north") }));
            var south = new ReferenceCompilationSnapshot(new[] { shared }, new CompilationContext(projectPath: "South", properties: new[] { new PropertyDefinition("P", "south") }));
            var target = north.TargetAtToken(shared.FilePath, shared.Version, shared.Text.IndexOf("$(P)", StringComparison.Ordinal) + 2);
            var found = ReferenceCapture.FindSolution(new[] { north, south }, north, target);
            Assert.True(found.IsAvailable, found.Failure);
            Assert.Contains(found.Coverage, message => message.Contains("Incompatible shared consumer binding"));
            var use = found.Contexts[0].Occurrences.Single(item => item.Role == "Expression");
            Assert.Null(use.Replacement); Assert.Contains("another family", use.Unavailable);
            Assert.Empty(found.Contexts[1].Symbols);
        }

        [Fact]
        public void OriginMembershipSnapshotFreshnessUniqueContextsAndCancellationAreRequired()
        {
            var resource = Resource("en"); var origin = Context("North", "en", resource);
            var target = origin.TargetAtToken(resource.FilePath, resource.Version, resource.Text.IndexOf("Greeting", StringComparison.Ordinal));
            Assert.False(ReferenceCapture.FindSolution(Array.Empty<ReferenceCompilationSnapshot>(), origin, target).IsAvailable);
            var changed = Context("North", "de-DE", resource);
            Assert.False(ReferenceCapture.FindSolution(new[] { changed }, changed, target).IsAvailable);
            Assert.Throws<ArgumentException>(() => ReferenceCapture.FindSolution(new[] { origin, origin }, origin, target));
            Assert.Throws<OperationCanceledException>(() => ReferenceCapture.FindSolution(new[] { origin }, origin, target, token: new CancellationToken(true)));
        }
    }
}
