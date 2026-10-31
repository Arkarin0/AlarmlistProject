// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Windows;
using System.Windows.Input;

namespace Alarmlist.VisualStudio.UI
{
    internal static class ReferenceValueCommands
    {
        internal static readonly RoutedUICommand Find = new RoutedUICommand("Find All References", "FindReferences", typeof(ReferenceValueCommands));
        internal static readonly RoutedUICommand Rename = new RoutedUICommand("Rename", "RenameSymbol", typeof(ReferenceValueCommands));
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
