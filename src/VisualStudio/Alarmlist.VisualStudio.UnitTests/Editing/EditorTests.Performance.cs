// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Controls;
using System.Windows.Interop;
using Alarmlist.VisualStudio.Analysis;
using Xunit;

namespace Alarmlist.VisualStudio.UnitTests.Editing
{
    // Opt-in measurements use the normal package/BAML fixtures, without timing
    // thresholds in CI. Run through eng/measure-visualstudio-analysis.ps1.
    public sealed class AnalysisPerformanceTheoryAttribute : TheoryAttribute
    {
        public AnalysisPerformanceTheoryAttribute()
        {
            if (Environment.GetEnvironmentVariable("ALARMLIST_RUN_PERFORMANCE_TESTS") != "1")
                Skip = "Run eng/measure-visualstudio-analysis.ps1 for opt-in measurements.";
        }
    }

    public partial class EditorTests
    {
        [AnalysisPerformanceTheory]
        [Trait("Category", "Performance")]
        [InlineData(1000, 1000)]
        [InlineData(1000, 100)]
        [InlineData(10000, 100)]
        public void MeasureLargeProjectAnalysis(int alarmCount, int alarmsPerFile)
        {
            string output = Environment.GetEnvironmentVariable("ALARMLIST_PERFORMANCE_OUTPUT_DIRECTORY");
            Assert.False(string.IsNullOrWhiteSpace(output), "The performance launcher must provide a results directory.");
            var rows = new List<string> { "metric,iteration,value,unit" };
            void Record(string metric, int iteration, double value, string unit = "ms")
                => rows.Add(metric + "," + iteration + "," + value.ToString("F3", CultureInfo.InvariantCulture) + "," + unit);
            Record("host_bitness", 0, IntPtr.Size * 8, "bits");
            Record("alarms", 0, alarmCount, "count");
            long baseline = GC.GetTotalMemory(true);
            InheritanceAdornerTests.RunOnSta(() =>
            {
                using (var session = new SemanticSession("<Alarmlist/>"))
                using (var host = new HwndSource(new HwndSourceParameters("ALMX performance")
                { Width = 1000, Height = 900, WindowStyle = unchecked((int)0x80000000) }))
                {
                    AnalysisInput[] inputs = CreatePerformanceInputs(session.Analysis.DocumentPath, alarmCount, alarmsPerFile);
                    session.Buffer.SetText(inputs.Last().Text);
                    session.View.Selected = session.View.Alarms.Last();
                    var project = new AnalysisProject(Path.Combine(Path.GetDirectoryName(session.Analysis.DocumentPath), "Performance.almproj"),
                        "Performance", session.Bridge.CompilerPath, inputs.Select(input => input.FilePath));
                    var captured = inputs.Select(input => new CapturedAnalysisInput(input)).ToArray();
                    session.Analysis.Projects = new[] { project };
                    Record("source_characters", 0, inputs.Sum(input => input.Text.Length), "chars");
                    Record("files", 0, inputs.Length, "count");
                    var watch = Stopwatch.StartNew();
                    AnalysisResponse response = session.Bridge.Analyze(inputs);
                    Record("first_bridge_analysis", 0, watch.Elapsed.TotalMilliseconds);
                    Assert.True(response.Snapshot.Success);
                    Assert.Equal(alarmCount, response.Snapshot.Alarms.Count);
                    Assert.NotNull(response.CompiledXml);
                    Record("xml_characters", 0, response.CompiledXml.Length, "chars");
                    var control = new EditFileControl(session.View) { Width = 950, Height = 850 };
                    host.RootVisual = control;
                    session.Analysis.Set(new ProjectAnalysisState(1, project, ProjectAnalysisStatus.Available, null, response, captured));
                    InheritanceAdornerTests.Pump();
                    foreach (Expander panel in VisualChildren(control).OfType<Expander>().Where(item =>
                        Equals(item.Header, "Resolved alarm preview") || Equals(item.Header, "Compiled project XML preview")).ToArray())
                        panel.IsExpanded = true;
                    InheritanceAdornerTests.Pump();
                    for (int iteration = 1; iteration <= 5; iteration++)
                    {
                        watch.Restart();
                        response = session.Bridge.Analyze(inputs);
                        Record("bridge_analysis", iteration, watch.Elapsed.TotalMilliseconds);
                        var state = new ProjectAnalysisState(iteration + 1, project, ProjectAnalysisStatus.Available, null, response, captured);
                        watch.Restart();
                        session.Analysis.Set(state);
                        InheritanceAdornerTests.Pump();
                        Record("publish_and_layout", iteration, watch.Elapsed.TotalMilliseconds);
                        watch.Restart();
                        session.Analysis.Set(state);
                        InheritanceAdornerTests.Pump();
                        Record("unchanged_refresh_and_layout", iteration, watch.Elapsed.TotalMilliseconds);
                        watch.Restart();
                        for (int i = 0; i < 100; i++) session.View.GoToReferenceCommand.CanExecute(null);
                        Record("command_queries_100", iteration, watch.Elapsed.TotalMilliseconds);
                        watch.Restart();
                        session.Field("Code").Value = "Draft " + iteration;
                        InheritanceAdornerTests.Pump();
                        Record("draft_and_layout", iteration, watch.Elapsed.TotalMilliseconds);
                        Assert.Null(session.View.CompiledXml);
                        session.View.CancelPending();
                        InheritanceAdornerTests.Pump();
                    }
                    Assert.NotEmpty(session.View.ResolvedFields);
                    Assert.Equal(0, session.Buffer.Edits);
                    Record("managed_live", 0, GC.GetTotalMemory(true), "bytes");
                    Record("private_process", 0, Process.GetCurrentProcess().PrivateMemorySize64, "bytes");
                    host.RootVisual = null;
                }
            }, TimeSpan.FromMinutes(5));
            GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
            Record("managed_after_close_delta", 0, GC.GetTotalMemory(true) - baseline, "bytes");
            Directory.CreateDirectory(output);
            File.WriteAllLines(Path.Combine(output, "analysis-" + alarmCount + "-" + alarmsPerFile + ".csv"), rows, new UTF8Encoding(false));
        }

