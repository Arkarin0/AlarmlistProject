// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.VisualStudio.Shell;

namespace Alarmlist.VisualStudio.UI
{
    /// <summary>Attaches value actions without changing a control's editing menu or popup DataContext.</summary>
    public static class ValueContextMenu
    {
        public static readonly DependencyProperty TargetProperty = DependencyProperty.RegisterAttached("Target", typeof(object),
            typeof(ValueContextMenu), new PropertyMetadata(null, OnTargetChanged));
        public static readonly DependencyProperty ReferenceOnlyProperty = DependencyProperty.RegisterAttached("ReferenceOnly", typeof(bool),
            typeof(ValueContextMenu), new PropertyMetadata(false));
        private static readonly DependencyProperty SessionProperty = DependencyProperty.RegisterAttached("Session", typeof(Session), typeof(ValueContextMenu));
        public static readonly DependencyProperty DescriptionProperty = DependencyProperty.RegisterAttached("Description", typeof(string),
            typeof(ValueContextMenu), new PropertyMetadata("this value"));
        public static readonly DependencyProperty ExplanationNameProperty = DependencyProperty.RegisterAttached("ExplanationName", typeof(string),
            typeof(ValueContextMenu), new PropertyMetadata("Explain this value"));
        public static string GetDescription(DependencyObject element) => (string)element.GetValue(DescriptionProperty);
        public static void SetDescription(DependencyObject element, string value) => element.SetValue(DescriptionProperty, value);
        public static string GetExplanationName(DependencyObject element) => (string)element.GetValue(ExplanationNameProperty);
        public static void SetExplanationName(DependencyObject element, string value) => element.SetValue(ExplanationNameProperty, value);
        public static object GetTarget(DependencyObject element) => element.GetValue(TargetProperty);
        public static void SetTarget(DependencyObject element, object value) => element.SetValue(TargetProperty, value);
        public static bool GetReferenceOnly(DependencyObject element) => (bool)element.GetValue(ReferenceOnlyProperty);
        public static void SetReferenceOnly(DependencyObject element, bool value) => element.SetValue(ReferenceOnlyProperty, value);

        private static void OnTargetChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
        {
            if (!(sender is FrameworkElement element)) return;
            element.ContextMenuOpening -= OnOpening;
            element.Loaded -= OnLoaded;
            if (args.NewValue == null) return;
            element.ContextMenuOpening += OnOpening;
            // A non-null menu makes keyboard context-menu invocation available on
            // labels as well as editors. Existing menus keep their own bindings.
            if (element.IsLoaded) EnsureMenu(element);
            else element.Loaded += OnLoaded;
        }

        private static void OnLoaded(object sender, RoutedEventArgs args) => EnsureMenu((FrameworkElement)sender);
        private static void EnsureMenu(FrameworkElement element)
        {
            // A stretched heading otherwise hit-tests only its text glyphs.
            // Its whole marked value area should own pointer context menus.
            if (element is TextBlock text && text.Background == null)
                text.SetCurrentValue(TextBlock.BackgroundProperty, Brushes.Transparent);
            if (element.ContextMenu == null) element.ContextMenu = NewMenu();
            ContextMenuService.SetShowOnDisabled(element, true);
            DescribeValue(element, element);
        }

        private static void DescribeValue(FrameworkElement anchor, DependencyObject element)
        {
            if (!ReferenceEquals(anchor, element) && GetTarget(element) != null) return;
            if (element is FrameworkElement value && (value is TextBoxBase || value is TextBlock || value is Label))
            {
                // TextBox's class handler consumes the opening event when it
                // creates its built-in menu. Supply a real editing menu before
                // that event, so the marked parent can append value actions.
                if (value is TextBoxBase && value.ContextMenu == null && FindOwner(anchor) != null)
                {
                    value.ContextMenu = NewMenu();
                    ContextMenuService.SetShowOnDisabled(value, true);
                }
                value.SetBinding(AutomationProperties.ItemStatusProperty, new Binding
                {
                    Source = anchor, Path = new PropertyPath("(0)", DescriptionProperty),
                    StringFormat = "Value actions for {0}. Press Shift+F10 or the Menu key."
                });
            }
            for (int index = 0; index < VisualTreeHelper.GetChildrenCount(element); index++)
                DescribeValue(anchor, VisualTreeHelper.GetChild(element, index));
        }

