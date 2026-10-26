// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Alarmlist.VisualStudio.UI
{
    // Move the real header, retaining its focus, automation and collapse behavior.
    // Its layout slot stays in place; its rendered position stays inside its group.
    internal sealed class StickyCollectionHeader
    {
        public static readonly DependencyProperty IsEnabledProperty = DependencyProperty.RegisterAttached(
            "IsEnabled", typeof(bool), typeof(StickyCollectionHeader), new PropertyMetadata(false, OnEnabled));
        private static readonly DependencyProperty TrackerProperty = DependencyProperty.RegisterAttached(
            "Tracker", typeof(StickyCollectionHeader), typeof(StickyCollectionHeader));
        public static bool GetIsEnabled(DependencyObject value) => (bool)value.GetValue(IsEnabledProperty);
        public static void SetIsEnabled(DependencyObject value, bool enabled) => value.SetValue(IsEnabledProperty, enabled);

        private readonly Expander _group;
        private readonly TranslateTransform _offset = new TranslateTransform();
        private FrameworkElement _header;
        private ScrollViewer _scroll;
        private ScrollContentPresenter _viewport;
        private bool _adjustingFocus;

        private StickyCollectionHeader(Expander group)
        {
            _group = group;
            group.Loaded += OnLoaded;
            group.Unloaded += OnUnloaded;
            if (group.IsLoaded) Attach();
        }
        private static void OnEnabled(DependencyObject value, DependencyPropertyChangedEventArgs args)
        {
            if (!(value is Expander group)) return;
            if (group.GetValue(TrackerProperty) is StickyCollectionHeader previous)
            {
                previous.Detach();
                group.Loaded -= previous.OnLoaded;
                group.Unloaded -= previous.OnUnloaded;
                group.ClearValue(TrackerProperty);
            }
            if ((bool)args.NewValue) group.SetValue(TrackerProperty, new StickyCollectionHeader(group));
        }
        private void OnLoaded(object sender, RoutedEventArgs args) => Attach();
        private void OnUnloaded(object sender, RoutedEventArgs args) => Detach();
        private void Attach()
        {
            Detach();
            _group.ApplyTemplate();
            _header = _group.Template.FindName("CollectionHeader", _group) as FrameworkElement;
            for (DependencyObject parent = VisualTreeHelper.GetParent(_group); parent != null; parent = VisualTreeHelper.GetParent(parent))
            {
                if (_viewport == null && parent is ScrollContentPresenter viewport) _viewport = viewport;
                if (parent is ScrollViewer scroll) { _scroll = scroll; break; }
            }
            if (_header == null || _viewport == null || _scroll == null) { Detach(); return; }
            _header.RenderTransform = _offset;
            _scroll.ScrollChanged += OnScroll;
            _group.LayoutUpdated += OnLayout;
            _group.GotKeyboardFocus += OnFocus;
            Update();
        }
        private void Detach()
        {
            if (_scroll != null) _scroll.ScrollChanged -= OnScroll;
            _group.LayoutUpdated -= OnLayout;
            _group.GotKeyboardFocus -= OnFocus;
            _offset.Y = 0;
            if (_header != null) _header.ClearValue(UIElement.RenderTransformProperty);
            _header = null; _scroll = null; _viewport = null;
        }
        private void OnScroll(object sender, ScrollChangedEventArgs args)
        {
            if (ReferenceEquals(args.OriginalSource, _scroll)) Update();
        }
        private void OnLayout(object sender, EventArgs args) => Update();
        private void Update()
        {
            if (_viewport == null || !_group.IsVisible) return;
            double top = _group.TranslatePoint(new Point(), _viewport).Y;
            double next = _group.IsExpanded ? Math.Max(0, Math.Min(-top, _group.ActualHeight - _header.ActualHeight - 2)) : 0;
            if (Math.Abs(_offset.Y - next) > 0.1) _offset.Y = next;
        }
        private void OnFocus(object sender, KeyboardFocusChangedEventArgs args)
        {
            if (_adjustingFocus || _viewport == null || !(args.NewFocus is FrameworkElement target)
                || _header.IsKeyboardFocusWithin) return;
            Update();
            double top = target.TranslatePoint(new Point(), _viewport).Y;
            double headerBottom = _header.TranslatePoint(new Point(0, _header.ActualHeight), _viewport).Y;
            if (_offset.Y > 0 && top < headerBottom && headerBottom > 0)
            {
                _adjustingFocus = true;
                try { _scroll.ScrollToVerticalOffset(Math.Max(0, _scroll.VerticalOffset - (headerBottom - top) - 4)); }
                finally { _adjustingFocus = false; }
            }
        }
    }
}
