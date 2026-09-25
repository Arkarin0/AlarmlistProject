// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using Alarmlist.VisualStudio.Analysis;
using Alarmlist.VisualStudio.Documents;
using Alarmlist.VisualStudio.Editing;
using Alarmlist.VisualStudio.UI;
using Xunit;

// These tests intentionally await view commands and controlled validation tasks.
#pragma warning disable VSTHRD003
namespace Alarmlist.VisualStudio.UnitTests.Editing
{
    public partial class EditorTests
    {
        private const string BaseAlarm = "<Alarmlist><Alarm><FullyQualifiedName>Base</FullyQualifiedName><Name>Inherited</Name><Code>Same</Code></Alarm></Alarmlist>";
        private const string ChildAlarm = "<Alarmlist><Alarm><FullyQualifiedName>Child</FullyQualifiedName><ReferenceName>Base</ReferenceName><Code>Same</Code></Alarm></Alarmlist>";

        [Theory]
        [InlineData("\n")]
        [InlineData("\r\n")]
        [InlineData("\r")]
        public void SourceLocationsMatchAcrossXmlLineEndings(string newline)
        {
            using (var session = new SemanticSession(ChildAlarm.Replace("<Alarm>", newline + "  <Alarm>")))
                Assert.True(session.Field("Name").IsInherited);
        }

        [Fact]
        public void DesignerXamlSeparatesInheritedDisplayFromLocalDraft()
        {
            InheritanceAdornerTests.RunOnSta(() =>
            {
                using (var session = new SemanticSession())
                using (var host = new HwndSource(new HwndSourceParameters("ALMX designer binding test")
                { Width = 950, Height = 850, WindowStyle = unchecked((int)0x80000000) }))
                {
                    var control = new EditFileControl(session.View) { Width = 900, Height = 800 };
                    host.RootVisual = control;
                    InheritanceAdornerTests.Pump();
                    TextBox inherited = VisualChildren(control).OfType<TextBox>().Single(box =>
                        box.IsReadOnly && AutomationProperties.GetName(box) == "Name");
                    Assert.Equal("Inherited", inherited.Text);
                    Assert.Contains("Inherited from Base", AutomationProperties.GetHelpText(inherited));
                    Assert.False(session.View.HasPending);
                    Assert.Equal(0, session.Buffer.Edits);
                    Button button = VisualChildren(control).OfType<Button>().Single(item =>
                        Equals(item.Content, "Override locally") && item.DataContext == session.Field("Name"));
                    Assert.True(button.Command.CanExecute(null));
                    button.Command.Execute(null);
                    InheritanceAdornerTests.Pump();
                    TextBox local = VisualChildren(control).OfType<TextBox>().Single(box =>
                        !box.IsReadOnly && AutomationProperties.GetName(box) == "Name");
                    Assert.Equal(Visibility.Visible, local.Visibility);
                    Assert.Equal("Inherited", local.Text);
                    Assert.True(session.View.HasPending);
                    Assert.Equal(0, session.Buffer.Edits);
                    session.View.CancelPending();
                    InheritanceAdornerTests.Pump();
                    Assert.Equal(0, session.Buffer.Edits);
                }
            });
        }

