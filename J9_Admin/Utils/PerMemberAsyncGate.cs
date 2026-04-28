using System.Collections.Generic;

namespace J9_Admin.Utils;

/// <summary>
/// 按会员维度串行化关键资金操作，避免同一会员并发重复上分/下分。
/// </summary>
public class PerMemberAsyncGate
{
    private readonly object _syncRoot = new();
    private readonly Dictionary<long, LockEntry> _locks = new();

    public async Task<IDisposable> LockAsync(long memberId)
    {
        LockEntry entry;
        lock (_syncRoot)
        {
            if (!_locks.TryGetValue(memberId, out entry))
            {
                entry = new LockEntry();
                _locks[memberId] = entry;
            }

            entry.RefCount++;
        }

        try
        {
            await entry.Semaphore.WaitAsync();
            return new Releaser(this, memberId, entry);
        }
        catch
        {
            Release(memberId, entry, releaseSemaphore: false);
            throw;
        }
    }

    private sealed class Releaser : IDisposable
    {
        private readonly PerMemberAsyncGate _gate;
        private readonly long _memberId;
        private readonly LockEntry _entry;
        private bool _disposed;

        public Releaser(PerMemberAsyncGate gate, long memberId, LockEntry entry)
        {
            _gate = gate;
            _memberId = memberId;
            _entry = entry;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _gate.Release(_memberId, _entry, releaseSemaphore: true);
        }
    }

    private void Release(long memberId, LockEntry entry, bool releaseSemaphore)
    {
        lock (_syncRoot)
        {
            if (releaseSemaphore)
            {
                entry.Semaphore.Release();
            }

            entry.RefCount--;
            if (entry.RefCount == 0 && _locks.TryGetValue(memberId, out var current) && ReferenceEquals(current, entry))
            {
                _locks.Remove(memberId);
                entry.Semaphore.Dispose();
            }
        }
    }

    private sealed class LockEntry
    {
        public SemaphoreSlim Semaphore { get; } = new(1, 1);
        public int RefCount { get; set; }
    }
}
