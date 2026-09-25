//
// LoggingService.cs
//
// Copyright (c) 2026 Jeremy Ellis and contributors
//     (a thin forwarder to the CodeBrix.Platform.AppSettings add-in's
//      AppSettingLoggingService)
// SPDX-License-Identifier: MIT
//

using System;
using CodeBrix.Platform.AppSettings;

namespace Pinta.Brix.Settings;

/// <summary>
/// Minimal logging facade for the settings backend, forwarding to the
/// AppSettings add-in's logging service (console output by default, plus
/// any registered sinks).
/// </summary>
public static class LoggingService
{
    /// <summary>Registers a sink that receives every logged line.</summary>
    public static void AddSink(Action<string> sink) => AppSettingLoggingService.AddSink(sink);

    /// <summary>Logs an informational message.</summary>
    public static void LogInfo(string message) => AppSettingLoggingService.LogInfo(message);

    /// <summary>Logs a warning message.</summary>
    public static void LogWarning(string message) => AppSettingLoggingService.LogWarning(message);

    /// <summary>Logs an error message.</summary>
    public static void LogError(string message) => AppSettingLoggingService.LogError(message);

    /// <summary>Logs an error message with exception details.</summary>
    public static void LogError(string message, Exception ex) => AppSettingLoggingService.LogError(message, ex);
}
