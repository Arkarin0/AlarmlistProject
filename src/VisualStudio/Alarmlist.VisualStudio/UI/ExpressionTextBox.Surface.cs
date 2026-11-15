// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Linq;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;

namespace Alarmlist.VisualStudio.UI
{
    public sealed partial class ExpressionTextBox
    {
        public static readonly DependencyProperty TextProperty = DependencyProperty.Register(nameof(Text), typeof(string), typeof(ExpressionTextBox),
            new FrameworkPropertyMetadata("", FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, (sender, args) => ((ExpressionTextBox)sender).DisplayTextChanged()));
        public static readonly DependencyProperty IsReadOnlyProperty = DependencyProperty.Register(nameof(IsReadOnly), typeof(bool), typeof(ExpressionTextBox), new PropertyMetadata(false, ConfigureChanged));
        public static readonly DependencyProperty AcceptsReturnProperty = DependencyProperty.Register(nameof(AcceptsReturn), typeof(bool), typeof(ExpressionTextBox), new PropertyMetadata(false, ConfigureChanged));
        public static readonly DependencyProperty TextWrappingProperty = DependencyProperty.Register(nameof(TextWrapping), typeof(TextWrapping), typeof(ExpressionTextBox), new PropertyMetadata(TextWrapping.NoWrap, ConfigureChanged));
        public static readonly DependencyProperty IsUndoEnabledProperty = DependencyProperty.Register(nameof(IsUndoEnabled), typeof(bool), typeof(ExpressionTextBox), new PropertyMetadata(false));
        public static readonly DependencyProperty VerticalScrollBarVisibilityProperty = ScrollViewer.VerticalScrollBarVisibilityProperty.AddOwner(typeof(ExpressionTextBox));
        private IExpressionEditorSurface _surface;
        private bool _menuKeyPending;
        private IExpressionEditorSurface _pointerMenuSurface;
        private object _pointerMenuValue;
        private int _selectionStart, _selectionLength, _caret;
        public string Text { get => (string)GetValue(TextProperty); set => SetValue(TextProperty, value ?? ""); }
        public bool IsReadOnly { get => (bool)GetValue(IsReadOnlyProperty); set => SetValue(IsReadOnlyProperty, value); }
        public bool AcceptsReturn { get => (bool)GetValue(AcceptsReturnProperty); set => SetValue(AcceptsReturnProperty, value); }
        public TextWrapping TextWrapping { get => (TextWrapping)GetValue(TextWrappingProperty); set => SetValue(TextWrappingProperty, value); }
        public bool IsUndoEnabled { get => (bool)GetValue(IsUndoEnabledProperty); set => SetValue(IsUndoEnabledProperty, value); }
        public ScrollBarVisibility VerticalScrollBarVisibility { get => (ScrollBarVisibility)GetValue(VerticalScrollBarVisibilityProperty); set => SetValue(VerticalScrollBarVisibilityProperty, value); }
        public int SelectionStart { get => _surface?.SelectionStart ?? _selectionStart; set => Select(value, SelectionLength); }
        public int SelectionLength { get => _surface?.SelectionLength ?? _selectionLength; set => Select(SelectionStart, value); }
        public int CaretIndex { get => _surface?.Caret ?? _caret; set => Select(value, 0); }
        public string SelectedText { get => Text.Substring(SelectionStart, SelectionLength); set => ReplaceSelection(value); }
        internal IExpressionEditorSurface Surface => _surface;
        protected override void OnPreviewMouseRightButtonDown(MouseButtonEventArgs args)
        {
            if (_surface != null && !(_surface is StandaloneExpressionSurface))
            {
                _pointerMenuSurface = _surface; _pointerMenuValue = DataContext; args.Handled = true; return;
            }
            base.OnPreviewMouseRightButtonDown(args);
        }
        protected override void OnPreviewMouseRightButtonUp(MouseButtonEventArgs args)
        {
            var surface = _pointerMenuSurface; var value = _pointerMenuValue; _pointerMenuSurface = null; _pointerMenuValue = null;
            if (surface != null && ReferenceEquals(surface, _surface) && ReferenceEquals(value, DataContext)
                && surface.OpenContextMenu(args.GetPosition(surface.Visual))) { args.Handled = true; return; }
            base.OnPreviewMouseRightButtonUp(args);
        }
        protected override void OnPreviewKeyDown(KeyEventArgs args)
        {
            Key key = args.Key == Key.System ? args.SystemKey : args.Key;
            if (key == Key.Apps && _surface != null && !(_surface is StandaloneExpressionSurface))
            { _menuKeyPending = true; args.Handled = true; return; }
            if ((key == Key.Apps || (key == Key.F10 && Keyboard.Modifiers == ModifierKeys.Shift)) && _surface?.OpenContextMenu(null) == true)
            { args.Handled = true; return; }
            base.OnPreviewKeyDown(args);
        }
        protected override void OnPreviewKeyUp(KeyEventArgs args)
        {
            Key key = args.Key == Key.System ? args.SystemKey : args.Key;
            if (key == Key.Apps && _menuKeyPending)
            {
                _menuKeyPending = false;
                if (_surface?.OpenContextMenu(null) == true) { args.Handled = true; return; }
            }
            base.OnPreviewKeyUp(args);
        }
        internal void HoldMenuDisplay(ContextMenu menu)
        {
            _keepRawForMenu = !IsDisplayingResolved;
            menu.Closed -= OnValueMenuClosed; menu.Closed += OnValueMenuClosed;
        }
        private void OnValueMenuClosed(object sender, RoutedEventArgs args)
        {
            if (!ReferenceEquals(args.Source, sender)) return;
            ((ContextMenu)sender).Closed -= OnValueMenuClosed;
#pragma warning disable VSTHRD001 // WPF popup commands execute after the closing event.
            _ = Dispatcher.BeginInvoke(new Action(() =>
            {
                if (ContextMenu?.IsOpen == true) return;
                _keepRawForMenu = false; TokenActions = null; RefreshDisplay();
            }), System.Windows.Threading.DispatcherPriority.ContextIdle);
#pragma warning restore VSTHRD001
        }
        public new bool Focus() { base.Focus(); return IsKeyboardFocusWithin; }
        internal static ExpressionTextBox Focused => ValueContextMenu.Ancestors(Keyboard.FocusedElement as DependencyObject).OfType<ExpressionTextBox>().FirstOrDefault();
        public void Select(int start, int length)
        {
            start = Math.Max(0, Math.Min(start, Text.Length)); length = Math.Max(0, Math.Min(length, Text.Length - start));
            _selectionStart = start; _selectionLength = length; _caret = start + length;
            _surface?.Select(start, length);
        }
        public void SelectAll() => Select(0, Text.Length);
        public Rect GetRectFromCharacterIndex(int index)
        {
            if (_surface == null) return Rect.Empty;
            var bounds = _surface.GetCharacterBounds(index);
            if (!bounds.IsEmpty) bounds.Offset((Vector)_surface.Visual.TranslatePoint(new Point(), this));
            return bounds;
        }
        public void Copy() => _surface?.Copy();
        public void Cut() { if (!IsReadOnly) _surface?.Cut(); }
        public void Paste() { if (!IsReadOnly) _surface?.Paste(); }
        internal void ReplaceSelection(string text)
        {
            if (IsReadOnly) return;
            if (_surface != null) _surface.ReplaceSelection(text ?? "");
            else { int start = SelectionStart; Text = Text.Substring(0, start) + text + Text.Substring(start + SelectionLength); Select(start + (text?.Length ?? 0), 0); }
        }
        private static void ConfigureChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
            => ((ExpressionTextBox)sender)._surface?.Configure(((ExpressionTextBox)sender).IsReadOnly, ((ExpressionTextBox)sender).AcceptsReturn, ((ExpressionTextBox)sender).TextWrapping);
        private void InitializeSurface(object sender, RoutedEventArgs args)
        {
            if (_surface != null || !IsVisible) return;
            var factory = ExpressionEditorServices.GetFactory(this);
            _surface = factory == null ? new StandaloneExpressionSurface(this)
                : factory.Create(this) ?? throw new InvalidOperationException("The expression editor factory returned no editing surface.");
            _surface.Text = Text; _surface.Configure(IsReadOnly, AcceptsReturn, TextWrapping);
            _surface.Select(_selectionStart, _selectionLength);
            _surface.TextChanged += OnSurfaceTextChanged; _surface.SelectionChanged += OnSurfaceSelectionChanged;
            var frame = new Border { Child = _surface.Visual };
            foreach (var pair in new[] { (Border.BackgroundProperty, BackgroundProperty), (Border.BorderBrushProperty, BorderBrushProperty),
                (Border.BorderThicknessProperty, BorderThicknessProperty), (Border.PaddingProperty, PaddingProperty) })
                BindingOperations.SetBinding(frame, pair.Item1, new Binding { Source = this, Path = new PropertyPath(pair.Item2) });
            Content = frame;
        }
        private void ReleaseSurface(object sender, RoutedEventArgs args)
        {
            if (_surface == null) return;
            _menuKeyPending = false; _pointerMenuSurface = null; _pointerMenuValue = null;
            _selectionStart = _surface.SelectionStart; _selectionLength = _surface.SelectionLength; _caret = _surface.Caret;
            _surface.TextChanged -= OnSurfaceTextChanged; _surface.SelectionChanged -= OnSurfaceSelectionChanged;
            _surface.Dispose(); _surface = null; Content = null;
        }
        private void OnSurfaceTextChanged(object sender, EventArgs args) => SetCurrentValue(TextProperty, _surface.Text);
        private void OnSurfaceSelectionChanged(object sender, EventArgs args) { if (!_displayChange) RefreshCompletion(); }
        private void DisplayTextChanged()
        {
            if (_surface != null && _surface.Text != Text) _surface.Text = Text;
            if (_displayChange) return;
            _writingRaw = true;
            try { SetCurrentValue(RawTextProperty, Text); }
            finally { _writingRaw = false; }
            RefreshCompletion();
        }
        protected override AutomationPeer OnCreateAutomationPeer() => new ExpressionPeer(this);
        private sealed class ExpressionPeer : FrameworkElementAutomationPeer, IValueProvider
        {
            private readonly ExpressionTextBox _owner;
            internal ExpressionPeer(ExpressionTextBox owner) : base(owner) { _owner = owner; }
            protected override string GetClassNameCore() => nameof(ExpressionTextBox);
            protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Edit;
            protected override bool HasKeyboardFocusCore() => _owner.IsKeyboardFocusWithin;
            protected override void SetFocusCore() => _owner.Focus();
            public override object GetPattern(PatternInterface patternInterface) => patternInterface == PatternInterface.Value ? this : base.GetPattern(patternInterface);
            bool IValueProvider.IsReadOnly => _owner.IsReadOnly || !_owner.IsEnabled;
            string IValueProvider.Value => _owner.Text;
            void IValueProvider.SetValue(string value)
            {
                if (((IValueProvider)this).IsReadOnly) throw new InvalidOperationException("The value is read-only.");
                _owner.SetCurrentValue(TextProperty, value ?? "");
            }
        }
    }
}
