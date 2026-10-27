// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
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
        public static readonly DependencyProperty ArgumentBindingsProperty = DependencyProperty.Register(nameof(ArgumentBindings), typeof(IEnumerable), typeof(ExpressionTextBox), new PropertyMetadata(null, OnSuggestionsChanged));
        public static readonly DependencyProperty ValidateSuggestionsProperty = DependencyProperty.Register(nameof(ValidateSuggestions), typeof(Func<Task<bool>>), typeof(ExpressionTextBox));
        private readonly ListBox _list;
        private readonly LiveStatusTextBlock _status;
        private readonly Popup _popup;
        private bool _displayChange;
        private bool _writingRaw;
        private bool _explicitCompletion;
        private string _dismissedText;
        private int _tokenStart;
        private int _tokenEnd;
        private int _completionVersion;
        private int _pendingCompletionVersion;
        private string _completionIntentText;
        private int _completionIntentCaret;
        private int _completionIntentSelection;

        public string RawText { get => (string)GetValue(RawTextProperty); set => SetValue(RawTextProperty, value); }
        public string ResolvedText { get => (string)GetValue(ResolvedTextProperty); set => SetValue(ResolvedTextProperty, value); }
        public bool HasResolvedValue { get => (bool)GetValue(HasResolvedValueProperty); set => SetValue(HasResolvedValueProperty, value); }
        public bool IsExpressionEnabled { get => (bool)GetValue(IsExpressionEnabledProperty); set => SetValue(IsExpressionEnabledProperty, value); }
        public IEnumerable Suggestions { get => (IEnumerable)GetValue(SuggestionsProperty); set => SetValue(SuggestionsProperty, value); }
        public IEnumerable ArgumentBindings { get => (IEnumerable)GetValue(ArgumentBindingsProperty); set => SetValue(ArgumentBindingsProperty, value); }
        public Func<Task<bool>> ValidateSuggestions { get => (Func<Task<bool>>)GetValue(ValidateSuggestionsProperty); set => SetValue(ValidateSuggestionsProperty, value); }
        internal Task PendingCompletion { get; private set; } = Task.CompletedTask;
        // Obsolete validation can still finish in the background, but must not
        // consume commit keys after its completion context has been closed.
        private bool IsCompletionPending => _pendingCompletionVersion == _completionVersion && !PendingCompletion.IsCompleted;
        internal bool IsCompletionOpen => _popup.IsOpen;
        internal int CompletionCount => _list.Items.Count;

        public ExpressionTextBox()
        {
            _status = new LiveStatusTextBlock { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(6) };
            AutomationProperties.SetHelpText(_status, "Expression suggestions. Use arrows to choose and Enter to insert.");
            _list = new ListBox { Focusable = false, IsTabStop = false, DisplayMemberPath = nameof(PropertyCompletion.Label), MaxHeight = 220, MinWidth = 300 };
            _list.ItemContainerStyle = SuggestionListStyle.Create(nameof(PropertyCompletion.Label));
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
            Unloaded += (sender, args) => { _completionIntentText = null; CloseCompletion(); };
        }

        private static void OnDisplayChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args) => ((ExpressionTextBox)sender).RefreshDisplay();
        private static void OnSuggestionsChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args) => ((ExpressionTextBox)sender).RefreshCompletion();

        private void RefreshDisplay()
        {
            // A source update can synchronously invalidate resolved analysis.
            // Replacing Text inside its native insertion changes the caret before
            // WPF finishes that insertion (notably the first character in an
            // empty resolved field). The current Text is already the new draft.
            if (_writingRaw) return;
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
            _completionIntentText = null;
            CloseCompletion();
            base.OnLostKeyboardFocus(args);
            RefreshDisplay();
        }

        protected override void OnTextChanged(TextChangedEventArgs args)
        {
            base.OnTextChanged(args);
            if (_displayChange) return;
            _writingRaw = true;
            try { SetCurrentValue(RawTextProperty, Text); }
            finally { _writingRaw = false; }
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
                return IsCompletionOpen || IsCompletionPending;
            }
            if (!IsCompletionOpen)
            {
                if (!IsCompletionPending)
                {
                    if (!HasCompletionIntent || (key != Key.Enter && key != Key.Tab && key != Key.Escape)) return false;
                    _completionIntentText = null;
                    if (key == Key.Escape) _dismissedText = RawText;
                    return true;
                }
                if (key == Key.Escape) { _completionIntentText = null; _dismissedText = RawText; CloseCompletion(); return true; }
                return key == Key.Enter || key == Key.Tab;
            }
            if (key == Key.Escape) { _completionIntentText = null; _dismissedText = RawText; CloseCompletion(); return true; }
            if (key == Key.Enter || key == Key.Tab) { _completionIntentText = null; AcceptCompletion(); return true; }
            if (key != Key.Down && key != Key.Up) return false;
            _list.SelectedIndex = (_list.SelectedIndex + (key == Key.Down ? 1 : _list.Items.Count - 1)) % _list.Items.Count;
            _list.ScrollIntoView(_list.SelectedItem);
            return true;
        }

        private bool HasCompletionIntent => _completionIntentText != null && _completionIntentText == RawText
            && _completionIntentCaret == CaretIndex && _completionIntentSelection == SelectionLength;

        private void RefreshCompletion()
        {
            if (_popup == null) return;
            if (!HasCompletionIntent) _completionIntentText = null;
            if (!IsLoaded || !IsKeyboardFocusWithin || !IsExpressionEnabled || IsReadOnly || Suggestions == null || _dismissedText == RawText)
            { CloseCompletion(); return; }
            string raw = RawText ?? "";
            int caret = Math.Min(CaretIndex, raw.Length);
            ExpressionEdit expression = ExpressionCompletionContext.FindExpression(raw, caret);
            _tokenStart = expression?.Start ?? -1;
            string prefix = expression?.Prefix;
            ArgumentEdit slot = prefix == null ? ExpressionCompletionContext.FindArgument(raw, SelectionLength == 0 ? caret : SelectionStart, SelectionLength) : null;
            bool boundSlot = slot != null && ArgumentBindings?.OfType<ArgumentCompletionBinding>()
                .Any(item => item.TemplateKey == slot.TemplateKey && item.ArgumentName == slot.ArgumentName) == true;
            if (prefix == null && !_explicitCompletion && (!boundSlot || slot.Query.Length == 0)) { CloseCompletion(); return; }
            PropertyCompletion[] matches;
            if (boundSlot)
            {
                _tokenStart = slot.Start; _tokenEnd = slot.End;
                matches = Suggestions.OfType<PropertyCompletion>().Where(item => item.IsBoundTo(slot.TemplateKey, slot.ArgumentName)
                    && item.MatchesListSearch(slot.Query)).ToArray();
            }
            else
            {
                if (prefix == null) { _tokenStart = slot?.Start ?? SelectionStart; _tokenEnd = slot?.End ?? (SelectionStart + SelectionLength); prefix = ""; }
                else
                {
                    _tokenEnd = expression.End;
                }
                matches = FindMatches(Suggestions, prefix);
                if (expression?.IsArgument == true || slot != null) matches = matches.Where(match => !match.IsLocalization).ToArray();
            }
            string selectedName = (_list.SelectedItem as PropertyCompletion)?.Name;
            if (matches.Length == 0) { CloseCompletion(); return; }
            // An analysis refresh can withdraw a visible list just before Enter.
            // Consume that completion key once for this exact editing context;
            // never turn an interrupted acceptance into a source commit.
            _completionIntentText = raw; _completionIntentCaret = caret; _completionIntentSelection = SelectionLength;
            if (matches.Any(match => match.RequiresValidation))
            {
                _popup.IsOpen = false;
                PendingCompletion = ShowValidatedCompletionAsync(matches, selectedName, raw, caret, SelectionLength, Suggestions, ++_completionVersion);
                return;
            }
            ShowCompletion(matches, selectedName);
        }

        private async Task ShowValidatedCompletionAsync(PropertyCompletion[] matches, string selectedName, string raw, int caret, int selectionLength, IEnumerable suggestions, int version)
        {
            _pendingCompletionVersion = version;
            if (ValidateSuggestions == null || !await ValidateSuggestions()) return;
            if (version != _completionVersion || !IsLoaded || !IsKeyboardFocusWithin || RawText != raw || CaretIndex != caret || SelectionLength != selectionLength
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
            => ExpressionCompletionCandidates.FindMatches(suggestions.OfType<PropertyCompletion>(), prefix);

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
            if (property.RequiresValidation)
            {
                PendingCompletion = AcceptValidatedCompletionAsync(property, RawText, Suggestions, _tokenStart, _tokenEnd);
                return;
            }
            InsertCompletion(property, _tokenStart, _tokenEnd);
        }

        private async Task AcceptValidatedCompletionAsync(PropertyCompletion property, string raw, IEnumerable suggestions, int start, int end)
        {
            int caret = CaretIndex;
            int selectionLength = SelectionLength;
            CloseCompletion();
            int version = _completionVersion;
            _pendingCompletionVersion = version;
            if (ValidateSuggestions == null || !await ValidateSuggestions()) return;
            if (version == _completionVersion && IsLoaded && IsKeyboardFocusWithin && RawText == raw && CaretIndex == caret && SelectionLength == selectionLength
                && ReferenceEquals(Suggestions, suggestions)) InsertCompletion(property, start, end);
        }

        private void InsertCompletion(PropertyCompletion property, int start, int end)
        {
            string raw = RawText ?? "";
            string insertion = property.Insertion;
            int caret = start + insertion.Length;
            string result = raw.Substring(0, start) + insertion + raw.Substring(end);
            CloseCompletion();
            _dismissedText = result;
            SetCurrentValue(RawTextProperty, result);
            CaretIndex = caret;
            if (property.ArgumentOffset >= 0) Select(start + property.ArgumentOffset, 2);
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
