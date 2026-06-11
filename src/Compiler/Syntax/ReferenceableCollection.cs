// Created/modified by Arkarin0 under one ore more license(s).

using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Alarmlist.Syntax
{
    public class ReferenceableCollection<T> : Collection<T>
        where T : IReferenceableCollectionItem
    {
        public IReadOnlyList<T> ResolveFrom(IEnumerable<T> inheritedItems)
        {
            var result = new List<T>();

            if (inheritedItems != null)
                result.AddRange(inheritedItems);

            foreach (var item in this)
            {
                if (item is Clear)
                {
                    result.Clear();
                    continue;
                }

                result.Add(item);
            }

            return result;
        }
    }
}
