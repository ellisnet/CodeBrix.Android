using CodeBrix.Platform.AppSettings;
using System;

namespace GitHubIssueFinder.Settings;

/// <summary>
/// Minimal logging facade for the settings backend, forwarding to the AppSettings add-in's
/// logging service (console output by default, plus any registered sinks).
/// </summary>
public static class LoggingService
{
    /// <summary>Registers a sink that receives every logged line.</summary>
    /// <param name="sink">The handler called with each line.</param>
    public static void AddSink(Action<string> sink) => AppSettingLoggingService.AddSink(sink);

    /// <summary>Logs an informational message.</summary>
    /// <param name="message">The text to log.</param>
    public static void LogInfo(string message) => AppSettingLoggingService.LogInfo(message);

    /// <summary>Logs a warning message.</summary>
    /// <param name="message">The text to log.</param>
    public static void LogWarning(string message) => AppSettingLoggingService.LogWarning(message);

    /// <summary>Logs an error message.</summary>
    /// <param name="message">The text to log.</param>
    public static void LogError(string message) => AppSettingLoggingService.LogError(message);

    /// <summary>Logs an error message with exception details.</summary>
    /// <param name="message">The text to log.</param>
    /// <param name="ex">The error to describe alongside the message.</param>
    public static void LogError(string message, Exception ex) => AppSettingLoggingService.LogError(message, ex);
}
