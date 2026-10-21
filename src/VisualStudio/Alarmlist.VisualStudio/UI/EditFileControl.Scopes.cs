// Created/modified by Arkarin0 under one ore more license(s).

using System.ComponentModel;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using Alarmlist.VisualStudio.UI;

namespace Alarmlist.VisualStudio
{
    public partial class EditFileControl
    {
        private DesignerLayoutViewModel _scopeLayout;
        private void OnLoaded(object sender, RoutedEventArgs args)
        {
            UnsubscribeLayout();
            _scopeLayout = ViewModel.DesignerLayout;
            _scopeLayout.PropertyChanged += OnScopeLayoutChanged;
            ApplyScopeLayout();
        }
        private void UnsubscribeLayout()
        {
            if (_scopeLayout != null) _scopeLayout.PropertyChanged -= OnScopeLayoutChanged;
            _scopeLayout = null;
        }
        private void OnScopeLayoutChanged(object sender, PropertyChangedEventArgs args) => ApplyScopeLayout();
        private void ApplyScopeLayout()
        {
            DesignerLayoutViewModel layout = _scopeLayout;
            if (layout == null) return;
            // Move focus to a draft-preserving header before collapsing its field.
            if (!layout.FileExpanded && FileAreaScroll.IsKeyboardFocusWithin) FileAreaHeader.Focus();
            if (!layout.AlarmExpanded && AlarmAreaContent.IsKeyboardFocusWithin) AlarmAreaHeader.Focus();
            FileAreaScroll.Visibility = layout.FileExpanded ? Visibility.Visible : Visibility.Collapsed;
            AlarmAreaContent.Visibility = layout.AlarmExpanded ? Visibility.Visible : Visibility.Collapsed;
            ScopeDivider.Visibility = layout.FileExpanded && layout.AlarmExpanded ? Visibility.Visible : Visibility.Collapsed;
            FileAreaRow.MinHeight = layout.FileExpanded ? 50 : 0;
            AlarmAreaRow.MinHeight = layout.AlarmExpanded ? 80 : 0;
            FileAreaRow.Height = layout.FileExpanded ? new GridLength(layout.AlarmExpanded ? layout.FileProportion : 1, GridUnitType.Star) : new GridLength(0);
            AlarmAreaRow.Height = layout.AlarmExpanded ? new GridLength(layout.FileExpanded ? 1 - layout.FileProportion : 1, GridUnitType.Star) : GridLength.Auto;
        }
        private void OnScopeDragCompleted(object sender, DragCompletedEventArgs args) => SaveScopeDivider();
        private void OnScopeDividerKeyUp(object sender, KeyEventArgs args)
        {
            if (args.Key == Key.Up || args.Key == Key.Down) SaveScopeDivider();
        }
        private void SaveScopeDivider()
        {
            if (_scopeLayout == null || !_scopeLayout.FileExpanded || !_scopeLayout.AlarmExpanded) return;
            double height = FileAreaRow.ActualHeight + AlarmAreaRow.ActualHeight;
            if (height > 0) _scopeLayout.FileProportion = FileAreaRow.ActualHeight / height;
        }
    }
}
