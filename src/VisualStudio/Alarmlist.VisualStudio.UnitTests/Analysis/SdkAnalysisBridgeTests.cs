// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using Alarmlist.VisualStudio.Analysis;
using Microsoft.CSharp;
using Xunit;

namespace Alarmlist.VisualStudio.UnitTests.Analysis
{
    public class SdkAnalysisBridgeTests
    {
        [Fact]
        public void PackagedWorkerRunsWithOnlyTheVsixAssemblyInItsApplicationDirectory()
        {
            using (var sdk = new SdkAsset())
            {
                string hostDirectory = Path.Combine(sdk.DirectoryPath, "host");
                Directory.CreateDirectory(hostDirectory);
                string assemblyPath = Path.Combine(hostDirectory, "Alarmlist.VisualStudio.dll");
                using (var vsix = ZipFile.OpenRead(Path.Combine(AppContext.BaseDirectory, "testassets", "Alarmlist.VisualStudio.vsix")))
                    vsix.GetEntry("Alarmlist.VisualStudio.dll").ExtractToFile(assemblyPath);
                AppDomain domain = AppDomain.CreateDomain("Packaged worker", null, new AppDomainSetup
                {
                    ApplicationBase = hostDirectory,
                    LoaderOptimization = LoaderOptimization.SingleDomain
                });
                try
                {
                    var worker = (SdkAnalysisWorker)domain.CreateInstanceFromAndUnwrap(assemblyPath, typeof(SdkAnalysisWorker).FullName);
                    AnalysisResponse initialized = worker.Initialize(sdk.CompilerPath);
                    Assert.True(initialized.Availability == AnalysisAvailability.Available, initialized.Message);
                    Assert.True(worker.Analyze(new[] { Input("<Name>Packaged</Name>") }).Snapshot.Success);
                    Assert.Single(Directory.GetFiles(hostDirectory));
                    Assert.DoesNotContain(AppDomain.CurrentDomain.GetAssemblies(), assembly => assembly.GetName().Name == "Alarmlist.Core");
                }
                finally { AppDomain.Unload(domain); }
            }
        }

