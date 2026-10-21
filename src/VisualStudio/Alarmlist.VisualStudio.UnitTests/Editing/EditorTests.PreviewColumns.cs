// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using Alarmlist.VisualStudio.Analysis;
using Alarmlist.VisualStudio.UI;
using Xunit;

#pragma warning disable VSTHRD003
namespace Alarmlist.VisualStudio.UnitTests.Editing
{
    public partial class EditorTests
    {
        [Fact]
        public void ProjectSettingsDoNotRetainDisposedPreviewColumnModels()
        {
            using (var store = new ProjectUserSetting(token => Task.FromResult(""), (value, token) => Task.CompletedTask))
            {
                WeakReference retired = RetirePreviewColumns(store);
                for (int index = 0; index < 3; index++) { GC.Collect(); GC.WaitForPendingFinalizers(); }
                Assert.False(retired.IsAlive); GC.KeepAlive(store);
            }
        }
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static WeakReference RetirePreviewColumns(ProjectUserSetting store)
        {
            var layout = new PreviewColumnLayout(); layout.SetStore(store);
            var reference = new WeakReference(layout); layout.Dispose(); return reference;
        }
        [Fact]
        public void PreviewColumnLayoutsRoundTripValidateAndKeepUnknownColumnsForwardCompatible()
        {
            using (var layout = new PreviewColumnLayout())
            using (var restored = new PreviewColumnLayout())
            {
                layout.Columns[1].IsVisible = false; layout.Columns[4].Width = 510; layout.Columns[4].MoveLeftCommand.Execute(null);
                restored.Apply(layout.Serialize()); Assert.Equal(layout.Serialize(), restored.Serialize());
                restored.Apply("1;Future,1,100;TestMethod,0,450;Name,1,180");
                Assert.Equal("TestMethod", restored.OrderedColumns.First().Id); Assert.False(restored.Columns[4].IsVisible);
                Assert.Equal(7, restored.OrderedColumns.Select(column => column.Order).Distinct().Count());
                string defaults = new PreviewColumnLayout().Serialize();
                foreach (string invalid in new[] { "2;Name,1,400", "1;Name,1,NaN", "1;Name,1,Infinity", "1;Name,1,0", "1;Name,1,2010", "1;Name,1,200;Name,0,300", "1;Name,maybe,200" })
                { restored.Apply(invalid); Assert.Equal(defaults, restored.Serialize()); }
                foreach (PreviewColumn column in layout.Columns) column.IsVisible = false;
                restored.Apply(layout.Serialize()); Assert.All(restored.Columns, column => Assert.False(column.IsVisible));
                restored.ResetCommand.Execute(null); Assert.Equal(defaults, restored.Serialize());
            }
        }

        [Fact]
        public async Task PreviewColumnSettingsShareProjectChoicesPreserveLateEditsAndIsolateProjectsAsync()
        {
            var read = new TaskCompletionSource<string>();
            var writes = new List<string>();
            using (var store = new ProjectUserSetting(token => read.Task, (value, token) => { writes.Add(value); return Task.CompletedTask; }, 1))
            using (var other = new ProjectUserSetting(token => Task.FromResult(""), (value, token) => Task.CompletedTask, 1))
            using (var first = new PreviewColumnLayout())
            using (var second = new PreviewColumnLayout())
            {
                first.SetStore(store); second.SetStore(store);
                first.Columns[0].IsVisible = false; first.Columns[4].Width = 600;
                Assert.False(second.Columns[0].IsVisible); Assert.Equal(600, second.Columns[4].Width);
                read.SetResult("1;Name,1,999"); await first.PendingLoad; await store.PendingSave;
                Assert.False(first.Columns[0].IsVisible); Assert.Equal(first.Serialize(), writes.Last());
                second.SetStore(other); await second.PendingLoad; Assert.True(second.Columns[0].IsVisible);
                first.Columns[1].IsVisible = false; Assert.True(second.Columns[1].IsVisible);
                second.SetStore(store); Assert.False(second.Columns[1].IsVisible);
                second.Dispose(); first.Columns[2].IsVisible = false; Assert.True(second.Columns[2].IsVisible);
                await store.PendingSave;
            }
        }

