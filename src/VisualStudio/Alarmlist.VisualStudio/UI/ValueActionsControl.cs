// Created/modified by Arkarin0 under one ore more license(s).

using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Data;

namespace Alarmlist.VisualStudio.UI
{
    public sealed class ValueActionsControl : StackPanel
    {
        public static readonly DependencyProperty ActionNameProperty = DependencyProperty.Register(nameof(ActionName), typeof(string),
            typeof(ValueActionsControl), new PropertyMetadata("Explain this value"));
        public string ActionName { get => (string)GetValue(ActionNameProperty); set => SetValue(ActionNameProperty, value); }
        public static readonly DependencyProperty ValueNameProperty = DependencyProperty.Register(nameof(ValueName), typeof(string),
            typeof(ValueActionsControl), new PropertyMetadata("this value"));
        public string ValueName { get => (string)GetValue(ValueNameProperty); set => SetValue(ValueNameProperty, value); }
        public ValueActionsControl()
        {
            var explain = new ExplainValueButton();
            explain.SetBinding(ExplainValueButton.ActionNameProperty, new Binding(nameof(ActionName)) { Source = this });
            var references = new ReferenceValueButton();
            references.SetBinding(AutomationProperties.NameProperty, new Binding(nameof(ValueName)) { Source = this, StringFormat = "Find references to {0}" });
            var rename = new RenameValueButton();
            rename.SetBinding(AutomationProperties.NameProperty, new Binding(nameof(ValueName)) { Source = this, StringFormat = "Rename symbol for {0}" });
            Children.Add(explain); Children.Add(references); Children.Add(rename);
        }
    }
}
