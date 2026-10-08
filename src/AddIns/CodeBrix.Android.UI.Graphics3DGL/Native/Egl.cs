using System;
using System.Runtime.InteropServices;

namespace CodeBrix.Android.UI.Graphics3DGL.Native;

/// <summary>
/// The EGL 1.4 entry points of the Android system library libEGL.so (part of every Android image since API 9; no
/// package). Handles are opaque pointers; booleans are EGLBoolean (unsigned int).
/// </summary>
internal static class Egl
{
    private const string Library = "libEGL.so";

    /// <summary>EGL_NO_DISPLAY / EGL_NO_CONTEXT / EGL_NO_SURFACE / EGL_DEFAULT_DISPLAY.</summary>
    internal static readonly IntPtr None = IntPtr.Zero;

    [DllImport(Library, EntryPoint = "eglGetDisplay")]
    internal static extern IntPtr GetDisplay(IntPtr nativeDisplay);

    [DllImport(Library, EntryPoint = "eglInitialize")]
    internal static extern uint Initialize(IntPtr display, out int major, out int minor);

    [DllImport(Library, EntryPoint = "eglBindAPI")]
    internal static extern uint BindApi(uint api);

    [DllImport(Library, EntryPoint = "eglChooseConfig")]
    internal static extern uint ChooseConfig(IntPtr display, int[] attributes, IntPtr[] configs, int configSize, out int count);

    [DllImport(Library, EntryPoint = "eglCreateContext")]
    internal static extern IntPtr CreateContext(IntPtr display, IntPtr config, IntPtr shareContext, int[] attributes);

    [DllImport(Library, EntryPoint = "eglCreatePbufferSurface")]
    internal static extern IntPtr CreatePbufferSurface(IntPtr display, IntPtr config, int[] attributes);

    [DllImport(Library, EntryPoint = "eglMakeCurrent")]
    internal static extern uint MakeCurrent(IntPtr display, IntPtr draw, IntPtr read, IntPtr context);

    [DllImport(Library, EntryPoint = "eglGetCurrentDisplay")]
    internal static extern IntPtr GetCurrentDisplay();

    [DllImport(Library, EntryPoint = "eglGetCurrentContext")]
    internal static extern IntPtr GetCurrentContext();

    [DllImport(Library, EntryPoint = "eglGetCurrentSurface")]
    internal static extern IntPtr GetCurrentSurface(int readOrDraw);

    [DllImport(Library, EntryPoint = "eglDestroySurface")]
    internal static extern uint DestroySurface(IntPtr display, IntPtr surface);

    [DllImport(Library, EntryPoint = "eglDestroyContext")]
    internal static extern uint DestroyContext(IntPtr display, IntPtr context);

    [DllImport(Library, EntryPoint = "eglGetError")]
    internal static extern int GetError();

    [DllImport(Library, EntryPoint = "eglGetProcAddress")]
    internal static extern IntPtr GetProcAddress([MarshalAs(UnmanagedType.LPStr)] string name);
}
