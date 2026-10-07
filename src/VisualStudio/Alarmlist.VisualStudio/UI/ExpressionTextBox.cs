// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using Microsoft.VisualStudio.PlatformUI;

namespace Alarmlist.VisualStudio.UI
{
    /// <summary>Displays evaluated text without changing the raw edit binding.</summary>
    public sealed class ExpressionTextBox : TextBox
    {
        public static readonly DependencyProperty RawTextProperty = DependencyProperty.Register(nameof(RawText), typeof(string), typeof(ExpressionTextBox),
            new FrameworkPropertyMetadata("", FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnDisplayChanged));
        public static readonly DependencyProperty ResolvedTextProperty = DependencyProperty.Register(nameof(ResolvedText), typeof(string), typeof(ExpressionTextBox), new PropertyMetadata(null, OnDisplayChanged));
        public static readonly DependencyProperty HasResolvedValueProperty = DependencyProperty.Register(nameof(HasResolvedValue), typeof(bool), typeof(ExpressionTextBox), new PropertyMetadata(false, OnDisplayChanged));
        public static readonly DependencyProperty IsExpressionEnabledProperty = DependencyProperty.Register(nameof(IsExpressionEnabled), typeof(bool), typeof(ExpressionTextBox), new PropertyMetadata(true, OnSuggestionsChanged));
        public static readonly DependencyProperty SuggestionsProperty = DependencyProperty.Register(nameof(Suggestions), typeof(IEnumerable), typeof(ExpressionTextBox), new PropertyMetadata(null, OnSuggestionsChanged));
        public static readonly DependencyProperty ValidateSuggestionsProperty = DependencyProperty.Register(nameof(ValidateSuggestions), typeof(Func<Task<bool>>), typeof(ExpressionTextBox));
        private readonly ListBox _list;
        private readonly LiveStatusTextBlock _status;
        private readonly Popup _popup;
        private bool _displayChange;
        private bool _explicitCompletion;
        private string _dismissedText;
        private int _tokenStart;
        private int _tokenEnd;
        private int _completionVersion;

        public string RawText { get => (string)GetValue(RawTextProperty); set => SetValue(RawTextProperty, value); }
        public string ResolvedText { get => (string)GetValue(ResolvedTextProperty); set => SetValue(ResolvedTextProperty, value); }
        public bool HasResolvedValue { get => (bool)GetValue(HasResolvedValueProperty); set => SetValue(HasResolvedValueProperty, value); }
        public bool IsExpressionEnabled { get => (bool)GetValue(IsExpressionEnabledProperty); set => SetValue(IsExpressionEnabledProperty, value); }
        public IEnumerable Suggestions { get => (IEnumerable)GetValue(SuggestionsProperty); set => SetValue(SuggestionsProperty, value); }
        public Func<Task<bool>> ValidateSuggestions { get => (Func<Task<bool>>)GetValue(ValidateSuggestionsProperty); set => SetValue(ValidateSuggestionsProperty, value); }
        internal Task PendingCompletion { get; private set; } = Task.CompletedTask;
        internal bool IsCompletionOpen => _popup.IsOpen;
        internal int CompletionCount => _list.Items.Count;

        public ExpressionTextBox()
        {
            _status = new LiveStatusTextBlock { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(6) };
            AutomationProperties.SetHelpText(_status, "Expression suggestions. Use arrows to choose and Enter to insert.");
            _list = new ListBox { Focusable = false, IsTabStop = false, DisplayMemberPath = nameof(PropertyCompletion.Label), MaxHeight = 220, MinWidth = 300 };
            var itemStyle = new Style(typeof(ListBoxItem));
            itemStyle.Setters.Add(new Setter(FocusableProperty, false));
            itemStyle.Setters.Add(new Setter(IsTabStopProperty, false));
            itemStyle.Setters.Add(new Setter(AutomationProperties.NameProperty, new Binding(nameof(PropertyCompletion.Label))));
            _list.ItemContainerStyle = itemStyle;
            _list.SetResourceReference(BackgroundProperty, EnvironmentColors.ComboBoxBackgroundBrushKey);
            _list.SetResourceReference(ForegroundProperty, EnvironmentColors.ComboBoxTextBrushKey);
            _list.SelectionChanged += (sender, args) => AnnounceSelection();
            _list.PreviewMouseDown += OnSuggestionMouseDown;
            var panel = new StackPanel();
            panel.Children.Add(_list);
            panel.Children.Add(_status);
            var border = new Border { Child = panel, BorderThickness = new Thickness(1), MaxWidth = 680 };
            border.SetResourceReference(BackgroundProperty, EnvironmentColors.ComboBoxBackgroundBrushKey);
            border.SetResourceReference(Border.BorderBrushProperty, EnvironmentColors.ToolWindowBorderBrushKey);
            _status.SetResourceReference(ForegroundProperty, EnvironmentColors.ComboBoxTextBrushKey);
            _status.Loaded += (sender, args) => { _status.Text = ""; AnnounceSelection(); };
            _popup = new Popup { Child = border, PlacementTarget = this, Placement = PlacementMode.Bottom, StaysOpen = true, AllowsTransparency = false };
            Unloaded += (sender, args) => CloseCompletion();
        }

