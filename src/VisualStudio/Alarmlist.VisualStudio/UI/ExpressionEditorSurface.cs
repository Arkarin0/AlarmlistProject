// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.VisualStudio.OLE.Interop;

namespace Alarmlist.VisualStudio.UI
{
    internal interface IExpressionEditorFactory
    {
        IExpressionEditorSurface Create(ExpressionTextBox owner);
    }
    internal interface IExpressionEditorSurface : IDisposable
    {
        FrameworkElement Visual { get; }
        FrameworkElement InputElement { get; }
        string Text { get; set; }
        int SelectionStart { get; }
        int SelectionLength { get; }
        int Caret { get; }
        bool Focus();
        bool OpenContextMenu(Point? pointer);
        void Select(int start, int length);
        Rect GetCharacterBounds(int index);
        void Configure(bool readOnly, bool multiline, TextWrapping wrapping);
        void ReplaceSelection(string text);
        void Copy();
        void Cut();
        void Paste();
        IOleCommandTarget Commands { get; }
        event EventHandler TextChanged;
        event EventHandler SelectionChanged;
    }
    internal static class ExpressionEditorServices
    {
        internal static readonly DependencyProperty FactoryProperty = DependencyProperty.RegisterAttached("Factory", typeof(IExpressionEditorFactory),
            typeof(ExpressionEditorServices), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.Inherits));
        internal static void SetFactory(DependencyObject element, IExpressionEditorFactory factory) => element.SetValue(FactoryProperty, factory);
        internal static IExpressionEditorFactory GetFactory(DependencyObject element) => (IExpressionEditorFactory)element.GetValue(FactoryProperty);
    }

    // Standalone WPF/design-time hosts have no Visual Studio editor services.
    // The installed editor always supplies its native factory explicitly.
    internal sealed class StandaloneExpressionSurface : IExpressionEditorSurface
    {
        private readonly TextBox _box = new TextBox { BorderThickness = new Thickness(0), Padding = new Thickness(0), IsUndoEnabled = false, IsTabStop = false };
        internal StandaloneExpressionSurface(ExpressionTextBox owner)
        {
            _box.SetBinding(Control.BackgroundProperty, new System.Windows.Data.Binding(nameof(owner.Background)) { Source = owner });
            _box.SetBinding(Control.ForegroundProperty, new System.Windows.Data.Binding(nameof(owner.Foreground)) { Source = owner });
            foreach (var property in new[] { System.Windows.Automation.AutomationProperties.NameProperty,
                System.Windows.Automation.AutomationProperties.HelpTextProperty, System.Windows.Automation.AutomationProperties.LabeledByProperty })
                System.Windows.Data.BindingOperations.SetBinding(_box, property, new System.Windows.Data.Binding { Source = owner, Path = new PropertyPath(property) });
            _box.TextChanged += OnText; _box.SelectionChanged += OnSelection;
        }
        public FrameworkElement Visual => _box;
        public FrameworkElement InputElement => _box;
        public string Text { get => _box.Text; set => _box.Text = value; }
        public int SelectionStart => _box.SelectionStart;
        public int SelectionLength => _box.SelectionLength;
        public int Caret => _box.CaretIndex;
        public bool Focus() => _box.Focus();
        public bool OpenContextMenu(Point? pointer) => false;
        public IOleCommandTarget Commands => null;
        public event EventHandler TextChanged;
        public event EventHandler SelectionChanged;
        private void OnText(object sender, TextChangedEventArgs args) => TextChanged?.Invoke(this, EventArgs.Empty);
        private void OnSelection(object sender, RoutedEventArgs args) => SelectionChanged?.Invoke(this, EventArgs.Empty);
        public void Select(int start, int length) => _box.Select(start, length);
        public Rect GetCharacterBounds(int index) => _box.GetRectFromCharacterIndex(index);
        public void Configure(bool readOnly, bool multiline, TextWrapping wrapping)
        { _box.IsReadOnly = readOnly; _box.AcceptsReturn = multiline; _box.TextWrapping = wrapping; _box.VerticalScrollBarVisibility = ScrollBarVisibility.Auto; }
        public void ReplaceSelection(string text) => _box.SelectedText = text;
        public void Copy() => _box.Copy();
        public void Cut() => _box.Cut();
        public void Paste() => _box.Paste();
        public void Dispose() { _box.TextChanged -= OnText; _box.SelectionChanged -= OnSelection; TextChanged = null; SelectionChanged = null; }
    }
}
