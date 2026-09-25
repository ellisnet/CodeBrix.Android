using CodeBrix.Platform.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace CodeBrix.Android.UI.Hosting;

/// <summary>
/// Loggers of the CodeBrix.Android hosting, from the ambient logger factory the
/// application configured (CodeBrix.Platform.Extensions.LogExtensionPoint).
/// </summary>
internal static class HostLog
{
    /// <summary>Returns a logger for the category, or a null logger before logging is configured.</summary>
    internal static ILogger For(string category) =>
        LogExtensionPoint.AmbientLoggerFactory?.CreateLogger(category) ?? NullLogger.Instance;
}
