using System;

namespace Engine.Core;

public static class IDGenerator
{
    private static ulong _nextID = 1;
    private static readonly object _lock = new object();

    public static ulong Generate()
    {
        lock (_lock)
        {
            return _nextID++;
        }
    }

    public static void Reset()
    {
        lock (_lock)
        {
            _nextID = 1;
        }
    }
}

