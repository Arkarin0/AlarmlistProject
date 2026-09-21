using System.IO;
using System.Linq;
using System.Xml.Linq;
using Xunit;
using Xunit.Abstractions;

namespace Alarmlist.MSBuild.SDK.Tests
{
    [Collection(TestProjectCollection.Name)]
    public class PackageTests
    {
        private readonly TestProjectFixture _fixture;
        private readonly ITestOutputHelper _output;

        public PackageTests(TestProjectFixture fixture, ITestOutputHelper output)
        {
            _fixture = fixture;
            _output = output;
        }

        [Fact]
        public void PackageContainsBothTaskRuntimesAndTheirDependencies()
        {
            var app = _fixture.CreateTestApp("MinimalProject");
            app.Build(_output);
            foreach (var framework in new[] { "net472", "net8.0" })
            {
                var taskDirectory = Path.Combine(app.PackageDirectory, "tasks", framework);
                Assert.True(File.Exists(Path.Combine(taskDirectory, "AlarmList.MSBuild.dll")));
                Assert.True(File.Exists(Path.Combine(taskDirectory, "Alarmlist.Core.dll")));
                Assert.True(File.Exists(Path.Combine(taskDirectory, "Microsoft.Extensions.Logging.Abstractions.dll")));
                Assert.Empty(Directory.GetFiles(taskDirectory, "Microsoft.Build*.dll"));
            }
            Assert.False(Directory.Exists(Path.Combine(app.PackageDirectory, "lib")));
            var nuspec = XDocument.Load(Path.Combine(app.PackageDirectory, "alarmlist.msbuild.sdk.nuspec"));
            Assert.Empty(nuspec.Descendants().Where(element => element.Name.LocalName == "dependency"));
        }
    }
}
