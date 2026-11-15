// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.ComponentModel.Composition;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Alarmlist.VisualStudio.UI;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Operations;
using Microsoft.VisualStudio.TextManager.Interop;
using Microsoft.VisualStudio.Utilities;
using OleServiceProvider = Microsoft.VisualStudio.OLE.Interop.IServiceProvider;

namespace Alarmlist.VisualStudio.Editor
{
    [Export(typeof(VsExpressionEditorFactory))]
    internal sealed class VsExpressionEditorFactory
    {
        internal const string ContentType = "AlarmlistExpression";
        internal const string ViewRole = "AlarmlistExpressionView";
#pragma warning disable CS0649
        [Export, Name(ContentType), BaseDefinition("text")]
        internal static ContentTypeDefinition ExpressionContent;
#pragma warning restore CS0649
        private readonly IVsEditorAdaptersFactoryService _adapters;
        private readonly IContentTypeRegistryService _types;
        private readonly IEditorOperationsFactoryService _operations;
        private readonly ITextEditorFactoryService _textViews;
        private readonly IClassificationTypeRegistryService _classifications;
        [ImportingConstructor]
        public VsExpressionEditorFactory(IVsEditorAdaptersFactoryService adapters, IContentTypeRegistryService types, IEditorOperationsFactoryService operations, ITextEditorFactoryService textViews, IClassificationTypeRegistryService classifications)
        { _adapters = adapters; _types = types; _operations = operations; _textViews = textViews; _classifications = classifications; }
        internal IExpressionEditorFactory Bind(OleServiceProvider site, EditFileControlViewModel view, Action<bool> undo, Func<bool, bool> canUndo)
            => new Bound(this, site, view, undo, canUndo);
        private sealed class Bound : IExpressionEditorFactory
        {
            private readonly VsExpressionEditorFactory _factory;
            private readonly OleServiceProvider _site;
            private readonly EditFileControlViewModel _view;
            private readonly Action<bool> _undo;
            private readonly Func<bool, bool> _canUndo;
            internal Bound(VsExpressionEditorFactory factory, OleServiceProvider site, EditFileControlViewModel view, Action<bool> undo, Func<bool, bool> canUndo)
            { _factory = factory; _site = site; _view = view; _undo = undo; _canUndo = canUndo; }
            public IExpressionEditorSurface Create(ExpressionTextBox owner)
                => new NativeExpressionSurface(_factory._adapters, _factory._types, _factory._operations, _site,
                    _factory._textViews.CreateTextViewRoleSet(PredefinedTextViewRoles.Interactive, PredefinedTextViewRoles.Editable, ViewRole), _view, owner, _undo, _canUndo, _factory._classifications);
        }
    }

