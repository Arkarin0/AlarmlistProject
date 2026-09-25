// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.IO;
using BenchmarkDotNet.Running;

namespace Alarmlist.VisualStudio.PerformanceTests
{
    internal static class Program
    {
        public static void Main(string[] args)
        {
            if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("ALARMLIST_BENCHMARK_ASSETS")))
                Environment.SetEnvironmentVariable("ALARMLIST_BENCHMARK_ASSETS", Path.Combine(AppContext.BaseDirectory, "testassets"));
            BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
        }
    }
}
