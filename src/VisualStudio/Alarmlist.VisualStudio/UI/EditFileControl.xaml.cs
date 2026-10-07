// Created/modified by Arkarin0 under one ore more license(s).

using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Alarmlist.VisualStudio.UI;

namespace Alarmlist.VisualStudio
{
    public partial class EditFileControl : UserControl
    {
        private int _inputVersion;

        internal EditFileControl(EditFileControlViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }
        private EditFileControlViewModel ViewModel => (EditFileControlViewModel)DataContext;
        private void OnFieldLostFocus(object sender, KeyboardFocusChangedEventArgs args)
        {
            // Selecting a preview culture must not commit the ALMX draft.
            if (!PreviewCultureSelector.IsKeyboardFocusWithin) ViewModel.CommitPending();
        }
        private void OnPresenceChanged(object sender, RoutedEventArgs args) => ViewModel.CommitPending();
        private void OnPreviewMouseDown(object sender, MouseButtonEventArgs args) => _inputVersion++;
        private void OnUnloaded(object sender, RoutedEventArgs args) => _inputVersion++;
        // This WPF event handler only awaits the view model's guarded command and
        // UI dispatch. Recoverable command failures are handled by the view model.
#pragma warning disable VSTHRD100
        private async void OnOverrideLocally(object sender, RoutedEventArgs args)
        {
            var button = (Button)sender;
            var field = (AlarmFieldViewModel)button.DataContext;
            var editor = (TextBox)button.Tag;
            EditFileControlViewModel viewModel = ViewModel;
            AlarmViewModel selected = viewModel.Selected;
            int inputVersion = _inputVersion;

            // WPF executes the bound command after Click. Wait for that command's
            // validation and for the cleared previews to finish changing layout.
            await Dispatcher.Yield(DispatcherPriority.Background);
#pragma warning disable VSTHRD003
            await viewModel.PendingAnalysisAction;
#pragma warning restore VSTHRD003
            await Dispatcher.Yield(DispatcherPriority.Background);
            if (inputVersion != _inputVersion || !IsLoaded || !IsKeyboardFocusWithin
                || !ReferenceEquals(DataContext, viewModel) || !ReferenceEquals(viewModel.Selected, selected)
                || !ReferenceEquals(editor.DataContext, field) || !field.HasChanges
                || !editor.IsVisible || !editor.IsEnabled) return;

            editor.Focus();
            editor.CaretIndex = editor.Text.Length;
            editor.BringIntoView();
        }
#pragma warning restore VSTHRD100
        private void OnPreviewKeyDown(object sender, KeyEventArgs args)
        {
            _inputVersion++;
            if (args.OriginalSource is ExpressionTextBox expression && expression.HandleCompletionKey(args.Key, Keyboard.Modifiers))
            { args.Handled = true; return; }
            if (args.Key == Key.Escape) { ViewModel.CancelPending(); args.Handled = true; }
            else if (args.Key == Key.Enter && Keyboard.Modifiers != ModifierKeys.Shift && args.OriginalSource is TextBox text && !text.IsReadOnly)
            { ViewModel.CommitPending(); args.Handled = true; }
        }
        private void OnSizeChanged(object sender, SizeChangedEventArgs args)
        {
            bool narrow = ActualWidth < 600;
            DockPanel.SetDock(CultureToolbar, narrow ? Dock.Top : Dock.Right);
            CultureToolbar.Margin = new Thickness(narrow ? 0 : 12, 0, 0, 4);
            ListColumn.Width = new GridLength(narrow ? 0 : 220);
            AlarmList.Visibility = narrow ? Visibility.Collapsed : Visibility.Visible;
            AlarmSelector.Visibility = narrow ? Visibility.Visible : Visibility.Collapsed;
        }
    }
}
