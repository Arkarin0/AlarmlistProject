// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Threading;
using Alarmlist.VisualStudio.Analysis;
using Microsoft.CSharp;
using Xunit;

namespace Alarmlist.VisualStudio.UnitTests.Analysis
{
    public partial class SdkAnalysisBridgeTests
    {
        [Fact]
        public void LookupCatalogueNamedTemplatesAndProvenanceSurviveDomainUnload()
        {
            using (var sdk = new SdkAsset())
            {
                var lookup = new AnalysisLookupInput(LookupTestData.Catalogue, Path.Combine(sdk.DirectoryPath, "Hmi.almlookup"), 27);
                var resource = new AnalysisResourceInput(LookupTestData.Resources, "Labels.en.resx", 19);
                var context = new AnalysisContext(resources: new[] { resource }, culture: "en-GB", lookups: new[] { lookup });
                AnalysisResponse response;
                using (SdkAnalysisBridge bridge = Open(sdk.CompilerPath))
                {
                    response = bridge.Analyze(new[] { new AnalysisInput(LookupTestData.Source, "A.almx", 12) }, context: context);
                    Assert.True(response.Snapshot?.Success == true, response.Message);
                    Assert.Contains("In Service -&gt; Vacuum, set ON to 060.", response.CompiledXml);
                }
                AnalysisLookupPath path = Assert.Single(response.Snapshot.LookupPaths);
                Assert.Equal(27, Assert.Single(response.Snapshot.LookupFiles).Version);
                Assert.Equal("Service -> Vacuum", path.Value);
                Assert.Equal("en", path.Segments[0].SupplyingCulture);
                Assert.Equal("ON", path.Members[0].Value);
                Assert.Equal("060", path.Members[0].Metadata.Single(item => item.Name == "Default").Value);
                Assert.Equal(new[] { "path", "property", "value" }, response.Snapshot.Translations.Single(item => item.Key == "Set").PlaceholderNames);
                Assert.Equal(new[] { "path", "property", "value" }, response.Snapshot.ResourceEntries.Single(item => item.Key == "Set").PlaceholderNames);
                Assert.Equal(3, response.Snapshot.Alarms[0].LookupUses.Count);
                Assert.All(response.Snapshot.Alarms[0].LookupUses, use => { Assert.Equal(27, use.DefinitionLocation.SourceVersion); Assert.Equal(12, use.UsageLocation.SourceVersion); });
                Assert.False(File.Exists(lookup.FilePath));
                Assert.DoesNotContain(AppDomain.CurrentDomain.GetAssemblies(), assembly => assembly.GetName().Name == "Alarmlist.Core");
                Assert.Throws<NotSupportedException>(() => ((IList<AnalysisLookupPath>)response.Snapshot.LookupPaths).Clear());
                Assert.Throws<NotSupportedException>(() => ((IList<AnalysisLookupMember>)path.Members).Clear());
            }
        }

