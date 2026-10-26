// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Specialized;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using Alarmlist.VisualStudio.Analysis;
using Alarmlist.VisualStudio.UI;
using Xunit;

#pragma warning disable VSTHRD003
namespace Alarmlist.VisualStudio.UnitTests.Editing
{
    public partial class EditorTests
    {
        [Fact]
        public void AlarmListSearchAndCompletionUseAllOrdinaryFields()
        {
            using (var session = new SemanticSession())
            {
                session.Analyze(BaseAlarm.Replace("</Code>", "</Code><Category>Inherited category needle</Category>"));
                foreach (var field in Alarmlist.VisualStudio.Definition.AlarmDefinition.Fields)
                    Assert.Contains(session.View.CompletionProperties, property => property.Name == "AlarmlistAlarm" + field.Name);
                session.View.Filter = "CATEGORY NEEDLE";
                Assert.Contains(session.View.Alarms, alarm => alarm.Identity == "Child");
                session.View.Filter = "No matching field";
                Assert.Empty(session.View.Alarms);
                Assert.Equal(0, session.Buffer.Edits);
            }
        }

        [Fact]
        public void AlarmTableRowsAreMaterializedOnlyForTheVisiblePreviewContext()
        {
            using (var session = new SemanticSession())
            {
                Assert.Empty(session.View.AlarmTableRows);
                Assert.NotEmpty(session.View.ResolvedFields);
                session.View.SetPreviewTableActive(true);
                Assert.Equal(2, session.View.AlarmTableRows.Count);
                Assert.Equal("Child", session.View.SelectedPreviewRow.Identity);
                session.View.SetPreviewTableActive(false);
                Assert.Empty(session.View.AlarmTableRows);
                session.Analyze();
                Assert.Empty(session.View.AlarmTableRows);
                session.View.SetPreviewTableActive(true);
                Assert.Equal(2, session.View.AlarmTableRows.Count);
                Assert.Equal("Child", session.View.SelectedPreviewRow.Identity);
            }
        }

        [Fact]
        public void AlarmTableUsesResolvedProjectValuesExcludesAbstractTemplatesAndKeepsSortAcrossRefresh()
        {
            using (var session = new SemanticSession(ChildAlarm.Replace("</Alarmlist>",
                "<Alarm><FullyQualifiedName>Z</FullyQualifiedName><Name>Zulu</Name><Code>001</Code></Alarm></Alarmlist>"), previewTable: true))
            {
                string source = BaseAlarm.Replace("<Alarm>", "<Alarm abstract='true'>")
                    .Replace("</Code>", "</Code><Category>Fire</Category><Description>Inspect $(AlarmlistAlarmCode)</Description>");
                session.Analyze(source);
                Assert.Equal(new[] { "Child", "Z" }, session.View.AlarmTableRows.Select(row => row.Identity));
                Assert.Equal("Inspect Same", session.View.AlarmTableRows[0].Column("Description"));
                Assert.Equal("Fire", session.View.AlarmTableRows[0].Column("Category"));
                Assert.StartsWith("Child.almx:", session.View.AlarmTableRows[0].Source);
                Assert.IsAssignableFrom<INotifyCollectionChanged>(session.View.AlarmTableRows);
                session.View.SortAlarmTable("Code");
                Assert.Equal(new[] { "Z", "Child" }, session.View.AlarmTableRows.Select(row => row.Identity));
                var rows = session.View.AlarmTableRows;
                session.Analysis.Set(session.Analysis.Current);
                Assert.Same(rows, session.View.AlarmTableRows);
                session.Analyze(source);
                Assert.Equal(new[] { "Z", "Child" }, session.View.AlarmTableRows.Select(row => row.Identity));
                session.View.SortAlarmTable("Code");
                Assert.Equal("Child", session.View.AlarmTableRows[0].Identity);
                foreach (string filter in new[] { "FIRE", "Inspect Same", "child.almx", "Child" })
                {
                    session.View.AlarmTableFilter = filter;
                    Assert.Contains(session.View.AlarmTableRows, row => row.Identity == "Child");
                }
                session.View.AlarmTableFilter = "No match";
                Assert.Empty(session.View.AlarmTableRows);
                Assert.Equal("0 of 2 concrete alarms", session.View.AlarmTableCount);
                session.View.Selected = session.View.Alarms.Last();
                Assert.Null(session.View.SelectedPreviewRow);
                Assert.Equal(0, session.Buffer.Edits);
            }
        }

