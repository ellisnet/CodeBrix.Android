using System;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;

namespace PolyHavenBrowser.Rendering;

/// <summary>
/// The raw <b>direct-to-Metal interop layer</b> used by <see cref="MetalSceneRenderer"/>. It
/// speaks to the Objective-C runtime (<c>objc_getClass</c>/<c>sel_registerName</c>/
/// <c>objc_msgSend</c>) and the Metal framework by dylib path - no MoltenVK, no Silk.NET, no
/// managed Apple bindings, and <b>no NuGet packages</b>: everything here is P/Invoke against
/// libraries present on every Mac since 10.14. These entry points are only ever called on the
/// macOS head (gated by <see cref="MetalPlatformSupport"/>); on other platforms the DllImports
/// simply never resolve because the code path is never taken.
/// <para>
/// <b>Both architectures.</b> The design deliberately avoids any Objective-C method that
/// <i>returns</i> a struct by value, which is the only place the calling convention differs
/// between Apple Silicon (arm64, one unified <c>objc_msgSend</c>) and Intel/Rosetta (x86-64,
/// which would need <c>objc_msgSend_stret</c>). Because every message here returns an
/// <c>id</c>/pointer, <c>void</c>, or is passed a struct only as an <i>argument</i> (which the
/// P/Invoke marshaller lays out per-architecture automatically), the single <c>objc_msgSend</c>
/// entry point is correct on both. The concrete (non-variadic) signatures below are the
/// standard, supported way to call <c>objc_msgSend</c> - the same approach every Objective-C
/// binding uses.
/// </para>
/// </summary>
internal static unsafe class MetalInterop
{
    private const string Objc = "/usr/lib/libobjc.A.dylib";
    private const string Metal = "/System/Library/Frameworks/Metal.framework/Metal";

    // ---- Objective-C runtime + Metal framework entry points -------------------------------

    [DllImport(Objc, EntryPoint = "objc_getClass")]
    private static extern IntPtr objc_getClass([MarshalAs(UnmanagedType.LPUTF8Str)] string name);

    [DllImport(Objc, EntryPoint = "sel_registerName")]
    private static extern IntPtr sel_registerName([MarshalAs(UnmanagedType.LPUTF8Str)] string name);

    [DllImport(Metal, EntryPoint = "MTLCreateSystemDefaultDevice")]
    internal static extern IntPtr MTLCreateSystemDefaultDevice();

    // ---- objc_msgSend, one concrete signature per call shape (never a struct return) ------

    [DllImport(Objc, EntryPoint = "objc_msgSend")]
    internal static extern IntPtr Send(IntPtr receiver, IntPtr selector);

    [DllImport(Objc, EntryPoint = "objc_msgSend")]
    internal static extern void SendV(IntPtr receiver, IntPtr selector);

    [DllImport(Objc, EntryPoint = "objc_msgSend")]
    internal static extern IntPtr Send(IntPtr receiver, IntPtr selector, IntPtr a);

    [DllImport(Objc, EntryPoint = "objc_msgSend")]
    internal static extern void SendV(IntPtr receiver, IntPtr selector, IntPtr a);

    [DllImport(Objc, EntryPoint = "objc_msgSend")]
    internal static extern IntPtr Send(IntPtr receiver, IntPtr selector, nuint a);

    [DllImport(Objc, EntryPoint = "objc_msgSend")]
    internal static extern void SendV(IntPtr receiver, IntPtr selector, nuint a);

    [DllImport(Objc, EntryPoint = "objc_msgSend")]
    internal static extern void SendV(IntPtr receiver, IntPtr selector, byte a);

    [DllImport(Objc, EntryPoint = "objc_msgSend")]
    internal static extern void SendV(IntPtr receiver, IntPtr selector, double a);

    [DllImport(Objc, EntryPoint = "objc_msgSend")]
    internal static extern IntPtr Send(IntPtr receiver, IntPtr selector, nuint a, nuint b);

