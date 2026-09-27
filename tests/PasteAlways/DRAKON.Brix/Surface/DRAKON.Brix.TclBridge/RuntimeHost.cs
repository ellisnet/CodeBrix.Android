// STUB (paste always): DRAKON.Brix.Drakon.RuntimeHost (CodeBrix.Samples DRAKON.Brix/src/libs/DRAKON.Brix.TclBridge/
// RuntimeHost.cs); the original starts DrakonRuntime, which runs the DRAKON Editor Tcl on the TclTk engine and the
// TkCanvas toolkit (packages that depend on CodeBrix.Platform, not allowed in a paste-always head; TkCanvas is D-O14
// NotSupported on Android). Same namespace, type name and public members the page calls.
using System;
using CodeBrix.Platform.TkCanvas.Hosting;

namespace DRAKON.Brix.Drakon;

/// <summary>Stand-in for the application-facing owner of the DRAKON Tcl runtime (compile-only).</summary>
public sealed class RuntimeHost : IDisposable
{
    /// <summary>Would create and start the DRAKON runtime inside the given host view.</summary>
    public void Start(TkHostView host)
    {
        if (host == null) { throw new ArgumentNullException(nameof(host)); }
    }

    /// <summary>Stops the runtime (nothing to stop here).</summary>
    public void Dispose()
    {
    }
}
