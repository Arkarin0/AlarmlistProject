// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.IO;
using System.Threading.Tasks;
using Alarmlist.VisualStudio.ProjectSystem;
using Microsoft.VisualStudio.ProjectSystem.Build;
using Xunit;

namespace Alarmlist.VisualStudio.UnitTests
{
    public class BuildUpToDateCheckTests
    {
        [Fact]
        public async Task EveryBuildActionIsPassedThroughToMSBuildAsync()
        {
            var provider = new AlarmlistBuildUpToDateCheckProvider();
            Assert.True(await provider.IsUpToDateCheckEnabledAsync());

            foreach (BuildAction action in Enum.GetValues(typeof(BuildAction)))
            {
                Assert.False(await provider.IsUpToDateAsync(action, TextWriter.Null));
            }
        }
    }
}
