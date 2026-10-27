// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Linq;
using System.Windows.Controls;
using System.Windows.Interop;
using Alarmlist.VisualStudio.Analysis;
using Alarmlist.VisualStudio.Diagnostics;
using Alarmlist.VisualStudio.Documents;
using Alarmlist.VisualStudio.Editing;
using Alarmlist.VisualStudio.UI;
using Xunit;

namespace Alarmlist.VisualStudio.UnitTests.Editing
{
    public partial class EditorTests
    {
        private const string NavigationSource = "<Alarmlist><Properties><Property Name='file' Value='1'/></Properties>"
            + "<Alarm><FullyQualifiedName>Same</FullyQualifiedName><Name>First</Name></Alarm>"
            + "<Alarm><FullyQualifiedName>Same</FullyQualifiedName><Name>Second</Name>"
            + "<Properties><Property Name='alarm' Value='2'/></Properties>"
            + "<TestProcedure><Group Name='Instructions'><Step>discarded</Step><Clear/><Step>keep</Step></Group>"
            + "<Group Name='Reset'><Hint>reset</Hint></Group></TestProcedure><Unknown>unsupported</Unknown></Alarm></Alarmlist>";

        [Theory]
        [InlineData("Second", "Name", null, -1, null)]
        [InlineData("discarded", null, "Instructions", 0, null)]
        [InlineData("<Clear", null, "Instructions", 1, null)]
        [InlineData("reset", null, "Reset", 0, null)]
        [InlineData("Name='alarm'", null, null, -1, "alarm")]
        [InlineData("Name='file'", null, null, -1, "file")]
        public void NavigationMapsExactLocalPositions(string marker, string field, string section, int entry, string parameter)
        {
            AlmxProjection source = _editing.Parse(NavigationSource);
            DesignerSourceTarget target = DesignerSourceTarget.Find(source, NavigationSource.IndexOf(marker, StringComparison.Ordinal));
            Assert.NotNull(target);
            Assert.Equal(field, target.Field); Assert.Equal(section, target.Section);
            Assert.Equal(entry, target.Entry); Assert.Equal(parameter, target.Parameter);
            if (parameter == "file") Assert.True(target.IsFile);
            else Assert.Same(source.Alarms[1], target.Alarm);
        }

        [Theory]
        [InlineData("<Unknown>unsupported</Unknown>", "unsupported")]
        [InlineData("<Name>one</Name><Name>two</Name>", "two")]
        [InlineData("<Name><Unknown>nested</Unknown></Name>", "nested")]
        [InlineData("<Properties><Property Name='x'/><Property Name='X'/></Properties>", "Name='X'")]
        [InlineData("<!-- comment -->", "comment")]
        public void UnrepresentableLocationsUseXml(string content, string marker)
        {
            string source = "<Alarmlist><Alarm>" + content + "</Alarm></Alarmlist>";
            Assert.Null(DesignerSourceTarget.Find(_editing.Parse(source), source.IndexOf(marker, StringComparison.Ordinal)));
            Assert.Null(DesignerSourceTarget.Find(_editing.Parse(source), source.Length));
            Assert.Null(DesignerSourceTarget.Find(_editing.Parse("<broken"), 2));
        }