        [Fact]
        public void PackagedCompilerReturnsDetachedOriginsAndClearExplanationsForUnsavedText()
        {
            using (var sdk = new SdkAsset())
            {
                AnalysisSnapshot snapshot;
                using (SdkAnalysisBridge bridge = Open(sdk.CompilerPath))
                {
                    string basePath = Path.Combine(sdk.DirectoryPath, "UnsavedBase.almx");
                    string childPath = Path.Combine(sdk.DirectoryPath, "UnsavedChild.almx");
                    var inputs = new[]
                    {
                        Input("<FullyQualifiedName>Base</FullyQualifiedName><Name>Inherited</Name><Code>B</Code>"
                            + "<TestProcedure><Instructions><Step>Old</Step><Clear/><Hint>Ready</Hint></Instructions>"
                            + "<Reset><Warning>Reset warning</Warning></Reset></TestProcedure>", basePath, 4),
                        Input("<FullyQualifiedName>Child</FullyQualifiedName><ReferenceName>Base</ReferenceName><Name/>"
                            + "<Code>B</Code><TestProcedure><Instructions><Note>Local</Note></Instructions>"
                            + "<Reset><Clear/><Step>Reset locally</Step></Reset></TestProcedure>", childPath, 9)
                    };
                    AnalysisResponse response = bridge.Analyze(inputs);
                    Assert.Equal(AnalysisAvailability.Available, response.Availability);
                    snapshot = response.Snapshot;
                    Assert.True(snapshot.Success);
                    Assert.Equal(1, snapshot.ContractVersion);
                    Assert.Equal(new[] { 4, 9 }, snapshot.Sources.Select(source => source.Version));
                    AnalysisAlarm child = snapshot.Alarms[1];
                    Assert.Equal("Base", Assert.Single(child.ReferenceChain).FullyQualifiedName);
                    AnalysisField name = child.Fields.Single(field => field.Name == "Name");
                    Assert.True(name.IsPresent);
                    Assert.Equal("", name.LocalValue);
                    Assert.Equal("Inherited", name.EffectiveValue);
                    Assert.True(name.IsInherited);
                    Assert.Equal(basePath, name.ValueLocation.FilePath);
                    Assert.Equal(4, name.ValueLocation.SourceVersion);
                    Assert.Equal(childPath, name.LocalLocation.FilePath);
                    Assert.Equal(9, name.LocalLocation.SourceVersion);
                    Assert.False(child.Fields.Single(field => field.Name == "Code").IsInherited);
                    Assert.Equal(new[] { AnalysisStepKind.Hint, AnalysisStepKind.Note }, child.Instructions.EffectiveEntries.Select(entry => entry.Kind.Value));
                    Assert.Equal(new[] { true, false }, child.Instructions.EffectiveEntries.Select(entry => entry.IsInherited));
                    AnalysisEntry excluded = Assert.Single(child.Instructions.ExcludedEntries);
                    Assert.Equal("Old", excluded.Text);
                    Assert.Equal(1, excluded.ExcludedBy.Index);
                    Assert.Equal("Base", excluded.ExcludedBy.Alarm.FullyQualifiedName);
                    Assert.True(child.Reset.LocalEntries[0].IsClear);
                    Assert.Null(child.Reset.LocalEntries[0].Kind);
                    Assert.Equal("Reset locally", Assert.Single(child.Reset.EffectiveEntries).Text);
                    Assert.Equal(AnalysisStepKind.Warning, Assert.Single(child.Reset.ExcludedEntries).Kind);
                    Assert.False(File.Exists(basePath));
                    Assert.False(File.Exists(childPath));
                    Assert.DoesNotContain(AppDomain.CurrentDomain.GetAssemblies(), assembly => assembly.GetName().Name == "Alarmlist.Core");
                }
                Assert.Equal("Inherited", snapshot.Alarms[1].Fields.Single(field => field.Name == "Name").EffectiveValue);
                Assert.Throws<NotSupportedException>(() => ((IList<AnalysisAlarm>)snapshot.Alarms).Clear());
                Assert.Throws<NotSupportedException>(() => ((IList<AnalysisEntry>)snapshot.Alarms[1].Instructions.EffectiveEntries).Clear());
                // Unloading must release the SDK image as well as preserve copied results.
                using (File.Open(sdk.CompilerPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) { }
            }
        }

        [Fact]
        public void SameAssemblyIdentityInTwoSdkLocationsStaysIsolatedAndCanBeReopened()
        {
            using (var firstSdk = new SdkAsset())
            using (var secondSdk = new SdkAsset())
            using (SdkAnalysisBridge first = Open(firstSdk.CompilerPath))
            using (SdkAnalysisBridge second = Open(secondSdk.CompilerPath))
            {
                Assert.Equal(first.CompilerIdentity, second.CompilerIdentity);
                Assert.NotEqual(first.CompilerPath, second.CompilerPath);
                Assert.Equal(firstSdk.CompilerPath, first.CompilerPath);
                Assert.Equal(secondSdk.CompilerPath, second.CompilerPath);
                Assert.True(first.Analyze(new[] { Input("<Name>First</Name>") }).Snapshot.Success);
                first.Dispose();
                first.Dispose();
                Assert.Throws<ObjectDisposedException>(() => first.Analyze(Array.Empty<AnalysisInput>()));
                Assert.True(second.Analyze(new[] { Input("<Name>Second</Name>") }).Snapshot.Success);
                using (SdkAnalysisBridge reopened = Open(firstSdk.CompilerPath))
                    Assert.True(reopened.Analyze(Array.Empty<AnalysisInput>()).Snapshot.Success);
            }
        }

        [Fact]
        public void ReferenceFailuresRemainLocatedCompilerDiagnostics()
        {
            using (var sdk = new SdkAsset())
            using (SdkAnalysisBridge bridge = Open(sdk.CompilerPath))
            {
                AnalysisSnapshot result = bridge.Analyze(new[]
                {
                    Input("<FullyQualifiedName>A</FullyQualifiedName><Name>First</Name>", "First.almx", 1),
                    Input("<FullyQualifiedName>A</FullyQualifiedName><Name>Second</Name>", "Second.almx", 2)
                }).Snapshot;
                Assert.False(result.Success);
                AnalysisDiagnostic diagnostic = Assert.Single(result.Diagnostics);
                Assert.Equal("ALM0001", diagnostic.Id);
                Assert.Equal(AnalysisSeverity.Error, diagnostic.Severity);
                Assert.Contains("A", diagnostic.Message);
                Assert.Equal("Second.almx", diagnostic.Location.FilePath);
                Assert.Equal("First.almx", Assert.Single(diagnostic.RelatedLocations).FilePath);
                Assert.True(diagnostic.Location.EndColumnNumber > diagnostic.Location.ColumnNumber);
                Assert.Equal(diagnostic.Location.LineNumber, diagnostic.Location.EndLineNumber);
                Assert.All(result.Alarms, alarm => Assert.False(alarm.IsResolved));
                Assert.Equal(new[] { 0, 1 }, result.Alarms.Select(alarm => alarm.Origin.Index));
            }
        }

        [Theory]
        [InlineData("<Alarmlist><Alarm>")]
        [InlineData("<!DOCTYPE Alarmlist [<!ENTITY value 'Unsafe'>]><Alarmlist/>")]
        public void InvalidXmlReturnsIncompleteStateAndTheNextAnalysisCanRecover(string text)
        {
            using (var sdk = new SdkAsset())
            using (SdkAnalysisBridge bridge = Open(sdk.CompilerPath))
            {
                AnalysisResponse response = bridge.Analyze(new[] { new AnalysisInput(text, "Broken.almx", 1) });
                Assert.Equal(AnalysisAvailability.InvalidInput, response.Availability);
                Assert.Null(response.Snapshot);
                Assert.NotEmpty(response.Message);
                Assert.True(bridge.Analyze(new[] { Input("<Name>Recovered</Name>") }).Snapshot.Success);
            }
        }

        [Fact]
        public void CancelledRequestsDoNotRunOrDamageTheBridge()
        {
            using (var sdk = new SdkAsset())
            using (SdkAnalysisBridge bridge = Open(sdk.CompilerPath))
            {
                Assert.Throws<OperationCanceledException>(() => bridge.Analyze(Array.Empty<AnalysisInput>(), new CancellationToken(true)));
                Assert.True(bridge.Analyze(Array.Empty<AnalysisInput>()).Snapshot.Success);
            }
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("relative/Alarmlist.Core.dll")]
        [InlineData(@"C:Alarmlist.Core.dll")]
        [InlineData(@"\Alarmlist.Core.dll")]
        public void MissingEvaluationNeverSubstitutesACompiler(string path)
        {
            BridgeOpenResult result = new SdkAnalysisBridgeFactory().Open(path);
            Assert.Equal(AnalysisAvailability.SdkUnavailable, result.Availability);
            Assert.Null(result.Bridge);
            Assert.NotEmpty(result.Message);
        }

        [Fact]
        public void MissingAndInvalidAssembliesGiveActionableLoadStates()
        {
            using (var sdk = new SdkAsset())
            {
                string path = Path.Combine(sdk.DirectoryPath, "missing.dll");
                BridgeOpenResult missing = new SdkAnalysisBridgeFactory().Open(path);
                Assert.Equal(AnalysisAvailability.SdkUnavailable, missing.Availability);
                Assert.Contains(path, missing.Message);
                File.WriteAllText(path, "not an assembly");
                BridgeOpenResult corrupt = new SdkAnalysisBridgeFactory().Open(path);
                Assert.Equal(AnalysisAvailability.LoadFailed, corrupt.Availability);
                Assert.Null(corrupt.Bridge);
                File.Delete(path);
            }
        }

        [Theory]
        [InlineData(-1)]
        [InlineData(99)]
        [InlineData(1)]
        public void IncompatibleSdkVersionsDoNotInterfereWithACompatibleProject(int contract)
        {
            using (var sdk = new SdkAsset())
            using (var incompatible = new SdkAsset())
            using (SdkAnalysisBridge bridge = Open(sdk.CompilerPath))
            {
                incompatible.ReplaceWithIncompatibleCompiler(contract);
                BridgeOpenResult result = new SdkAnalysisBridgeFactory().Open(incompatible.CompilerPath);
                Assert.Equal(AnalysisAvailability.UnsupportedContract, result.Availability);
                Assert.Contains("contract 1", result.Message);
                Assert.Contains(incompatible.CompilerPath, result.Message);
                Assert.Null(result.Bridge);
                Assert.True(bridge.Analyze(Array.Empty<AnalysisInput>()).Snapshot.Success);
                // Failed initialization must also release its image.
                using (File.Open(incompatible.CompilerPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) { }
            }
        }

        private static SdkAnalysisBridge Open(string path)
        {
            BridgeOpenResult result = new SdkAnalysisBridgeFactory().Open(path);
            Assert.True(result.Availability == AnalysisAvailability.Available, result.Message);
            return Assert.IsType<SdkAnalysisBridge>(result.Bridge);
        }

        private static AnalysisInput Input(string contents, string path = "Unsaved.almx", int version = 0)
            => new AnalysisInput("<Alarmlist><Alarm>" + contents + "</Alarm></Alarmlist>", path, version);

        private sealed class SdkAsset : IDisposable
        {
            private static readonly string s_root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "Alarmlist Bridge Tests"));
            public string DirectoryPath { get; } = Path.Combine(s_root, Guid.NewGuid().ToString("N"));
            public string CompilerPath => Path.Combine(DirectoryPath, "tasks", "net472", "Alarmlist.Core.dll");

            public SdkAsset()
            {
                ZipFile.ExtractToDirectory(Path.Combine(AppContext.BaseDirectory, "testassets", "analysis-sdk.nupkg"), DirectoryPath);
            }

            public void ReplaceWithIncompatibleCompiler(int contract)
            {
                // Synthetic old/future/malformed API versions; usable API coverage
                // above always uses the compiler extracted from the real SDK package.
                File.Delete(CompilerPath);
                using (var provider = new CSharpCodeProvider())
                {
                    var options = new CompilerParameters(new[] { "System.dll" }) { OutputAssembly = CompilerPath, GenerateExecutable = false };
                    string source = "[assembly: System.Reflection.AssemblyVersion(\"99.0.0.0\")]"
                        + "[assembly: System.Runtime.Versioning.TargetFramework(\".NETFramework,Version=v4.7.2\")]"
                        + "namespace Alarmlist.Analysis { public class AnalysisResult { "
                        + (contract < 0 ? "" : "public const int CurrentContractVersion = " + contract + ";") + " } }";
                    CompilerResults result = provider.CompileAssemblyFromSource(options, source);
                    Assert.False(result.Errors.HasErrors, string.Join(Environment.NewLine, result.Errors.Cast<CompilerError>()));
                }
            }

            public void Dispose()
            {
                string path = Path.GetFullPath(DirectoryPath);
                if (!path.StartsWith(s_root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Unsafe test cleanup path.");
                if (Directory.Exists(path)) Directory.Delete(path, true);
            }
        }
    }
}