        private static ContextMenu NewMenu()
        {
            var menu = new ContextMenu { Tag = "PreserveDraft" };
            menu.SetResourceReference(FrameworkElement.StyleProperty, VsResourceKeys.ContextMenuStyleKey);
            return menu;
        }

        private static void OnOpening(object sender, ContextMenuEventArgs args)
        {
            if (args.Handled || !(sender is FrameworkElement anchor)) return;
            var path = Ancestors(args.OriginalSource as DependencyObject).OfType<FrameworkElement>().ToArray();
            // Only the closest explicitly marked value owns this invocation.
            if (path.FirstOrDefault(element => GetTarget(element) != null) != anchor) return;
            if (FindOwner(anchor) == null) return; // Preserve standalone/native editing menus.
            args.Handled = true;
            Open(path.FirstOrDefault() ?? anchor);
        }

        internal static ContextMenu Open(FrameworkElement invoked)
        {
            var path = Ancestors(invoked).OfType<FrameworkElement>().ToArray();
            FrameworkElement anchor = path.FirstOrDefault(element => GetTarget(element) != null);
            if (anchor == null) return null;
            FrameworkElement owner = FindOwner(anchor);
            object target = GetTarget(anchor);
            if (owner == null || target == null) return null;
            FrameworkElement source = path.FirstOrDefault(element => element is TextBoxBase) ?? path.FirstOrDefault() ?? anchor;
            FrameworkElement menuOwner = path.FirstOrDefault(element => element.ContextMenu != null) ?? anchor;
            ContextMenu menu = menuOwner.ContextMenu;
            if (menu.IsOpen && menu.GetValue(SessionProperty) is Session active && active.Matches(anchor, target)) return menu;
            (menu.GetValue(SessionProperty) as Session)?.End();
            if (menu.Items.Count == 0 && source is TextBoxBase)
            {
                AddTextAction(menu, "Cut", ApplicationCommands.Cut, source);
                AddTextAction(menu, "Copy", ApplicationCommands.Copy, source);
                AddTextAction(menu, "Paste", ApplicationCommands.Paste, source);
                AddTextAction(menu, "Select all", ApplicationCommands.SelectAll, source);
            }
            menu.SetValue(SessionProperty, new Session(anchor, source, owner, target, menu));
            // Open explicitly so a child TextBox's built-in menu cannot replace
            // the marked value's menu after the routed opening event returns.
            menu.PlacementTarget = source;
            menu.DataContext = menuOwner.DataContext;
            menu.IsOpen = true;
            return menu;
        }

        private static void AddTextAction(ContextMenu menu, string header, RoutedCommand command, IInputElement target)
            => menu.Items.Add(new MenuItem { Header = header, Command = command, CommandTarget = target, Tag = "PreserveDraft" });

        private static FrameworkElement FindOwner(FrameworkElement anchor) => Ancestors(anchor).OfType<FrameworkElement>()
            .FirstOrDefault(element => element.DataContext is IReferenceValueView || element.DataContext is IValueExplanationView);

        internal static IEnumerable<DependencyObject> Ancestors(DependencyObject value)
        {
            while (value != null)
            {
                yield return value;
                value = value is Visual || value is System.Windows.Media.Media3D.Visual3D
                    ? VisualTreeHelper.GetParent(value) : LogicalTreeHelper.GetParent(value);
            }
        }

        private sealed class Session
        {
            private readonly FrameworkElement _anchor, _source, _owner;
            private readonly object _target, _ownerView;
            private readonly ContextMenu _menu;
            private readonly List<FrameworkElement> _items = new List<FrameworkElement>();
            private readonly INotifyPropertyChanged[] _observed;
            private readonly int? _referenceVersion;
            private bool _invalid;
            private bool _ended;

