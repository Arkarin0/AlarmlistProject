// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
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
        internal bool NavigateSource(int position, string expectedText)
        {
            object target = ViewModel.SelectSource(position, expectedText);
            if (target == null) return false;
            // Selection is synchronous: no delayed focus can steal later input.
            UpdateLayout();
            if (target is ParameterViewModel parameter)
            {
                ParameterListControl list = Children(this).OfType<ParameterListControl>().FirstOrDefault(control =>
                    (control.DataContext as ParameterListViewModel)?.Rows.Contains(parameter) == true);
                return list?.NavigateTo(parameter) == true;
            }
            FrameworkElement editor = Children(this).OfType<TextBox>().FirstOrDefault(item => ReferenceEquals(item.DataContext, target)
                && item.IsEnabled && item.Visibility == Visibility.Visible)
                ?? (FrameworkElement)Children(this).OfType<ComboBox>().FirstOrDefault(item => ReferenceEquals(item.DataContext, target));
            if (editor == null) return false;
            for (DependencyObject parent = editor; parent != null; parent = VisualTreeHelper.GetParent(parent))
                if (parent is Expander expander) expander.IsExpanded = true;
            UpdateLayout();
            editor.BringIntoView();
            if (editor is TextBox text) text.SelectAll();
            editor.Focus();
            return true;
        }
        private void OnFieldLostFocus(object sender, KeyboardFocusChangedEventArgs args)
        {
            // Shell toolbars live outside this view. Leaving the designer keeps
            // its draft private; moving to another designer control still commits.
            // Explicit Save/Enter and editor layout changes also commit as before.
            if (IsKeyboardFocusWithin && !Equals((args.NewFocus as FrameworkElement)?.Tag, "PreserveDraft")) ViewModel.CommitPending();
        }
        private void OnPreviewMouseDown(object sender, MouseButtonEventArgs args) => _inputVersion++;
        private void OnExpandProcedureGroups(object sender, RoutedEventArgs args)
        {
            foreach (ProcedureSectionViewModel section in ViewModel.ProcedureSections) section.IsExpanded = true;
        }
        private void OnCollapseProcedureGroups(object sender, RoutedEventArgs args)
        {
            foreach (ProcedureSectionViewModel section in ViewModel.ProcedureSections) section.IsExpanded = false;
        }
        private void OnNextProcedureMatch(object sender, RoutedEventArgs args) => MoveProcedureMatch(1);
        private void OnPreviousProcedureMatch(object sender, RoutedEventArgs args) => MoveProcedureMatch(-1);
        private void MoveProcedureMatch(int direction)
        {
            ViewModel.MoveProcedureMatch(direction);
            RevealProcedureMatch();
        }
        private void OnProcedureMatchSelected(object sender, SelectionChangedEventArgs args)
        {
            if (ViewModel.IsRefreshingProcedureSearch) return;
            if (ProcedureSearchResults.SelectedItem is ProcedureSearchMatch match)
            {
                ViewModel.SelectedProcedureMatch = match;
                RevealProcedureMatch();
            }
        }
        private void RevealProcedureMatch()
        {
            ProcedureSearchMatch match = ViewModel.SelectedProcedureMatch;
            if (match == null || !ViewModel.ProcedureMatches.Contains(match)) return;
            match.Section.IsExpanded = true;
            match.Section.ShowCleared = true;
            UpdateLayout();
            FrameworkElement target = Children(this).OfType<FrameworkElement>().FirstOrDefault(element =>
                ReferenceEquals(element.DataContext, match.Target) && element.IsVisible
                && !(element is ContentPresenter));
            target?.BringIntoView();
        }
        private void OnUnloaded(object sender, RoutedEventArgs args)
        {
            _inputVersion++;
            UnsubscribeLayout();
        }
        private void OnInheritanceActions(object sender, RoutedEventArgs args)
        {
            var button = (Button)sender;
            PrepareActionsMenu(button);
            button.ContextMenu.IsOpen = true;
        }
        private void OnActionsMenuOpening(object sender, ContextMenuEventArgs args) => PrepareActionsMenu((FrameworkElement)sender);
        private static void PrepareActionsMenu(FrameworkElement button)
        {
            button.ContextMenu.DataContext = button.DataContext;
            button.ContextMenu.PlacementTarget = button;
            button.ContextMenu.Placement = PlacementMode.Bottom;
        }
        private void OnActionsUnloaded(object sender, RoutedEventArgs args)
        {
            var menu = ((FrameworkElement)sender).ContextMenu;
            menu.IsOpen = false;
            menu.DataContext = null;
            menu.PlacementTarget = null;
        }
        // This WPF event handler only awaits the view model's guarded command and
        // UI dispatch. Recoverable command failures are handled by the view model.
