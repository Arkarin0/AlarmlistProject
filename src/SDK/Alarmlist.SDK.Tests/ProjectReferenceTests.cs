using System.IO;
using System.Linq;
using System.Reflection;
using Xunit;
using Xunit.Abstractions;

namespace Alarmlist.MSBuild.SDK.Tests
{
    [Collection(TestProjectCollection.Name)]
    public class ProjectReferenceTests
    {
        private readonly TestProjectFixture _fixture;
        private readonly ITestOutputHelper _output;

        public ProjectReferenceTests(TestProjectFixture fixture, ITestOutputHelper output)
        {
            _fixture = fixture;
            _output = output;
        }

        [Theory]
        [InlineData("net472", "/p:BuildingInsideVisualStudio=true")]
        [InlineData("net8.0", "/p:BuildingInsideVisualStudio=true")]
        [InlineData("net472", "/p:BuildProjectReferences=false")]
        [InlineData("net8.0", "/p:BuildProjectReferences=false")]
        public void SdkReferenceCanBeQueriedWithoutBuildingIt(string framework, string buildMode)
        {
            var projectPath = typeof(ProjectReferenceTests).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
                .Single(attribute => attribute.Key == "SdkTestsProjectPath").Value;
            Assert.True(File.Exists(projectPath), "The project-reference regression tests require the repository checkout.");
            var configuration = typeof(ProjectReferenceTests).Assembly.GetCustomAttribute<AssemblyConfigurationAttribute>().Configuration;
            var app = _fixture.CreateTestApp("MinimalProject");

            // Exercise the actual repository reference, not only the SDK's published targets.
            // ResolveProjectReferences cannot recursively build or run the test project.
            var result = app.RunProject(_output, projectPath, "/t:ResolveProjectReferences",
                "/p:TargetFramework=" + framework, "/p:Configuration=" + configuration, buildMode);

            Assert.True(result.ExitCode == 0, result.Output);
        }
    }
}
