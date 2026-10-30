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
        internal static void Explain(Window window, string action, string expected, string step = null, Action beforeFreshRequest = null)
        {
            const string obsolete = "Inputs or the selected value changed. Explain this value again.";
            for (int attempt = 0; attempt < 2; attempt++)
            {
                Button button = null;
                VisualStudioSession.Wait(() => (button = window.FindAllDescendants(cf => cf.ByName(action).And(cf.ByControlType(ControlType.Button)))
                    .FirstOrDefault(item => item.IsEnabled)?.AsButton()) != null, action);
                button.Invoke();
                bool refresh = false;
                VisualStudioSession.Wait(() =>
                {
                    string status = window.FindFirstDescendant(cf => cf.ByAutomationId("ExplanationStatus"))?.Name ?? "";
                    Assert.False(status.StartsWith("Explanation unavailable", StringComparison.Ordinal), status);
                    if (attempt == 0 && status == obsolete)
                    {
                        Assert.True(beforeFreshRequest != null, status);
                        refresh = true; return true;
                    }
                    // The second request may be queued while the previous obsolete
                    // message is still visible. Only its actual result can pass.
                    return Value(window)?.Contains(expected) == true
                        && (step == null || window.FindAllDescendants().Any(item => item.Name?.Contains(step) == true));
                }, "current explanation: " + expected);
                if (!refresh) break;
                Alarmlist.VisualStudio.Testing.VisualStudioAutomation.Log("Initial explanation invalidated; verify the fixture and explicitly request its current value once.");
                beforeFreshRequest();
            }
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
