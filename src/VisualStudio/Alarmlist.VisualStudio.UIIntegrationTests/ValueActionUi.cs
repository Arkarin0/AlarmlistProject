// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Diagnostics;
using System.Linq;
using Alarmlist.VisualStudio.Testing;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.Core.Input;
using FlaUI.Core.WindowsAPI;

namespace Alarmlist.VisualStudio.UIIntegrationTests
{
    internal static class ValueActionUi
    {
        // Find the accessible value, then exercise its actual native menu. The
        // returned wrapper uses Invoke/Focus, which MenuItem also supports.
        internal static Button Find(Window window, string action, bool keyboard = false, bool useMenuKey = false)
        {
            string description = action.StartsWith("Explain this value for ", StringComparison.Ordinal) ? action.Substring(23)
                : action.StartsWith("Find references to ", StringComparison.Ordinal) ? action.Substring(19)
                : action.StartsWith("Rename symbol for ", StringComparison.Ordinal) ? action.Substring(18)
                : action.StartsWith("Rename this ", StringComparison.Ordinal) ? action.Substring(7) : null;
            if (description == null)
                return window.FindAllDescendants(cf => cf.ByName(action).And(cf.ByControlType(ControlType.Button)))
                    .FirstOrDefault(item => item.IsEnabled)?.AsButton();
            var desktop = window.Automation.GetDesktop();
            int process = window.Properties.ProcessId.Value;
            AutomationElement MenuAction() => desktop.FindAllChildren(cf => cf.ByProcessId(process))
                .SelectMany(owned => owned.FindAllDescendants(cf => cf.ByControlType(ControlType.MenuItem).And(cf.ByName(action))))
                .FirstOrDefault(item => !item.IsOffscreen);
            var existing = MenuAction();
            if (existing != null) return existing.IsEnabled ? existing.AsButton() : null;
            string status = "Value actions for " + description + ". Press Shift+F10 or the Menu key.";
            var target = window.FindAllDescendants().Where(item => item.Properties.ItemStatus.ValueOrDefault == status)
                .OrderBy(item => item.IsOffscreen ? 1 : 0).ThenBy(item => item.ControlType == ControlType.Edit ? 0 : 1).FirstOrDefault();
            if (target == null) return null;
            VisualStudioSession.Wait(() =>
            {
                window.SetForeground();
                try { VisualStudioSession.RequireForeground(process); return true; }
                catch (InvalidOperationException) { return false; }
            }, "owned IDE foreground before value input", 10);
            // Pointer invocation preserves a pending draft in another control.
            if (target.Patterns.ScrollItem.IsSupported) target.Patterns.ScrollItem.Pattern.ScrollIntoView();
            if (keyboard)
            {
                target.Focus();
                VisualStudioSession.Wait(() => target.Properties.HasKeyboardFocus.Value, "value menu keyboard focus");
                VisualStudioSession.RequireForeground(process);
                if (useMenuKey) Keyboard.Type(VirtualKeyShort.APPS);
                else Keyboard.TypeSimultaneously(VirtualKeyShort.SHIFT, VirtualKeyShort.F10);
            }
            else
            {
                System.Drawing.Point point = default;
                VisualStudioSession.Wait(() =>
                {
                    if (TargetPoint(window, target, process, out point)) return true;
                    // WPF can report IsOffscreen=false for clipped fields. Scroll
                    // their actual viewport without focusing/committing a draft.
                    for (var parent = target.Parent; parent != null; parent = parent.Parent)
                    {
                        if (!parent.Patterns.Scroll.IsSupported || !parent.Patterns.Scroll.Pattern.VerticallyScrollable.Value) continue;
                        var viewport = parent.BoundingRectangle;
                        bool above = target.BoundingRectangle.Top < viewport.Top;
                        parent.Patterns.Scroll.Pattern.Scroll(ScrollAmount.NoAmount, above ? ScrollAmount.SmallDecrement : ScrollAmount.SmallIncrement);
                        break;
                    }
                    return false;
                }, "hit-tested visible value: " + description);
                VisualStudioSession.RequireForeground(process);
                Mouse.RightClick(point);
            }
            AutomationElement selected = null;
            var opening = Stopwatch.StartNew();
            VisualStudioSession.Wait(() =>
            {
                if ((selected = MenuAction()) != null) return true;
                // Initial catalogue publication can invalidate a menu opened
                // against the preceding context. Retry only the absent menu;
                // never repeat the semantic action it will eventually contain.
                if (opening.ElapsedMilliseconds >= 750)
                {
                    window.SetForeground();
                    try { VisualStudioSession.RequireForeground(process); }
                    catch (InvalidOperationException) { return false; }
                    if (keyboard)
                    {
                        target.Focus();
                        if (useMenuKey) Keyboard.Type(VirtualKeyShort.APPS);
                        else Keyboard.TypeSimultaneously(VirtualKeyShort.SHIFT, VirtualKeyShort.F10);
                    }
                    else if (TargetPoint(window, target, process, out var point)) Mouse.RightClick(point);
                    opening.Restart();
                }
                return false;
            }, "value context menu: " + action);
            if (selected.IsEnabled) return selected.AsButton();
            Keyboard.Type(VirtualKeyShort.ESCAPE);
            return null;
        }

        private static bool TargetPoint(Window window, AutomationElement target, int process, out System.Drawing.Point point)
        {
            if (!target.TryGetClickablePoint(out point) || !window.BoundingRectangle.Contains(point)) return false;
            var hit = window.Automation.FromPoint(point);
            for (var element = hit; element != null && element.Properties.ProcessId.ValueOrDefault == process; element = element.Parent)
                if (element.Equals(target)) return true;
            return hit != null && hit.Properties.ProcessId.ValueOrDefault == process && hit.Equals(target.Parent);
        }
    }
}
