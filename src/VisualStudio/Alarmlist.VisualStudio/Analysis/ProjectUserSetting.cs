// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Threading;
using System.Threading.Tasks;

namespace Alarmlist.VisualStudio.Analysis
{
    // One project-owned value, shared by all its previews. Callers use the UI
    // context; CPS owns persistence. Coalesce rapid column changes without losing the
    // final value or letting a late initial read replace a user's changes.
    internal sealed class ProjectUserSetting : IDisposable
    {
        private readonly Func<CancellationToken, Task<string>> _read;
        private readonly Func<string, CancellationToken, Task> _write;
        private readonly CancellationTokenSource _lifetime = new CancellationTokenSource();
        private readonly int _delay;
        private int _version;
        private Task _loading;
        private bool _disposed;
        public string Value { get; private set; } = "";
        public string Error { get; private set; }
        public bool IsLoaded { get; private set; }
        public Task PendingSave { get; private set; } = Task.CompletedTask;
        public event EventHandler Changed;
        public ProjectUserSetting(Func<CancellationToken, Task<string>> read, Func<string, CancellationToken, Task> write, int delay = 200)
        { _read = read; _write = write; _delay = delay; }
        public Task LoadAsync() => _loading ?? (_loading = ReadAsync());
        private async Task ReadAsync()
        {
            int version = _version;
            try
            {
                string value = await _read(_lifetime.Token);
                if (_disposed || version != _version) return;
                Value = value ?? ""; IsLoaded = true; Error = null; Changed?.Invoke(this, EventArgs.Empty);
            }
            catch (OperationCanceledException) when (_disposed) { }
            catch (Exception exception) when (SdkAnalysisBridge.IsRecoverable(exception))
            { if (!_disposed && version == _version) { IsLoaded = true; Error = "Could not read summary columns: " + exception.Message; Changed?.Invoke(this, EventArgs.Empty); } }
        }
        public void Set(string value)
        {
            if (_disposed || (IsLoaded && Value == value)) return;
            Value = value; IsLoaded = true; Error = null; _version++;
            Changed?.Invoke(this, EventArgs.Empty);
            if (PendingSave.IsCompleted) PendingSave = SaveAsync();
        }
        private async Task SaveAsync()
        {
            while (!_disposed)
            {
                int version = _version;
                try
                {
                    // Always yield before publication of PendingSave.
                    await Task.Delay(Math.Max(1, _delay), _lifetime.Token);
                    if (version != _version) continue;
                    await _write(Value, _lifetime.Token);
                    if (_disposed) return;
                    Error = null;
                }
                catch (OperationCanceledException) when (_disposed) { return; }
                catch (Exception exception) when (SdkAnalysisBridge.IsRecoverable(exception))
                { if (!_disposed) Error = "Could not save summary columns; this layout is still available in this session. " + exception.Message; }
                if (_disposed) return;
                Changed?.Invoke(this, EventArgs.Empty);
                if (version == _version) return;
            }
        }
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true; _lifetime.Cancel(); _lifetime.Dispose(); Changed = null;
        }
    }
}
