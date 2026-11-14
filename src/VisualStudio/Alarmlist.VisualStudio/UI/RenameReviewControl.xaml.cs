// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Windows.Controls;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace Alarmlist.VisualStudio.UI
{
    public partial class RenameReviewControl : UserControl
    {
        private DispatcherOperation _focus;
        private ScrollViewer ChangesScroll => FindScroll(ChangesList);
        public RenameReviewControl()
        {
            InitializeComponent();
            Loaded += (sender, args) => { Dispatcher.VerifyAccess(); FocusName(); };
            DataContextChanged += (sender, args) => { Dispatcher.VerifyAccess(); FocusName(); };
            Unloaded += (sender, args) => { _focus?.Abort(); _focus = null; };
        }
        private void FocusName()
        {
            Dispatcher.VerifyAccess(); _focus?.Abort(); _focus = null;
            var expected = DataContext as RenameReviewViewModel;
            if (expected?.IsExpanded != true) return;
#pragma warning disable VSTHRD001 // Already on this WPF dispatcher; wait for Loaded layout/bindings, not a thread switch.
            _focus = Dispatcher.BeginInvoke(new Action(() =>
            {
                _focus = null;
                if (IsLoaded && NewNameBox.IsVisible && ReferenceEquals(DataContext, expected) && expected.IsExpanded && expected.CanEditName)
                { NewNameBox.Focus(); NewNameBox.SelectAll(); }
            }), DispatcherPriority.Loaded);
#pragma warning restore VSTHRD001
        }
        private static ScrollViewer FindScroll(DependencyObject element)
        {
            if (element is ScrollViewer scroll) return scroll;
            for (int index = 0; index < VisualTreeHelper.GetChildrenCount(element); index++)
            {
                var found = FindScroll(VisualTreeHelper.GetChild(element, index));
                if (found != null) return found;
            }
            return null;
        }
        private void OnPreviewTextMouseWheel(object sender, MouseWheelEventArgs args)
        {
            if (Keyboard.Modifiers != ModifierKeys.None || ChangesScroll is not ScrollViewer scroll) return;
            args.Handled = true;
            // Let the standard list viewport apply system wheel/page settings;
            // a readonly TextBox must not consume scrolling in its own viewer.
            scroll.RaiseEvent(new MouseWheelEventArgs(args.MouseDevice, args.Timestamp, args.Delta)
            { RoutedEvent = Mouse.MouseWheelEvent, Source = scroll });
        }
        private void OnPreviewTextKeyDown(object sender, KeyEventArgs args)
        {
            if (Keyboard.Modifiers != ModifierKeys.None || ChangesScroll is not ScrollViewer scroll) return;
            if (args.Key == Key.PageDown) scroll.PageDown();
            else if (args.Key == Key.PageUp) scroll.PageUp();
            else return;
            args.Handled = true;
        }
    }
}