        [Fact]
        public void SourceSelectionRevealsExactDuplicateAndNeverCommitsDrafts()
        {
            var buffer = new TestBuffer(NavigationSource);
            using (var model = new AlmxDocumentModel(buffer, _editing))
            using (var view = new EditFileControlViewModel(model, new AlarmViewModelFactory()))
            {
                view.Filter = "First";
                object target = view.SelectSource(NavigationSource.IndexOf("Second", StringComparison.Ordinal), NavigationSource);
                Assert.Equal("Second", view.Selected.Title);
                Assert.Same(view.Fields.Single(field => field.Name == "Name"), target);
                Assert.Empty(view.Filter);
                view.Fields.Single(field => field.Name == "Name").Value = "private";
                Assert.Throws<InvalidOperationException>(() => view.SelectSource(1, NavigationSource));
                Assert.Equal("Second", view.Selected.Title);
                Assert.True(view.HasPending);
                Assert.Equal(0, buffer.Edits);
                Assert.Throws<InvalidOperationException>(() => view.SelectSource(1, NavigationSource + " "));
            }
            var settings = new TestSettings();
            var layout = new EditorLayoutViewModel(settings, false);
            layout.ShowForNavigation(EditorMode.Designer);
            Assert.True(layout.IsDesigner);
            Assert.Equal("unchanged", settings.Read("Mode", "unchanged"));
        }

        [Theory]
        [InlineData("Second")]
        [InlineData("discarded")]
        [InlineData("<Clear")]
        [InlineData("Name='file'")]
        [InlineData("Name='alarm'")]
        public void NativeDesignerControlsRevealNavigationTargetsWithoutEdits(string marker)
        {
            InheritanceAdornerTests.RunOnSta(() =>
            {
                var buffer = new TestBuffer(NavigationSource);
                using (var model = new AlmxDocumentModel(buffer, _editing))
                using (var view = new EditFileControlViewModel(model, new AlarmViewModelFactory()))
                using (var host = new HwndSource(new HwndSourceParameters("Navigation target test")
                { Width = 950, Height = 850, WindowStyle = unchecked((int)0x80000000) }))
                {
                    var control = new EditFileControl(view) { Width = 900, Height = 800 };
                    host.RootVisual = control;
                    InheritanceAdornerTests.Pump();
                    foreach (Expander expander in VisualChildren(control).OfType<Expander>()) expander.IsExpanded = false;
                    view.Selected = view.Alarms[1];
                    view.ProcedureSections[0].ShowCleared = false;
                    Assert.True(control.NavigateSource(NavigationSource.IndexOf(marker, StringComparison.Ordinal), NavigationSource));
                    Assert.Equal(0, buffer.Edits);
                    Assert.Equal(NavigationSource, buffer.Current.Text);
                }
            });
        }

        [Fact]
        public void BuildNavigationRequiresMatchingCurrentDiagnosticIdentityAndPosition()
        {
            using (var session = new SemanticSession(ChildAlarm.Replace(">Base<", ">Missing<")))
            {
                LiveDiagnostic diagnostic = LiveDiagnostic.Create(session.Analysis.Current).Single();
                AnalysisLocation location = diagnostic.Location;
                var target = new BuildDiagnosticTarget(location.FilePath, diagnostic.Id, diagnostic.Message, location.LineNumber - 1, location.ColumnNumber - 1);
                Assert.True(target.Matches(diagnostic));
                Assert.False(new BuildDiagnosticTarget(location.FilePath, diagnostic.Id, "old message", location.LineNumber - 1, location.ColumnNumber - 1).Matches(diagnostic));
                Assert.False(new BuildDiagnosticTarget(location.FilePath, diagnostic.Id, diagnostic.Message, location.LineNumber, location.ColumnNumber - 1).Matches(diagnostic));
            }
        }

        [Fact]
        public void RepeatedReferenceDiagnosticUsesXmlFallback()
        {
            const string source = "<Alarmlist><Alarm><FullyQualifiedName>Fallback</FullyQualifiedName>"
                + "<ReferenceName>MissingFallback</ReferenceName><ReferenceName>MissingFallback</ReferenceName></Alarm></Alarmlist>\r\n";
            using (var session = new SemanticSession(source))
            {
                AnalysisLocation location = session.Analysis.Current.Response.Snapshot.Diagnostics.Single(item => item.Id == "ALM0002").Location;
                Assert.Equal(1, location.LineNumber);
                Assert.Null(DesignerSourceTarget.Find(session.Model.Projection, location.ColumnNumber - 1));
            }
        }
    }
}
