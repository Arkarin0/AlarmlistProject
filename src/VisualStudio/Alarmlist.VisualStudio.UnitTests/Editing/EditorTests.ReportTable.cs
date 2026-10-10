// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Alarmlist.VisualStudio.UI;
using Xunit;

#pragma warning disable VSTHRD003
namespace Alarmlist.VisualStudio.UnitTests.Editing
{
    public partial class EditorTests
    {
        [Fact]
        public async Task ReportTestMethodUsesEvaluatedSurvivingEntriesAndMultilineClipboardAsync()
        {
            string child = WithProcedure(ChildAlarm, "<Instructions><Step>Discarded local</Step><Clear/>"
                + "<Step>Check $(AlarmlistAlarmCode)&#10;Second line</Step><Hint>Careful &quot;now&quot;</Hint><Warning>Voltage</Warning><Note>Record</Note></Instructions>");
            using (var session = new SemanticSession(child, previewTable: true))
            {
                session.Analyze(ProcedureBase.Replace("<Alarm>", "<Alarm abstract='true'>"));
                AlarmPreviewRow row = Assert.Single(session.View.AlarmTableRows);
                const string expected = "Instructions:\r\n• Check Same\nSecond line\r\nHint: Careful \"now\"\r\nWarning: Voltage\r\nNote: Record\r\n\r\nReset alarm:\r\n• Reset panel";
                Assert.Equal(expected, row.TestMethod);
                Assert.DoesNotContain("Discarded", row.TestMethod); Assert.DoesNotContain("Open", row.TestMethod);
                session.View.AlarmTableFilter = "RESET PANEL"; Assert.Single(session.View.AlarmTableRows);
                session.View.SortAlarmTable("TestMethod"); Assert.Same(row, Assert.Single(session.View.AlarmTableRows));
                string copied = null;
                session.View.CopyPreviewRows(new[] { row }, text => copied = text);
                await session.View.PendingAnalysisAction;
                Assert.Contains("\t\"" + expected.Replace("\"", "\"\"") + "\"\tChild\t", copied);
                Assert.Equal(child, session.Buffer.Current.Text); Assert.Equal(0, session.Buffer.Edits);
                session.View.AlarmTableFilter = ""; session.Analyze(BaseAlarm);
                Assert.DoesNotContain("Reset alarm:", Assert.Single(session.View.AlarmTableRows.Where(item => item.Identity == "Child")).TestMethod);
            }
        }

        [Fact]
        public void ReportRowsWrapGrowAndRemainVirtualizedAcrossOneThousandAlarms()
        {
            InheritanceAdornerTests.RunOnSta(() =>
            {
                string longText = string.Join(" ", Enumerable.Repeat("Inspect the safety door and reset the alarm.", 10));
                string source = "<Alarmlist>" + string.Concat(Enumerable.Range(0, 1200).Select(index => "<Alarm><FullyQualifiedName>A" + index.ToString("D4")
                    + "</FullyQualifiedName><Name>Alarm " + index.ToString("D4") + "</Name><Description>" + longText
                    + "</Description><TestProcedure><Instructions><Step>Inspect door</Step><Hint>Use caution</Hint></Instructions>"
                    + "<Reset><Step>Close door</Step><Step>Press reset</Step></Reset></TestProcedure></Alarm>")) + "</Alarmlist>";
                using (var session = new SemanticSession(source, previewTable: true))
                using (var host = ExpressionHost())
                {
                    var control = new PreviewControl { DataContext = session.View, Width = 1400, Height = 850 };
                    foreach (string key in new[] { "ToolWindowTextBrushKey", "ComboBoxTextBrushKey", "ToolWindowBorderBrushKey" }) SetThemeBrush(control, key, Brushes.WhiteSmoke);
                    foreach (string key in new[] { "ToolWindowBackgroundBrushKey", "ComboBoxBackgroundBrushKey" }) SetThemeBrush(control, key, Brushes.Black);
                    ((TabControl)control.FindName("PreviewTabs")).SelectedIndex = 0;
                    host.RootVisual = control; InheritanceAdornerTests.Pump();
                    var table = (DataGrid)control.FindName("AlarmTable");
                    Assert.True(double.IsNaN(table.RowHeight)); Assert.Equal(DataGridGridLinesVisibility.All, table.GridLinesVisibility);
                    Assert.True(table.EnableRowVirtualization); Assert.Equal(VirtualizationMode.Recycling, VirtualizingPanel.GetVirtualizationMode(table));
                    Assert.Equal(ScrollUnit.Pixel, VirtualizingPanel.GetScrollUnit(table));
                    var first = (DataGridRow)table.ItemContainerGenerator.ContainerFromIndex(0);
                    Assert.True(first.ActualHeight > 100);
                    int count = VisualChildren(table).OfType<DataGridRow>().Count(); Assert.InRange(count, 1, 30);
                    Assert.All(VisualChildren(first).OfType<TextBlock>().Where(text => text.Text == longText), text => Assert.Equal(TextWrapping.Wrap, text.TextWrapping));
                    double height = first.ActualHeight;
                    table.Columns.Single(column => column.SortMemberPath == "Description").Width = 150;
                    table.UpdateLayout(); Assert.True(first.ActualHeight > height);
                    table.Columns.Single(column => column.SortMemberPath == "Description").Width = 300;
                    table.UpdateLayout(); RenderPreview(control, "report-table-1200", 1);
                    AlarmPreviewRow last = session.View.AlarmTableRows.Last();
                    table.ScrollIntoView(last); InheritanceAdornerTests.Pump();
                    Assert.NotNull(table.ItemContainerGenerator.ContainerFromItem(last));
                    Assert.InRange(VisualChildren(table).OfType<DataGridRow>().Count(), 1, 30);
                    Assert.Equal(0, session.Buffer.Edits);
                }
            }, TimeSpan.FromSeconds(30));
        }
    }
}
