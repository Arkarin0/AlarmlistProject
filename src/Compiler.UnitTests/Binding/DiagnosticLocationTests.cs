// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.IO;
using System.Linq;
using Alarmlist.Compiler;
using Alarmlist.Diagnostics;
using Alarmlist.Text;
using Xunit;

namespace Alarmlist.Core.UnitTests.Binding
{
    public class DiagnosticLocationTests
    {
        [Theory]
        [InlineData("Missing", "ALM0002")]
        [InlineData("A", "ALM0003")]
        public void ReferenceDiagnosticsPointToTheReferenceDeclaration(string reference, string expectedId)
        {
            CompilationResult result = new AlarmCompiler().CompileSnapshots(new[] { Snapshot("A", reference, "A.almx", 8) });

            Diagnostic diagnostic = Assert.Single(result.Diagnostics);
            Assert.Equal(expectedId, diagnostic.Id);
            AssertLocation(diagnostic.Location, "A.almx", 4, 8, "ReferenceName");
            Assert.Empty(diagnostic.RelatedLocations);
        }

        [Fact]
        public void DuplicateNameReportsBothDistinctDeclarationsAcrossFiles()
        {
            CompilationResult result = new AlarmCompiler().CompileSnapshots(new[]
            {
                Snapshot("A", "", "First.almx", 2), Snapshot("A", "", "Second.almx", 9)
            });

            Diagnostic diagnostic = Assert.Single(result.Diagnostics);
            Assert.Equal("ALM0001", diagnostic.Id);
            AssertLocation(diagnostic.Location, "Second.almx", 3, 9, "FullyQualifiedName");
            AssertLocation(Assert.Single(diagnostic.RelatedLocations), "First.almx", 3, 2, "FullyQualifiedName");
        }

        [Fact]
        public void CycleReportsEveryParticipatingReferenceButNotTheDependentAlarm()
        {
            CompilationResult result = new AlarmCompiler().CompileSnapshots(new[]
            {
                Snapshot("Child", "A", "Child.almx", 0),
                Snapshot("A", "B", "A.almx", 1),
                Snapshot("B", "C", "B.almx", 2),
                Snapshot("C", "A", "C.almx", 3)
            });

            Diagnostic diagnostic = Assert.Single(result.Diagnostics);
            Assert.Equal("ALM0004", diagnostic.Id);
            Assert.Contains("A -> B -> C -> A", diagnostic.Message);
            AssertLocation(diagnostic.Location, "A.almx", 4, 1, "ReferenceName");
            Assert.Equal(new[] { "B.almx", "C.almx" }, diagnostic.RelatedLocations.Select(location => location.FilePath));
            AssertLocation(diagnostic.RelatedLocations[0], "B.almx", 4, 2, "ReferenceName");
            AssertLocation(diagnostic.RelatedLocations[1], "C.almx", 4, 3, "ReferenceName");
        }

        [Fact]
        public void DiskDiagnosticsHaveLocationsWithoutInventingABufferVersion()
        {
            string path = Path.GetTempFileName();
            try
            {
                File.WriteAllText(path, Snapshot("A", "Missing", path, 4).Text);
                Diagnostic diagnostic = Assert.Single(new AlarmCompiler().CompileFiles(new[] { path }).Diagnostics);
                AssertLocation(diagnostic.Location, path, 4, null, "ReferenceName");
            }
            finally
            {
                File.Delete(path);
            }
        }

        [Fact]
        public void LocationAttachmentPreservesLegacyDiagnosticAndCopiesRelatedLocations()
        {
            var original = new Diagnostic(DiagnosticDescriptors.MissingReference, "A", "Missing");
            var location = new SourceLocation("A.almx", 4, 6, 13, 3);
            var related = new[] { new SourceLocation("B.almx", 3, 6, 18, 2) };

            Diagnostic located = original.WithLocation(location, related);
            related[0] = null;

            Assert.Null(original.Location);
            Assert.Empty(original.RelatedLocations);
            Assert.Equal(original.Id, located.Id);
            Assert.Equal(original.Severity, located.Severity);
            Assert.Equal(original.Message, located.Message);
            Assert.Equal("B.almx", Assert.Single(located.RelatedLocations).FilePath);
            Assert.Same(location, located.Location);
        }

        private static AlmxTextSnapshot Snapshot(string name, string reference, string path, int version)
        {
            return new AlmxTextSnapshot("<Alarmlist>\n  <Alarm>\n    <FullyQualifiedName>" + name
                + "</FullyQualifiedName>\n    <ReferenceName>" + reference
                + "</ReferenceName>\n  </Alarm>\n</Alarmlist>", path, version);
        }

        private static void AssertLocation(SourceLocation location, string path, int line, int? version, string field)
        {
            Assert.NotNull(location);
            Assert.Equal(path, location.FilePath);
            Assert.Equal(line, location.LineNumber);
            Assert.Equal(6, location.ColumnNumber);
            Assert.Equal(line, location.EndLineNumber);
            Assert.Equal(6 + field.Length, location.EndColumnNumber);
            Assert.Equal(version, location.SourceVersion);
        }
    }
}
