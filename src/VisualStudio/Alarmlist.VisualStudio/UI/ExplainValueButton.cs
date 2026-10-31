// Created/modified by Arkarin0 under one ore more license(s).
using System;
using System.Windows;
using System.Windows.Input;

namespace Alarmlist.VisualStudio.UI
{
    internal static class ValueExplanationCommands
    {
        internal static readonly RoutedUICommand Explain = new RoutedUICommand("Explain this value", "ExplainValue", typeof(ValueExplanationCommands));
        internal static void Bind(UIElement element, Func<IValueExplanationView> current, Action show = null)
        {
            element.CommandBindings.Add(new CommandBinding(Explain, (sender, args) =>
            {
                var view = current();
                if (view?.CanExplainValue(args.Parameter) == true) { view.ExplainValue(args.Parameter); show?.Invoke(); }
                args.Handled = true;
            }, (sender, args) => { args.CanExecute = current()?.CanExplainValue(args.Parameter) == true; args.Handled = true; }));
        }
    }
}
