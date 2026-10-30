// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Windows.Controls;
using System.Windows.Threading;

namespace Alarmlist.VisualStudio.UI
{
    public partial class RenameReviewControl : UserControl
    {
        private DispatcherOperation _focus;
        public RenameReviewControl()
        {
            InitializeComponent();
            Loaded += (sender, args) => { Dispatcher.VerifyAccess(); FocusName(); };
            DataContextChanged += (sender, args) => { Dispatcher.VerifyAccess(); FocusName(); };
            Unloaded += (sender, args) => { _focus?.Abort(); _focus = null; };
        }
        private void FocusName()
        {
            Dispatcher.VerifyAccess(); _focus?.Abort(); _focus = null;
            var expected = DataContext as RenameReviewViewModel;
            if (expected?.IsExpanded != true) return;
#pragma warning disable VSTHRD001 // Already on this WPF dispatcher; wait for Loaded layout/bindings, not a thread switch.
            _focus = Dispatcher.BeginInvoke(new Action(() =>
            {
                _focus = null;
                if (IsLoaded && NewNameBox.IsVisible && ReferenceEquals(DataContext, expected) && expected.IsExpanded && expected.CanEditName)
                { NewNameBox.Focus(); NewNameBox.SelectAll(); }
            }), DispatcherPriority.Loaded);
#pragma warning restore VSTHRD001
        }
    }
}
