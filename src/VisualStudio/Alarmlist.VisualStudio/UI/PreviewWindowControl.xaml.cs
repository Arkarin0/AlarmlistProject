// Created/modified by Arkarin0 under one ore more license(s).

using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace Alarmlist.VisualStudio.UI
{
    public partial class PreviewWindowControl : UserControl
    {
        public PreviewWindowControl()
        {
            InitializeComponent();
            Unloaded += (sender, args) =>
            {
                ScopeSelector.IsDropDownOpen = false; LanguageSelector.IsDropDownOpen = false;
            };
        }
        internal PreviewControl PreviewView => Preview;
    }

    public sealed class PreviewSelectorComboBox : ComboBox
    {
        private Popup _popup;
        public override void OnApplyTemplate()
        {
            if (_popup?.Child is FrameworkElement previous) previous.Loaded -= OnPopupChildLoaded;
            base.OnApplyTemplate();
            // Items have their own data contexts. The popup root needs no preview
            // model; prevent its disconnected inheritance cache from retaining
            // that model after scope changes or window hide.
            _popup = GetTemplateChild("PART_Popup") as Popup;
            if (_popup?.Child is FrameworkElement child) child.Loaded += OnPopupChildLoaded;
        }

        private void OnPopupChildLoaded(object sender, RoutedEventArgs args)
        {
            if (_popup?.Child != null && PresentationSource.FromVisual(_popup.Child)?.RootVisual is FrameworkElement root)
                root.SetValue(DataContextProperty, null);
        }
    }
}
