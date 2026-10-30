// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;

namespace Alarmlist.VisualStudio.UI
{
    public sealed class ReferenceValueButton : Button
    {
        public ReferenceValueButton()
            => ReferenceValueCommands.Configure(this, false);
    }
    public sealed class RenameValueButton : Button
    {
        public RenameValueButton() => ReferenceValueCommands.Configure(this, true);
    }
    internal static class ReferenceValueCommands
    {
        internal static readonly RoutedUICommand Find = new RoutedUICommand("Find All References", "FindReferences", typeof(ReferenceValueCommands));
        internal static readonly RoutedUICommand Rename = new RoutedUICommand("Rename", "RenameSymbol", typeof(ReferenceValueCommands));
        internal static void Configure(Button button, bool rename)
        {
            button.Content = rename ? "Rename…" : "Find All References"; button.Command = rename ? Rename : Find;
            button.SetBinding(Button.CommandParameterProperty, new Binding()); button.Tag = "PreserveDraft";
            button.Padding = new Thickness(5, 2, 5, 2); button.Margin = new Thickness(0, 3, 4, 3);
            button.HorizontalAlignment = HorizontalAlignment.Left; button.SetResourceReference(FrameworkElement.StyleProperty, typeof(Button));
            button.ToolTip = rename ? "Preview a rename of the symbol bound at this value. Apply or cancel private drafts first."
                : "Find symbols bound at this value in the solution. Apply or cancel private drafts first.";
            AutomationProperties.SetName(button, rename ? "Rename symbol" : "Find All References"); AutomationProperties.SetHelpText(button, (string)button.ToolTip);
        }
        internal static void Bind(UIElement element, Func<IReferenceValueView> current)
        {
            element.CommandBindings.Add(new CommandBinding(Find, (sender, args) =>
            {
                current()?.ReferenceActions?.Find(args.Parameter); args.Handled = true;
            }, (sender, args) => { args.CanExecute = current()?.ReferenceActions?.CanFind(args.Parameter) == true; args.Handled = true; }));
            element.CommandBindings.Add(new CommandBinding(Rename, (sender, args) =>
            {
                current()?.ReferenceActions?.Rename(args.Parameter); args.Handled = true;
            }, (sender, args) => { args.CanExecute = current()?.ReferenceActions?.CanFind(args.Parameter) == true; args.Handled = true; }));
        }
    }
}
