using System;
using System.IO;
using Xunit;
using Xunit.Abstractions;

namespace Alarmlist.MSBuild.SDK.Tests
{
    [Collection(TestProjectCollection.Name)]
    public class TargetFrameworkTests
    {
        private readonly TestProjectFixture _fixture;
        private readonly ITestOutputHelper _output;

        public TargetFrameworkTests(TestProjectFixture fixture, ITestOutputHelper output)
        {
            _fixture = fixture;
            _output = output;
        }

        [Theory]
        [InlineData("")]
        [InlineData("Alarmlist")]
        [InlineData("net472")]
        [InlineData("net8.0")]
        public void TaskRuntimeDependsOnMSBuildHostRatherThanConsumerFramework(string framework)
        {
            var app = _fixture.CreateTestApp("ConsumerFramework");
            app.Build(_output, "/p:TargetFramework=" + framework);
            var expected = string.IsNullOrEmpty(Environment.GetEnvironmentVariable("ALARMLIST_TEST_MSBUILD_PATH")) ? "net8.0" : "net472";
            Assert.Equal(expected, File.ReadAllText(Path.Combine(app.WorkingDirectory, "runtime.txt")).Trim());
            Assert.True(File.Exists(app.OutputPath));
        }
    }
}