        private static AnalysisInput[] CreatePerformanceInputs(string currentPath, int count, int perFile)
        {
            var inputs = new List<AnalysisInput>();
            for (int first = 0; first < count; first += perFile)
            {
                var text = new StringBuilder("<Alarmlist>\r\n");
                for (int index = first; index < Math.Min(count, first + perFile); index++)
                {
                    text.Append("  <Alarm><FullyQualifiedName>A").Append(index).Append("</FullyQualifiedName>\r\n");
                    if (index % 4 != 0) text.Append("    <ReferenceName>A").Append(index - 1).Append("</ReferenceName>\r\n");
                    else text.Append("    <Name>Base alarm ").Append(index).Append("</Name><Category>Fire</Category><Description>Inspect equipment &amp; record result.</Description>\r\n");
                    text.Append("    <Code>C").Append(index).Append("</Code>\r\n    <TestProcedure><Instructions>");
                    if (index % 4 == 3) text.Append("<Clear/>");
                    text.Append("<Step>Inspect ").Append(index).Append("</Step><Note>Record</Note></Instructions>");
                    if (index % 4 == 0) text.Append("<Reset><Hint>Reset panel</Hint></Reset>");
                    text.Append("</TestProcedure>\r\n  </Alarm>\r\n");
                }
                text.Append("</Alarmlist>");
                string path = first + perFile >= count ? currentPath : Path.Combine(Path.GetDirectoryName(currentPath), "Input" + first + ".almx");
                inputs.Add(new AnalysisInput(text.ToString(), path, 1));
            }
            return inputs.ToArray();
        }
    }
}
