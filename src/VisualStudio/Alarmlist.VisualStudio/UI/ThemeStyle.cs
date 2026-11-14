// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Data;
using System.Windows.Threading;
using Microsoft.VisualStudio.PlatformUI;

namespace Alarmlist.VisualStudio.UI
{
    // Keep the IDE's actual control templates and state triggers. Only our
    // layout/behavior overlay is composed above them; isolated hosts use the
    // explicitly supplied fallback. Resource expressions keep theme changes live.
    public static class ThemeStyle
    {
        public static readonly DependencyProperty KeyProperty = DependencyProperty.RegisterAttached("Key", typeof(object),
            typeof(ThemeStyle), new PropertyMetadata(null, OnKeyChanged));
        public static readonly DependencyProperty FallbackProperty = DependencyProperty.RegisterAttached("Fallback", typeof(Style),
            typeof(ThemeStyle), new PropertyMetadata(null));
        public static readonly DependencyProperty DefaultsProperty = DependencyProperty.RegisterAttached("Defaults", typeof(ResourceDictionary),
            typeof(ThemeStyle), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.Inherits, OnResolvedChanged));
        public static readonly DependencyProperty SelectionPaletteProperty = DependencyProperty.RegisterAttached("SelectionPalette", typeof(bool),
            typeof(ThemeStyle), new PropertyMetadata(false, OnSelectionPaletteChanged));
        private static readonly DependencyProperty ActiveBackgroundProperty = PaletteBrush("ActiveBackground", SystemColors.HighlightBrushKey);
        private static readonly DependencyProperty ActiveTextProperty = PaletteBrush("ActiveText", SystemColors.HighlightTextBrushKey);
        private static readonly DependencyProperty InactiveBackgroundProperty = PaletteBrush("InactiveBackground", SystemColors.InactiveSelectionHighlightBrushKey);
        private static readonly DependencyProperty InactiveTextProperty = PaletteBrush("InactiveText", SystemColors.InactiveSelectionHighlightTextBrushKey);
        private static readonly DependencyProperty ResolvedProperty = DependencyProperty.RegisterAttached("Resolved", typeof(Style),
            typeof(ThemeStyle), new PropertyMetadata(null, OnResolvedChanged));
        private static readonly DependencyProperty ObservedStyleProperty = DependencyProperty.RegisterAttached("ObservedStyle", typeof(Style),
            typeof(ThemeStyle), new PropertyMetadata(null, OnResolvedChanged));
        private static readonly DependencyProperty StateProperty = DependencyProperty.RegisterAttached("State", typeof(State), typeof(ThemeStyle));
        public static object GetKey(DependencyObject element) => element.GetValue(KeyProperty);
        public static void SetKey(DependencyObject element, object value) => element.SetValue(KeyProperty, value);
        public static Style GetFallback(DependencyObject element) => (Style)element.GetValue(FallbackProperty);
        public static void SetFallback(DependencyObject element, Style value) => element.SetValue(FallbackProperty, value);
        public static ResourceDictionary GetDefaults(DependencyObject element) => (ResourceDictionary)element.GetValue(DefaultsProperty);
        public static void SetDefaults(DependencyObject element, ResourceDictionary value) => element.SetValue(DefaultsProperty, value);
        public static bool GetSelectionPalette(DependencyObject element) => (bool)element.GetValue(SelectionPaletteProperty);
        public static void SetSelectionPalette(DependencyObject element, bool value) => element.SetValue(SelectionPaletteProperty, value);

        // Stock WPF selection templates read system brush keys directly on their
        // borders. Resolve the VS pairs in each owning row's resource scope and
        // alias the live brushes; a shared Freezable in Style.Resources has no
        // unambiguous row context for a deferred color lookup.
        private static DependencyProperty PaletteBrush(string name, object systemKey)
            => DependencyProperty.RegisterAttached(name, typeof(System.Windows.Media.Brush), typeof(ThemeStyle),
                new PropertyMetadata(null, (sender, args) =>
                {
                    var element = (FrameworkElement)sender;
                    if (args.NewValue == null) element.Resources.Remove(systemKey);
                    else element.Resources[systemKey] = args.NewValue;
                }));
        private static void OnSelectionPaletteChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
        {
            if (!(sender is FrameworkElement element)) return;
            var pairs = new[] {
                new KeyValuePair<DependencyProperty, object>(ActiveBackgroundProperty, TreeViewColors.SelectedItemActiveBrushKey),
                new KeyValuePair<DependencyProperty, object>(ActiveTextProperty, TreeViewColors.SelectedItemActiveTextBrushKey),
                new KeyValuePair<DependencyProperty, object>(InactiveBackgroundProperty, TreeViewColors.SelectedItemInactiveBrushKey),
                new KeyValuePair<DependencyProperty, object>(InactiveTextProperty, TreeViewColors.SelectedItemInactiveTextBrushKey) };
            foreach (var pair in pairs)
                if ((bool)args.NewValue) element.SetResourceReference(pair.Key, pair.Value);
                else element.ClearValue(pair.Key);
        }

