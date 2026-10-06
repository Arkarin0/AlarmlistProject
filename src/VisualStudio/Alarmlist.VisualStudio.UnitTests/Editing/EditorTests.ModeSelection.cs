// Created/modified by Arkarin0 under one ore more license(s).

using System.Linq;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using Alarmlist.VisualStudio.UI;
using Xunit;

namespace Alarmlist.VisualStudio.UnitTests.Editing
{
    public partial class EditorTests
    {
        [Fact]
        public void AccessibleModeSelectionSwitchesPanesAndRestoresSelectionWhenCommitFails()
        {
            InheritanceAdornerTests.RunOnSta(() =>
            {
                bool canCommit = false;
                var settings = new TestSettings();
                settings.Write("Mode", "XML");
                var layout = new EditorLayoutViewModel(settings, () => canCommit, false);
                var designer = new TextBlock { Text = "Designer content" };
                var source = new TextBlock { Text = "Source content" };
                using (var control = new SplitEditorControl(layout, designer, source))
                {
                    control.Measure(new Size(600, 400));
                    control.Arrange(new Rect(0, 0, 600, 400));
                    InheritanceAdornerTests.Pump();
                    RadioButton[] buttons = VisualChildren(control).OfType<RadioButton>().ToArray();
                    SelectMode(buttons, "Designer");
                    Assert.Equal(EditorMode.XML, layout.Mode);
                    Assert.Equal("XML", Assert.Single(buttons.Where(button => button.IsChecked == true)).Content);
                    Assert.Equal(Visibility.Collapsed, designer.Visibility);
                    canCommit = true;
                    SelectMode(buttons, "Designer");
                    Assert.Equal(EditorMode.Designer, layout.Mode);
                    Assert.Equal(Visibility.Visible, designer.Visibility);
                    Assert.Equal(Visibility.Collapsed, source.Visibility);
                    canCommit = false;
                    SelectMode(buttons, "XML");
                    Assert.Equal(EditorMode.Split, layout.Mode);
                    Assert.Equal("Split", Assert.Single(buttons.Where(button => button.IsChecked == true)).Content);
                    Assert.Equal(Visibility.Visible, source.Visibility);
                    Assert.Equal(Visibility.Visible, designer.Visibility);
                }
            });
        }

        private static void SelectMode(RadioButton[] buttons, string name)
        {
            RadioButton button = buttons.Single(item => Equals(item.Content, name));
            AutomationPeer peer = UIElementAutomationPeer.CreatePeerForElement(button);
            ((ISelectionItemProvider)peer.GetPattern(PatternInterface.SelectionItem)).Select();
            InheritanceAdornerTests.Pump();
        }
    }
}