        private static IEnumerable<DependencyObject> VisualChildren(DependencyObject parent)
        {
            for (int index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(parent, index);
                yield return child;
                foreach (DependencyObject nested in VisualChildren(child)) yield return nested;
            }
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void InheritedDisplayPreservesAbsentAndExplicitEmptyXml(bool present)
        {
            using (var session = new SemanticSession(present ? ChildAlarm.Replace("<Code>", "<Name/><Code>") : ChildAlarm))
            {
                AlarmFieldViewModel name = session.Field("Name");
                Assert.True(name.IsInherited);
                Assert.Equal(present, name.IsPresent);
                Assert.Equal("", name.Value);
                Assert.Equal("Inherited", name.EffectiveValue);
                Assert.Contains("Base", name.OriginText);
                Assert.Equal(present, name.OriginText.Contains("local element is empty"));
                Assert.False(session.Field("Code").IsInherited); // Equal text is still local.
                Assert.False(session.Field("FullyQualifiedName").IsInherited);
                Assert.False(session.Field("Category").IsInherited);
                Assert.True(session.View.CommitPending());
                Assert.Equal(0, session.Buffer.Edits);
            }
        }

        [Fact]
        public async Task OverrideStartsDraftEscapeCancelsAndCommitOnlyWritesSelectedFieldAsync()
        {
            using (var session = new SemanticSession())
            {
                AlarmFieldViewModel name = session.Field("Name");
                name.OverrideCommand.Execute(null);
                await session.View.PendingAnalysisAction;
                Assert.True(session.View.HasPending);
                Assert.False(name.IsInherited);
                Assert.Equal("Inherited", name.Value);
                Assert.Equal(0, session.Buffer.Edits);
                session.View.CancelPending();
                Assert.True(name.IsInherited);
                Assert.Equal(0, session.Buffer.Edits);
                name.OverrideCommand.Execute(null);
                await session.View.PendingAnalysisAction;
                name.Value = "Local draft";
                Assert.True(session.View.CommitPending());
                Assert.Equal(1, session.Buffer.Edits);
                Assert.Equal("Local draft", session.Model.Projection.Alarms[0].GetValue("Name"));
                Assert.Equal("Base", session.Model.Projection.Alarms[0].GetValue("ReferenceName"));
                Assert.Equal("Same", session.Model.Projection.Alarms[0].GetValue("Code"));
            }
        }

        [Fact]
        public async Task UpstreamRefreshPreservesDraftAndRefreshesUntouchedInheritedFieldsAsync()
        {
            using (var session = new SemanticSession(ChildAlarm.Replace("<Code>Same</Code>", "")))
            {
                session.Field("Name").OverrideCommand.Execute(null);
                await session.View.PendingAnalysisAction;
                session.Field("Name").Value = "Keep draft";
                session.Analyze(BaseAlarm.Replace("Inherited", "Changed upstream").Replace("Same", "New code"));
                Assert.Equal("Keep draft", session.Field("Name").Value);
                Assert.False(session.Field("Name").IsInherited);
                Assert.Equal("New code", session.Field("Code").EffectiveValue);
                Assert.True(session.View.HasPending);
                session.View.CancelPending();
                Assert.Equal("Changed upstream", session.Field("Name").EffectiveValue);
                Assert.Equal(0, session.Buffer.Edits);
            }
        }

        [Theory]
        [InlineData("<Name/>")]
        [InlineData("<Name>Local</Name>")]
        public void UseInheritedRemovesOnlyTheLocalElement(string element)
        {
            using (var session = new SemanticSession(ChildAlarm.Replace("<Code>", element + "<Code>")))
            {
                session.Field("Name").UseInheritedCommand.Execute(null);
                Assert.Equal(ChildAlarm, session.Buffer.Current.Text);
                Assert.Equal(1, session.Buffer.Edits);
                session.Analyze();
                Assert.True(session.Field("Name").IsInherited);
            }
        }

        [Fact]
        public async Task StaleCommandDoesNotCopyOrNavigateAsync()
        {
            using (var session = new SemanticSession())
            {
                session.Analysis.Valid = false; // Change before its UI notification.
                session.Field("Name").OverrideCommand.Execute(null);
                await session.View.PendingAnalysisAction;
                Assert.False(session.View.HasPending);
                Assert.Contains("changed", session.View.Error);
                session.View.GoToReferenceCommand.Execute(null);
                await session.View.PendingAnalysisAction;
                Assert.Null(session.Analysis.Navigation);
                Assert.Equal(0, session.Buffer.Edits);
            }
        }

        [Fact]
        public async Task DraftChangedDuringValidationIsNeverOverwrittenAsync()
        {
            using (var session = new SemanticSession())
            {
                var validation = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                session.Analysis.Validation = validation.Task;
                session.Field("Name").OverrideCommand.Execute(null);
                session.Field("Name").IsPresent = true;
                session.Field("Name").Value = "Typed during validation";
                validation.SetResult(true);
                await session.View.PendingAnalysisAction;
                Assert.Equal("Typed during validation", session.Field("Name").Value);
                Assert.True(session.View.HasPending);
                Assert.Equal(0, session.Buffer.Edits);
            }
        }

        [Fact]
        public void OldResultsCannotAttachToChangedOrMovedAlarms()
        {
            using (var session = new SemanticSession())
            {
                session.Field("Code").Value = "Draft";
                session.Buffer.SetText(session.Buffer.Current.Text.Replace("<Alarm>", "<Alarm><Name>Inserted</Name></Alarm><Alarm>"));
                Assert.False(session.Field("Name").IsInherited);
                Assert.Empty(session.View.ReferenceChoices);
                Assert.Equal("Draft", session.Field("Code").Value);
                Assert.False(session.Field("Name").OverrideCommand.CanExecute(null));
            }
        }

        [Fact]
        public async Task PickerExcludesSelfDuplicatesAndCyclesAndKeepsOrdinalNamesAsync()
        {
            using (var session = new SemanticSession())
            {
                string declarations = "<Alarm><FullyQualifiedName>base</FullyQualifiedName><Name>Lower</Name></Alarm>"
                    + "<Alarm><FullyQualifiedName>Descendant</FullyQualifiedName><ReferenceName>Child</ReferenceName></Alarm>"
                    + "<Alarm><FullyQualifiedName>Dup</FullyQualifiedName></Alarm><Alarm><FullyQualifiedName>Dup</FullyQualifiedName></Alarm>"
                    + "<Alarm><FullyQualifiedName>Invalid</FullyQualifiedName><ReferenceName>Missing</ReferenceName></Alarm>";
                session.Analyze(BaseAlarm.Replace("</Alarmlist>", declarations + "</Alarmlist>"));
                Assert.Equal(new[] { "Base", "base" }, session.View.ReferenceChoices.Select(choice => choice.Identifier));
                session.View.SelectedReferenceChoice = session.View.ReferenceChoices.Single(choice => choice.Identifier == "base");
                session.View.ApplyReferenceCommand.Execute(null);
                await session.View.PendingAnalysisAction;
                Assert.Equal(ChildAlarm.Replace(">Base<", ">base<"), session.Buffer.Current.Text);
                Assert.Equal(1, session.Buffer.Edits);
            }
        }

        [Fact]
        public void LiteralInvalidReferenceRemainsEditableAndHasNoInheritedPresentation()
        {
            using (var session = new SemanticSession())
            {
                session.Field("ReferenceName").Value = "Missing";
                Assert.False(session.Field("Name").IsInherited);
                Assert.True(session.View.CommitPending());
                session.Analyze();
                Assert.False(session.Field("Name").IsInherited);
                Assert.Contains("could not be resolved", session.View.AnalysisMessage);
                Assert.True(session.View.CanEdit);
                Assert.NotEmpty(session.View.ReferenceChoices);
            }
        }

        [Fact]
        public async Task NavigationDistinguishesImmediateReferenceAndActualDefiningFieldAsync()
        {
            using (var session = new SemanticSession(ChildAlarm.Replace(">Base<", ">Middle<")))
            {
                session.Analyze(BaseAlarm.Replace("</Alarmlist>", "<Alarm><FullyQualifiedName>Middle</FullyQualifiedName><ReferenceName>Base</ReferenceName></Alarm></Alarmlist>"));
                session.View.GoToReferenceCommand.Execute(null);
                await session.View.PendingAnalysisAction;
                AnalysisLocation immediate = session.Analysis.Navigation;
                session.Field("Name").GoToSourceCommand.Execute(null);
                await session.View.PendingAnalysisAction;
                Assert.NotEqual(immediate.ColumnNumber, session.Analysis.Navigation.ColumnNumber);
                Assert.Equal(session.Analysis.Current.Response.Snapshot.Alarms[0].Fields.Single(field => field.Name == "Name").ValueLocation.ColumnNumber,
                    session.Analysis.Navigation.ColumnNumber);
                Assert.Contains("<Name>Inherited</Name>", session.Analysis.NavigationText);
                Assert.Equal(0, session.Buffer.Edits);
            }
        }

        [Fact]
        public void PendingUnavailableAndContextChangesClearInheritanceWithoutLosingLocalDraft()
        {
            using (var session = new SemanticSession())
            {
                session.Field("Code").Value = "Draft";
                AnalysisProject project = session.Analysis.Current.Project;
                foreach (ProjectAnalysisStatus status in new[] { ProjectAnalysisStatus.Pending, ProjectAnalysisStatus.Incomplete, ProjectAnalysisStatus.Unavailable })
                {
                    session.Analysis.Set(new ProjectAnalysisState(2, project, status, "State changed"));
                    Assert.False(session.Field("Name").IsInherited);
                    Assert.Equal("Draft", session.Field("Code").Value);
                    Assert.Empty(session.View.ReferenceChoices);
                    Assert.True(session.View.CanEdit);
                }
                session.Analysis.Set(null);
                Assert.Contains("unavailable", session.View.AnalysisMessage);
                session.Analysis.Projects = new[] { project, new AnalysisProject("Second.almproj", "Release", null, project.Inputs) };
                session.Analysis.Set(null);
                Assert.Contains("Choose a project", session.View.AnalysisMessage);
                session.View.SelectedProjectPath = "Second.almproj";
                Assert.Equal("Second.almproj", session.Analysis.SelectedProjectPath);
                session.View.Dispose();
                Assert.Equal(0, session.Analysis.Subscribers);
                Assert.True(session.Analysis.Disposed);
            }
        }

        [Fact]
        public void UnnamedAlarmUsesSourcePositionInsteadOfBorrowingAnotherAlarmsValues()
        {
            using (var session = new SemanticSession(ChildAlarm.Replace("<FullyQualifiedName>Child</FullyQualifiedName>", "")))
            {
                Assert.True(session.Field("Name").IsInherited);
                Assert.Equal("Inherited", session.Field("Name").EffectiveValue);
            }
        }

        private sealed class SemanticSession : IDisposable
        {
            private readonly string _directory = Path.Combine(Path.GetTempPath(), "Alarmlist Designer Tests", Guid.NewGuid().ToString("N"));
            private readonly ISdkAnalysisBridge _bridge;
            public ISdkAnalysisBridge Bridge => _bridge;
            public TestBuffer Buffer { get; }
            public AlmxDocumentModel Model { get; }
            public EditFileControlViewModel View { get; }
            public DesignerAnalysis Analysis { get; } = new DesignerAnalysis();
            public SemanticSession(string child = ChildAlarm)
            {
                ZipFile.ExtractToDirectory(Path.Combine(AppContext.BaseDirectory, "testassets", "analysis-sdk.nupkg"), _directory);
                BridgeOpenResult opened = new SdkAnalysisBridgeFactory().Open(Path.Combine(_directory, "tasks", "net472", "Alarmlist.Core.dll"));
                Assert.Equal(AnalysisAvailability.Available, opened.Availability);
                _bridge = opened.Bridge;
                Analysis.DocumentPath = Path.Combine(_directory, "Child.almx");
                Buffer = new TestBuffer(child);
                Model = new AlmxDocumentModel(Buffer, new AlmxEditingService());
                View = new EditFileControlViewModel(Model, new AlarmViewModelFactory(), Analysis);
                Analyze();
            }
            public AlarmFieldViewModel Field(string name) => View.Fields.Single(field => field.Name == name);
            public void Analyze(string source = BaseAlarm)
            {
                var inputs = new[] { new AnalysisInput(source, Path.Combine(_directory, "Base.almx"), 1),
                    new AnalysisInput(Buffer.Current.Text, Analysis.DocumentPath, Buffer.Current.Version) };
                var project = new AnalysisProject(Path.Combine(_directory, "Plant.almproj"), "Debug", null, inputs.Select(input => input.FilePath));
                Analysis.Projects = new[] { project };
                Analysis.Set(new ProjectAnalysisState((Analysis.Current?.Generation ?? 0) + 1, project, ProjectAnalysisStatus.Available, null,
                    _bridge.Analyze(inputs), inputs.Select(input => new CapturedAnalysisInput(input)).ToArray()));
            }
            public void Dispose()
            {
                View.Dispose(); Model.Dispose(); _bridge.Dispose();
                string root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "Alarmlist Designer Tests")) + Path.DirectorySeparatorChar;
                if (!Path.GetFullPath(_directory).StartsWith(root, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Unsafe test cleanup.");
                Directory.Delete(_directory, true);
            }
        }

        private sealed class DesignerAnalysis : IDesignerAnalysis
        {
            public event EventHandler Changed;
            public int Subscribers => Changed?.GetInvocationList().Length ?? 0;
            public string DocumentPath { get; set; }
            public IReadOnlyList<AnalysisProject> Projects { get; set; } = Array.Empty<AnalysisProject>();
            public string SelectedProjectPath { get; set; }
            private ProjectAnalysisState _current;
            public Func<ProjectAnalysisState> ReadCurrent { get; set; }
            public ProjectAnalysisState Current => ReadCurrent == null ? _current : ReadCurrent();
            public bool Valid { get; set; } = true;
            public bool Disposed { get; private set; }
            public Task<bool> Validation { get; set; }
            public AnalysisLocation Navigation { get; private set; }
            public string NavigationText { get; private set; }
            public Task<bool> IsCurrentAsync(ProjectAnalysisState state, CancellationToken token) => Validation ?? Task.FromResult(Valid && ReferenceEquals(state, Current));
            public void Navigate(AnalysisLocation location, string text) { Navigation = location; NavigationText = text; }
            public void Set(ProjectAnalysisState state) { _current = state; Changed?.Invoke(this, EventArgs.Empty); }
            public void Dispose() { Disposed = true; }
        }
    }
}