    // newBufferWithBytes:length:options:
    [DllImport(Objc, EntryPoint = "objc_msgSend")]
    internal static extern IntPtr Send(IntPtr receiver, IntPtr selector, IntPtr a, nuint b, nuint c);

    // setVertexBuffer:offset:atIndex: / setVertexBytes:length:atIndex: / setFragmentBytes:length:atIndex:
    [DllImport(Objc, EntryPoint = "objc_msgSend")]
    internal static extern void SendV(IntPtr receiver, IntPtr selector, IntPtr a, nuint b, nuint c);

    // setFragmentTexture:atIndex: / setFragmentSamplerState:atIndex:
    [DllImport(Objc, EntryPoint = "objc_msgSend")]
    internal static extern void SendV(IntPtr receiver, IntPtr selector, IntPtr a, nuint b);

    // newRenderPipelineStateWithDescriptor:error:
    [DllImport(Objc, EntryPoint = "objc_msgSend")]
    internal static extern IntPtr Send(IntPtr receiver, IntPtr selector, IntPtr a, out IntPtr error);

    // newLibraryWithSource:options:error:
    [DllImport(Objc, EntryPoint = "objc_msgSend")]
    internal static extern IntPtr Send(IntPtr receiver, IntPtr selector, IntPtr a, IntPtr b, out IntPtr error);

    // setClearColor: (struct argument - marshalled per-architecture)
    [DllImport(Objc, EntryPoint = "objc_msgSend")]
    internal static extern void SendV(IntPtr receiver, IntPtr selector, MTLClearColor a);

    // setViewport: (struct argument)
    [DllImport(Objc, EntryPoint = "objc_msgSend")]
    internal static extern void SendV(IntPtr receiver, IntPtr selector, MTLViewport a);

    // drawIndexedPrimitives:indexCount:indexType:indexBuffer:indexBufferOffset:
    [DllImport(Objc, EntryPoint = "objc_msgSend")]
    internal static extern void SendV(
        IntPtr receiver, IntPtr selector, nuint a, nuint b, nuint c, IntPtr d, nuint e);

    // copyFromTexture:sourceSlice:sourceLevel:sourceOrigin:sourceSize:toBuffer:destinationOffset:destinationBytesPerRow:destinationBytesPerImage:
    [DllImport(Objc, EntryPoint = "objc_msgSend")]
    internal static extern void SendCopyTextureToBuffer(
        IntPtr receiver, IntPtr selector,
        IntPtr texture, nuint slice, nuint level, MTLOrigin origin, MTLSize size,
        IntPtr buffer, nuint offset, nuint bytesPerRow, nuint bytesPerImage);

    // copyFromBuffer:sourceOffset:sourceBytesPerRow:sourceBytesPerImage:sourceSize:toTexture:destinationSlice:destinationLevel:destinationOrigin:
    [DllImport(Objc, EntryPoint = "objc_msgSend")]
    internal static extern void SendCopyBufferToTexture(
        IntPtr receiver, IntPtr selector,
        IntPtr buffer, nuint offset, nuint bytesPerRow, nuint bytesPerImage, MTLSize size,
        IntPtr texture, nuint slice, nuint level, MTLOrigin origin);

    // ---- Cached class + selector lookups ---------------------------------------------------

    private static readonly ConcurrentDictionary<string, IntPtr> Classes = new();
    private static readonly ConcurrentDictionary<string, IntPtr> Selectors = new();

    /// <summary>Returns the Objective-C class with the given name (cached).</summary>
    internal static IntPtr Cls(string name) => Classes.GetOrAdd(name, objc_getClass);

    /// <summary>Returns (registers) the selector with the given name (cached).</summary>
    internal static IntPtr Sel(string name) => Selectors.GetOrAdd(name, sel_registerName);

    // ---- Small semantic helpers over the raw Send/SendV overloads --------------------------

    /// <summary><c>[[cls alloc] init]</c> - a new, owned (+1 retain) instance.</summary>
    internal static IntPtr New(string className)
    {
        var instance = Send(Cls(className), Sel("alloc"));
        return Send(instance, Sel("init"));
    }

