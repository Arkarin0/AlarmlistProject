// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace Alarmlist.VisualStudio.UI
{
    public partial class ParameterListControl : UserControl
    {
        private int _inputVersion;
        private ParameterListViewModel _menuOwner;
        private ParameterViewModel _menuRow;
        public ParameterListControl()
        {
            InitializeComponent();
            Unloaded += (sender, args) =>
            {
                _inputVersion++;
                var menu = (ContextMenu)Resources["ParameterRowMenu"];
                menu.IsOpen = false;
                menu.DataContext = null;
                menu.PlacementTarget = null;
            };
        }
        private ParameterListViewModel ViewModel => DataContext as ParameterListViewModel;
        private void OnListSizeChanged(object sender, SizeChangedEventArgs args) => SizeColumns();
        private void OnListScrollChanged(object sender, ScrollChangedEventArgs args)
        {
            if (args.ViewportWidthChange != 0 || args.ExtentHeightChange != 0) SizeColumns();
        }
        private void SizeColumns()
        {
            ScrollViewer scroll = Children(ParameterList).OfType<ScrollViewer>().FirstOrDefault();
            double scrollbar = scroll?.ComputedVerticalScrollBarVisibility == Visibility.Visible ? SystemParameters.VerticalScrollBarWidth : 0;
            double width = Math.Max(280, ParameterList.ActualWidth - 12 - scrollbar);
            NameColumn.Width = width * 0.4;
            ValueColumn.Width = width * 0.6;
        }
        private void OnCellFocus(object sender, KeyboardFocusChangedEventArgs args)
        {
            if (((FrameworkElement)sender).DataContext is ParameterViewModel row && ViewModel != null)
                ViewModel.Selected = row;
        }
        private void OnRowMenuOpened(object sender, RoutedEventArgs args)
        {
            var menu = (ContextMenu)sender;
            if (!((menu.PlacementTarget as FrameworkElement)?.DataContext is ParameterViewModel row)
                || ViewModel == null || !ViewModel.Rows.Contains(row))
            { menu.IsOpen = false; return; }
            // Right-click can target an unselected row. Use the ordinary selection
            // guard so a conflicting draft cannot redirect an action elsewhere.
            ViewModel.Selected = row;
            if (!ReferenceEquals(ViewModel.Selected, row)) { menu.IsOpen = false; return; }
            menu.DataContext = ViewModel;
            _menuOwner = ViewModel;
            _menuRow = row;
            _menuOwner.PropertyChanged += OnMenuSelectionChanged;
            foreach (FrameworkElement item in menu.Items.OfType<FrameworkElement>().Where(item => Equals(item.Tag, "TextEditing")))
                item.Visibility = menu.PlacementTarget is TextBox ? Visibility.Visible : Visibility.Collapsed;
        }
        private void OnMenuSelectionChanged(object sender, PropertyChangedEventArgs args)
        {
            if (ReferenceEquals(sender, _menuOwner) && args.PropertyName == nameof(ParameterListViewModel.Selected)
                && !ReferenceEquals(_menuOwner.Selected, _menuRow))
                ((ContextMenu)Resources["ParameterRowMenu"]).IsOpen = false;
        }
        private void OnRowMenuClosed(object sender, RoutedEventArgs args)
        {
            if (_menuOwner != null) _menuOwner.PropertyChanged -= OnMenuSelectionChanged;
            _menuOwner = null;
            _menuRow = null;
        }
        // These WPF event handlers only await the dispatcher after bound commands.
#pragma warning disable VSTHRD100
        private async void OnAdd(object sender, RoutedEventArgs args) => await FocusAfterCommandAsync(true);
        private async void OnAssign(object sender, RoutedEventArgs args) => await FocusAfterCommandAsync(false);
#pragma warning restore VSTHRD100
        private async Task FocusAfterCommandAsync(bool name)
        {
            ParameterListViewModel list = ViewModel;
            int version = _inputVersion;
            // Click precedes the bound command. Let it update the rows first.
            await Dispatcher.Yield(DispatcherPriority.Background);
            if (version != _inputVersion || !IsLoaded || !IsKeyboardFocusWithin || !ReferenceEquals(list, ViewModel) || list?.Selected == null) return;
            Section.IsExpanded = true;
            ParameterList.ScrollIntoView(list.Selected);
            ParameterList.UpdateLayout();
            string target = name ? list.Selected.NameAutomationName : list.Selected.ValueAutomationName;
            TextBox editor = Children(ParameterList).OfType<TextBox>().FirstOrDefault(box =>
                ReferenceEquals(box.DataContext, list.Selected) && AutomationProperties.GetName(box) == target && box.IsVisible && !box.IsReadOnly);
            if (editor != null) { editor.BringIntoView(); editor.Focus(); editor.SelectAll(); }
        }
        protected override void OnPreviewMouseDown(MouseButtonEventArgs args) { _inputVersion++; base.OnPreviewMouseDown(args); }
        protected override void OnPreviewKeyDown(KeyEventArgs args) { _inputVersion++; base.OnPreviewKeyDown(args); }
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
