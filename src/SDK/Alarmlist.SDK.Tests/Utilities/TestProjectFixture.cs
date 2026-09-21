using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Xunit;

namespace Alarmlist.MSBuild.SDK.Tests
{
    [CollectionDefinition(Name)]
    public class TestProjectCollection : ICollectionFixture<TestProjectFixture>
    {
        public const string Name = nameof(TestProjectCollection);
    }

    // Follows Arcade's TestProjectFixture: shared setup, disposable copies of named test assets.
    public sealed class TestProjectFixture : IDisposable
    {
        private readonly List<TestApp> _apps = new List<TestApp>();
        private readonly string _assets = Path.Combine(AppContext.BaseDirectory, "testassets");
        private readonly string _logDirectory;

        public TestProjectFixture()
        {
            _logDirectory = typeof(TestProjectFixture).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
                .Single(attribute => attribute.Key == "LogOutputDir").Value;
            var feed = Path.Combine(_assets, "boilerplate", "feed");
            Assert.True(Directory.Exists(feed) && Directory.GetFiles(feed, "*.nupkg").Length > 0,
                "Build AlarmList.MSBuild.SDK.Tests to prepare its SDK package and test assets.");
        }

        public TestApp CreateTestApp(string name, [CallerMemberName] string testName = "")
        {
            var app = new TestApp(_assets, name, Path.Combine(_logDirectory, testName));
            _apps.Add(app);
            return app;
        }

        public void Dispose()
        {
            foreach (var app in _apps)
                app.Dispose();
        }
    }
}