        private static void OnKeyChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
        {
            if (!(sender is FrameworkElement element)) return;
            (element.GetValue(StateProperty) as State)?.Stop();
            element.ClearValue(StateProperty);
            if (args.NewValue == null) return;
            var state = new State(element);
            element.SetValue(StateProperty, state);
            BindingOperations.SetBinding(element, ObservedStyleProperty, new Binding(nameof(FrameworkElement.Style)) { Source = element });
            element.SetResourceReference(ResolvedProperty, args.NewValue);
            state.Queue();
        }
        private static void OnResolvedChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
            => (sender.GetValue(StateProperty) as State)?.Queue();

        private sealed class State
        {
            private readonly FrameworkElement _element;
            private Style _overlay, _applied, _native;
            private DispatcherOperation _pending;
            private bool _stopped;
            internal State(FrameworkElement element)
            {
                _element = element;
                element.Loaded += OnLoaded; element.Unloaded += OnUnloaded;
            }
            private void OnLoaded(object sender, RoutedEventArgs args) => Queue();
            private void OnUnloaded(object sender, RoutedEventArgs args) { _pending?.Abort(); _pending = null; }
            internal void Queue()
            {
                if (_stopped || !_element.IsLoaded || _pending != null) return;
#pragma warning disable VSTHRD001 // Resource/style callbacks already run on this element's dispatcher.
                _pending = _element.Dispatcher.BeginInvoke(new Action(Apply), DispatcherPriority.Loaded);
#pragma warning restore VSTHRD001
            }
            private void Apply()
            {
                _pending = null;
                if (_stopped || !_element.IsLoaded) return;
                RefreshAutomation();
                _overlay = _overlay ?? _element.Style;
                var native = _element.GetValue(ResolvedProperty) as Style;
                // The public default dictionary exposes implicit styles by control
                // type even when a tool-window scope has no named dialog styles.
                // Read it directly so our own implicit layout overlays cannot win
                // that lookup or replace the native control's state templates.
                var defaults = GetDefaults(_element);
                if (native == null && defaults != null)
                    for (var type = _element.GetType(); type != null && native == null; type = type.BaseType)
                        native = defaults[type] as Style;
                if (ReferenceEquals(native, _native) && (_applied == null || ReferenceEquals(_element.Style, _applied))) return;
                _native = native;
                if (native == null)
                {
                    if (_applied != null) _element.SetCurrentValue(FrameworkElement.StyleProperty, _overlay);
                    _applied = null; return;
                }
                if (!native.TargetType.IsAssignableFrom(_element.GetType()))
                    throw new InvalidOperationException("The native theme style does not match " + _element.GetType().Name + ".");
                var combined = new Style(_overlay?.TargetType ?? _element.GetType(), native);
                var chain = new Stack<Style>();
                var fallback = GetFallback(_element);
                for (var style = _overlay; style != null && !ReferenceEquals(style, fallback); style = style.BasedOn) chain.Push(style);
                while (chain.Count != 0)
                {
                    var style = chain.Pop();
                    foreach (SetterBase setter in style.Setters) combined.Setters.Add(setter);
                    foreach (TriggerBase trigger in style.Triggers) combined.Triggers.Add(trigger);
                    foreach (object key in style.Resources.Keys) combined.Resources[key] = style.Resources[key];
                }
                _applied = combined;
                _element.SetCurrentValue(FrameworkElement.StyleProperty, combined);
                RefreshAutomation();
            }
            private void RefreshAutomation()
            {
                // Recreated native templates must not leave a virtualized row's
                // existing data-item peer caching an empty child collection.
                // Refresh existing peers only; do not create an automation tree.
                var peer = UIElementAutomationPeer.FromElement(_element);
                peer?.InvalidatePeer();
                peer?.EventsSource?.InvalidatePeer();
            }
            internal void Stop()
            {
                _stopped = true; _pending?.Abort(); _pending = null;
                _element.Loaded -= OnLoaded; _element.Unloaded -= OnUnloaded;
                _element.ClearValue(ResolvedProperty);
                BindingOperations.ClearBinding(_element, ObservedStyleProperty);
                if (_applied != null && ReferenceEquals(_element.Style, _applied))
                    _element.SetCurrentValue(FrameworkElement.StyleProperty, _overlay);
            }
        }
    }
}
