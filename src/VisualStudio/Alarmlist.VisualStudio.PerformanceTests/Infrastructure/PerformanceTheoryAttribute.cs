// Created/modified by Arkarin0 under one ore more license(s).

using System;
using Xunit;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace Alarmlist.VisualStudio.PerformanceTests.Infrastructure
{
    public sealed class PerformanceTheoryAttribute : TheoryAttribute
    {
        public PerformanceTheoryAttribute()
        {
            if (Environment.GetEnvironmentVariable("ALARMLIST_RUN_PERFORMANCE_TESTS") != "1")
                Skip = "Run eng/measure-visualstudio-analysis.ps1 for opt-in measurements.";
        }
    }
}