    internal sealed partial class NativeExpressionSurface : IExpressionEditorSurface, IOleCommandTarget
    {
        private readonly ExpressionTextBox _owner;
        private readonly EditFileControlViewModel _model;
        private readonly Action<bool> _undo;
        private readonly Func<bool, bool> _canUndo;
        private readonly IVsTextView _adapter;
        private readonly IOleCommandTarget _next;
        private readonly IWpfTextView _view;
        private readonly IWpfTextViewHost _host;
        private readonly IEditorOperations _operations;
        private bool _disposed;
        private ITextSnapshot _notifiedSnapshot;
        private int _notifiedCaret, _notifiedSelectionStart, _notifiedSelectionLength;
        internal NativeExpressionSurface(IVsEditorAdaptersFactoryService adapters, IContentTypeRegistryService types, IEditorOperationsFactoryService operations,
            OleServiceProvider site, ITextViewRoleSet roles, EditFileControlViewModel model, ExpressionTextBox owner, Action<bool> undo, Func<bool, bool> canUndo, IClassificationTypeRegistryService classifications)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            _owner = owner; _model = model; _undo = undo; _canUndo = canUndo;
            var buffer = adapters.CreateVsTextBufferAdapter(site, types.GetContentType(VsExpressionEditorFactory.ContentType));
            ErrorHandler.ThrowOnFailure(buffer.InitializeContent(owner.Text, owner.Text.Length));
            _adapter = adapters.CreateVsTextViewAdapter(site, roles);
            ErrorHandler.ThrowOnFailure(_adapter.Initialize((IVsTextLines)buffer, IntPtr.Zero, 0, new INITVIEW[1]));
            _view = adapters.GetWpfTextView(_adapter);
            _host = adapters.GetWpfTextViewHost(_adapter);
            if (_view == null || _host == null) throw new InvalidOperationException("Visual Studio could not create the expression editor.");
            if (!_view.TextBuffer.ContentType.IsOfType(VsExpressionEditorFactory.ContentType))
                _view.TextBuffer.ChangeContentType(types.GetContentType(VsExpressionEditorFactory.ContentType), this);
            _view.Options.SetOptionValue(DefaultWpfViewOptions.AppearanceCategory, "text");
            _operations = operations.GetEditorOperations(_view);
            // These buffers mirror drafts/presentation. The owning ALMX
            // session supplies Undo; private mirror edits must not clear a
            // linked source transaction's Redo history during synchronization.
            ErrorHandler.ThrowOnFailure(((IVsTextLines)buffer).GetUndoManager(out var privateUndo));
            privateUndo?.Enable(0);
            _view.Options.SetOptionValue(DefaultTextViewHostOptions.HorizontalScrollBarId, false);
            _view.Options.SetOptionValue(DefaultTextViewHostOptions.VerticalScrollBarId, true);
            _view.Options.SetOptionValue(DefaultTextViewHostOptions.GlyphMarginId, false);
            _view.Options.SetOptionValue(DefaultTextViewHostOptions.LineNumberMarginId, false);
            _view.Options.SetOptionValue(DefaultTextViewHostOptions.OutliningMarginId, false);
            InitializeClassification(classifications);
            _view.TextBuffer.Changed += OnTextChanged;
            _view.Selection.SelectionChanged += OnSelectionChanged; _view.Caret.PositionChanged += OnCaretChanged;
            _view.LayoutChanged += OnLayoutChanged;
            KeyboardNavigation.SetIsTabStop(_view.VisualElement, false);
            KeyboardNavigation.SetTabNavigation(owner, KeyboardNavigationMode.Once);
            _host.HostControl.Height = _view.LineHeight;
            ErrorHandler.ThrowOnFailure(_adapter.AddCommandFilter(this, out _next));
            InitializeTokenCommands();
        }
        public FrameworkElement Visual => _host.HostControl;
        public FrameworkElement InputElement => _view.VisualElement;
        public string Text
        {
            get => _view.TextBuffer.CurrentSnapshot.GetText();
            set { if (Text != value) _view.TextBuffer.Replace(new Span(0, _view.TextBuffer.CurrentSnapshot.Length), value); }
        }
        public int SelectionStart => _view.Selection.StreamSelectionSpan.SnapshotSpan.Start.Position;
        public int SelectionLength => _view.Selection.StreamSelectionSpan.SnapshotSpan.Length;
        public int Caret => _view.Caret.Position.BufferPosition.Position;
        public bool Focus() => _view.VisualElement.Focus();
        public IOleCommandTarget Commands { get { ThreadHelper.ThrowIfNotOnUIThread(); return (IOleCommandTarget)_adapter; } }
        public event EventHandler TextChanged;
        public event EventHandler SelectionChanged;
        public void Select(int start, int length)
        {
            var snapshot = _view.TextSnapshot;
            _view.Selection.Select(new SnapshotSpan(snapshot, start, length), false);
            _view.Caret.MoveTo(new SnapshotPoint(snapshot, start + length));
        }
        public Rect GetCharacterBounds(int index)
        {
            if (!_view.TextViewLines.IsValid) return Rect.Empty;
            var point = new SnapshotPoint(_view.TextSnapshot, index);
            var line = _view.TextViewLines.GetTextViewLineContainingBufferPosition(point);
            if (line == null) return Rect.Empty;
            var bounds = line.GetCharacterBounds(point);
            var origin = _view.VisualElement.TranslatePoint(new Point(bounds.Left - _view.ViewportLeft, bounds.Top - _view.ViewportTop), Visual);
            return new Rect(origin, new Size(bounds.Width, bounds.Height));
        }
        public void Configure(bool readOnly, bool multiline, TextWrapping wrapping)
        {
            _view.Options.SetOptionValue(DefaultTextViewOptions.ViewProhibitUserInputId, readOnly);
            _view.Options.SetOptionValue(DefaultTextViewOptions.WordWrapStyleId, wrapping == TextWrapping.NoWrap ? WordWrapStyles.None : WordWrapStyles.WordWrap);
        }
        public void ReplaceSelection(string text) { if (!_owner.IsReadOnly) _operations.InsertText(text); }
        public void Copy() => _operations.CopySelection();
        public void Cut() { if (!_owner.IsReadOnly) _operations.CutSelection(); }
        public void Paste() { if (!_owner.IsReadOnly) _operations.Paste(); }
        private void OnTextChanged(object sender, TextContentChangedEventArgs args) => TextChanged?.Invoke(this, EventArgs.Empty);
        private void OnSelectionChanged(object sender, EventArgs args) => NotifySelection();
        private void OnCaretChanged(object sender, CaretPositionChangedEventArgs args) => NotifySelection();
        private void NotifySelection()
        {
            if (_disposed || (ReferenceEquals(_notifiedSnapshot, _view.TextBuffer.CurrentSnapshot)
                && _notifiedCaret == Caret && _notifiedSelectionStart == SelectionStart && _notifiedSelectionLength == SelectionLength)) return;
            _notifiedSnapshot = _view.TextBuffer.CurrentSnapshot;
            _notifiedCaret = Caret; _notifiedSelectionStart = SelectionStart; _notifiedSelectionLength = SelectionLength;
            SelectionChanged?.Invoke(this, EventArgs.Empty);
        }
        private void OnLayoutChanged(object sender, TextViewLayoutChangedEventArgs args)
        {
            if (_disposed || !_view.TextViewLines.IsValid || _view.TextViewLines.Count == 0) return;
            double maximum = double.IsInfinity(_owner.MaxHeight) ? 110 : _owner.MaxHeight;
            maximum = Math.Max(_view.LineHeight, maximum - _owner.Padding.Top - _owner.Padding.Bottom - _owner.BorderThickness.Top - _owner.BorderThickness.Bottom);
            var last = _view.TextViewLines.LastVisibleLine;
            double height = last != null && last.EndIncludingLineBreak.Position < _view.TextSnapshot.Length ? maximum : _view.TextViewLines.Count * _view.LineHeight;
            height = Math.Max(_view.LineHeight, Math.Min(maximum, height));
            if (Math.Abs(_host.HostControl.Height - height) > 0.5) _host.HostControl.Height = height;
            // Native insertion can publish its final caret during layout rather
            // than a separate caret event. Complete against that final position.
            NotifySelection();
        }
        private bool Special(Guid group, uint id, bool execute, out bool enabled)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            enabled = true;
            if (TokenCommand(group, id, execute, out bool tokenEnabled)) { enabled = tokenEnabled; return true; }
            if (group == VSConstants.GUID_VSStandardCommandSet97 && (id == (uint)VSConstants.VSStd97CmdID.Undo || id == (uint)VSConstants.VSStd97CmdID.Redo))
            {
                bool redo = id == (uint)VSConstants.VSStd97CmdID.Redo; enabled = _canUndo(redo);
                if (execute && enabled) _undo(redo);
                return true;
            }
            if (group != VSConstants.VSStd2K) return false;
            if (id == (uint)VSConstants.VSStd2KCmdID.SHOWCONTEXTMENU)
            { enabled = _owner.ContextMenu != null; if (execute && enabled) OpenContextMenu(null); return true; }
            if (id == (uint)VSConstants.VSStd2KCmdID.SHOWMEMBERLIST || id == (uint)VSConstants.VSStd2KCmdID.COMPLETEWORD || id == (uint)VSConstants.VSStd2KCmdID.AUTOCOMPLETE)
            { enabled = !_owner.IsReadOnly && _owner.IsExpressionEnabled; if (execute && enabled) _owner.HandleCompletionKey(Key.Space, ModifierKeys.Control); return true; }
            if (id == (uint)VSConstants.VSStd2KCmdID.RETURN)
            {
                if (execute && !_owner.IsReadOnly && !_owner.HandleCompletionKey(Key.Enter, Keyboard.Modifiers))
                {
                    if (Keyboard.Modifiers == ModifierKeys.Shift) { if (_owner.AcceptsReturn) ReplaceSelection(Environment.NewLine); }
                    else _model.CommitPending();
                }
                return true;
            }
            if (id == (uint)VSConstants.VSStd2KCmdID.TAB)
            {
                if (execute && !_owner.HandleCompletionKey(Key.Tab, Keyboard.Modifiers))
                    _owner.MoveFocus(new TraversalRequest(Keyboard.Modifiers == ModifierKeys.Shift ? FocusNavigationDirection.Previous : FocusNavigationDirection.Next));
                return true;
            }
            if (id == (uint)VSConstants.VSStd2KCmdID.CANCEL)
            { if (execute && !_owner.HandleCompletionKey(Key.Escape, Keyboard.Modifiers)) _model.CancelPending(); return true; }
            return false;
        }
        public int QueryStatus(ref Guid group, uint count, OLECMD[] commands, IntPtr text)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            if (count == 1 && Special(group, commands[0].cmdID, false, out bool enabled))
            { commands[0].cmdf = (uint)(OLECMDF.OLECMDF_SUPPORTED | (enabled ? OLECMDF.OLECMDF_ENABLED : 0)); return VSConstants.S_OK; }
            return _next.QueryStatus(ref group, count, commands, text);
        }
        public int Exec(ref Guid group, uint id, uint options, IntPtr input, IntPtr output)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            if (XmlReferenceCommands.Command(group, id, out _) && (!XmlReferenceCommands.HasNoArguments(input) || output != IntPtr.Zero))
                return _next.Exec(ref group, id, options, input, output);
            return Special(group, id, true, out _) ? VSConstants.S_OK : _next.Exec(ref group, id, options, input, output);
        }
        public void Dispose()
        {
            ThreadHelper.ThrowIfNotOnUIThread(); if (_disposed) return; _disposed = true;
            DisposeTokenCommands();
            DisposeClassification();
            _view.TextBuffer.Changed -= OnTextChanged; _view.Selection.SelectionChanged -= OnSelectionChanged; _view.Caret.PositionChanged -= OnCaretChanged;
            _view.LayoutChanged -= OnLayoutChanged; _adapter.RemoveCommandFilter(this);
            if (!_view.IsClosed) _host.Close();
            TextChanged = null; SelectionChanged = null;
        }
    }
}
