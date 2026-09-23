// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Alarmlist.MSBuild.SDK.Tests;
using Xunit;
using Xunit.Abstractions;

namespace Alarmlist.VisualStudio.IntegrationTests
{
    public class ProjectEvaluationTests
    {
        private readonly ITestOutputHelper _output;

        public ProjectEvaluationTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public void ProjectCanBeEvaluatedBeforeNuGetImportsAreAvailable()
        {
            using (var app = CreateApp(nameof(ProjectCanBeEvaluatedBeforeNuGetImportsAreAvailable)))
            {
                var result = app.RunProject(_output, GetMetadata("ExtensionProjectPath"),
                    "/getProperty:TargetFramework", "/p:ExcludeRestorePackageImports=true",
                    "/p:BuildingInsideVisualStudio=true");
                Assert.True(result.ExitCode == 0, result.Output);
                Assert.Contains("net472", result.Output);
            }
        }

        [Fact]
        public void BuildWithoutNuGetImportsReportsRequiredRestore()
        {
            using (var app = CreateApp(nameof(BuildWithoutNuGetImportsReportsRequiredRestore)))
            {
                var result = app.RunProject(_output, GetMetadata("ExtensionProjectPath"),
                    "/t:PrepareForBuild", "/p:ExcludeRestorePackageImports=true");
                Assert.NotEqual(0, result.ExitCode);
                Assert.Contains("Microsoft.VSSDK.BuildTools has not been restored", result.Output);
            }
        }

        [Theory]
        [InlineData("true", "true")]
        [InlineData("false", "false")]
        public void DeploymentDefaultsToVisualStudioBuilds(string insideVisualStudio, string expected)
        {
            using (var app = CreateApp(nameof(DeploymentDefaultsToVisualStudioBuilds)))
            {
                var result = app.RunProject(_output, GetMetadata("ExtensionProjectPath"),
                    "/getProperty:DeployExtension", "/p:BuildingInsideVisualStudio=" + insideVisualStudio);
                Assert.True(result.ExitCode == 0, result.Output);
                Assert.Equal(expected, result.Output.Trim());
            }
        }

        [Theory]
        [InlineData("AlarmlistExp")]
        [InlineData("CustomExp")]
        public void DebugLaunchUsesDeploymentInstance(string suffix)
        {
            using (var app = CreateApp(nameof(DebugLaunchUsesDeploymentInstance)))
            {
                var result = app.RunProject(_output, GetMetadata("ExtensionProjectPath"),
                    "/getProperty:StartArguments", "/p:VSSDKTargetPlatformRegRootSuffix=" + suffix);
                Assert.True(result.ExitCode == 0, result.Output);
                Assert.Equal("/RootSuffix " + suffix, result.Output.Trim());
            }
        }

        private static TestApp CreateApp(string testName)
        {
            return new TestApp(Path.Combine(AppContext.BaseDirectory, "testassets"), "boilerplate",
                Path.Combine(GetMetadata("LogOutputDir"), testName));
        }

        private static string GetMetadata(string name)
        {
            return typeof(ProjectEvaluationTests).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
                .Single(attribute => attribute.Key == name).Value;
        }
    }
}
