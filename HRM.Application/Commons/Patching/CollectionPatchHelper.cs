using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HRM.Application.Commons.Patching
{

    public static class CollectionPatchHelper
    {
        /// <summary>
        /// Replace a child collection by removing missing items, updating existing items, and adding new items based on key matching.
        /// Đồng bộ collection con: xóa item không còn trong request, update item đã tồn tại, và thêm item mới dựa trên key.
        /// </summary>
        public static bool ReplaceCollection<TEntity, TRequest, TKey>(
            ICollection<TEntity> currentItems,
            IEnumerable<TRequest> requestedItems,
            Func<TEntity, TKey> entityKey,
            Func<TRequest, TKey> requestKey,
            Func<TRequest, TEntity> createEntity,
            Action<TEntity, TRequest> updateEntity,
            Action<TEntity>? removeEntity = null)
            where TKey : notnull
        {
            var changed = false;

            var requestedList = requestedItems.ToList();
            var requestedKeys = requestedList.Select(requestKey).ToHashSet();

            foreach (var existing in currentItems.ToList())
            {
                if (!requestedKeys.Contains(entityKey(existing)))
                {
                    if (removeEntity is not null)
                    {
                        removeEntity(existing);
                    }
                    else
                    {
                        currentItems.Remove(existing);
                    }

                    changed = true;
                }
            }

            var currentLookup = currentItems.ToDictionary(entityKey);

            foreach (var request in requestedList)
            {
                var key = requestKey(request);

                if (currentLookup.TryGetValue(key, out var existing))
                {
                    updateEntity(existing, request);
                    changed = true;
                }
                else
                {
                    currentItems.Add(createEntity(request));
                    changed = true;
                }
            }

            return changed;
        }
    }
}
