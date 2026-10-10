// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.IO;
using System.Linq;
using Alarmlist.Analyzers;
using Alarmlist.Compiler;
using Alarmlist.Text;
using Xunit;
#if NET8_0_OR_GREATER
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
#endif

namespace Alarmlist.Core.UnitTests.Configuration
{
    public class AnalyzerAssemblyLoaderTests
    {
        [Fact]
        public void CapturedImageSurvivesCallerMutationAndLoadsWithoutItsOriginalFile()
        {
            byte[] bytes = File.ReadAllBytes(SamplePath);
            string absentPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "Alarmlist.SampleAnalyzer.dll");
            var image = new AnalyzerAssemblyImage(absentPath, bytes);
            Array.Clear(bytes, 0, bytes.Length);
            byte[] returned = image.GetImage();
            Array.Clear(returned, 0, returned.Length);
            Assert.False(File.Exists(absentPath));
            var loader = new AnalyzerAssemblyLoader(new[] { new AnalyzerAssemblyReference(absentPath, new[] { image }) });
            try { Assert.Equal("SAMPLE001", Assert.Single(Compile(loader).Diagnostics).Id); }
            finally { loader.Dispose(); }
            loader.Dispose();
            Assert.Throws<ObjectDisposedException>(() => loader.Analyzers);
            Assert.False(File.Exists(absentPath));
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void MissingOrInvalidImagesProduceMandatoryFailuresAndCanBeDisposed(bool invalid)
        {
            const string path = "Unavailable.dll";
            var images = invalid ? new[] { new AnalyzerAssemblyImage(path, new byte[] { 1, 2, 3 }) } : Array.Empty<AnalyzerAssemblyImage>();
            using (var loader = new AnalyzerAssemblyLoader(new[] { new AnalyzerAssemblyReference(path, images) }))
            {
                CompilationResult result = Compile(loader);
                Assert.False(result.Success);
                Assert.Equal("ALM0025", Assert.Single(result.Diagnostics).Id);
            }
        }

#if NET8_0_OR_GREATER
        [Fact]
        public void UnloadingFailureStillReleasesEveryContextAndDisposedLoaderReference()
        {
            var run = LoadAndFailUnloading();
            for (int attempt = 0; attempt < 10 && run.Contexts.Any(context => context.IsAlive); attempt++)
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
            }
            Assert.All(run.Contexts, context => Assert.False(context.IsAlive));
            Assert.Throws<ObjectDisposedException>(() => run.Loader.Analyzers);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static (WeakReference[] Contexts, AnalyzerAssemblyLoader Loader) LoadAndFailUnloading()
        {
            var image = new AnalyzerAssemblyImage(SamplePath, File.ReadAllBytes(SamplePath));
            var reference = new AnalyzerAssemblyReference(SamplePath, new[] { image });
            var loader = new AnalyzerAssemblyLoader(new[] { reference, reference });
            AssemblyLoadContext[] contexts = loader.Analyzers.Select(analyzer =>
                AssemblyLoadContext.GetLoadContext(analyzer.GetType().Assembly)).ToArray();
            var callbacks = new int[2];
            contexts[0].Unloading += _ => { callbacks[0]++; throw new InvalidOperationException("unloading callback failure"); };
            contexts[1].Unloading += _ => callbacks[1]++;
            try
            {
                InvalidOperationException failure = Assert.Throws<InvalidOperationException>(() => loader.Dispose());
                Assert.Contains("unloading callback failure", failure.Message);
                Assert.Equal(new[] { 1, 1 }, callbacks);
                Assert.DoesNotContain(contexts[0], AssemblyLoadContext.All);
                Assert.DoesNotContain(contexts[1], AssemblyLoadContext.All);
                loader.Dispose();
                Assert.Equal(new[] { 1, 1 }, callbacks);
                return (contexts.Select(context => new WeakReference(context)).ToArray(), loader);
            }
            finally
            {
                // Ensure a failing regression does not leave these test contexts alive.
                foreach (AssemblyLoadContext context in contexts) context.Unload();
            }
        }

        [Fact]
        public void RepeatedLoadsReleaseCollectibleContextsWhileCompilationResultsRemainUsable()
        {
            // Keep the disposed loader and detached result alive. Neither may root the plugin.
            var runs = Enumerable.Range(0, 20).Select(_ => LoadAndRelease()).ToArray();
            for (int attempt = 0; attempt < 10 && runs.Any(run => run.Context.IsAlive); attempt++)
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
            }
            Assert.All(runs, run =>
            {
                Assert.False(run.Context.IsAlive);
                Assert.True(run.Result.Success);
                Assert.Contains("AL-", Assert.Single(run.Result.Diagnostics).Message);
                Assert.Throws<ObjectDisposedException>(() => run.Loader.Analyzers);
            });
            Assert.DoesNotContain(AssemblyLoadContext.Default.Assemblies, assembly => assembly.GetName().Name == "Alarmlist.SampleAnalyzer");
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static (WeakReference Context, CompilationResult Result, AnalyzerAssemblyLoader Loader) LoadAndRelease()
        {
            var image = new AnalyzerAssemblyImage(SamplePath, File.ReadAllBytes(SamplePath));
            var loader = new AnalyzerAssemblyLoader(new[] { new AnalyzerAssemblyReference(SamplePath, new[] { image }) });
            using (loader)
            {
                AssemblyLoadContext context = AssemblyLoadContext.GetLoadContext(Assert.Single(loader.Analyzers).GetType().Assembly);
                Assert.True(context.IsCollectible);
                Assert.NotSame(AssemblyLoadContext.Default, context);
                return (new WeakReference(context), Compile(loader), loader);
            }
        }
#endif

        private static string SamplePath => Path.Combine(AppContext.BaseDirectory, "testassets", "Alarmlist.SampleAnalyzer.dll");
        private static CompilationResult Compile(AnalyzerAssemblyLoader loader)
            => new AlarmCompiler().CompileSnapshots(new[] { new AlmxTextSnapshot(
                "<Alarmlist><Alarm><FullyQualifiedName>A</FullyQualifiedName><Code>001</Code></Alarm></Alarmlist>", "A.almx", 0) },
                CompilationContext.Empty.WithAnalyzers(loader.Analyzers));
    }
}
