// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Threading;

namespace Alarmlist.VisualStudio.Editor
{
    // UI-thread ownership only. Frozen solution captures and SDK workers are not
    // held here; each window owns its own query session and watchers.
    internal sealed class ReferenceIntentLease
    {
        private Func<bool> _current;
        private Action _release;
        private int _owners = 1;
        internal ReferenceIntentLease(Func<bool> current, CancellationToken token, Action release)
        { _current = current ?? throw new ArgumentNullException(nameof(current)); Token = token; _release = release; }
        internal CancellationToken Token { get; }
        internal bool IsCurrent => _owners != 0 && !Token.IsCancellationRequested && _current();
        internal ReferenceIntentLease Retain()
        {
            if (_owners == 0) throw new ObjectDisposedException(nameof(ReferenceIntentLease));
            _owners++; return this;
        }
        internal void Release()
        {
            if (_owners == 0 || --_owners != 0) return;
            _current = null;
            var release = _release; _release = null; release?.Invoke();
        }
    }
}
