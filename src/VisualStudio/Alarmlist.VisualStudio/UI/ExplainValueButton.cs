// Created/modified by Arkarin0 under one ore more license(s).
using System;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;

namespace Alarmlist.VisualStudio.UI
{
    public sealed class ExplainValueButton : Button
    {
        public static readonly DependencyProperty ActionNameProperty = DependencyProperty.Register(nameof(ActionName), typeof(string),
            typeof(ExplainValueButton), new PropertyMetadata("Explain this value"));
        public string ActionName { get => (string)GetValue(ActionNameProperty); set => SetValue(ActionNameProperty, value); }
        public ExplainValueButton()
        {
            Content = "Explain this value"; Command = ValueExplanationCommands.Explain;
            SetBinding(CommandParameterProperty, new Binding()); Tag = "PreserveDraft";
            Padding = new Thickness(5, 2, 5, 2); Margin = new Thickness(0, 3, 4, 3);
            HorizontalAlignment = HorizontalAlignment.Left; SetResourceReference(StyleProperty, typeof(Button));
            ToolTip = "Show a read-only compiler explanation in Alarm Summary. Commit or cancel private drafts first.";
            AutomationProperties.SetName(this, "Explain this value"); AutomationProperties.SetHelpText(this, (string)ToolTip);
        }
        protected override AutomationPeer OnCreateAutomationPeer() => new ExplanationButtonPeer(this);
        private sealed class ExplanationButtonPeer : ButtonAutomationPeer
        {
            internal ExplanationButtonPeer(ExplainValueButton owner) : base(owner) { }
            protected override string GetNameCore() => ((ExplainValueButton)Owner).ActionName;
        }
    }
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
