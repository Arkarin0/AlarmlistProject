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
            ValueExplanationCommands.Bind(this, () => DataContext as IValueExplanationView, ShowExplanation);
            ReferenceValueCommands.Bind(this, () => DataContext as IReferenceValueView);
            DataObject.AddCopyingHandler(this, OnCopyingText);
            DataContextChanged += (sender, args) => { UpdateSortArrows(); BindColumnLayout(); };
            Loaded += (sender, args) => { UpdateSortArrows(); BindColumnLayout(); };
            Unloaded += (sender, args) => { AlarmTable.ContextMenu.IsOpen = false; DetachColumnLayout(); };
            AlarmTable.AddHandler(System.Windows.Controls.Primitives.Thumb.DragCompletedEvent,
                new System.Windows.Controls.Primitives.DragCompletedEventHandler(OnColumnResizeCompleted), handledEventsToo: true);
        }
        private IPreviewView View => DataContext as IPreviewView;
        internal void ShowAlarmTable() => PreviewTabs.SelectedIndex = 0;
        internal void ShowExplanation()
        {
            PreviewTabs.SelectedIndex = 1;
            if (DataContext is IValueExplanationView view) view.Explanation.IsExpanded = true;
            ExplanationSection.BringIntoView();
        }
        internal int SelectedTabIndex { get => PreviewTabs.SelectedIndex; set => PreviewTabs.SelectedIndex = value; }

        private void OnPreviewToolbarSizeChanged(object sender, SizeChangedEventArgs args)
        {
            // Reposition the existing controls so resizing preserves filter text
            // and focus while leaving enough room to type in a narrow tool window.
            bool narrow = args.NewSize.Width < 700;
            Grid.SetRow(PreviewTableActions, narrow ? 1 : 0);
            Grid.SetColumn(PreviewTableActions, narrow ? 0 : 1);
            Grid.SetColumnSpan(PreviewTableActions, narrow ? 2 : 1);
            PreviewTableActions.Margin = narrow ? new Thickness(0, 6, 0, 0) : new Thickness(8, 0, 0, 0);
        }

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
            if (args.OriginalSource is Button) return;
            if (args.Key == Key.C && Keyboard.Modifiers == ModifierKeys.Control) { args.Handled = true; CopyRows(); }
            else if (args.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.None) { args.Handled = true; GoToSource(); }
        }

        private void OnTableDoubleClick(object sender, MouseButtonEventArgs args)
        {
            DependencyObject source = args.OriginalSource as DependencyObject;
            while (source != null && !(source is DataGridRow))
            {
                if (source is Button) return;
                source = source is Visual ? VisualTreeHelper.GetParent(source) : LogicalTreeHelper.GetParent(source);
            }
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