        [Fact]
        public async Task TableSelectionInspectsAnotherFileAndNavigatesWithoutChangingDesignerOrSourceAsync()
        {
            using (var session = new SemanticSession(previewTable: true))
            {
                session.View.Selected = null;
                AlarmPreviewRow row = session.View.AlarmTableRows.Single(item => item.Identity == "Base");
                session.View.SelectedPreviewRow = row;
                Assert.Equal("Base", session.View.ResolvedFields[0].Value);
                Assert.Null(session.View.Selected);
                row.GoToSourceCommand.Execute(null);
                await session.View.PendingAnalysisAction;
                Assert.Same(row.Alarm.Origin.Location, session.Analysis.Navigation);
                Assert.Contains("<FullyQualifiedName>Base</FullyQualifiedName>", session.Analysis.NavigationText);
                Assert.Equal(0, session.Buffer.Edits);
                session.View.Selected = session.View.Alarms[0];
                Assert.Equal("Child", session.View.ResolvedFields[0].Value);
            }
        }

        [Fact]
        public async Task AlarmTableCopyUsesDisplayedCultureLiteralTextAndTabularQuotingAsync()
        {
            using (var session = new SemanticSession(LocalizedSource, previewTable: true))
            {
                AnalyzeLocalization(session);
                Assert.Equal("Inspect 001", Assert.Single(session.View.AlarmTableRows).Name);
                string copied = null;
                session.View.CopyPreviewRows(session.View.AlarmTableRows, text => copied = text);
                await session.View.PendingAnalysisAction;
                Assert.StartsWith("Name\tCode\tCategory\tDescription\tTest method\tIdentity\tSource\r\nInspect 001\t", copied);
                AnalyzeLocalization(session, "keyword");
                Assert.Equal("$(loc:Alarm.Inspect-now)", Assert.Single(session.View.AlarmTableRows).Name);
                AnalyzeLocalization(session, saved: "<root><data name='Alarm.Inspect-now'><value>Line&#10;Tab&#9;&quot;Quoted&quot;</value></data></root>");
                session.View.CopyPreviewRows(session.View.AlarmTableRows, text => copied = text);
                await session.View.PendingAnalysisAction;
                Assert.Contains("\r\n\"Line\nTab\t\"\"Quoted\"\"\"\t", copied);
                Assert.Equal(0, session.Buffer.Edits);
            }
        }

        [Theory]
        [InlineData("inputs")]
        [InlineData("context")]
        [InlineData("draft")]
        [InlineData("selection")]
        [InlineData("filter")]
        [InlineData("hide")]
        [InlineData("dispose")]
        public async Task AlarmTableCopyRejectsStaleInputsAndLateActionsAsync(string change)
        {
            using (var session = new SemanticSession(previewTable: true))
            {
                var validation = new TaskCompletionSource<bool>();
                session.Analysis.Validation = validation.Task;
                string copied = null;
                session.View.CopyPreviewRows(session.View.AlarmTableRows, text => copied = text);
                if (change == "context") session.Analyze();
                if (change == "draft") session.Field("Code").Value = "Private";
                if (change == "selection") session.View.SelectedPreviewRow = session.View.AlarmTableRows.First(row => row != session.View.SelectedPreviewRow);
                if (change == "filter") session.View.AlarmTableFilter = "Other";
                if (change == "hide") session.View.InvalidatePreviewActions();
                if (change == "dispose") session.View.Dispose();
                validation.SetResult(change != "inputs");
                await session.View.PendingAnalysisAction;
                Assert.Null(copied);
                Assert.Equal(0, session.Buffer.Edits);
            }
        }

        [Fact]
        public void AlarmTableMarksErrorsAndClearsImmediatelyForDraftsPendingAnalysisAndClose()
        {
            using (var session = new SemanticSession(previewTable: true))
            {
                session.Analyze(BaseAlarm.Replace("</Alarmlist>", "<Alarm><FullyQualifiedName>Broken</FullyQualifiedName><ReferenceName>Missing</ReferenceName></Alarm></Alarmlist>"));
                Assert.Equal("(unavailable)", session.View.AlarmTableRows.Single(row => row.Identity == "Broken").Name);
                Assert.Equal("Inherited", session.View.AlarmTableRows.Single(row => row.Identity == "Child").Name);
                Assert.Null(session.View.CompiledXml);
                session.Field("Code").Value = "Private";
                Assert.Empty(session.View.AlarmTableRows);
                Assert.False(session.View.CanUseProjectPreview);
                session.View.CancelPending();
                Assert.NotEmpty(session.View.AlarmTableRows);
                session.Analysis.Set(new ProjectAnalysisState(10, session.Analysis.Current.Project, ProjectAnalysisStatus.Pending, "Waiting"));
                Assert.Empty(session.View.AlarmTableRows);
                session.Analyze();
                session.View.Dispose();
                Assert.Empty(session.View.AlarmTableRows);
            }
        }

