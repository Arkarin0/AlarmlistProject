using System;
using System.Linq;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using Xunit;

namespace Alarmlist.VisualStudio.UIIntegrationTests
{
    internal static class ValueExplanationUi
    {
        internal static string Value(Window window) => window.FindAllDescendants(cf => cf.ByName("Explained value").And(cf.ByControlType(ControlType.Edit)))
            .Select(item => item.AsTextBox()).FirstOrDefault(item => item.IsReadOnly)?.Text;
        internal static void Explain(Window window, string action, string expected, string step = null)
        {
            Button button = null;
            VisualStudioSession.Wait(() => (button = window.FindAllDescendants(cf => cf.ByName(action).And(cf.ByControlType(ControlType.Button)))
                .FirstOrDefault(item => item.IsEnabled)?.AsButton()) != null, action);
            button.Invoke();
            VisualStudioSession.Wait(() => Value(window)?.Contains(expected) == true
                && (step == null || window.FindAllDescendants().Any(item => item.Name?.Contains(step) == true)), "current explanation: " + expected);
            Assert.True(window.FindAllDescendants(cf => cf.ByName("Explained value").And(cf.ByControlType(ControlType.Edit)))
                .Select(item => item.AsTextBox()).First(item => item.IsReadOnly).IsReadOnly);
        }
        internal static void Navigate(Window window, string step)
        {
            AutomationElement header = null;
            VisualStudioSession.Wait(() => (header = window.FindAllDescendants().FirstOrDefault(item => item.Name?.Contains(step) == true
                && item.Patterns.ExpandCollapse.IsSupported)) != null, "explanation step " + step);
            header.Patterns.ExpandCollapse.Pattern.Expand();
            Button button = null;
            VisualStudioSession.Wait(() => (button = window.FindAllDescendants(cf => cf.ByControlType(ControlType.Button))
                .FirstOrDefault(item => item.IsEnabled && item.Name?.StartsWith("Go to source for #", StringComparison.Ordinal) == true
                    && item.Name.Contains(step))?.AsButton()) != null, "explanation source for " + step);
            button.Invoke();
        }
    }
}
