// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using Microsoft.VisualStudio.PlatformUI;

namespace Alarmlist.VisualStudio.UI
{
    internal sealed class SplitEditorControl : UserControl, IDisposable
    {
        private readonly EditorLayoutViewModel _layout;
        private readonly Grid _panes = new Grid();
        private readonly FrameworkElement _designer;
        private readonly FrameworkElement _source;
        private readonly GridSplitter _splitter = new GridSplitter { Background = System.Windows.Media.Brushes.Gray };
        public SplitEditorControl(EditorLayoutViewModel layout, FrameworkElement designer, FrameworkElement source)
        {
            _layout = layout;
            _designer = designer;
            _source = source;
            SetResourceReference(BackgroundProperty, EnvironmentColors.ToolWindowBackgroundBrushKey);
            SetResourceReference(ForegroundProperty, EnvironmentColors.ToolWindowTextBrushKey);
            DockPanel root = new DockPanel();
            WrapPanel toolbar = new WrapPanel { Margin = new Thickness(8, 4, 8, 4), DataContext = layout };
            foreach (string mode in new[] { "Designer", "Split", "XML" })
            {
                RadioButton button = new RadioButton { Content = mode, GroupName = "EditorMode", Margin = new Thickness(5, 3, 12, 3) };
                button.SetResourceReference(ForegroundProperty, EnvironmentColors.ToolWindowTextBrushKey);
                button.SetBinding(ButtonBase.CommandProperty, new Binding(mode == "XML" ? "XmlCommand" : mode + "Command"));
                button.SetBinding(ToggleButton.IsCheckedProperty, new Binding(mode == "XML" ? "IsXml" : "Is" + mode) { Mode = BindingMode.OneWay });
                toolbar.Children.Add(button);
            }
            CheckBox orientation = new CheckBox { Content = "Side by side", Margin = new Thickness(8, 3, 12, 3) };
            orientation.SetResourceReference(ForegroundProperty, EnvironmentColors.ToolWindowTextBrushKey);
            orientation.SetBinding(ToggleButton.IsCheckedProperty, new Binding("SideBySide"));
            toolbar.Children.Add(orientation);
            Button swap = new Button { Content = "Swap panes", Padding = new Thickness(8, 2, 8, 2) };
            swap.SetBinding(ButtonBase.CommandProperty, new Binding("SwapCommand"));
            toolbar.Children.Add(swap);
            DockPanel.SetDock(toolbar, Dock.Bottom);
            root.Children.Add(toolbar);
            root.Children.Add(_panes);
            _panes.Children.Add(_designer);
            _panes.Children.Add(_source);
            _panes.Children.Add(_splitter);
            Content = root;
            _splitter.DragCompleted += OnDragCompleted;
            _layout.PropertyChanged += OnLayoutChanged;
            ArrangePanes();
        }
        private void OnLayoutChanged(object sender, PropertyChangedEventArgs args)
        {
            if (args.PropertyName == nameof(EditorLayoutViewModel.Mode) || args.PropertyName == nameof(EditorLayoutViewModel.SideBySide) || args.PropertyName == nameof(EditorLayoutViewModel.Swapped)) ArrangePanes();
        }
        private void ArrangePanes()
        {
            _panes.RowDefinitions.Clear();
            _panes.ColumnDefinitions.Clear();
            foreach (FrameworkElement element in new[] { _designer, _source, _splitter }) { Grid.SetRow(element, 0); Grid.SetColumn(element, 0); }
            _designer.Visibility = _layout.IsXml ? Visibility.Collapsed : Visibility.Visible;
            _source.Visibility = _layout.IsDesigner ? Visibility.Collapsed : Visibility.Visible;
            _splitter.Visibility = _layout.IsSplit ? Visibility.Visible : Visibility.Collapsed;
            if (!_layout.IsSplit) return;
            FrameworkElement first = _layout.Swapped ? _source : _designer;
            FrameworkElement second = _layout.Swapped ? _designer : _source;
            if (_layout.SideBySide)
            {
                _panes.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(_layout.Proportion, GridUnitType.Star), MinWidth = 60 });
                _panes.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(5) });
                _panes.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1 - _layout.Proportion, GridUnitType.Star), MinWidth = 60 });
                Grid.SetColumn(first, 0); Grid.SetColumn(_splitter, 1); Grid.SetColumn(second, 2);
                _splitter.ResizeDirection = GridResizeDirection.Columns;
            }
            else
            {
                _panes.RowDefinitions.Add(new RowDefinition { Height = new GridLength(_layout.Proportion, GridUnitType.Star), MinHeight = 60 });
                _panes.RowDefinitions.Add(new RowDefinition { Height = new GridLength(5) });
                _panes.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1 - _layout.Proportion, GridUnitType.Star), MinHeight = 60 });
                Grid.SetRow(first, 0); Grid.SetRow(_splitter, 1); Grid.SetRow(second, 2);
                _splitter.ResizeDirection = GridResizeDirection.Rows;
            }
            _splitter.HorizontalAlignment = HorizontalAlignment.Stretch;
            _splitter.VerticalAlignment = VerticalAlignment.Stretch;
            _splitter.ResizeBehavior = GridResizeBehavior.PreviousAndNext;
        }
        private void OnDragCompleted(object sender, DragCompletedEventArgs args)
        {
            double total = _layout.SideBySide ? _panes.ActualWidth - 5 : _panes.ActualHeight - 5;
            if (total > 0) _layout.Proportion = (_layout.SideBySide ? _panes.ColumnDefinitions[0].ActualWidth : _panes.RowDefinitions[0].ActualHeight) / total;
        }
        public void Dispose() { _layout.PropertyChanged -= OnLayoutChanged; _splitter.DragCompleted -= OnDragCompleted; }
    }
}