        [Theory]
        [InlineData("Light", 950, 1.0)]
        [InlineData("Dark", 650, 1.5)]
        [InlineData("HighContrast", 440, 2.0)]
        public void AlarmTableBamlIsReadOnlyAccessibleAndKeepsRowSelectionOnUnchangedAnalysis(string palette, int width, double scale)
        {
            InheritanceAdornerTests.RunOnSta(() =>
            {
                using (var session = new SemanticSession(previewTable: true))
                using (var host = ExpressionHost())
                {
                    var control = new PreviewControl { DataContext = session.View, Width = width, Height = 850 };
                    Brush text = palette == "Light" ? Brushes.Black : palette == "Dark" ? Brushes.WhiteSmoke : Brushes.Yellow;
                    Brush background = palette == "Light" ? Brushes.White : Brushes.Black;
                    foreach (string key in new[] { "ToolWindowTextBrushKey", "ComboBoxTextBrushKey", "ToolWindowBorderBrushKey" }) SetThemeBrush(control, key, text);
                    foreach (string key in new[] { "ToolWindowBackgroundBrushKey", "ComboBoxBackgroundBrushKey" }) SetThemeBrush(control, key, background);
                    ((TabControl)control.FindName("PreviewTabs")).SelectedIndex = 0;
                    host.RootVisual = control;
                    InheritanceAdornerTests.Pump();
                    var table = (DataGrid)control.FindName("AlarmTable");
                    Assert.True(table.IsReadOnly);
                    Assert.Equal(7, table.Columns.Count);
                    Assert.Equal("Resolved alarm table", AutomationProperties.GetName(table));
                    session.View.SortAlarmTable("Code");
                    session.View.AlarmTableFilter = "Child";
                    InheritanceAdornerTests.Pump();
                    Assert.Equal(System.ComponentModel.ListSortDirection.Ascending, table.Columns[1].SortDirection);
                    session.View.AlarmTableFilter = "";
                    InheritanceAdornerTests.Pump();
                    table.SelectedItem = session.View.AlarmTableRows[0];
                    table.UpdateLayout();
                    session.Analysis.Set(session.Analysis.Current);
                    Assert.Same(session.View.AlarmTableRows[0], table.SelectedItem);
                    var row = (DataGridRow)table.ItemContainerGenerator.ContainerFromItem(table.SelectedItem);
                    Assert.Contains("Inherited", AutomationProperties.GetName(row));
                    Assert.Equal("Resolved", AutomationProperties.GetHelpText(row));
                    Assert.All(VisualChildren(table).OfType<System.Windows.Controls.Primitives.DataGridColumnHeader>(), header => Assert.Same(text, header.Foreground));
                    RenderPreview(control, palette + "-alarm-table", scale);
                    session.Field("Code").Value = "Private";
                    InheritanceAdornerTests.Pump();
                    Assert.Empty(table.Items);
                    Assert.Equal(0, session.Buffer.Edits);
                }
            });
        }

        [Fact]
        public void DetachedPreviewDoesNotRetainTableRowsOrAnalysisThroughWpf()
        {
            InheritanceAdornerTests.RunOnSta(() =>
            {
                var control = new PreviewControl { Width = 950, Height = 850 };
                WeakReference[] retired = CloseTablePreview(control);
                for (int pass = 0; pass < 3; pass++)
                {
                    InheritanceAdornerTests.Pump(); GC.Collect(); GC.WaitForPendingFinalizers();
                }
                Assert.All(retired, item => Assert.False(item.IsAlive));
                GC.KeepAlive(control);
            });
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static WeakReference[] CloseTablePreview(PreviewControl control)
        {
            using (var session = new SemanticSession(previewTable: true))
            {
                control.DataContext = session.View;
                ((TabControl)control.FindName("PreviewTabs")).SelectedIndex = 0;
                control.Measure(new Size(950, 850)); control.Arrange(new Rect(0, 0, 950, 850));
                InheritanceAdornerTests.Pump();
                ((DataGrid)control.FindName("AlarmTable")).SelectedItem = session.View.AlarmTableRows[0];
                var retired = new[] { new WeakReference(session.View), new WeakReference(session.View.AlarmTableRows[0]), new WeakReference(session.Analysis.Current) };
                control.DataContext = null;
                InheritanceAdornerTests.Pump();
                return retired;
            }
        }
    }
}