        private static void OnDisplayChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args) => ((ExpressionTextBox)sender).RefreshDisplay();
        private static void OnSuggestionsChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args) => ((ExpressionTextBox)sender).RefreshCompletion();

        private void RefreshDisplay()
        {
            string display = !IsKeyboardFocusWithin && HasResolvedValue ? ResolvedText ?? "" : RawText ?? "";
            if (Text == display) return;
            int start = SelectionStart;
            int length = SelectionLength;
            _displayChange = true;
            try { Text = display; Select(Math.Min(start, display.Length), Math.Min(length, display.Length - Math.Min(start, display.Length))); }
            finally { _displayChange = false; }
        }

        protected override void OnGotKeyboardFocus(KeyboardFocusChangedEventArgs args)
        {
            base.OnGotKeyboardFocus(args);
            RefreshDisplay();
        }

        protected override void OnLostKeyboardFocus(KeyboardFocusChangedEventArgs args)
        {
            CloseCompletion();
            base.OnLostKeyboardFocus(args);
            RefreshDisplay();
        }

        protected override void OnTextChanged(TextChangedEventArgs args)
        {
            base.OnTextChanged(args);
            if (_displayChange) return;
            SetCurrentValue(RawTextProperty, Text);
            RefreshCompletion();
        }

        protected override void OnSelectionChanged(RoutedEventArgs args)
        {
            base.OnSelectionChanged(args);
            if (!_displayChange) RefreshCompletion();
        }

        internal bool HandleCompletionKey(Key key, ModifierKeys modifiers)
        {
            if (key == Key.Space && modifiers == ModifierKeys.Control)
            {
                _explicitCompletion = true;
                _dismissedText = null;
                RefreshCompletion();
                return IsCompletionOpen || !PendingCompletion.IsCompleted;
            }
            if (!IsCompletionOpen)
            {
                if (PendingCompletion.IsCompleted) return false;
                if (key == Key.Escape) { _dismissedText = RawText; CloseCompletion(); return true; }
                return key == Key.Enter || key == Key.Tab;
            }
            if (key == Key.Escape) { _dismissedText = RawText; CloseCompletion(); return true; }
            if (key == Key.Enter || key == Key.Tab) { AcceptCompletion(); return true; }
            if (key != Key.Down && key != Key.Up) return false;
            _list.SelectedIndex = (_list.SelectedIndex + (key == Key.Down ? 1 : _list.Items.Count - 1)) % _list.Items.Count;
            _list.ScrollIntoView(_list.SelectedItem);
            return true;
        }

        private void RefreshCompletion()
        {
            if (_popup == null) return;
            if (!IsLoaded || !IsKeyboardFocusWithin || !IsExpressionEnabled || IsReadOnly || Suggestions == null || _dismissedText == RawText)
            { CloseCompletion(); return; }
            string raw = RawText ?? "";
            int caret = Math.Min(CaretIndex, raw.Length);
            _tokenStart = -1;
            for (int i = 0; i < caret; i++)
            {
                if (raw[i] != '$' || i + 1 >= caret) continue;
                if (raw[i + 1] == '$') { i++; continue; }
                if (raw[i + 1] == '(') { _tokenStart = i; i++; }
            }
            string prefix = _tokenStart < 0 ? null : raw.Substring(_tokenStart + 2, caret - _tokenStart - 2);
            if (prefix != null && !IsPrefix(prefix)) prefix = null;
            if (prefix == null && !_explicitCompletion) { CloseCompletion(); return; }
            if (prefix == null) { _tokenStart = SelectionStart; _tokenEnd = SelectionStart + SelectionLength; prefix = ""; }
            else
            {
                _tokenEnd = caret;
                while (_tokenEnd < raw.Length && IsTokenCharacter(raw[_tokenEnd])) _tokenEnd++;
                if (_tokenEnd < raw.Length && raw[_tokenEnd] == ')') _tokenEnd++;
            }
            string selectedName = (_list.SelectedItem as PropertyCompletion)?.Name;
            PropertyCompletion[] matches = FindMatches(Suggestions, prefix);
            if (matches.Length == 0) { CloseCompletion(); return; }
            if (matches.Any(match => match.IsLocalization))
            {
                _popup.IsOpen = false;
                PendingCompletion = ShowValidatedCompletionAsync(matches, selectedName, raw, caret, Suggestions, ++_completionVersion);
                return;
            }
            ShowCompletion(matches, selectedName);
        }

        private static bool IsTokenCharacter(char character) => char.IsLetterOrDigit(character) || character == '_' || character == '.' || character == '-' || character == ':';
        private static bool IsPrefix(string prefix) => prefix.StartsWith("loc:", StringComparison.Ordinal)
            ? prefix.Substring(4).All(character => IsTokenCharacter(character) && character != ':')
            : prefix.All(character => char.IsLetterOrDigit(character) || character == '_');

        private async Task ShowValidatedCompletionAsync(PropertyCompletion[] matches, string selectedName, string raw, int caret, IEnumerable suggestions, int version)
        {
            if (ValidateSuggestions == null || !await ValidateSuggestions()) return;
            if (version != _completionVersion || !IsLoaded || !IsKeyboardFocusWithin || RawText != raw || CaretIndex != caret
                || !ReferenceEquals(Suggestions, suggestions)) return;
            ShowCompletion(matches, selectedName);
        }

        private void ShowCompletion(PropertyCompletion[] matches, string selectedName)
        {
            _list.ItemsSource = BindingCollection.Create(matches);
            _list.SelectedIndex = Math.Max(0, Array.FindIndex(matches, property => property.Name == selectedName));
            _popup.IsOpen = true;
            AnnounceSelection();
        }

        internal static PropertyCompletion[] FindMatches(IEnumerable suggestions, string prefix)
            => suggestions.OfType<PropertyCompletion>().Where(property => property.Name.StartsWith(prefix,
                property.IsLocalization ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase)).ToArray();

        private void AnnounceSelection()
        {
            if (_list.SelectedItem is PropertyCompletion selected)
                _status.Text = _list.Items.Count + " expressions. " + selected.Label + ". Enter inserts; Escape closes.";
        }

        private void OnSuggestionMouseDown(object sender, MouseButtonEventArgs args)
        {
            var container = ItemsControl.ContainerFromElement(_list, args.OriginalSource as DependencyObject) as ListBoxItem;
            if (container?.Content is PropertyCompletion property)
            { _list.SelectedItem = property; AcceptCompletion(); args.Handled = true; }
        }

        private void AcceptCompletion()
        {
            if (!(_list.SelectedItem is PropertyCompletion property)) return;
            if (property.IsLocalization)
            {
                PendingCompletion = AcceptValidatedCompletionAsync(property, RawText, Suggestions, _tokenStart, _tokenEnd);
                return;
            }
            InsertCompletion(property, _tokenStart, _tokenEnd);
        }

        private async Task AcceptValidatedCompletionAsync(PropertyCompletion property, string raw, IEnumerable suggestions, int start, int end)
        {
            int caret = CaretIndex;
            CloseCompletion();
            int version = _completionVersion;
            if (ValidateSuggestions == null || !await ValidateSuggestions()) return;
            if (version == _completionVersion && IsLoaded && IsKeyboardFocusWithin && RawText == raw && CaretIndex == caret
                && ReferenceEquals(Suggestions, suggestions)) InsertCompletion(property, start, end);
        }

        private void InsertCompletion(PropertyCompletion property, int start, int end)
        {
            string raw = RawText ?? "";
            string insertion = "$(" + property.Name + ")";
            int caret = start + insertion.Length;
            string result = raw.Substring(0, start) + insertion + raw.Substring(end);
            CloseCompletion();
            _dismissedText = result;
            SetCurrentValue(RawTextProperty, result);
            CaretIndex = caret;
        }

        private void CloseCompletion()
        {
            _completionVersion++;
            _explicitCompletion = false;
            if (_popup == null) return;
            _popup.IsOpen = false;
            _list.ItemsSource = null;
            _status.Text = "";
        }
    }
}
