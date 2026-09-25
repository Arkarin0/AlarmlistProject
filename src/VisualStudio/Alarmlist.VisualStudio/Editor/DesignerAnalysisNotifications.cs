// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Diagnostics;
using Alarmlist.VisualStudio.Analysis;
using Microsoft.VisualStudio.Threading;

namespace Alarmlist.VisualStudio.Editor
{
    internal sealed class DesignerAnalysisNotifications : IDisposable
    {
        private readonly object _gate = new object();
        private readonly JoinableTaskCollection _tasks;
        private readonly JoinableTaskFactory _dispatch;
        private readonly JoinableTaskFactory _lifetime;
        private Action _changed;

        public DesignerAnalysisNotifications(JoinableTaskContext threading, Action changed)
        {
            _tasks = threading.CreateCollection();
            _dispatch = threading.CreateFactory(_tasks);
            _lifetime = threading.Factory;
            _changed = changed;
        }

        public void Post()
        {
            lock (_gate)
            {
                if (_changed == null) return;
                _dispatch.RunAsync(async () =>
                {
                    try
                    {
                        await _dispatch.SwitchToMainThreadAsync();
                        _changed?.Invoke();
                    }
                    catch (Exception exception) when (SdkAnalysisBridge.IsRecoverable(exception))
                    { Trace.TraceError("ALMX designer refresh failed: {0}", exception); }
                }).Task.Forget();
            }
        }

        public void Dispose()
        {
            lock (_gate) _changed = null;
            // Join outside the collection: including this join in the collection
            // would make it wait on itself when a designer window closes.
            _lifetime.Run(_tasks.JoinTillEmptyAsync);
        }
    }
}
