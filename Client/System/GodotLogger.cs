using Godot;
using Microsoft.Extensions.Logging;
using System;

namespace Tower.System;

public class GodotLoggerProvider : ILoggerProvider
{
    public ILogger CreateLogger(string categoryName)
    {
        return new GodotLogger(categoryName);
    }
    
    public void Dispose() { }
}

public class GodotLogger(string categoryName) : ILogger
{
    private readonly string _categoryName = categoryName;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
    {
        var message = formatter(state, exception);
        var fullMessage = $"[{_categoryName}] {message}";

        switch (logLevel)
        {
            case LogLevel.Trace:
            case LogLevel.Debug:
            case LogLevel.Information:
                GD.Print(fullMessage);
                break;
            case LogLevel.Warning:
            case LogLevel.Error:
            case LogLevel.Critical:
                GD.PrintErr(fullMessage);
                break;
            default:
                GD.Print(fullMessage);
                break;
        }
    }

    public bool IsEnabled(LogLevel logLevel) => true;

    public IDisposable BeginScope<TState>(TState state) where TState : notnull => default!;
}