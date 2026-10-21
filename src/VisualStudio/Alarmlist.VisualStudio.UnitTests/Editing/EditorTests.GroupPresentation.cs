// Created/modified by Arkarin0 under one ore more license(s).

using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Alarmlist.VisualStudio.UI;
using Xunit;

namespace Alarmlist.VisualStudio.UnitTests.Editing
{
    public partial class EditorTests
    {
        [Fact]
        public void GroupHeadingRetainsRawDraftFocusAndSourceNavigation()
        {
            InheritanceAdornerTests.RunOnSta(() =>
            {
                string source = WithProcedure(ChildAlarm, "<Group Name='Verify'><Step>local</Step></Group>");
                string ancestor = WithProcedure(BaseAlarm, "<Group Name='Verify'><Title>Check $(AlarmlistAlarmCode)</Title></Group>");
                using (var session = new SemanticSession(source))
                using (var host = ExpressionHost())
                {
                    session.Analyze(ancestor);
                    var control = new EditFileControl(session.View) { Width = 650, Height = 850 };
                    host.RootVisual = control; InheritanceAdornerTests.Pump();
                    GroupBox group = Assert.Single(VisualChildren(control).OfType<GroupBox>());
                    var heading = Assert.IsType<ExpressionTextBox>(group.Header);
                    Assert.Same(session.View.ProcedureSections[0].TitleField, heading.DataContext);
                    Assert.Equal("Check Same", heading.Text);
                    Assert.True(InheritedValueBehavior.GetIsInherited(heading));
                    Assert.True(heading.IsEnabled);
                    Assert.Contains(VisualChildren(group).OfType<TextBlock>(), text => text.Text == "Group ID: Verify");
                    heading.BringIntoView(); Assert.True(heading.Focus());
                    Assert.Equal("Check $(AlarmlistAlarmCode)", heading.Text);
                    Assert.False(session.View.HasPending);
                    Assert.Equal(source, session.Buffer.Current.Text);
                    heading.Text = "Draft $(AlarmlistAlarmCode)";
                    heading.Select(2, 3);
                    session.Analyze(ancestor); InheritanceAdornerTests.Pump();
                    Assert.Same(heading, group.Header);
                    Assert.Same(heading, Keyboard.FocusedElement);
                    Assert.Equal("Draft $(AlarmlistAlarmCode)", heading.Text);
                    Assert.Equal(2, heading.SelectionStart); Assert.Equal(3, heading.SelectionLength);
                    Assert.Equal(source, session.Buffer.Current.Text);
                    RaiseExpressionKey(heading, host, Key.Escape);
                    Assert.False(session.View.HasPending);
                    Assert.Equal("Check $(AlarmlistAlarmCode)", heading.Text);
                    heading.Text = "Local $(AlarmlistAlarmCode)";
                    RaiseExpressionKey(heading, host, Key.Enter);
                    Assert.Equal(1, session.Buffer.Edits);
                    string changed = session.Buffer.Current.Text;
                    Assert.Same(heading.DataContext, session.View.SelectSource(changed.IndexOf("Local", System.StringComparison.Ordinal), changed));
                    session.Buffer.Undo(); session.Analyze(ancestor);
                    Assert.Equal(source, session.Buffer.Current.Text);
                    Assert.Same(heading, group.Header);
                }
            });
        }

        [Theory]
        [InlineData("Light", 900, 1.0)]
        [InlineData("Dark", 650, 1.5)]
        [InlineData("HighContrast", 440, 2.0)]
        public void GroupHeadingsWrapAndShowCompilerTitlesWithoutEditingSource(string palette, int width, double scale)
        {
            InheritanceAdornerTests.RunOnSta(() =>
            {
                const string token = "$(loc:Alarm.Inspect-now)";
                string source = "<Alarmlist><Alarm><FullyQualifiedName>A</FullyQualifiedName><TestProcedure>"
                    + "<Group Name='Localized'><Title>" + token + "</Title><Step>Inspect the cover.</Step></Group>"
                    + "<Group Name='Missing'/><Group Name='Empty'><Title/></Group>"
                    + "<Group Name='Error'><Title>$(loc:Unknown)</Title></Group></TestProcedure></Alarm></Alarmlist>";
                string translated = string.Join(" ", Enumerable.Repeat("Long translated safety inspection heading", 5));
                using (var session = new SemanticSession(source))
                using (var host = ExpressionHost())
                {
                    AnalyzeLocalization(session, saved: "<root><data name='Alarm.Inspect-now'><value>" + translated + "</value></data></root>");
                    var control = new EditFileControl(session.View) { Width = width, Height = 850 };
                    Brush text = palette == "Light" ? Brushes.Black : palette == "Dark" ? Brushes.WhiteSmoke : Brushes.Yellow;
                    Brush background = palette == "Light" ? Brushes.White : Brushes.Black;
                    foreach (string key in new[] { "ToolWindowTextBrushKey", "ComboBoxTextBrushKey", "ToolWindowBorderBrushKey" }) SetThemeBrush(control, key, text);
                    foreach (string key in new[] { "ToolWindowBackgroundBrushKey", "ComboBoxBackgroundBrushKey" }) SetThemeBrush(control, key, background);
                    host.RootVisual = control; InheritanceAdornerTests.Pump();
                    GroupBox[] groups = VisualChildren(control).OfType<GroupBox>().ToArray();
                    Assert.Equal(4, groups.Length);
                    ExpressionTextBox[] titles = groups.Select(group => Assert.IsType<ExpressionTextBox>(group.Header)).ToArray();
                    Assert.Equal(new[] { translated, "Missing", "Empty", "$(loc:Unknown)" }, titles.Select(title => title.Text));
                    Assert.Equal(4, VisualChildren(control).OfType<ExpressionTextBox>().Count(title => AutomationProperties.GetName(title) == "Procedure group title"));
                    foreach (GroupBox group in groups)
                    {
                        var heading = (ExpressionTextBox)group.Header;
                        Assert.Same(text, heading.Foreground);
                        Assert.True(heading.ActualWidth > 100);
                        Assert.True(heading.ActualWidth < group.ActualWidth);
                        Assert.True(group.ActualWidth <= width);
                        TextBlock id = VisualChildren(group).OfType<TextBlock>().Single(item => item.Text.StartsWith("Group ID: "));
                        Assert.True(id.TranslatePoint(new Point(), group).Y >= heading.ActualHeight);
                    }
                    Assert.True(titles[0].ActualHeight > titles[1].ActualHeight);
                    titles[0].BringIntoView(); InheritanceAdornerTests.Pump();
                    RenderPreview(groups[0], palette + "-procedure-groups", scale);
                    Assert.True(titles[0].Focus()); Assert.Equal(token, titles[0].Text);
                    Assert.False(session.View.HasPending);
                    Assert.Equal(source, session.Buffer.Current.Text);
                }
            });
        }
    }
}
