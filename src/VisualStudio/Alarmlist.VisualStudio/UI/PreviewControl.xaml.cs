// Created/modified by Arkarin0 under one ore more license(s).

using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;

namespace Alarmlist.VisualStudio.UI
{
    public partial class PreviewControl : UserControl
    {
        public PreviewControl()
        {
            InitializeComponent();
            DataObject.AddCopyingHandler(this, OnCopyingText);
            DataContextChanged += (sender, args) => UpdateSortArrows();
            Loaded += (sender, args) => UpdateSortArrows();
        }
        private EditFileControlViewModel View => DataContext as EditFileControlViewModel;

        private void OnTableSorting(object sender, DataGridSortingEventArgs args)
        {
            args.Handled = true;
            View?.SortAlarmTable(args.Column.SortMemberPath);
            UpdateSortArrows();
        }

        private void UpdateSortArrows()
        {
            foreach (DataGridColumn column in AlarmTable.Columns)
                column.SortDirection = column.SortMemberPath == View?.AlarmTableSortColumn
                    ? (View.AlarmTableSortDescending ? ListSortDirection.Descending : ListSortDirection.Ascending) : (ListSortDirection?)null;
        }

        private void OnTableRowsUpdated(object sender, DataTransferEventArgs args)
        {
            if (args.Property == ItemsControl.ItemsSourceProperty) UpdateSortArrows();
        }

        private void OnTableSelectionChanged(object sender, SelectionChangedEventArgs args) => View?.InvalidatePreviewActions();
        private void OnCopyRows(object sender, RoutedEventArgs args) => CopyRows();
        private void CopyRows() => View?.CopyPreviewRows(AlarmTable.SelectedItems.Cast<AlarmPreviewRow>().ToArray(), Clipboard.SetText);
        private void OnGoToSource(object sender, RoutedEventArgs args) => GoToSource();
        private void GoToSource()
        {
            DelegateCommand command = View?.SelectedPreviewRow?.GoToSourceCommand;
            if (command?.CanExecute(null) == true) command.Execute(null);
        }

        private void OnPreviewKeyDown(object sender, KeyEventArgs args)
        {
            if (!AlarmTable.IsKeyboardFocusWithin) return;
            if (args.Key == Key.C && Keyboard.Modifiers == ModifierKeys.Control) { args.Handled = true; CopyRows(); }
            else if (args.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.None) { args.Handled = true; GoToSource(); }
        }

        private void OnTableDoubleClick(object sender, MouseButtonEventArgs args)
        {
            DependencyObject source = args.OriginalSource as DependencyObject;
            while (source != null && !(source is DataGridRow))
                source = source is Visual ? VisualTreeHelper.GetParent(source) : LogicalTreeHelper.GetParent(source);
            if (source is DataGridRow) { args.Handled = true; GoToSource(); }
        }

        private void OnCopyingText(object sender, DataObjectCopyingEventArgs args)
        {
            if (!(args.OriginalSource is TextBox box) || !box.IsReadOnly) return;
            args.CancelCommand();
            string selected = box.SelectedText;
            if (selected.Length > 0) View?.StartPreviewAction(() => Clipboard.SetText(selected));
        }
    }
}
