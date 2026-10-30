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
    public class CoordinatedRenameTests
    {
        private static AlmxTextSnapshot Source(string name, string body, string path)
            => new AlmxTextSnapshot("<Alarmlist><Alarm><FullyQualifiedName>" + name + "</FullyQualifiedName>" + body + "</Alarm></Alarmlist>", path, 5);

        [Fact]
        public void ConnectedSolutionFamiliesValidateOneJointSelectedOverlay()
        {
            var sources = new[] { Source("A", "<Properties><Property Name='P' Value='one'/></Properties><Code>$(P)</Code>", "a.almx"),
                Source("B", "<Properties><Property Name='P' Value='two'/></Properties><Code>$(P)</Code>", "b.almx") };
            var request = new ReferenceCompilationSnapshot(sources, CompilationContext.Empty);
            var targets = sources.Select(source => request.TargetAtToken(source.FilePath, source.Version, source.Text.IndexOf("$(P)", StringComparison.Ordinal) + 2)).ToArray();
            var compiler = new AlarmCompiler();
            var full = compiler.PreviewRenameSnapshots(request, targets, "Next");
            Assert.True(full.IsApplicable, full.Failure); Assert.Equal(4, full.Edits.Count); Assert.Equal(2, full.ReferenceSearches.Count);
            Assert.Equal(2, full.ReferenceSearches.Select(result => result.Symbol.Family).Distinct().Count()); Assert.Empty(full.Diagnostics);
            var partial = compiler.PreviewRenameSnapshots(request, targets, "Next", includedGroups: full.Groups.Where(group => group.Path == "b.almx").Select(group => group.Id).ToArray());
            Assert.True(partial.IsApplicable, partial.Failure); Assert.Equal(2, partial.Edits.Count); Assert.All(partial.Edits, edit => Assert.Equal("b.almx", edit.Path));
            Assert.True(partial.RequiresAcknowledgement); Assert.Empty(partial.Diagnostics);
            var stale = compiler.PreviewRenameSnapshots(request, targets, "Other", includedGroups: full.Groups.Select(group => group.Id).ToArray());
            Assert.False(stale.IsApplicable); Assert.Empty(stale.Edits);
            Assert.Throws<OperationCanceledException>(() => compiler.PreviewRenameSnapshots(request, targets, "Next", new CancellationToken(true)));
        }

        [Fact]
        public void SharedTokenCanChangeOnlyWhenAllItsBoundFamiliesParticipate()
        {
            var source = new AlmxTextSnapshot("<Alarmlist><Properties><Property Name='P' Value='file'/></Properties>"
                + "<Alarm abstract='true'><FullyQualifiedName>Base</FullyQualifiedName><Code>$(P)</Code></Alarm>"
                + "<Alarm><FullyQualifiedName>Child</FullyQualifiedName><ReferenceName>Base</ReferenceName><Properties><Property Name='P' Value='child'/></Properties></Alarm></Alarmlist>", "shared.almx", 7);
            var request = new ReferenceCompilationSnapshot(new[] { source }, CompilationContext.Empty);
            var declarations = ReferenceCapture.Capture(new[] { source }, CompilationContext.Empty).Symbols.Where(symbol => symbol.Kind == "Parameter").ToArray();
            var targets = declarations.Select(symbol => request.TargetIdentity(symbol.DeclarationId)).ToArray();
            var compiler = new AlarmCompiler();
            var separate = compiler.PreviewRenameSnapshots(request, targets[0], "Next");
            Assert.Single(separate.Edits); Assert.True(separate.RequiresAcknowledgement);
            var joint = compiler.PreviewRenameSnapshots(request, targets, "Next");
            Assert.True(joint.IsApplicable, joint.Failure); Assert.Equal(3, joint.Edits.Count); Assert.Empty(joint.Skipped); Assert.Empty(joint.Diagnostics);
            string text = source.Text;
            foreach (var edit in joint.Edits.OrderByDescending(edit => edit.Start)) text = edit.Apply(text);
            Assert.Equal("child", compiler.CompileSnapshots(new[] { new AlmxTextSnapshot(text, source.FilePath, source.Version) }).AlarmList.Single().Code);
        }

        [Fact]
        public void CollisionKeepsThatFamilyNonApplicableWhileProvenEditsElsewhereRequireAcknowledgement()
        {
            var sources = new[] { Source("A", "<Properties><Property Name='P' Value='one'/><Property Name='Next' Value='existing'/></Properties><Code>$(P)</Code>", "a.almx"),
                Source("B", "<Properties><Property Name='P' Value='two'/></Properties><Code>$(P)</Code>", "b.almx") };
            var request = new ReferenceCompilationSnapshot(sources, CompilationContext.Empty);
            var targets = sources.Select(source => request.TargetAtToken(source.FilePath, source.Version, source.Text.IndexOf("$(P)", StringComparison.Ordinal) + 2)).ToArray();
            var result = new AlarmCompiler().PreviewRenameSnapshots(request, targets, "Next");
            Assert.True(result.IsApplicable, result.Failure); Assert.True(result.RequiresAcknowledgement); Assert.Equal(2, result.Edits.Count);
            Assert.All(result.Edits, edit => Assert.Equal("b.almx", edit.Path)); Assert.Contains(result.Skipped, message => message.Contains("colliding family"));
            Assert.Single(result.NonApplicableFamilies);
            Assert.Empty(result.Diagnostics);
        }

        [Fact]
        public void CaseOnlyRenameUsesEachDecodedSpellingAndPreservesAlreadyCorrectEntities()
        {
            var source = Source("A", "<Properties><Property Name='&#112;' Value='one'/></Properties><Code>$(P)</Code>", "a.almx");
            var request = new ReferenceCompilationSnapshot(new[] { source }, CompilationContext.Empty);
            var target = request.TargetAtToken(source.FilePath, source.Version, source.Text.IndexOf("$(P)", StringComparison.Ordinal) + 2);
            var compiler = new AlarmCompiler(); var renamed = compiler.PreviewRenameSnapshots(request, target, "p");
            Assert.True(renamed.IsApplicable, renamed.Failure); Assert.Single(renamed.Edits); Assert.Empty(renamed.Skipped);
            Assert.Contains(renamed.References.Occurrences, use => use.Role == "Declaration" && use.Spelling == "p");
            Assert.Contains(renamed.References.Occurrences, use => use.Role == "Expression" && use.Spelling == "P");
            string changed = renamed.Edits[0].Apply(source.Text);
            Assert.Contains("Name='&#112;'", changed); Assert.Contains("$(p)", changed);
            Assert.Equal("one", compiler.CompileSnapshots(new[] { new AlmxTextSnapshot(changed, source.FilePath, source.Version) }).AlarmList.Single().Code);
            var noChange = new ReferenceCompilationSnapshot(new[] { new AlmxTextSnapshot(changed, source.FilePath, source.Version) }, CompilationContext.Empty);
            Assert.Empty(compiler.PreviewRenameSnapshots(noChange, noChange.TargetAtToken(source.FilePath, source.Version, changed.IndexOf("$(p)", StringComparison.Ordinal) + 2), "p").Edits);
        }

        [Fact]
        public void ExcludingOnePhysicalReplacementKeepsOtherProvenEditsInTheSameFile()
        {
            var source = Source("A", "<Properties><Property Name='P' Value='one'/></Properties><Code>$(P)</Code>", "a.almx");
            var request = new ReferenceCompilationSnapshot(new[] { source }, CompilationContext.Empty);
            var target = request.TargetAtToken(source.FilePath, source.Version, source.Text.IndexOf("$(P)", StringComparison.Ordinal) + 2);
            var compiler = new AlarmCompiler(); var full = compiler.PreviewRenameSnapshots(request, target, "Next");
            Assert.Equal(2, full.Groups.Count); Assert.Equal(2, full.Groups.Select(group => group.Id).Distinct().Count());
            var declaration = full.Groups.Single(group => group.Edits[0].Start < source.Text.IndexOf("<Code>", StringComparison.Ordinal));
            var partial = compiler.PreviewRenameSnapshots(request, target, "Next", includedGroups: new[] { declaration.Id });
            Assert.True(partial.IsApplicable, partial.Failure); Assert.Single(partial.Edits); Assert.True(partial.RequiresAcknowledgement);
            Assert.Contains(partial.Diagnostics, diagnostic => diagnostic.Id == "ALM0007"); Assert.Contains(partial.Skipped, finding => finding.Contains("1 changes remain"));
        }

        [Fact]
        public void DifferentNamesAndUnreliableTargetsCannotBeCoordinatedByGuessing()
        {
            var source = Source("A", "<Properties><Property Name='P' Value='1'/><Property Name='Q' Value='2'/></Properties><Code>$(P)</Code><Name>$(Q)</Name>", "a.almx");
            var request = new ReferenceCompilationSnapshot(new[] { source }, CompilationContext.Empty);
            var targets = new[] { request.TargetAtToken(source.FilePath, 5, source.Text.IndexOf("$(P)", StringComparison.Ordinal) + 2), request.TargetAtToken(source.FilePath, 5, source.Text.IndexOf("$(Q)", StringComparison.Ordinal) + 2) };
            var compiler = new AlarmCompiler(); var result = compiler.PreviewRenameSnapshots(request, targets, "Next");
            Assert.False(result.IsApplicable); Assert.Empty(result.Edits); Assert.Contains("compatible declarations", result.Failure);
            var unavailable = compiler.PreviewRenameSnapshots(request, new[] { targets[0], request.TargetIdentity("absent") }, "Next");
            Assert.False(unavailable.IsApplicable); Assert.Empty(unavailable.Edits); Assert.Equal(2, unavailable.ReferenceSearches.Count);
        }
    }
}
