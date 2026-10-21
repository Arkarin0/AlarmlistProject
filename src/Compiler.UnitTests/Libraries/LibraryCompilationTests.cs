// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Linq;
using Alarmlist.Analysis;
using Alarmlist.Compiler;
using Alarmlist.Libraries;
using Alarmlist.Text;
using Xunit;

namespace Alarmlist.Core.UnitTests.Libraries
{
    public class LibraryCompilationTests
    {
        [Theory]
        [InlineData(" Value='alarm'", " Value='file'", "consumer", "alarm")]
        [InlineData("", " Value='file'", "consumer", "file")]
        [InlineData("", "", "consumer", "consumer")]
        [InlineData("", "", null, "library")]
        [InlineData(" Value=''", " Value='file'", "consumer", "")]
        [InlineData("", " Value=''", "consumer", "")]
        [InlineData("", "", "", "")]
        public void ImportedConcreteAlarmUsesItsOwnScopesThenConsumerThenLibraryDefaults(
            string alarmAssignment, string fileAssignment, string consumer, string expected)
        {
            LibraryArtifact library = Library("Library", "<Properties><Property Name='p'" + fileAssignment + "/></Properties>"
                + "<Alarm><FullyQualifiedName>A</FullyQualifiedName><Properties><Property Name='P'" + alarmAssignment
                + "/></Properties><Code>$(p)</Code></Alarm>", "library");
            CompilationResult result = Compile(new[] { library }, "", consumer);
            Assert.True(result.Success, string.Join("; ", result.Diagnostics));
            Assert.Equal(expected, Assert.Single(result.AlarmList).Code);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("consumer")]
        public void ConsumerDerivedAlarmDoesNotInheritLibraryAssignments(string consumer)
        {
            LibraryArtifact library = Library("Library", "<Properties><Property Name='p' Value='file'/></Properties>"
                + "<Alarm abstract='true'><FullyQualifiedName>Base</FullyQualifiedName>"
                + "<Properties><Property Name='p' Value='base'/></Properties><Code>$(p)</Code></Alarm>", "library");
            CompilationResult result = Compile(new[] { library },
                "<Alarm><FullyQualifiedName>Local</FullyQualifiedName><ReferenceName>Base</ReferenceName></Alarm>", consumer);
            if (consumer != null)
            {
                Assert.True(result.Success, string.Join("; ", result.Diagnostics));
                Assert.Equal(consumer, Assert.Single(result.AlarmList).Code);
            }
            else
            {
                Assert.False(result.Success);
                var diagnostic = Assert.Single(result.Diagnostics);
                Assert.Equal("ALM0008", diagnostic.Id);
                Assert.Equal("library://Library/1.0.0/sources/alarms.almx", diagnostic.Location.FilePath);
                Assert.Null(diagnostic.Location.SourceVersion);
            }
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void LibraryCannotReferenceConsumerOrSibling(bool sibling)
        {
            const string target = "<Alarm abstract='true'><FullyQualifiedName>Target</FullyQualifiedName></Alarm>";
            LibraryArtifact library = Library("Library", "<Alarm><FullyQualifiedName>A</FullyQualifiedName>"
                + "<ReferenceName>Target</ReferenceName></Alarm>");
            CompilationResult result = Compile(sibling ? new[] { library, Library("Sibling", target) } : new[] { library },
                sibling ? "" : target);
            Assert.False(result.Success);
            Assert.Equal("ALM0002", Assert.Single(result.Diagnostics).Id);
        }

        [Fact]
        public void DependencyInheritanceUsesDerivedLibraryDefaultsAndFreshSyntax()
        {
            LibraryArtifact dependency = Library("Dependency", "<Alarm abstract='true'><FullyQualifiedName>Base</FullyQualifiedName>"
                + "<Properties><Property Name='p' Value='base'/></Properties><Code>$(p)</Code></Alarm>", "dependency");
            LibraryArtifact library = Library("Library", "<Alarm><FullyQualifiedName>A</FullyQualifiedName>"
                + "<ReferenceName>Base</ReferenceName></Alarm>", "owner", dependency.Identity);
            var graph = LibraryGraph.Resolve(new[] { library.Identity }, new[] { library, dependency });
            var first = LibraryCompilation.Bind(Array.Empty<AlmxTextSnapshot>(), graph);
            var second = LibraryCompilation.Bind(Array.Empty<AlmxTextSnapshot>(), graph);
            first.SyntaxTree.Alarms[0].Code = "mutated";
            CompilationResult result = SnapshotAnalyzer.Compile(second, CompilationContext.Empty);
            Assert.True(result.Success, string.Join("; ", result.Diagnostics));
            Assert.Equal("owner", Assert.Single(result.AlarmList).Code);
        }

        [Fact]
        public void DuplicateAlarmIdentitiesRemainGlobal()
        {
            const string alarm = "<Alarm><FullyQualifiedName>A</FullyQualifiedName></Alarm>";
            CompilationResult result = Compile(new[] { Library("Library", alarm) }, alarm);
            Assert.False(result.Success);
            Assert.Equal("ALM0001", Assert.Single(result.Diagnostics).Id);
        }

        [Fact]
        public void SourceIdentityIsPortableAndProjectIdentityBelongsToConsumer()
        {
            LibraryArtifact library = Library("Library", "<Alarm><Name>$(AlarmlistThisFile)|$(AlarmlistThisFileName)|"
                + "$(AlarmlistThisFileExtension)|$(AlarmlistProjectName)</Name></Alarm>");
            var graph = LibraryGraph.Resolve(new[] { library.Identity }, new[] { library });
            CompilationResult result = SnapshotAnalyzer.Compile(LibraryCompilation.Bind(Array.Empty<AlmxTextSnapshot>(), graph),
                new CompilationContext(projectPath: System.IO.Path.GetFullPath("Consumer.almproj")));
            Assert.True(result.Success, string.Join("; ", result.Diagnostics));
            Assert.Equal("alarms.almx|alarms|.almx|Consumer", Assert.Single(result.AlarmList).Name);
        }

        [Theory]
        [InlineData("AlarmlistThisFileFullPath")]
        [InlineData("AlarmlistThisFileDirectory")]
        public void ImportedPhysicalSourcePathsAreUnavailable(string property)
        {
            CompilationResult result = Compile(new[] { Library("Library", "<Alarm><Name>$(" + property + ")</Name></Alarm>") }, "");
            Assert.False(result.Success);
            Assert.Contains(property, Assert.Single(result.Diagnostics).Message);
        }

        private static LibraryArtifact Library(string id, string content, string value = null, params LibraryIdentity[] dependencies)
            => new LibraryArtifact(new LibraryIdentity(id, "1.0.0"), dependencies,
                value == null ? null : new[] { new PropertyDefinition("p", value) },
                new[] { LibrarySource.Capture("sources/alarms.almx", new AlmxTextSnapshot("<Alarmlist>" + content + "</Alarmlist>",
                    "original.almx", 12).Parse()) }, null, null);

        private static CompilationResult Compile(LibraryArtifact[] libraries, string local, string consumer = null)
        {
            var graph = LibraryGraph.Resolve(libraries.Select(library => library.Identity), libraries);
            var binding = LibraryCompilation.Bind(new[] { new AlmxTextSnapshot("<Alarmlist>" + local + "</Alarmlist>", "Consumer.almx", 3) }, graph);
            return SnapshotAnalyzer.Compile(binding, new CompilationContext(properties: new[] { new PropertyDefinition("p", consumer) }));
        }
    }
}
