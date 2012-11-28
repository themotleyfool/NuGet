using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace NuGet.Server.Infrastructure.Lucene
{
    public static class BlockingCollectionExtensions
    {
        /// <summary>
        /// Waiting up to <paramref name="firstItemTimeout">firstItemTimeout</paramref>
        /// for the first item to become available, then return all other items that
        /// are immediately available.
        /// </summary>
        public static IList<T> TakeAvailable<T>(this BlockingCollection<T> collection, TimeSpan firstItemTimeout)
        {
            var items = new List<T>();

            T item;
            while (collection.TryTake(out item, items.Count == 0 ? firstItemTimeout : TimeSpan.Zero))
            {
                items.Add(item);
            }

            return items;
        }
    }
}