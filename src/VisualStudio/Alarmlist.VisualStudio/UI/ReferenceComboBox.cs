// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using Microsoft.VisualStudio.PlatformUI;

namespace Alarmlist.VisualStudio.UI
{
    // One literal editor with a suggestion popup. Keeping selection out of Text
    // prevents filtering/arrow navigation from replacing a private reference draft.
    public sealed class ReferenceComboBox : TextBox
    {
        public static readonly DependencyProperty ChoicesProperty = DependencyProperty.Register(nameof(Choices), typeof(IEnumerable), typeof(ReferenceComboBox), new PropertyMetadata(null, OnChoicesChanged));
        public static readonly DependencyProperty ApplyChoiceProperty = DependencyProperty.Register(nameof(ApplyChoice), typeof(Func<string, Func<bool>, Task<bool>>), typeof(ReferenceComboBox));
        public IEnumerable Choices { get => (IEnumerable)GetValue(ChoicesProperty); set => SetValue(ChoicesProperty, value); }
        public Func<string, Func<bool>, Task<bool>> ApplyChoice { get => (Func<string, Func<bool>, Task<bool>>)GetValue(ApplyChoiceProperty); set => SetValue(ApplyChoiceProperty, value); }
        private readonly ListBox _list;
        private readonly LiveStatusTextBlock _status;
        private readonly Popup _popup;
        private Button _toggle;
        private int _version;
        private bool _accepting;
        internal Task PendingSelection { get; private set; } = Task.CompletedTask;
        internal bool IsDropDownOpen => _popup.IsOpen;
        internal int MatchCount => _list.Items.Count;