    /// <summary>Sends a no-argument message that returns an <c>id</c>.</summary>
    internal static IntPtr GetId(IntPtr obj, string selector) => Send(obj, Sel(selector));

    /// <summary>Sends a message with one <c>NSUInteger</c> argument that returns an <c>id</c> (e.g. <c>objectAtIndexedSubscript:</c>).</summary>
    internal static IntPtr GetIdAt(IntPtr obj, string selector, nuint index) => Send(obj, Sel(selector), index);

    /// <summary>Sends a message with a single <c>id</c>/pointer argument, returning <c>void</c>.</summary>
    internal static void SetPtr(IntPtr obj, string selector, IntPtr value) => SendV(obj, Sel(selector), value);

    /// <summary>Sends a message with a single <c>NSUInteger</c> argument, returning <c>void</c>.</summary>
    internal static void SetUInt(IntPtr obj, string selector, nuint value) => SendV(obj, Sel(selector), value);

    /// <summary>Sends a message with a single <c>BOOL</c> argument (marshalled as a byte), returning <c>void</c>.</summary>
    internal static void SetBool(IntPtr obj, string selector, bool value) => SendV(obj, Sel(selector), (byte)(value ? 1 : 0));

    /// <summary>Sends a no-argument message that returns <c>void</c> (e.g. <c>release</c>, <c>commit</c>).</summary>
    internal static void Call(IntPtr obj, string selector) => SendV(obj, Sel(selector));

    /// <summary>Releases (<c>-release</c>) an owned Objective-C object, tolerating null.</summary>
    internal static void Release(IntPtr obj)
    {
        if (obj != IntPtr.Zero) { SendV(obj, Sel("release")); }
    }

    /// <summary>
    /// Creates an autoreleased <c>NSString</c> from a managed string. Valid only while an
    /// autorelease pool is active on the calling thread (the renderer wraps every frame and its
    /// one-time init in a pool).
    /// </summary>
    internal static IntPtr NSString(string value)
    {
        var utf8 = Marshal.StringToCoTaskMemUTF8(value);
        try
        {
            return Send(Cls("NSString"), Sel("stringWithUTF8String:"), utf8);
        }
        finally
        {
            Marshal.FreeCoTaskMem(utf8);
        }
    }

    /// <summary>Reads an <c>NSString</c> (e.g. an <c>NSError</c>'s localizedDescription) into a managed string.</summary>
    internal static string? NSStringToManaged(IntPtr nsString)
    {
        if (nsString == IntPtr.Zero) { return null; }
        var utf8 = Send(nsString, Sel("UTF8String"));
        return utf8 == IntPtr.Zero ? null : Marshal.PtrToStringUTF8(utf8);
    }

    /// <summary>Allocates and initializes an <c>NSAutoreleasePool</c>; drain it with <see cref="Release"/>.</summary>
    internal static IntPtr NewAutoreleasePool() => New("NSAutoreleasePool");
}

// ---- Metal value types passed by value as arguments (never returned by value) --------------

/// <summary>Mirrors <c>MTLClearColor</c> (four doubles).</summary>
[StructLayout(LayoutKind.Sequential)]
internal struct MTLClearColor
{
    public double Red;
    public double Green;
    public double Blue;
    public double Alpha;
}

/// <summary>Mirrors <c>MTLViewport</c> (six doubles).</summary>
[StructLayout(LayoutKind.Sequential)]
internal struct MTLViewport
{
    public double OriginX;
    public double OriginY;
    public double Width;
    public double Height;
    public double ZNear;
    public double ZFar;
}

/// <summary>Mirrors <c>MTLOrigin</c> (three <c>NSInteger</c>).</summary>
[StructLayout(LayoutKind.Sequential)]
internal struct MTLOrigin
{
    public nint X;
    public nint Y;
    public nint Z;
}

/// <summary>Mirrors <c>MTLSize</c> (three <c>NSUInteger</c>).</summary>
[StructLayout(LayoutKind.Sequential)]
internal struct MTLSize
{
    public nuint Width;
    public nuint Height;
    public nuint Depth;
}
