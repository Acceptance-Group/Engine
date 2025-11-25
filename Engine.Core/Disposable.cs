using System;

namespace Engine.Core;

public abstract class Disposable : IDisposable
{
    private bool _disposed = false;

    public bool IsDisposed => _disposed;

    public void Dispose()
    {
        if (_disposed)
            return;

        Dispose(true);
        GC.SuppressFinalize(this);
        _disposed = true;
    }

    protected abstract void Dispose(bool disposing);

    protected void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(GetType().Name);
    }
}

