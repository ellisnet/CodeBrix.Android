using System;
using System.Globalization;
using System.Runtime.InteropServices;
using CodeBrix.Android.WinUI.Graphics3DGL.Native;
using CodeBrix.Android.WinUI.Graphics3DGL.Portable;
using CodeBrix.Platform.Graphics;

namespace CodeBrix.Android.WinUI.Graphics3DGL.Android;

/// <summary>
/// The Android off-screen OpenGL context (the head's INativeOpenGLWrapper, created through ApiExtensibility by
/// GLCanvasElement once per XamlRoot and by OffscreenGLContext once per caller): an OpenGL ES 3 context on a 1x1 EGL
/// pbuffer of the default display.
/// <para>
/// Everything runs on the UI thread (the Core renders from the dispatcher). Android draws the window on its own
/// render thread with its own context, so making this one current on the UI thread disturbs nothing; each
/// <see cref="MakeCurrent"/> scope puts back whatever was current before it. The default display is shared with the
/// system's renderer and is never terminated here.
/// </para>
/// </summary>
internal sealed class AndroidEglOpenGLWrapper : INativeOpenGLWrapper
{
    private const string GlesLibrary = "libGLESv3.so";

    private static readonly object _libraryGate = new();
    private static IntPtr _gles;
    private static bool _glesTried;

    private IntPtr _display;
    private IntPtr _context;
    private IntPtr _surface;
    private bool _disposed;

    /// <summary>Creates the context.</summary>
    /// <exception cref="InvalidOperationException">EGL could not give an OpenGL ES 3 context (the message says which step failed).</exception>
    internal AndroidEglOpenGLWrapper()
    {
        _display = Egl.GetDisplay(Egl.None);
        if (_display == Egl.None)
        {
            throw Failure("eglGetDisplay returned no display");
        }

        if (Egl.Initialize(_display, out var major, out var minor) == 0)
        {
            throw Failure("eglInitialize failed");
        }

        EglVersion = string.Create(CultureInfo.InvariantCulture, $"{major}.{minor}");
        Egl.BindApi(EglAttributes.OpenGlEsApi);

        var configs = new IntPtr[1];
        if (Egl.ChooseConfig(_display, EglAttributes.Config(), configs, 1, out var count) == 0 || count < 1 || configs[0] == IntPtr.Zero)
        {
            throw Failure("no EGL config offers OpenGL ES 3 with an 8-bit RGBA pbuffer");
        }

        _context = Egl.CreateContext(_display, configs[0], Egl.None, EglAttributes.Context());
        if (_context == Egl.None)
        {
            throw Failure("eglCreateContext could not create an OpenGL ES 3 context");
        }

        _surface = Egl.CreatePbufferSurface(_display, configs[0], EglAttributes.Pbuffer());
        if (_surface == Egl.None)
        {
            Egl.DestroyContext(_display, _context);
            _context = Egl.None;
            throw Failure("eglCreatePbufferSurface failed");
        }
    }

    /// <summary>The EGL version of the default display ("1.4", "1.5").</summary>
    internal string EglVersion { get; }

    /// <inheritdoc />
    public IntPtr GetProcAddress(string proc) => TryGetProcAddress(proc, out var address) ? address : IntPtr.Zero;

    /// <inheritdoc />
    public bool TryGetProcAddress(string proc, out IntPtr addr)
    {
        addr = IntPtr.Zero;
        if (string.IsNullOrEmpty(proc))
        {
            return false;
        }

        // Core entry points from the GLES library itself; extension entry points from EGL.
        var gles = GlesLibraryHandle();
        if (gles != IntPtr.Zero && NativeLibrary.TryGetExport(gles, proc, out var export))
        {
            addr = export;
        }
        else
        {
            addr = Egl.GetProcAddress(proc);
        }

        return addr != IntPtr.Zero;
    }

    /// <inheritdoc />
    public IDisposable MakeCurrent()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var previous = new CurrentContext(
            Egl.GetCurrentDisplay(),
            Egl.GetCurrentSurface(EglAttributes.Draw),
            Egl.GetCurrentSurface(EglAttributes.Read),
            Egl.GetCurrentContext());

        if (Egl.MakeCurrent(_display, _surface, _surface, _context) == 0)
        {
            throw Failure("eglMakeCurrent failed");
        }

        return new Restorer(_display, previous);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (Egl.GetCurrentContext() == _context)
        {
            Egl.MakeCurrent(_display, Egl.None, Egl.None, Egl.None);
        }

        if (_surface != Egl.None)
        {
            Egl.DestroySurface(_display, _surface);
            _surface = Egl.None;
        }

        if (_context != Egl.None)
        {
            Egl.DestroyContext(_display, _context);
            _context = Egl.None;
        }

        _display = Egl.None;
    }

    private static IntPtr GlesLibraryHandle()
    {
        lock (_libraryGate)
        {
            if (!_glesTried)
            {
                _glesTried = true;
                NativeLibrary.TryLoad(GlesLibrary, out _gles);
            }

            return _gles;
        }
    }

    private static InvalidOperationException Failure(string step) => new(string.Create(
        CultureInfo.InvariantCulture,
        $"The Android off-screen OpenGL context could not be created: {step} (EGL error 0x{Egl.GetError():X4}). OpenGL ES 3.0 is required."));

    private readonly record struct CurrentContext(IntPtr Display, IntPtr Draw, IntPtr Read, IntPtr Context);

    private sealed class Restorer : IDisposable
    {
        private readonly IntPtr _display;
        private readonly CurrentContext _previous;
        private bool _done;

        internal Restorer(IntPtr display, CurrentContext previous)
        {
            _display = display;
            _previous = previous;
        }

        public void Dispose()
        {
            if (_done)
            {
                return;
            }

            _done = true;
            if (_previous.Context == Egl.None || _previous.Display == Egl.None)
            {
                Egl.MakeCurrent(_display, Egl.None, Egl.None, Egl.None);
            }
            else
            {
                Egl.MakeCurrent(_previous.Display, _previous.Draw, _previous.Read, _previous.Context);
            }
        }
    }
}
