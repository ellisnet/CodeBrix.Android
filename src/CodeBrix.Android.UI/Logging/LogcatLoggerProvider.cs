using System;
using Microsoft.Extensions.Logging;
using ALog = global::Android.Util.Log;
using ALogPriority = global::Android.Util.LogPriority;

namespace CodeBrix.Android.UI.Logging;

/// <summary>
/// A Microsoft.Extensions.Logging provider that writes to Android's logcat. Every entry
/// uses one tag (by default the application's package name) and starts with the logger
/// category, so <c>adb logcat -s &lt;package&gt;</c> shows the whole application log.
/// </summary>
public sealed class LogcatLoggerProvider : ILoggerProvider
{
    private readonly string _tag;

    /// <summary>Creates a provider that logs with the given logcat tag.</summary>
    /// <param name="tag">The logcat tag (the application's package name is the convention).</param>
    public LogcatLoggerProvider(string tag)
    {
        _tag = string.IsNullOrWhiteSpace(tag) ? "CodeBrix" : tag;
    }

    /// <summary>The logcat tag every entry is written with.</summary>
    public string Tag => _tag;

    /// <inheritdoc />
    public ILogger CreateLogger(string categoryName) => new LogcatLogger(_tag, categoryName);

    /// <inheritdoc />
    public void Dispose()
    {
    }

    private sealed class LogcatLogger : ILogger
    {
        private readonly string _tag;
        private readonly string _category;

        public LogcatLogger(string tag, string category)
        {
            _tag = tag;
            _category = category ?? string.Empty;
        }

        public IDisposable BeginScope<TState>(TState state) => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
            if (!IsEnabled(logLevel) || formatter == null)
            {
                return;
            }

            var message = _category + ": " + formatter(state, exception);
            if (exception != null)
            {
                message += Environment.NewLine + exception;
            }

            ALog.WriteLine(ToPriority(logLevel), _tag, message);
        }

        private static ALogPriority ToPriority(LogLevel level) => level switch
        {
            LogLevel.Trace => ALogPriority.Verbose,
            LogLevel.Debug => ALogPriority.Debug,
            LogLevel.Information => ALogPriority.Info,
            LogLevel.Warning => ALogPriority.Warn,
            LogLevel.Error => ALogPriority.Error,
            _ => ALogPriority.Assert,
        };
    }
}