        [Fact]
        public void NamedListValuesDescriptionsAndBindingsSurviveDomainUnload()
        {
            using (var sdk = new SdkAsset())
            {
                AnalysisResponse response;
                var context = new AnalysisContext(culture: "en-GB",
                    resources: new[] { new AnalysisResourceInput(ListTestData.Resources, "Labels.en.resx", 19) },
                    lookups: new[] { new AnalysisLookupInput(ListTestData.Catalogue, "Electrical.almlookup", 27) });
                using (SdkAnalysisBridge bridge = Open(sdk.CompilerPath))
                    response = bridge.Analyze(new[] { new AnalysisInput(ListTestData.Source, "A.almx", 12) }, context: context);
                Assert.True(response.Snapshot?.Success == true, response.Message);
                Assert.Contains("Disconnect wire =EC-120W1 from =EC-120K1:12.", response.CompiledXml);
                AnalysisLookupList list = response.Snapshot.LookupLists.Single(item => item.Key == "Wires");
                Assert.Equal("=EC-120W1", list.Entries[0].Value);
                Assert.Equal("Outlet motor cable", list.Entries[0].Description);
                Assert.Equal("en", list.Entries[0].DescriptionTranslation.SupplyingCulture);
                Assert.Equal("023", Assert.Single(list.Entries[0].Metadata).Value);
                Assert.Equal("Spare", list.Entries[1].Description);
                Assert.Null(list.Entries[1].DescriptionTranslation);
                Assert.Equal(27, list.Entries[0].Location.SourceVersion);
                Assert.Equal("Terminals", response.Snapshot.ArgumentBindings.Single(binding => binding.ArgumentName == "connection").ListKey);
                Assert.All(response.Snapshot.ArgumentBindings, binding => Assert.True(binding.IsAvailable));
                Assert.Throws<NotSupportedException>(() => ((IList<AnalysisLookupList>)response.Snapshot.LookupLists).Clear());
                Assert.Throws<NotSupportedException>(() => ((IList<AnalysisLookupListEntry>)list.Entries).Clear());
                Assert.DoesNotContain(AppDomain.CurrentDomain.GetAssemblies(), assembly => assembly.GetName().Name == "Alarmlist.Core");
            }
        }

        [Fact]
        public void CapturedResourcesAndParentOriginsRemainUsableAfterDomainUnload()
        {
            using (var sdk = new SdkAsset())
            {
                string resourcePath = Path.Combine(sdk.DirectoryPath, "Labels.de.resx");
                var resource = new AnalysisResourceInput("<root><data name='Inspection'><value>Inspect $(p)</value><comment>Operator</comment></data></root>", resourcePath, 31);
                var context = new AnalysisContext(properties: new[] { new AnalysisPropertyDefinition("p", "001") }, resources: new[] { resource }, culture: "de-DE");
                AnalysisResponse response;
                using (SdkAnalysisBridge bridge = Open(sdk.CompilerPath))
                {
                    response = bridge.Analyze(new[] { Input("<Name>$(loc:Inspection)</Name>", version: 5) }, context: context);
                    Assert.True(response.Snapshot?.Success == true, response.Message);
                    AnalysisResponse keyword = bridge.Analyze(new[] { Input("<Name>$(loc:Inspection)</Name>") });
                    Assert.Contains("$(loc:Inspection)", keyword.CompiledXml);
                }
                AnalysisSnapshot snapshot = response.Snapshot;
                Assert.Equal(3, snapshot.ContractVersion);
                Assert.Equal("de-DE", snapshot.Culture);
                Assert.Equal("de", Assert.Single(snapshot.Resources).Culture);
                Assert.Equal(31, snapshot.Resources[0].Version);
                AnalysisTranslation translation = Assert.Single(snapshot.Translations);
                Assert.Equal("de", translation.SupplyingCulture);
                Assert.Equal("de-DE", translation.RequestedCulture);
                Assert.Equal("Inspect $(p)", translation.RawValue);
                Assert.Equal("Operator", translation.Comment);
                Assert.Equal(resourcePath, translation.ValueLocation.FilePath);
                Assert.Equal(31, translation.ValueLocation.SourceVersion);
                AnalysisLocalizationUse use = Assert.Single(snapshot.Alarms[0].LocalizationUses);
                Assert.Equal("Inspect 001", use.Value);
                Assert.Equal(5, use.UsageLocation.SourceVersion);
                Assert.Contains("<Name>Inspect 001</Name>", response.CompiledXml);
                Assert.False(File.Exists(resourcePath));
                Assert.DoesNotContain(AppDomain.CurrentDomain.GetAssemblies(), assembly => assembly.GetName().Name == "Alarmlist.Core");
                Assert.Throws<NotSupportedException>(() => ((IList<AnalysisResource>)snapshot.Resources).Clear());
                Assert.Throws<NotSupportedException>(() => ((IList<AnalysisTranslation>)snapshot.Translations).Clear());
            }
        }