        [Fact]
        public async Task PreviewColumnSettingsSerializeConcurrentSavesAndKeepFailedLayoutUsableAsync()
        {
            var started = new TaskCompletionSource<bool>(); var release = new TaskCompletionSource<bool>();
            var writes = new List<string>();
            using (var store = new ProjectUserSetting(token => Task.FromResult(""), async (value, token) =>
            {
                writes.Add(value);
                if (writes.Count == 1) { started.SetResult(true); await release.Task; }
                else throw new IOException("Read-only user file");
            }, 1))
            {
                await store.LoadAsync(); store.Set("first"); await started.Task;
                store.Set("second"); store.Set("latest"); release.SetResult(true); await store.PendingSave;
                Assert.Equal(new[] { "first", "latest" }, writes); Assert.Equal("latest", store.Value);
                Assert.Contains("Read-only user file", store.Error); Assert.Contains("session", store.Error);
            }
        }

        [Fact]
        public async Task PreviewCopyUsesVisibleColumnOrderAndLayoutChangesRejectLateCopyAsync()
        {
            using (var session = new SemanticSession(previewTable: true))
            {
                PreviewColumnLayout layout = session.View.PreviewColumns;
                layout.Columns[0].IsVisible = false;
                layout.Columns[1].MoveLeftCommand.Execute(null);
                string copied = null;
                session.View.CopyPreviewRows(session.View.AlarmTableRows, text => copied = text); await session.View.PendingAnalysisAction;
                Assert.StartsWith("Code\tCategory\tDescription\tTest method\tIdentity\tSource\r\n", copied);
                var validation = new TaskCompletionSource<bool>(); session.Analysis.Validation = validation.Task; copied = null;
                session.View.CopyPreviewRows(session.View.AlarmTableRows, text => copied = text);
                layout.Columns[2].IsVisible = false; validation.SetResult(true); await session.View.PendingAnalysisAction;
                Assert.Null(copied); Assert.Equal(0, session.Buffer.Edits);
            }
        }

        [Fact]
        public void PreviewColumnChooserRemainsUsableWithNoColumnsAndReordersWithoutLosingFocus()
        {
            InheritanceAdornerTests.RunOnSta(() =>
            {
                using (var session = new SemanticSession(previewTable: true))
                using (var host = ExpressionHost())
                {
                    var control = new PreviewControl { DataContext = session.View, Width = 1000, Height = 850 };
                    ((TabControl)control.FindName("PreviewTabs")).SelectedIndex = 0;
                    host.RootVisual = control; InheritanceAdornerTests.Pump();
                    var chooser = (Expander)control.FindName("ColumnsChooser"); chooser.IsExpanded = true;
                    InheritanceAdornerTests.Pump();
                    Button move = VisualChildren(chooser).OfType<Button>().Single(button => ReferenceEquals(button.Command, session.View.PreviewColumns.Columns[4].MoveLeftCommand));
                    Assert.True(move.Focus()); move.Command.Execute(null); InheritanceAdornerTests.Pump();
                    Assert.True(move.IsKeyboardFocused);
                    var table = (DataGrid)control.FindName("AlarmTable");
                    Assert.Equal(3, table.Columns.Single(column => column.SortMemberPath == "TestMethod").DisplayIndex);
                    foreach (PreviewColumn column in session.View.PreviewColumns.Columns) column.IsVisible = false;
                    InheritanceAdornerTests.Pump(); Assert.True(chooser.IsVisible);
                    Assert.All(table.Columns, column => Assert.Equal(Visibility.Collapsed, column.Visibility));
                    session.View.PreviewColumns.ResetCommand.Execute(null); InheritanceAdornerTests.Pump();
                    Assert.All(table.Columns, column => Assert.Equal(Visibility.Visible, column.Visibility));
                    Assert.Equal(4, table.Columns.Single(column => column.SortMemberPath == "TestMethod").DisplayIndex);
                    DataGridColumn method = table.Columns.Single(column => column.SortMemberPath == "TestMethod");
                    method.Width = new DataGridLength(520); table.UpdateLayout();
                    DataGridColumnHeader header = VisualChildren(table).OfType<DataGridColumnHeader>().Single(item => item.Column == method);
                    Thumb gripper = VisualChildren(header).OfType<Thumb>().Single(item => item.Name == "PART_RightHeaderGripper");
                    // WPF's header handles this event before it reaches the grid.
                    gripper.RaiseEvent(new DragCompletedEventArgs(100, 0, false) { RoutedEvent = Thumb.DragCompletedEvent, Handled = true });
                    Assert.Equal(520, session.View.PreviewColumns.Columns[4].Width);
                    Assert.Equal(0, session.Buffer.Edits);
                    host.RootVisual = null; InheritanceAdornerTests.Pump();
                    session.View.PreviewColumns.Columns[0].IsVisible = false;
                    Assert.Equal(Visibility.Visible, table.Columns[0].Visibility);
                }
            });
        }
    }
}
