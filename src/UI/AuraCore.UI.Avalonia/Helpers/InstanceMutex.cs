using System;
using System.Collections.Generic;
using System.Threading;

namespace AuraCore.UI.Avalonia.Helpers;

/// <summary>
/// Per-user single-instance lock via a named Mutex.
/// TryAcquire returns true iff this process is the first to claim the name;
/// false otherwise. Dispose releases the lock and is safe to call multiple times.
/// Name is scoped Local\ to the current user session — different users on
/// the same machine can each own their own instance.
/// </summary>
public sealed class InstanceMutex : IDisposable
{
    private static readonly object ProcessLockGuard = new();
    private static readonly HashSet<string> AcquiredInProcess = new();

    private readonly string _name;
    private Mutex? _mutex;
    private bool _owned;
    private bool _disposed;

    public InstanceMutex(string name)
    {
        _name = $"Local\\{name}";
    }

    public bool TryAcquire()
    {
        if (_disposed) return false;

        if (_mutex is null)
        {
            _mutex = new Mutex(false, _name);

            lock (ProcessLockGuard)
            {
                // Check if another InstanceMutex in this process already acquired this name.
                if (AcquiredInProcess.Contains(_name))
                {
                    _owned = false;
                    return false;
                }

                // Try to acquire the system Mutex non-blocking.
                try
                {
                    _owned = _mutex.WaitOne(TimeSpan.Zero, exitContext: false);
                }
                catch (AbandonedMutexException)
                {
                    _owned = true;
                }

                if (_owned)
                {
                    AcquiredInProcess.Add(_name);
                }
            }
        }
        return _owned;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        if (_mutex is not null)
        {
            lock (ProcessLockGuard)
            {
                if (_owned)
                {
                    try { _mutex.ReleaseMutex(); } catch { }
                    AcquiredInProcess.Remove(_name);
                }
            }
            try { _mutex.Dispose(); } catch { }
            _mutex = null;
        }
    }
}
