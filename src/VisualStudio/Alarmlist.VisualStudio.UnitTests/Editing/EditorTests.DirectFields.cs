// Created/modified by Arkarin0 under one ore more license(s).

using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using Alarmlist.VisualStudio.UI;
using Xunit;

namespace Alarmlist.VisualStudio.UnitTests.Editing
{
    public partial class EditorTests
    {
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void DirectInheritedEditingPreservesFocusAndRawDraftWithoutMaterializingUntouchedFields(bool present)
        {
            InheritanceAdornerTests.RunOnSta(() =>
            {
                string source = present ? PropertyChild.Replace("</Alarm>", "<Code/></Alarm>") : PropertyChild;
                using (var session = new SemanticSession(source))
                using (var host = ExpressionHost())
                {
                    session.Analyze(PropertyBase);
                    var control = new EditFileControl(session.View) { Width = 900, Height = 850 };
                    host.RootVisual = control;
                    InheritanceAdornerTests.Pump();
                    ExpressionTextBox code = ExpressionBox(control, "Code");
                    Assert.Equal("001", code.Text);
                    code.BringIntoView();
                    Assert.True(code.Focus());
                    Assert.Equal("$(par1)", code.Text);
                    Assert.False(session.View.HasPending);
                    Assert.Equal(present, session.Field("Code").IsPresent);
                    Assert.True(PreviewBox(control, "Filter alarms").Focus());
                    Assert.Equal(source, session.Buffer.Current.Text);
                    Assert.Equal(0, session.Buffer.Edits);
                    Assert.True(code.Focus());
                    code.CaretIndex = code.Text.Length;
                    TextCompositionManager.StartComposition(new TextComposition(InputManager.Current, code, "!"));
                    Assert.Equal("$(par1)!", code.Text);
                    Assert.Equal(code.Text.Length, code.CaretIndex);
                    Assert.True(session.Field("Code").IsPresent);
                    Assert.False(session.Field("Code").IsInherited);
                    Assert.True(session.View.HasPending);
                    code.Select(2, 3);
                    session.Analyze(PropertyBase);
                    Assert.Equal("$(par1)!", code.Text);
                    Assert.Equal(2, code.SelectionStart);
                    Assert.Equal(3, code.SelectionLength);
                    Assert.Same(code, Keyboard.FocusedElement);
                    Assert.Equal(0, session.Buffer.Edits);
                    Assert.True(session.View.CommitPending());
                    Assert.Contains("<Code>$(par1)!</Code>", session.Buffer.Current.Text);
                    Assert.DoesNotContain("<Description>", session.Buffer.Current.Text);
                    Assert.Equal(1, session.Buffer.Edits);
                    session.Buffer.Undo();
                    Assert.Equal(source, session.Buffer.Current.Text);
                }
            });
        }

        [Theory]
        [InlineData("Name")]
        [InlineData("Description")]
        [InlineData("FullyQualifiedName")]
        [InlineData("ReferenceName")]
        public void EditingAbsentFieldsCreatesOnlyTheirLocalValueAndEscapeLeavesNoXml(string name)
        {
            using (var session = new SemanticSession("<Alarmlist><Alarm/></Alarmlist>"))
            {
                AlarmFieldViewModel field = session.Field(name);
                field.EditValue = "New value";
                Assert.True(field.IsPresent);
                Assert.True(session.View.HasPending);
                session.View.CancelPending();
                Assert.False(field.IsPresent);
                Assert.Equal(0, session.Buffer.Edits);
                field.EditValue = "New value";
                Assert.True(session.View.CommitPending());
                Assert.Equal("New value", session.Model.Projection.Alarms[0].GetValue(name));
                Assert.Equal(1, session.Buffer.Edits);
            }
        }

        [Fact]
        public void ClearingAnAlreadyEmptyInheritedFieldKeepsTheDraftUntilCommitWithoutExtraXmlEdits()
        {
            string source = ChildAlarm.Replace("<Code>", "<Name/><Code>");
            using (var session = new SemanticSession(source))
            {
                AlarmFieldViewModel name = session.Field("Name");
                Assert.Equal("Inherited", name.EditValue);
                name.EditValue = "";
                Assert.Equal("", name.EditValue);
                Assert.True(session.View.HasPending);
                session.Analyze();
                Assert.Equal("", name.EditValue);
                Assert.True(session.View.CommitPending());
                Assert.Equal(source, session.Buffer.Current.Text);
                Assert.Equal(0, session.Buffer.Edits);
                Assert.Equal("Inherited", name.EditValue);
            }
        }

        [Theory]
        [InlineData("Light", 900, 1.0)]
        [InlineData("Dark", 650, 1.5)]
        [InlineData("HighContrast", 440, 2.0)]
        public void DirectFieldFormAlignsEditorsAndAdornersAndThemesAbstractState(string palette, int width, double scale)
        {
            InheritanceAdornerTests.RunOnSta(() =>
            {
                using (var session = new SemanticSession())
                using (var host = ExpressionHost())
                {
                    var control = new EditFileControl(session.View) { Width = width, Height = 850 };
                    Brush text = palette == "Light" ? Brushes.Black : palette == "Dark" ? Brushes.WhiteSmoke : Brushes.Yellow;
                    Brush background = palette == "Light" ? Brushes.White : Brushes.Black;
                    SetThemeBrush(control, "ToolWindowTextBrushKey", text);
                    SetThemeBrush(control, "ComboBoxTextBrushKey", text);
                    SetThemeBrush(control, "ComboBoxBackgroundBrushKey", background);
                    SetThemeBrush(control, "ToolWindowBackgroundBrushKey", background);
                    host.RootVisual = control;
                    InheritanceAdornerTests.Pump();
                    ExpressionTextBox name = ExpressionBox(control, "Name"), code = ExpressionBox(control, "Code");
                    Assert.Equal(name.TranslatePoint(new Point(), control).X, code.TranslatePoint(new Point(), control).X);
                    Assert.True(name.ActualWidth > 180);
                    ContentControl indicator = VisualChildren(control).OfType<ContentControl>().Single(item => ReferenceEquals(item.DataContext, session.Field("Name")) && InheritedValueBehavior.GetIsInherited(item));
                    Assert.True(indicator.TranslatePoint(new Point(), control).X < name.TranslatePoint(new Point(), control).X);
                    var adorner = Assert.IsType<InheritedValueAdorner>(Assert.Single(AdornerLayer.GetAdornerLayer(indicator).GetAdorners(indicator)));
                    Assert.Same(text, adorner.Brush);
                    CheckBox abstractBox = Assert.Single(VisualChildren(control).OfType<CheckBox>());
                    Assert.Equal("Abstract template", AutomationProperties.GetName(abstractBox));
                    Assert.Same(text, abstractBox.Foreground);
                    Assert.Same(background, abstractBox.Background);
                    abstractBox.IsChecked = true;
                    InheritanceAdornerTests.Pump();
                    Assert.Contains(VisualChildren(abstractBox).OfType<System.Windows.Shapes.Path>(), path => path.IsVisible);
                    Assert.True(abstractBox.Focus());
                    abstractBox.IsEnabled = false;
                    Assert.Same(text, abstractBox.Foreground);
                    Assert.Same(background, abstractBox.Background);
                    RenderPreview(control, palette + "-direct-fields", scale);
                }
            });
        }
    }
}