        [Fact]
        public void ResourceAndCultureFailuresSuppressPreviewAndRecoverWithoutAlmxEdits()
        {
            using (var sdk = new SdkAsset())
            using (SdkAnalysisBridge bridge = Open(sdk.CompilerPath))
            {
                AnalysisInput[] inputs = { Input("<Name>$(loc:K)</Name>") };
                var english = new AnalysisResourceInput("<root><data name='K'><value>English</value></data></root>", "Labels.en.resx", 1);
                AnalysisResponse missing = bridge.Analyze(inputs, context: new AnalysisContext(resources: new[] { english }, culture: "de"));
                Assert.False(missing.Snapshot.Success);
                Assert.Null(missing.CompiledXml);
                Assert.Contains(missing.Snapshot.Diagnostics, diagnostic => diagnostic.Id == "ALM0016");
                var malformed = new AnalysisResourceInput("<broken>", "Labels.en.resx", 2);
                AnalysisResponse invalid = bridge.Analyze(inputs, context: new AnalysisContext(resources: new[] { malformed }, culture: "en"));
                Assert.False(invalid.Snapshot.Success);
                Assert.Null(invalid.CompiledXml);
                Assert.Contains(invalid.Snapshot.Diagnostics, diagnostic => diagnostic.Id == "ALM0014" && diagnostic.Location.SourceVersion == 2);
                AnalysisResponse recovered = bridge.Analyze(inputs, context: new AnalysisContext(resources: new[] { english }, culture: "en"));
                Assert.True(recovered.Snapshot.Success);
                Assert.Contains("<Name>English</Name>", recovered.CompiledXml);
                Assert.False(missing.Snapshot.Success);
            }
        }

