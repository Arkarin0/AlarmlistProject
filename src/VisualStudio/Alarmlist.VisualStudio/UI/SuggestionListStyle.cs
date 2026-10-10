// Created/modified by Arkarin0 under one ore more license(s).

using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace Alarmlist.VisualStudio.UI
{
    internal static class SuggestionListStyle
    {
        public static Style Create(string labelProperty, string helpProperty = null)
        {
            var style = new Style(typeof(ListBoxItem));
            style.Setters.Add(new Setter(UIElement.FocusableProperty, false));
            style.Setters.Add(new Setter(Control.IsTabStopProperty, false));
            style.Setters.Add(new Setter(AutomationProperties.NameProperty, new Binding(labelProperty)));
            style.Setters.Add(new Setter(FrameworkElement.ToolTipProperty, new Binding(helpProperty ?? labelProperty)));
            style.Setters.Add(new Setter(AutomationProperties.HelpTextProperty, new Binding(helpProperty ?? labelProperty)));
            style.Setters.Add(new Setter(Control.ForegroundProperty, new Binding(nameof(Control.Foreground)) { RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor, typeof(ListBox), 1) }));
            style.Setters.Add(new Setter(Control.BackgroundProperty, Brushes.Transparent));
            var presenter = new FrameworkElementFactory(typeof(ContentPresenter));
            presenter.SetValue(FrameworkElement.MarginProperty, new Thickness(6, 3, 6, 3));
            var border = new FrameworkElementFactory(typeof(Border));
            border.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Control.BackgroundProperty));
            border.AppendChild(presenter);
            style.Setters.Add(new Setter(Control.TemplateProperty, new ControlTemplate(typeof(ListBoxItem)) { VisualTree = border }));
            var selected = new Trigger { Property = ListBoxItem.IsSelectedProperty, Value = true };
            selected.Setters.Add(new Setter(Control.BackgroundProperty, new DynamicResourceExtension(SystemColors.HighlightBrushKey)));
            selected.Setters.Add(new Setter(Control.ForegroundProperty, new DynamicResourceExtension(SystemColors.HighlightTextBrushKey)));
            style.Triggers.Add(selected);
            return style;
        }
    }
}
