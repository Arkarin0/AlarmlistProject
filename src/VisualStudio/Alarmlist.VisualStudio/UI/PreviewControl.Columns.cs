// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;

namespace Alarmlist.VisualStudio.UI
{
    public partial class PreviewControl
    {
        private PreviewColumnLayout _columnLayout;
        private bool _applyingColumns;
        private void BindColumnLayout()
        {
            DetachColumnLayout();
            _columnLayout = View?.PreviewColumns;
            if (_columnLayout != null) _columnLayout.Changed += OnColumnLayoutChanged;
            ApplyColumns();
        }
        private void DetachColumnLayout()
        {
            if (_columnLayout != null) _columnLayout.Changed -= OnColumnLayoutChanged;
            _columnLayout = null;
        }
        private void OnColumnLayoutChanged(object sender, EventArgs args) => ApplyColumns();
        private void ApplyColumns()
        {
            if (_columnLayout == null || _applyingColumns) return;
            _applyingColumns = true;
            try
            {
                if (!AlarmTable.Columns.Select(column => column.SortMemberPath).OrderBy(id => id)
                    .SequenceEqual(_columnLayout.Columns.Select(column => column.Id).OrderBy(id => id)))
                {
                    AlarmTable.Columns.Clear();
                    foreach (PreviewColumn preference in _columnLayout.Columns)
                    {
                        DataGridColumn column;
                        if (preference.Id.StartsWith("Collection:", StringComparison.Ordinal))
                        {
                            var content = new FrameworkElementFactory(typeof(ContentControl));
                            content.SetBinding(ContentControl.ContentProperty, new Binding("CollectionCells[" + preference.Id.Substring(11) + "]"));
                            content.SetValue(ContentControl.ContentTemplateProperty, FindResource("CollectionPreviewCell"));
                            column = new DataGridTemplateColumn { CellTemplate = new DataTemplate { VisualTree = content } };
                            var headerStyle = new Style(typeof(DataGridColumnHeader), (Style)FindResource(typeof(DataGridColumnHeader)));
                            headerStyle.Setters.Add(new Setter(ToolTipProperty, "Drag to reorder or resize. Collection order is preserved; sorting is unavailable."));
                            column.HeaderStyle = headerStyle;
                        }
                        else column = new DataGridTextColumn
                        {
                            Binding = new Binding("[" + preference.Id + "]"),
                            ElementStyle = (Style)FindResource("ReportCellText")
                        };
                        column.Header = preference.Label; column.SortMemberPath = preference.Id;
                        column.Width = new DataGridLength(preference.Width); column.CanUserSort = preference.CanSort;
                        AlarmTable.Columns.Add(column);
                    }
                }
                foreach (PreviewColumn preference in _columnLayout.OrderedColumns)
                {
                    DataGridColumn column = AlarmTable.Columns.Single(item => item.SortMemberPath == preference.Id);
                    column.Header = preference.Label;
                    column.DisplayIndex = preference.Order;
                    column.Visibility = preference.IsVisible ? Visibility.Visible : Visibility.Collapsed;
                    if (Math.Abs(column.Width.Value - preference.Width) > 0.1) column.Width = new DataGridLength(preference.Width);
                }
                UpdateSortArrows();
            }
            finally { _applyingColumns = false; }
        }
        private void OnColumnReordered(object sender, DataGridColumnEventArgs args)
        {
            if (!_applyingColumns) _columnLayout?.SetOrder(AlarmTable.Columns.OrderBy(column => column.DisplayIndex).Select(column => column.SortMemberPath));
        }
        private void OnColumnResizeCompleted(object sender, DragCompletedEventArgs args)
        {
            if (!(args.OriginalSource is Thumb thumb) || (thumb.Name != "PART_LeftHeaderGripper" && thumb.Name != "PART_RightHeaderGripper")) return;
            if (!_applyingColumns && !args.Canceled)
                _columnLayout?.SetWidths(AlarmTable.Columns.Where(column => column.Visibility == Visibility.Visible).ToDictionary(column => column.SortMemberPath, column => column.ActualWidth));
        }
        private void OnOpenColumns(object sender, RoutedEventArgs args)
        {
            ColumnsChooser.IsExpanded = true;
            ColumnsChooser.Focus();
        }
    }
}