        public ReferenceComboBox()
        {
            _list = new ListBox { Focusable = false, IsTabStop = false, MaxHeight = 220, MinWidth = 280, DisplayMemberPath = nameof(ReferenceChoice.Label) };
            ScrollViewer.SetCanContentScroll(_list, true);
            VirtualizingPanel.SetIsVirtualizing(_list, true);
            VirtualizingPanel.SetVirtualizationMode(_list, VirtualizationMode.Recycling);
            _list.ItemContainerStyle = SuggestionListStyle.Create(nameof(ReferenceChoice.Label), nameof(ReferenceChoice.HelpText));
            _list.SetResourceReference(BackgroundProperty, EnvironmentColors.ComboBoxBackgroundBrushKey);
            _list.SetResourceReference(ForegroundProperty, EnvironmentColors.ComboBoxTextBrushKey);
            _list.PreviewMouseDown += OnChoiceMouseDown;
            _list.SelectionChanged += (sender, args) => Announce();
            _status = new LiveStatusTextBlock { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(6) };
            _status.SetResourceReference(ForegroundProperty, EnvironmentColors.ComboBoxTextBrushKey);
            var panel = new StackPanel();
            panel.Children.Add(_list); panel.Children.Add(_status);
            var border = new Border { Child = panel, BorderThickness = new Thickness(1), MaxWidth = 800 };
            border.SetResourceReference(BackgroundProperty, EnvironmentColors.ComboBoxBackgroundBrushKey);
            border.SetResourceReference(Border.BorderBrushProperty, EnvironmentColors.ToolWindowBorderBrushKey);
            _popup = new Popup { Child = border, PlacementTarget = this, Placement = PlacementMode.Bottom, StaysOpen = true };
            Unloaded += (sender, args) => CloseChoices();
            DataContextChanged += (sender, args) => CloseChoices();
        }
        public override void OnApplyTemplate()
        {
            if (_toggle != null) _toggle.Click -= OnToggle;
            base.OnApplyTemplate();
            _toggle = GetTemplateChild("PART_ReferenceToggle") as Button;
            if (_toggle != null) _toggle.Click += OnToggle;
        }
        private void OnToggle(object sender, RoutedEventArgs args)
        {
            if (IsDropDownOpen) CloseChoices();
            else { Focus(); OpenChoices(); }
        }
        private static void OnChoicesChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
        {
            var control = (ReferenceComboBox)sender;
            control._version++;
            if (control.IsDropDownOpen) control.OpenChoices();
        }
        protected override void OnTextChanged(TextChangedEventArgs args)
        {
            _version++;
            base.OnTextChanged(args);
            if (_popup != null && !_accepting && IsKeyboardFocusWithin) OpenChoices();
        }
        protected override void OnLostKeyboardFocus(KeyboardFocusChangedEventArgs args)
        { CloseChoices(); base.OnLostKeyboardFocus(args); }
        protected override void OnPreviewKeyDown(KeyEventArgs args)
        {
            if (!args.Handled)
            {
                _version++;
                if (HandleChoiceKey(args.Key, Keyboard.Modifiers)) args.Handled = true;
            }
            base.OnPreviewKeyDown(args);
        }
        protected override void OnPreviewMouseDown(MouseButtonEventArgs args)
        { _version++; base.OnPreviewMouseDown(args); }
        internal void OpenChoices()
        {
            if (!IsLoaded || !IsEnabled || IsReadOnly) return;
            string selected = (_list.SelectedItem as ReferenceChoice)?.Identifier;
            ReferenceChoice[] matches = FindMatches(Choices, Text);
            _list.ItemsSource = BindingCollection.Create(matches);
            int exact = Array.FindIndex(matches, item => item.Identifier == Text);
            _list.SelectedIndex = Text.Length == 0 ? -1 : exact >= 0 ? exact : Math.Max(0, Array.FindIndex(matches, item => item.Identifier == selected));
            bool wasOpen = _popup.IsOpen;
            _popup.IsOpen = true;
            if (!wasOpen) AnnounceExpanded(false, true);
            Announce();
        }
        internal static ReferenceChoice[] FindMatches(IEnumerable choices, string query) => choices?.OfType<ReferenceChoice>()
            .Where(item => item.Identifier.IndexOf(query ?? "", StringComparison.OrdinalIgnoreCase) >= 0
                || item.Name.IndexOf(query ?? "", StringComparison.OrdinalIgnoreCase) >= 0
                || item.OriginLabel.IndexOf(query ?? "", StringComparison.OrdinalIgnoreCase) >= 0).ToArray() ?? Array.Empty<ReferenceChoice>();
        internal void CloseChoices()
        {
            _version++;
            if (_popup == null) return;
            bool wasOpen = _popup.IsOpen;
            _popup.IsOpen = false;
            if (wasOpen) AnnounceExpanded(true, false);
            _list.ItemsSource = null;
            _status.Text = "";
        }
        private void AnnounceExpanded(bool oldValue, bool newValue) => UIElementAutomationPeer.FromElement(this)?.RaisePropertyChangedEvent(
            ExpandCollapsePatternIdentifiers.ExpandCollapseStateProperty,
            oldValue ? ExpandCollapseState.Expanded : ExpandCollapseState.Collapsed,
            newValue ? ExpandCollapseState.Expanded : ExpandCollapseState.Collapsed);
        private void Announce()
        {
            if (_status == null) return;
            _status.Text = _list.Items.Count == 0 ? "No matching references. Enter keeps the literal value."
                : _list.Items.Count + " references. " + (_list.SelectedItem as ReferenceChoice)?.Label
                    + ". Arrows choose; Enter applies; Escape closes to keep typing a literal value.";
        }
        internal bool HandleChoiceKey(Key key, ModifierKeys modifiers)
        {
            if (key == Key.F4 || (key == Key.Down && modifiers == ModifierKeys.Alt))
            { if (IsDropDownOpen) CloseChoices(); else OpenChoices(); return true; }
            if (!PendingSelection.IsCompleted && key == Key.Enter) return true;
            if (!PendingSelection.IsCompleted && key == Key.Escape) { CloseChoices(); return true; }
            if (!IsDropDownOpen) return false;
            if (key == Key.Escape) { CloseChoices(); return true; }
            if (key == Key.Enter)
            {
                if (_list.SelectedItem is ReferenceChoice) { AcceptChoice(); return true; }
                CloseChoices(); return false;
            }
            if (key == Key.Tab) { CloseChoices(); return false; }
            if ((key == Key.Down || key == Key.Up) && modifiers == ModifierKeys.None)
            {
                if (_list.Items.Count != 0)
                {
                    _list.SelectedIndex = Math.Max(0, Math.Min(_list.Items.Count - 1, _list.SelectedIndex + (key == Key.Down ? 1 : -1)));
                    _list.ScrollIntoView(_list.SelectedItem);
                }
                return true;
            }
            return false;
        }
        private void OnChoiceMouseDown(object sender, MouseButtonEventArgs args)
        {
            var row = ItemsControl.ContainerFromElement(_list, args.OriginalSource as DependencyObject) as ListBoxItem;
            if (!(row?.Content is ReferenceChoice)) return;
            _list.SelectedItem = row.Content;
            AcceptChoice(); args.Handled = true;
        }
        private void AcceptChoice()
        {
            var choice = _list.SelectedItem as ReferenceChoice;
            if (choice == null || ApplyChoice == null) return;
            CloseChoices();
            int version = _version;
            object context = DataContext;
            PendingSelection = ApplyAsync(choice.Identifier, version, context);
        }
        private async Task ApplyAsync(string identifier, int version, object context)
        {
            _accepting = true;
            try
            {
                if (await ApplyChoice(identifier, () => version == _version && IsLoaded && IsKeyboardFocusWithin && ReferenceEquals(context, DataContext)))
                    if (IsLoaded && IsKeyboardFocusWithin && ReferenceEquals(context, DataContext)) CaretIndex = Text.Length;
            }
            finally { _accepting = false; }
        }
        protected override AutomationPeer OnCreateAutomationPeer() => new ReferenceComboBoxPeer(this);
        private sealed class ReferenceComboBoxPeer : TextBoxAutomationPeer, IExpandCollapseProvider
        {
            private readonly ReferenceComboBox _owner;
            public ReferenceComboBoxPeer(ReferenceComboBox owner) : base(owner) { _owner = owner; }
            protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.ComboBox;
            protected override string GetClassNameCore() => nameof(ReferenceComboBox);
            public override object GetPattern(PatternInterface patternInterface) => patternInterface == PatternInterface.ExpandCollapse ? this : base.GetPattern(patternInterface);
            public ExpandCollapseState ExpandCollapseState => _owner.IsDropDownOpen ? ExpandCollapseState.Expanded : ExpandCollapseState.Collapsed;
            public void Expand() => _owner.OpenChoices();
            public void Collapse() => _owner.CloseChoices();
        }
    }
}
