// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace Alarmlist.VisualStudio.UI
{
    public partial class ParameterListControl : UserControl
    {
        private int _inputVersion;
        public ParameterListControl()
        {
            InitializeComponent();
            Unloaded += (sender, args) =>
            {
                _inputVersion++;
                RowActionsButton.ContextMenu.IsOpen = false;
                RowActionsButton.ContextMenu.DataContext = null;
                RowActionsButton.ContextMenu.PlacementTarget = null;
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
        private void OnRowActions(object sender, RoutedEventArgs args)
        {
            var button = (Button)sender;
            button.ContextMenu.DataContext = ViewModel;
            button.ContextMenu.PlacementTarget = button;
            button.ContextMenu.Placement = PlacementMode.Bottom;
            button.ContextMenu.IsOpen = true;
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
