namespace HRM.Application.Commons.Concurrency;

public sealed class KeyedMutationLock<TKey>
    where TKey : notnull
{
    private readonly object _syncRoot = new();
    private readonly Dictionary<TKey, LockEntry> _entries = [];

    public async Task<IDisposable> AcquireAsync(
        TKey key,
        CancellationToken cancellationToken)
    {
        LockEntry entry;
        lock (_syncRoot)
        {
            if (!_entries.TryGetValue(key, out entry!))
            {
                entry = new LockEntry();
                _entries.Add(key, entry);
            }

            entry.ReferenceCount++;
        }

        try
        {
            await entry.Semaphore.WaitAsync(cancellationToken);
            return new Releaser(this, key, entry);
        }
        catch
        {
            ReleaseReference(key, entry);
            throw;
        }
    }

    private void Release(TKey key, LockEntry entry)
    {
        entry.Semaphore.Release();
        ReleaseReference(key, entry);
    }

    private void ReleaseReference(TKey key, LockEntry entry)
    {
        lock (_syncRoot)
        {
            entry.ReferenceCount--;
            if (entry.ReferenceCount == 0 &&
                _entries.TryGetValue(key, out var currentEntry) &&
                ReferenceEquals(currentEntry, entry))
            {
                _entries.Remove(key);
                entry.Semaphore.Dispose();
            }
        }
    }

    private sealed class LockEntry
    {
        public SemaphoreSlim Semaphore { get; } = new(1, 1);
        public int ReferenceCount { get; set; }
    }

    private sealed class Releaser : IDisposable
    {
        private readonly KeyedMutationLock<TKey> _owner;
        private readonly TKey _key;
        private readonly LockEntry _entry;
        private bool _disposed;

        public Releaser(
            KeyedMutationLock<TKey> owner,
            TKey key,
            LockEntry entry)
        {
            _owner = owner;
            _key = key;
            _entry = entry;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _owner.Release(_key, _entry);
        }
    }
}
