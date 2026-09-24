// Created/modified by Arkarin0 under one ore more license(s).

using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Alarmlist.VisualStudio.UI;

namespace Alarmlist.VisualStudio
{
    public partial class EditFileControl : UserControl
    {
        internal EditFileControl(EditFileControlViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }
        private EditFileControlViewModel ViewModel => (EditFileControlViewModel)DataContext;
        private void OnFieldLostFocus(object sender, KeyboardFocusChangedEventArgs args) => ViewModel.CommitPending();
        private void OnPresenceChanged(object sender, RoutedEventArgs args) => ViewModel.CommitPending();
        private void OnPreviewKeyDown(object sender, KeyEventArgs args)
        {
            if (args.Key == Key.Escape) { ViewModel.CancelPending(); args.Handled = true; }
            else if (args.Key == Key.Enter && Keyboard.Modifiers != ModifierKeys.Shift) { ViewModel.CommitPending(); args.Handled = true; }
        }
        private void OnSizeChanged(object sender, SizeChangedEventArgs args)
        {
            bool narrow = ActualWidth < 600;
            ListColumn.Width = new GridLength(narrow ? 0 : 220);
            AlarmList.Visibility = narrow ? Visibility.Collapsed : Visibility.Visible;
            AlarmSelector.Visibility = narrow ? Visibility.Visible : Visibility.Collapsed;
        }
    }
}
