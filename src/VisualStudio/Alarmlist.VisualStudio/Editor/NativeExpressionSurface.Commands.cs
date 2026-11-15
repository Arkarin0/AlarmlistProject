// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Alarmlist.VisualStudio.UI;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text.Editor;

namespace Alarmlist.VisualStudio.Editor
{
    internal sealed partial class NativeExpressionSurface
    {
        private readonly CancellationTokenSource _tokenLifetime = new CancellationTokenSource();
        private CancellationTokenSource _tokenDiscovery;
        private int _tokenGeneration, _rawCaret, _rawSelectionStart, _rawSelectionLength;
        private void InitializeTokenCommands()
        {
            _view.Caret.PositionChanged += OnTokenCaret;
            _view.Selection.SelectionChanged += OnTokenSelection;
            _view.TextBuffer.Changed += OnTokenText;
            RememberTokenSelection();
        }
        private void RememberTokenSelection()
        { _rawCaret = Caret; _rawSelectionStart = SelectionStart; _rawSelectionLength = SelectionLength; }
        private void OnTokenCaret(object sender, CaretPositionChangedEventArgs args) { ThreadHelper.ThrowIfNotOnUIThread(); ObserveTokenSelection(); }
        private void OnTokenSelection(object sender, EventArgs args) { ThreadHelper.ThrowIfNotOnUIThread(); ObserveTokenSelection(); }
        private void ObserveTokenSelection()
        {
            if (_disposed || _owner.IsChangingDisplay || _owner.IsDisplayingResolved
                || (_rawCaret == Caret && _rawSelectionStart == SelectionStart && _rawSelectionLength == SelectionLength)) return;
            RememberTokenSelection(); InvalidateTokenDiscovery();
        }
        private void OnTokenText(object sender, Microsoft.VisualStudio.Text.TextContentChangedEventArgs args)
        { ThreadHelper.ThrowIfNotOnUIThread(); if (!_owner.IsChangingDisplay) InvalidateTokenDiscovery(); }
        private void InvalidateTokenDiscovery()
        {
            _tokenGeneration++; _tokenDiscovery?.Cancel();
            if (_owner.ContextMenu?.IsOpen == true) _owner.ContextMenu.IsOpen = false;
        }
        public bool OpenContextMenu(Point? pointer)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            if (!pointer.HasValue) return OpenTokenMenu(Caret);
            int? offset = null;
            if (!_owner.IsDisplayingResolved && _view.TextViewLines.IsValid)
            {
                var positionInView = Visual.TranslatePoint(pointer.Value, _view.VisualElement);
                var line = _view.TextViewLines.GetTextViewLineContainingYCoordinate(positionInView.Y + _view.ViewportTop);
                var position = line?.GetBufferPositionFromXCoordinate(positionInView.X + _view.ViewportLeft);
                if (position.HasValue && ReferenceEquals(position.Value.Snapshot, _view.TextBuffer.CurrentSnapshot))
                {
                    offset = position.Value.Position;
                    var selection = _view.Selection.StreamSelectionSpan.SnapshotSpan;
                    if (selection.Length == 0 || position.Value < selection.Start || position.Value > selection.End)
                    { _view.Selection.Clear(); _view.Caret.MoveTo(position.Value); }
                }
            }
            return OpenTokenMenu(offset);
        }
        private bool OpenTokenMenu(int? offset)
        {
            if (_disposed || _owner.ContextMenu == null) return false;
            _owner.TokenActions = !_owner.IsDisplayingResolved && _owner.IsExpressionEnabled && offset.HasValue && _tags.AtCaret(offset.Value) != null
                ? new TokenActions(this, offset.Value) : null;
            _owner.HoldMenuDisplay(_owner.ContextMenu);
            var menu = ValueContextMenu.Open(_owner);
            if (menu == null)
            {
                menu = _owner.ContextMenu; menu.PlacementTarget = _owner; menu.IsOpen = true;
            }
            return true;
        }
        private bool TokenCommand(Guid group, uint id, bool execute, out bool enabled)
        {
            enabled = false;
            if (!_owner.IsExpressionEnabled || !XmlReferenceCommands.Command(group, id, out bool rename, out bool definition)) return false;
            var action = new TokenActions(this, Caret); enabled = !_owner.IsDisplayingResolved && action.CanInvoke;
            if (execute && enabled) action.Invoke(rename, definition);
            return true;
        }
        private sealed class TokenActions : IValueTokenActions
        {
            private readonly NativeExpressionSurface _surface;
            private readonly object _value;
            private readonly string _raw;
            private readonly int _offset, _generation, _contextVersion;
            internal TokenActions(NativeExpressionSurface surface, int offset)
            {
                _surface = surface; _value = surface._owner.DataContext; _raw = surface._owner.RawText;
                _offset = offset; _generation = surface._tokenGeneration; _contextVersion = surface._model.ReferenceActions.ContextVersion;
            }
            public bool CanInvoke => !_surface._disposed && _surface._owner.IsLoaded && _surface._tokenGeneration == _generation
                && ReferenceEquals(_surface._owner.DataContext, _value) && _surface._owner.RawText == _raw
                && _surface._model.ReferenceActions.ContextVersion == _contextVersion && _surface._model.ReferenceActions.CanFindToken(_value);
            public void Invoke(bool rename, bool definitionsOnly)
            {
                if (!CanInvoke) return;
                var previous = _surface._tokenDiscovery; _surface._tokenDiscovery = null;
                previous?.Cancel(); previous?.Dispose();
                if (!CanInvoke) return;
                _surface._tokenDiscovery = CancellationTokenSource.CreateLinkedTokenSource(_surface._tokenLifetime.Token);
                _surface._model.ReferenceActions.FindToken(_value, _offset, () => CanInvoke,
                    _surface._tokenDiscovery.Token, rename, definitionsOnly);
            }
        }
        private void DisposeTokenCommands()
        {
            _tokenLifetime.Cancel(); _tokenDiscovery?.Cancel(); _tokenDiscovery?.Dispose();
            _view.Caret.PositionChanged -= OnTokenCaret; _view.Selection.SelectionChanged -= OnTokenSelection; _view.TextBuffer.Changed -= OnTokenText;
            _owner.TokenActions = null; if (_owner.ContextMenu?.IsOpen == true) _owner.ContextMenu.IsOpen = false;
            _tokenLifetime.Dispose();
        }
    }
}
