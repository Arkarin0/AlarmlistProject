// Created/modified by Arkarin0 under one ore more license(s).

using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Alarmlist.VisualStudio.UI
{
    internal static class BindingCollection
    {
        // WPF keeps views over non-notifying collections in its global inactive
        // view cache. A notifying source lets the collection and its view become
        // collectible together, without retaining closed designers or snapshots
        // until later dispatcher cleanup cycles. Keep the UI snapshot read-only.
        public static ReadOnlyObservableCollection<T> Create<T>(IEnumerable<T> items)
            => new ReadOnlyObservableCollection<T>(new ObservableCollection<T>(items ?? Enumerable.Empty<T>()));
    }
}