#pragma warning disable VSTHRD100
        private async void OnProcedureAction(object sender, RoutedEventArgs args)
        {
            var button = (Button)sender;
            EditFileControlViewModel view = ViewModel;
            ProcedureFocusRequest previous = view.ProcedureFocus;
            int version = _inputVersion;
            TextBox source = Children(this).OfType<ExpressionTextBox>().FirstOrDefault(editor =>
                ReferenceEquals(editor.DataContext, button.DataContext));
            int start = source?.SelectionStart ?? 0, length = source?.SelectionLength ?? 0;
            // Click precedes ICommand execution. Let its source edit and bindings
            // finish before focusing the new row, as in the parameter list.
            bool newerInput = false;
            PreProcessInputEventHandler observeInput = (inputSender, inputArgs) =>
            {
                RoutedEvent routedEvent = inputArgs.StagingItem.Input.RoutedEvent;
                if (routedEvent == Keyboard.PreviewKeyDownEvent || routedEvent == Mouse.PreviewMouseDownEvent) newerInput = true;
            };
            InputManager.Current.PreProcessInput += observeInput;
#pragma warning disable VSTHRD003 // This click starts the guarded command before the dispatcher continuation.
            try { await Dispatcher.Yield(DispatcherPriority.Background); await view.PendingAnalysisAction; }
#pragma warning restore VSTHRD003
            finally { InputManager.Current.PreProcessInput -= observeInput; }
            ProcedureFocusRequest request = view.ProcedureFocus;
            if (newerInput || version != _inputVersion || !IsLoaded || !ReferenceEquals(view, ViewModel)
                || request == null || ReferenceEquals(previous, request)
                || !ReferenceEquals(request.Alarm, view.Selected?.Source)
                // Deleting the focused row can temporarily focus the native XML
                // view. The removed button is no longer loaded; newer physical
                // input anywhere in the shell still cancels this focus request.
                || (button.IsLoaded && Keyboard.FocusedElement != null && !IsKeyboardFocusWithin)) return;

            if (request.Section != null) request.Section.IsExpanded = true;
            UpdateLayout();
            if (request.Index == -2)
            {
                FrameworkElement groupTarget = request.Section == null ? NewGroupIdEditor : Children(this).OfType<ExpressionTextBox>()
                    .FirstOrDefault(editor => ReferenceEquals(editor.DataContext, request.Section.TitleField));
                groupTarget?.BringIntoView();
                groupTarget?.Focus();
                return;
            }
            ProcedureItemViewModel row = request.Index < 0 ? null : request.Section.Items.ElementAtOrDefault(request.Index);
            FrameworkElement target = row == null ? Children(this).OfType<Button>().FirstOrDefault(item =>
                ReferenceEquals(item.DataContext, request.Section) && ReferenceEquals(item.Command, request.Section.AddCommand))
                : row.IsTextEnabled ? (FrameworkElement)Children(this).OfType<ExpressionTextBox>().FirstOrDefault(item => ReferenceEquals(item.DataContext, row))
                : Children(this).OfType<ComboBox>().FirstOrDefault(item => ReferenceEquals(item.DataContext, row));
            if (target == null || !target.IsVisible || !target.IsEnabled) return;
            target.BringIntoView();
            // Prepare selection before exposing keyboard focus. Native input may
            // arrive reentrantly from Focus, so nothing may reset it afterwards.
            if (target is TextBox text)
                text.Select(Math.Min(start, text.Text.Length), Math.Min(length, text.Text.Length - Math.Min(start, text.Text.Length)));
            target.Focus();
        }

#pragma warning restore VSTHRD100
        private void OnPreviewKeyDown(object sender, KeyEventArgs args)
        {
            _inputVersion++;
            if (ProcedureSearchEditor.IsKeyboardFocusWithin || ProcedureSearchResults.IsKeyboardFocusWithin)
            {
                if (args.Key == Key.Escape) { ViewModel.ProcedureSearch = string.Empty; ProcedureSearchEditor.Focus(); args.Handled = true; return; }
                if (args.Key == Key.Enter) { MoveProcedureMatch(Keyboard.Modifiers == ModifierKeys.Shift ? -1 : 1); args.Handled = true; return; }
            }
            if (args.OriginalSource is ReferenceComboBox reference && reference.HandleChoiceKey(args.Key, Keyboard.Modifiers))
            { args.Handled = true; return; }
            if (args.OriginalSource is ExpressionTextBox expression && expression.HandleCompletionKey(args.Key, Keyboard.Modifiers))
            { args.Handled = true; return; }
            if (args.Key == Key.Escape) { ViewModel.CancelPending(); args.Handled = true; }
            else if (args.Key == Key.Enter) args.Handled = HandleReturnCommand(true, Keyboard.Modifiers);
        }
        internal bool HandleReturnCommand(bool execute, ModifierKeys modifiers)
        {
            if (!IsKeyboardFocusWithin || !(Keyboard.FocusedElement is TextBox text)) return false;
            if (ReferenceEquals(text, ProcedureSearchEditor))
            {
                if (execute) MoveProcedureMatch(modifiers == ModifierKeys.Shift ? -1 : 1);
                return true;
            }
            if (!execute || text.IsReadOnly) return true;
            _inputVersion++;
            if (text is ReferenceComboBox reference && reference.HandleChoiceKey(Key.Enter, modifiers)) return true;
            if (text is ExpressionTextBox expression && expression.HandleCompletionKey(Key.Enter, modifiers)) return true;
            if (modifiers == ModifierKeys.Shift)
            {
                if (text.AcceptsReturn) EditingCommands.EnterLineBreak.Execute(null, text);
            }
            else ViewModel.CommitPending();
            return true;
        }
        private void OnSizeChanged(object sender, SizeChangedEventArgs args)
        {
            bool narrow = ActualWidth < 600;
            ListColumn.Width = new GridLength(narrow ? 0 : 220);
            AlarmList.Visibility = narrow ? Visibility.Collapsed : Visibility.Visible;
            AlarmSelector.Visibility = narrow ? Visibility.Visible : Visibility.Collapsed;
        }
        private static IEnumerable<DependencyObject> Children(DependencyObject parent)
        {
            for (int index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(parent, index);
                yield return child;
                foreach (DependencyObject nested in Children(child)) yield return nested;
            }
        }
    }
}
