using System;
using AContext = global::Android.Content.Context;

namespace CodeBrix.Android.Android;

/// <summary>
/// Access to the process-wide Android application context.
/// </summary>
internal static class AndroidContext
{
    /// <summary>
    /// The application context (Android.App.Application.Context). Available once the
    /// Android Application object exists, i.e. from Application.OnCreate on.
    /// </summary>
    internal static AContext Current =>
        global::Android.App.Application.Context
        ?? throw new InvalidOperationException("The Android application context is not available yet.");
}
