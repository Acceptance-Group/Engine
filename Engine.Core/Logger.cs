using System;
using System.Collections.Generic;

namespace Engine.Core;

public enum LogLevel
{
    Debug,
    Info,
    Warning,
    Error
}

public static class Logger
{
    private static readonly List<LogEntry> _logs = new List<LogEntry>();
    private static readonly object _lock = new object();
    private static LogLevel _minLevel = LogLevel.Debug;

    public static event Action<LogEntry>? OnLog;

    public static LogLevel MinLevel
    {
        get => _minLevel;
        set => _minLevel = value;
    }

    public static IReadOnlyList<LogEntry> Logs
    {
        get
        {
            lock (_lock)
            {
                return _logs.AsReadOnly();
            }
        }
    }

    public static void Debug(string message)
    {
        Log(LogLevel.Debug, message);
    }

    public static void Info(string message)
    {
        Log(LogLevel.Info, message);
    }

    public static void Warning(string message)
    {
        Log(LogLevel.Warning, message);
    }

    public static void Error(string message)
    {
        Log(LogLevel.Error, message);
    }

    public static void Error(Exception exception, string? message = null)
    {
        string fullMessage = message != null ? $"{message}: {exception}" : exception.ToString();
        Log(LogLevel.Error, fullMessage);
    }

    private static void Log(LogLevel level, string message)
    {
        if (level < _minLevel)
            return;

        var entry = new LogEntry(level, message, DateTime.Now);

        lock (_lock)
        {
            _logs.Add(entry);
            if (_logs.Count > 1000)
                _logs.RemoveAt(0);
        }

        OnLog?.Invoke(entry);
    }

    public static void Clear()
    {
        lock (_lock)
        {
            _logs.Clear();
        }
    }
}

public class LogEntry
{
    public LogLevel Level { get; }
    public string Message { get; }
    public DateTime Timestamp { get; }

    public LogEntry(LogLevel level, string message, DateTime timestamp)
    {
        Level = level;
        Message = message;
        Timestamp = timestamp;
    }
}

