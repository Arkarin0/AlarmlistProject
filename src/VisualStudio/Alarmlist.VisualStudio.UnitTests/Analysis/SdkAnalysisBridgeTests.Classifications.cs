// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Alarmlist.ConfigurationInput;
using Alarmlist.VisualStudio.Analysis;
using Xunit;

namespace Alarmlist.VisualStudio.UnitTests.Analysis
{
    public partial class SdkAnalysisBridgeTests
    {
        [Fact]
        public void ClassificationsRemainDetachedAndDoNotLoadCapturedAnalyzers()
        {
            using (var sdk = new SdkAsset())
            {
                var source = Input("<Name>$(loc:K, X=$(P), Literal=&quot;$(Quoted)&quot;)</Name>", "A.almx", 8);
                var analyzer = new CapturedAnalyzerReference("Broken.dll", new[] { new CapturedAnalyzerImage("Broken.dll", new byte[] { 1, 2, 3 }) });
                var context = new AnalysisContext(projectPath: "Plant.almproj", configuration: "Debug", platform: "AnyCPU", culture: "en", analyzers: new[] { analyzer });
                var target = new AnalysisClassificationTarget("xml-view", "Almx", source);
                var request = new AnalysisClassificationRequest(new AnalysisReferenceRequest(new[] { source }, context), target);
                ClassificationResponse response;
                using (var bridge = Open(sdk.CompilerPath))
                {
                    response = bridge.Classify(request);
                    using (var cancellation = new CancellationTokenSource())
                    {
                        cancellation.Cancel(); Assert.Throws<OperationCanceledException>(() => bridge.Classify(request, cancellation.Token));
                    }
                    Assert.True(bridge.Classify(request).Result.IsAvailable);
                }
                Assert.Equal(AnalysisAvailability.Available, response.Availability);
                var result = response.Result; Assert.True(result.IsAvailable, response.Message); Assert.True(result.IsLexicalOnly);
                Assert.Equal(10, result.ContractVersion); Assert.Equal("xml-view", result.Target.Id); Assert.Equal(8, result.Target.Text.Version);
                Assert.Equal("Plant.almproj", result.ProjectPath); Assert.Equal("en", result.Culture);
                Assert.Equal(new[] { "P" }, result.Spans.Where(span => span.Kind == "Parameter").Select(span => source.Text.Substring(span.Start, span.Length)));
                Assert.Contains(result.Spans, span => span.Kind == "StringLiteral" && source.Text.Substring(span.Start, span.Length) == "&quot;$(Quoted)&quot;");
                Assert.All(result.Spans, span => Assert.Equal(8, span.ValueLocation.SourceVersion));
                Assert.Throws<NotSupportedException>(() => ((IList<AnalysisClassificationSpan>)result.Spans).Clear());
                Assert.Throws<NotSupportedException>(() => ((IList<AnalysisClassificationRange>)result.Spans[0].SourceRanges).Clear());
                Assert.DoesNotContain(AppDomain.CurrentDomain.GetAssemblies(), assembly => assembly.GetName().Name == "Alarmlist.Core");
            }
        }

        [Fact]
        public void PrivateValueAndResourceClassificationsDoNotReplaceSavedCompilerInputs()
        {
            using (var sdk = new SdkAsset())
            using (var bridge = Open(sdk.CompilerPath))
            {
                var source = Input("<Name>$(loc:K, X=$(P))</Name>", "A.almx", 8);
                const string saved = "<root><data name='K'><value>{X} $(P)</value></data></root>";
                var context = new AnalysisContext(properties: new[] { new AnalysisPropertyDefinition("P", "saved") },
                    resources: new[] { new AnalysisResourceInput(saved, "Labels.en.resx", 3) }, culture: "en");
                var ordinary = bridge.Analyze(new[] { source }, context: context);
                Assert.True(ordinary.Snapshot.Success, ordinary.Message); Assert.Contains("saved saved", ordinary.CompiledXml);
                var inputs = new AnalysisReferenceRequest(new[] { source }, context);
                var value = new AnalysisClassificationTarget("private", "Value", new AnalysisInput("$(loc:K, X=$(Pending", "value", 42),
                    new AnalysisExplanationTarget(ordinary.Snapshot.Alarms[0].Origin.Location, "Field", "Name"), isDraft: true);
                var draft = bridge.Classify(new AnalysisClassificationRequest(inputs, value));
                Assert.True(draft.Result.IsAvailable, draft.Message); Assert.NotEmpty(draft.Result.Diagnostics);
                Assert.All(draft.Result.Spans, span => Assert.Empty(span.SourceRanges));
                var resourceText = saved.Replace("$(P)", "$(Next)");
                var resource = new AnalysisClassificationTarget("resource", "Resx", new AnalysisInput(resourceText, "Labels.en.resx", 12), isDraft: true);
                var result = bridge.Classify(new AnalysisClassificationRequest(inputs, resource)).Result;
                Assert.True(result.IsAvailable, result.Message);
                Assert.Contains(result.Spans, span => span.Kind == "ArgumentName" && resourceText.Substring(span.Start, span.Length) == "X");
                Assert.Contains(result.Spans, span => span.Kind == "Parameter" && resourceText.Substring(span.Start, span.Length) == "Next");
                Assert.Equal(saved, context.Resources[0].Text); Assert.Equal(3, context.Resources[0].Version);
                Assert.Equal(ordinary.CompiledXml, bridge.Analyze(new[] { source }, context: context).CompiledXml);
            }
        }
    }
}
