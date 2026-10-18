// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace Alarmlist.VisualStudio.ProjectSystem
{
    // Own the link and queued callbacks together so unloading cannot retain a
    // provider or publish a queued evaluation after its lifetime has ended.
    internal sealed class DependencyTreeSubscription<T> : IDisposable
    {
        private readonly CancellationTokenSource _lifetime;
        private readonly IDisposable _link;
        private readonly ActionBlock<T> _updates;
        private int _disposed;

        internal DependencyTreeSubscription(ISourceBlock<T> source, Func<T, Task> update, CancellationToken cancellationToken)
        {
            _lifetime = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _updates = new ActionBlock<T>(update, new ExecutionDataflowBlockOptions
            {
                MaxDegreeOfParallelism = 1,
                CancellationToken = _lifetime.Token
            });
            _link = source.LinkTo(_updates, new DataflowLinkOptions { PropagateCompletion = true });
        }

        internal Task Completion => _updates.Completion;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
            _link.Dispose();
            _lifetime.Cancel();
            _lifetime.Dispose();
        }
    }
}