        [Fact]
        public void ExplicitEmptyResourceCultureRemainsDistinctFromFilenameInference()
        {
            using (var sdk = new SdkAsset())
            using (SdkAnalysisBridge bridge = Open(sdk.CompilerPath))
            {
                const string resource = "<root><data name='K'><value>catalogue</value></data></root>";
                AnalysisResponse inferred = bridge.Analyze(Array.Empty<AnalysisInput>(), context: new AnalysisContext(
                    resources: new[] { new AnalysisResourceInput(resource, "Labels.invalid.resx", 1) }));
                Assert.Contains(inferred.Snapshot.Diagnostics, diagnostic => diagnostic.Id == "ALM0013");
                AnalysisResponse explicitEmpty = bridge.Analyze(Array.Empty<AnalysisInput>(), context: new AnalysisContext(
                    resources: new[] { new AnalysisResourceInput(resource, "Labels.invalid.resx", 1, "") }));
                Assert.True(explicitEmpty.Snapshot.Success);
                Assert.Equal("", Assert.Single(explicitEmpty.Snapshot.Resources).Culture);
                Assert.Equal("$(loc:K)", Assert.Single(explicitEmpty.Snapshot.Translations).RawValue);
            }
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void BridgeWorksWhenExtensionIsLoadedOutsideTheHostApplicationBase(bool loadFile)
        {
            using (var sdk = new SdkAsset())
            {
                string directory = Path.Combine(sdk.DirectoryPath, "empty-host");
                Directory.CreateDirectory(directory);
                AppDomain domain = AppDomain.CreateDomain("Extension host", null, new AppDomainSetup { ApplicationBase = directory });
                try
                {
                    var host = (ExtensionHost)domain.CreateInstanceFromAndUnwrap(typeof(ExtensionHost).Assembly.Location, typeof(ExtensionHost).FullName);
                    string failure = host.Analyze(typeof(SdkAnalysisBridge).Assembly.Location, sdk.CompilerPath, loadFile);
                    Assert.True(failure == null, failure);
                }
                finally { AppDomain.Unload(domain); }
            }
        }

        public sealed class ExtensionHost : MarshalByRefObject
        {
            public override object InitializeLifetimeService() => null;

            public string Analyze(string extensionPath, string compilerPath, bool loadFile)
            {
                Assembly assembly = loadFile ? Assembly.LoadFile(extensionPath) : Assembly.LoadFrom(extensionPath);
                Type type = assembly.GetType("Alarmlist.VisualStudio.Analysis.SdkAnalysisBridge", true);
                object result = type.GetMethod("Open", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { compilerPath });
                object bridge = result.GetType().GetProperty("Bridge").GetValue(result);
                if (bridge == null) return (string)result.GetType().GetProperty("Message").GetValue(result);
                try
                {
                    Type inputType = assembly.GetType("Alarmlist.VisualStudio.Analysis.AnalysisInput", true);
                    Array inputs = Array.CreateInstance(inputType, 1);
                    inputs.SetValue(Activator.CreateInstance(inputType, "<Alarmlist><Alarm><Name>Hosted</Name></Alarm></Alarmlist>", "Unsaved.almx", 3), 0);
                    object response = type.GetMethod("Analyze").Invoke(bridge, new object[] { inputs, CancellationToken.None, null });
                    if (!((string)response.GetType().GetProperty("CompiledXml").GetValue(response) ?? "").Contains("<Name>Hosted</Name>"))
                        return "Compiled XML did not cross the remoting boundary.";
                    if (AppDomain.CurrentDomain.GetAssemblies().Any(loaded => loaded.GetName().Name == "Alarmlist.Core"))
                        return "The SDK compiler was loaded into the host domain.";
                    return response.GetType().GetProperty("Availability").GetValue(response).ToString() == "Available"
                        ? null : (string)response.GetType().GetProperty("Message").GetValue(response);
                }
                finally { ((IDisposable)bridge).Dispose(); }
            }
        }

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
        public void CapturedPropertiesAndRawEvaluatedOriginsSurviveDomainUnload()
        {
            using (var sdk = new SdkAsset())
            {
                AnalysisResponse response;
                var definitions = new[] { new AnalysisPropertyDefinition("p", "001"), new AnalysisPropertyDefinition("empty", "") };
                var context = new AnalysisContext(Path.Combine(sdk.DirectoryPath, "Plant.almproj"), "Release", "AnyCPU", definitions);
                definitions[0] = new AnalysisPropertyDefinition("p", "changed");
                using (SdkAnalysisBridge bridge = Open(sdk.CompilerPath))
                {
                    response = bridge.Analyze(new[] { new AnalysisInput("<Alarmlist>"
                        + "<Alarm abstract='true'><FullyQualifiedName>A</FullyQualifiedName><Properties><Property Name='p' Value='ignored'/></Properties>"
                        + "<Code>$(p)</Code><Description>Inspect $(AlarmlistAlarmCode):$(AlarmlistConfiguration)[$(empty)]</Description>"
                        + "<TestProcedure><Instructions><Step>$(AlarmlistAlarmCode) $$(p)</Step></Instructions></TestProcedure></Alarm>"
                        + "<Alarm><ReferenceName>A</ReferenceName></Alarm></Alarmlist>", "Unsaved.almx", 9) }, context: context);
                    Assert.True(response.Snapshot?.Success == true, response.Message);
                }
                AnalysisAlarm child = response.Snapshot.Alarms[1];
                Assert.True(child.HasEvaluatedValues);
                Assert.True(response.Snapshot.Alarms[0].IsAbstract);
                Assert.False(response.Snapshot.Alarms[0].HasEvaluatedValues);
                AnalysisField code = child.Fields.Single(field => field.Name == "Code");
                Assert.Equal("$(p)", code.EffectiveExpression);
                Assert.Equal("001", code.EffectiveValue);
                AnalysisProperty parameter = child.Properties.Single(property => property.Name == "p");
                Assert.Equal("A", parameter.DeclarationOrigin.Alarm.FullyQualifiedName);
                Assert.Equal("Project", parameter.ValueOrigin.Scope);
                Assert.Equal("001", parameter.EffectiveValue);
                Assert.Equal("$(AlarmlistAlarmCode) $$(p)", child.Instructions.EffectiveEntries[0].RawText);
                Assert.Equal("001 $(p)", child.Instructions.EffectiveEntries[0].Text);
                Assert.Contains("Inspect 001:Release[]", response.CompiledXml);
                Assert.DoesNotContain(AppDomain.CurrentDomain.GetAssemblies(), assembly => assembly.GetName().Name == "Alarmlist.Core");
            }
        }

        [Fact]
        public void PackagedCompilerReturnsDetachedOriginsAndClearExplanationsForUnsavedText()
        {
            using (var sdk = new SdkAsset())
            {
                AnalysisSnapshot snapshot;
                string xml;
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
                    xml = response.CompiledXml;
                    Assert.Null(response.XmlPreviewMessage);
                    Assert.Contains("<AlarmList>", xml);
                    Assert.DoesNotContain("<ReferenceName>", xml);
                    Assert.DoesNotContain("<Clear", xml);
                    Assert.True(snapshot.Success);
                    Assert.Equal(3, snapshot.ContractVersion);
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
                Assert.Contains("<Name>Inherited</Name>", xml);
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
                AnalysisResponse response = bridge.Analyze(new[]
                {
                    Input("<FullyQualifiedName>A</FullyQualifiedName><Name>First</Name>", "First.almx", 1),
                    Input("<FullyQualifiedName>A</FullyQualifiedName><Name>Second</Name>", "Second.almx", 2)
                });
                Assert.Null(response.CompiledXml);
                AnalysisSnapshot result = response.Snapshot;
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
                Assert.Null(response.CompiledXml);
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
        [InlineData(2)]
        public void IncompatibleSdkVersionsDoNotInterfereWithACompatibleProject(int contract)
        {
            using (var sdk = new SdkAsset())
            using (var incompatible = new SdkAsset())
            using (SdkAnalysisBridge bridge = Open(sdk.CompilerPath))
            {
                incompatible.ReplaceWithIncompatibleCompiler(contract);
                BridgeOpenResult result = new SdkAnalysisBridgeFactory().Open(incompatible.CompilerPath);
                Assert.Equal(AnalysisAvailability.UnsupportedContract, result.Availability);
                Assert.Contains("contract 3", result.Message);
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

        [Theory]
        [InlineData(false, false)]
        [InlineData(true, false)]
        [InlineData(false, true)]
        public void LegacySdkIsRejectedWithoutLoadingCompilerIntoTheHost(bool method, bool property)
        {
            using (var sdk = new SdkAsset())
            {
                sdk.ReplaceWithLegacyCompiler(method, property);
                BridgeOpenResult response = SdkAnalysisBridge.Open(sdk.CompilerPath);
                Assert.Equal(AnalysisAvailability.UnsupportedContract, response.Availability);
                Assert.Null(response.Bridge);
                Assert.Contains("contract 3", response.Message);
                Assert.DoesNotContain(AppDomain.CurrentDomain.GetAssemblies(), assembly => assembly.GetName().Name == "Alarmlist.Core");
            }
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

            public void ReplaceWithLegacyCompiler(bool method, bool property)
            {
                File.Delete(CompilerPath);
                string source = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Analysis", "LegacyAnalysisCompiler.txt"));
                if (method) source = source.Replace("// OPTIONAL_PREVIEW_METHOD",
                    "public Alarmlist.Analysis.AnalysisResult AnalyzeSnapshots(IEnumerable<Alarmlist.Text.AlmxTextSnapshot> snapshots, bool xml) { throw new System.InvalidOperationException(); }");
                if (property) source = source.Replace("// OPTIONAL_PREVIEW_PROPERTY", "public string CompiledXml { get { throw new System.InvalidOperationException(); } }");
                using (var provider = new CSharpCodeProvider())
                {
                    var options = new CompilerParameters(new[] { "System.dll" }) { OutputAssembly = CompilerPath, GenerateExecutable = false };
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