            internal Session(FrameworkElement anchor, FrameworkElement source, FrameworkElement owner, object target, ContextMenu menu)
            {
                _anchor = anchor; _source = source; _owner = owner; _target = target; _ownerView = owner.DataContext; _menu = menu;
                _referenceVersion = (_ownerView as IReferenceValueView)?.ReferenceActions?.ContextVersion;
                if (menu.Items.Count != 0) Add(new Separator());
                if (!GetReferenceOnly(anchor)) AddAction("Explain this value", ValueExplanationCommands.Explain);
                AddAction("Find All References", ReferenceValueCommands.Find);
                AddAction("Rename…", ReferenceValueCommands.Rename);
                _observed = new[] { _target as INotifyPropertyChanged, _ownerView as INotifyPropertyChanged,
                    (_ownerView as IReferenceValueView)?.ReferenceActions }.Where(value => value != null).Distinct().ToArray();
                foreach (var value in _observed) value.PropertyChanged += OnChanged;
                _anchor.DataContextChanged += OnContextChanged;
                DependencyPropertyDescriptor.FromProperty(TargetProperty, _anchor.GetType()).AddValueChanged(_anchor, OnTargetReplaced);
                _owner.DataContextChanged += OnContextChanged;
                _anchor.Unloaded += OnUnloaded;
                _menu.Closed += OnClosed;
                _menu.Tag = "PreserveDraft";
                CommandManager.InvalidateRequerySuggested();
            }

            private void Add(FrameworkElement item) { _items.Add(item); _menu.Items.Add(item); }
            private void AddAction(string header, RoutedCommand command)
            {
                var action = new MenuItem { Header = header, Tag = "PreserveDraft",
                    Command = new DelegateCommand(() =>
                    {
                        if (Current() && command.CanExecute(_target, _owner)) command.Execute(_target, _owner);
                    }, () => Current() && command.CanExecute(_target, _owner)) };
                AutomationProperties.SetName(action, command == ValueExplanationCommands.Explain ? GetExplanationName(_anchor)
                    : command == ReferenceValueCommands.Find ? "Find references to " + GetDescription(_anchor)
                    : (GetReferenceOnly(_anchor) ? "Rename " : "Rename symbol for ") + GetDescription(_anchor));
                Add(action);
            }
            private bool Current() => !_invalid && _anchor.IsLoaded && _source.IsLoaded
                && ReferenceEquals(GetTarget(_anchor), _target) && ReferenceEquals(_owner.DataContext, _ownerView)
                && (_ownerView as IReferenceValueView)?.ReferenceActions?.ContextVersion == _referenceVersion;
            internal bool Matches(FrameworkElement anchor, object target) => ReferenceEquals(_anchor, anchor)
                && ReferenceEquals(_target, target) && Current();
            private void OnChanged(object sender, PropertyChangedEventArgs args) => Invalidate();
            private void OnContextChanged(object sender, DependencyPropertyChangedEventArgs args) => Invalidate();
            private void OnTargetReplaced(object sender, EventArgs args) => Invalidate();
            private void OnUnloaded(object sender, RoutedEventArgs args) => Invalidate();
            private void Invalidate() { _invalid = true; _menu.IsOpen = false; }
            private void OnClosed(object sender, RoutedEventArgs args)
            { if (ReferenceEquals(args.Source, _menu)) End(); }
            internal void End()
            {
                if (_ended) return;
                _ended = true;
                if (ReferenceEquals(_menu.GetValue(SessionProperty), this)) _menu.ClearValue(SessionProperty);
                foreach (var value in _observed) value.PropertyChanged -= OnChanged;
                _anchor.DataContextChanged -= OnContextChanged;
                DependencyPropertyDescriptor.FromProperty(TargetProperty, _anchor.GetType()).RemoveValueChanged(_anchor, OnTargetReplaced);
                _owner.DataContextChanged -= OnContextChanged;
                _anchor.Unloaded -= OnUnloaded;
                _menu.Closed -= OnClosed;
                foreach (var item in _items) _menu.Items.Remove(item);
                // Command execution can follow popup closure in WPF. Removed
                // items retain their frozen target only for that invocation.
                // Keep native text-command bindings alive through WPF's closing
                // click, then release the popup's reference to the editor.
#pragma warning disable VSTHRD001 // Already on this WPF dispatcher; run after the closing command.
                _ = _menu.Dispatcher.BeginInvoke(new Action(() =>
                {
                    if (!_menu.IsOpen && ReferenceEquals(_menu.PlacementTarget, _source)) _menu.PlacementTarget = null;
                }), System.Windows.Threading.DispatcherPriority.ContextIdle);
#pragma warning restore VSTHRD001
            }
        }
    }
}
