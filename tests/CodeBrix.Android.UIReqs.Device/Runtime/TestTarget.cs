using System;
using System.Threading;
using System.Threading.Tasks;
using CodeBrix.Android.UI.Input;
using CodeBrix.Android.UIReqs.Device.Canvas;
using CodeBrix.Android.UIReqs.Device.Runtime;
using Windows.System;
using Windows.UI.Input.Preview.Injection;
using AChoreographer = Android.Views.Choreographer;
using AHandler = Android.OS.Handler;
using AInputSourceType = Android.Views.InputSourceType;
using AKeyCharacterMap = Android.Views.KeyCharacterMap;
using AKeyEvent = Android.Views.KeyEvent;
using AKeyEventActions = Android.Views.KeyEventActions;
using AKeycode = Android.Views.Keycode;
using ASystemClock = Android.OS.SystemClock;
using ALooper = Android.OS.Looper;

namespace CodeBrix.Android.UIReqs.Device.TestTarget;

/// <summary>The panel orientation a UIReqs run was declared for.</summary>
public enum TestDisplayOrientation
{
    /// <summary>Wider than tall.</summary>
    Landscape,

    /// <summary>Taller than wide.</summary>
    Portrait,
}

/// <summary>
/// One captured frame: the pixels of an <c>adb exec-out screencap</c> the host took after the
/// device had laid out and drawn, cropped to the app window (the app runs full screen, so the
/// window is the whole display). Straight RGBA, row-major, <see cref="Width"/> x <see cref="Height"/>.
/// </summary>
public sealed class TestFrame
{
    private readonly byte[] _rgba;

    /// <summary>Creates a frame.</summary>
    public TestFrame(byte[] rgba, int width, int height, long sequence, long renderGeneration, string? hostPath)
    {
        _rgba = rgba ?? throw new ArgumentNullException(nameof(rgba));
        Width = width;
        Height = height;
        Sequence = sequence;
        RenderGeneration = renderGeneration;
        HostPath = hostPath;
    }

    /// <summary>Width in device pixels.</summary>
    public int Width { get; }

    /// <summary>Height in device pixels.</summary>
    public int Height { get; }

    /// <summary>The capture number in this run.</summary>
    public long Sequence { get; }

    /// <summary>The device's Android frame counter when the capture was requested.</summary>
    public long RenderGeneration { get; }

    /// <summary>Where the host saved this frame (null when the host did not save it).</summary>
    public string? HostPath { get; }

    /// <summary>The pixel at (x, y).</summary>
    public PixelColor GetPixel(int x, int y)
    {
        if (x < 0 || y < 0 || x >= Width || y >= Height)
        {
            throw new ArgumentOutOfRangeException(nameof(x), $"({x},{y}) is outside the {Width} x {Height} frame.");
        }

        var i = ((y * Width) + x) * 4;
        return new PixelColor(_rgba[i], _rgba[i + 1], _rgba[i + 2], _rgba[i + 3]);
    }

    /// <summary>Frames are saved by the host (it took the screencap); the device never writes one.</summary>
    public void SavePng(string path) => throw new NotSupportedException("On Android the host runner saves frames (" + (HostPath ?? "not saved") + ").");
}

/// <summary>
/// The running scenario app as the copied harness sees it (the Android counterpart of the
/// emulated frame-buffer head's session): its panel size, the UI thread, input through Core's
/// OWN input injector (the managed pointer pipeline, exactly what the frame-buffer head feeds),
/// and the frame handshake: lay out, let Android draw, then ask the host for a screencap.
/// </summary>
public sealed class TestTargetSession
{
    private readonly AHandler _main = new(ALooper.MainLooper!);
    private long _sequence;
    private long _frames;
    private InputInjector? _injector;

    internal TestTargetSession(Func<(int Width, int Height, double Density)> panel)
    {
        Panel = panel;
    }

    /// <summary>The current panel size in pixels and the density.</summary>
    internal Func<(int Width, int Height, double Density)> Panel { get; }

    /// <summary>Panel width in device pixels.</summary>
    public int Width => Panel().Width;

    /// <summary>Panel height in device pixels.</summary>
    public int Height => Panel().Height;

    /// <summary>Always true once the session exists (the app was launched by the runner script).</summary>
    public bool IsLaunched => true;

    /// <summary>Runs an action on the UI thread.</summary>
    public Task RunOnUIThreadAsync(Action action)
    {
        var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _main.Post(() =>
        {
            try
            {
                action();
                done.TrySetResult();
            }
            catch (Exception exception)
            {
                done.TrySetException(exception);
            }
        });
        return done.Task;
    }

    /// <summary>Runs asynchronous work on the UI thread.</summary>
    public Task RunOnUIThreadAsync(Func<Task> action)
    {
        var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _main.Post(async () =>
        {
            try
            {
                await action().ConfigureAwait(true);
                done.TrySetResult();
            }
            catch (Exception exception)
            {
                done.TrySetException(exception);
            }
        });
        return done.Task;
    }

    /// <summary>
    /// Waits until Android has drawn two more frames after everything queued (the native views
    /// replayed the layout Core just did), then asks the host for a screencap.
    /// </summary>
    public async Task<TestFrame> RequestFrameAsync(TimeSpan timeout)
    {
        await WaitForAndroidFramesAsync(2).ConfigureAwait(false);

        // SurfaceFlinger composes the app's last buffer on the next vsync; give it that.
        await Task.Delay(50).ConfigureAwait(false);
        var sequence = Interlocked.Increment(ref _sequence);
        using var cancel = new CancellationTokenSource(timeout + TimeSpan.FromSeconds(10));
        return await HostChannel.CaptureAsync(sequence, Interlocked.Read(ref _frames), cancel.Token).ConfigureAwait(false);
    }

    /// <summary>A tap (press + release) at (x, y) device pixels.</summary>
    public void Tap(int x, int y)
    {
        TouchPress(1, x, y);
        TouchRelease(1, x, y);
    }

    /// <summary>A finger goes down.</summary>
    public void TouchPress(uint pointerId, int x, int y) =>
        Inject(pointerId, x, y, InjectedInputPointerOptions.New | InjectedInputPointerOptions.PointerDown | InjectedInputPointerOptions.InContact | InjectedInputPointerOptions.InRange | InjectedInputPointerOptions.FirstButton | InjectedInputPointerOptions.Primary);

    /// <summary>A finger moves while down.</summary>
    public void TouchMove(uint pointerId, int x, int y) =>
        Inject(pointerId, x, y, InjectedInputPointerOptions.Update | InjectedInputPointerOptions.InContact | InjectedInputPointerOptions.InRange | InjectedInputPointerOptions.FirstButton | InjectedInputPointerOptions.Primary);

    /// <summary>A finger lifts.</summary>
    public void TouchRelease(uint pointerId, int x, int y) =>
        Inject(pointerId, x, y, InjectedInputPointerOptions.PointerUp | InjectedInputPointerOptions.FirstButton | InjectedInputPointerOptions.InRange | InjectedInputPointerOptions.Primary);

    /// <summary>
    /// A key goes down: a real Android KeyEvent from the (virtual) hardware keyboard, dispatched to the
    /// activity - so it takes the path a key press takes (DispatchKeyEvent -> the window's keyboard input
    /// source -> Core's focused element, then the focused native widget). Core's own keyboard injection
    /// (InputInjector.InjectKeyboardInput) is a NotImplemented stub in the pinned build (FIXLIST).
    /// Held modifiers (Control, Shift, Alt) apply to the keys pressed while they are down.
    /// </summary>
    public void KeyDown(VirtualKey key) => DispatchKey(key, AKeyEventActions.Down);

    /// <summary>A key goes up (see <see cref="KeyDown"/>).</summary>
    public void KeyUp(VirtualKey key) => DispatchKey(key, AKeyEventActions.Up);

    /// <summary>
    /// Types text: the key events the virtual keyboard's character map produces for it (with Shift where a
    /// character needs it), each dispatched to the activity like <see cref="KeyDown"/>.
    /// </summary>
    public void TypeText(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        RunOnUIThreadAsync(() =>
        {
            using var map = AKeyCharacterMap.Load(VirtualKeyboardDeviceId);
            var events = map?.GetEvents(text.ToCharArray());
            if (events == null)
            {
                throw new InvalidOperationException("The virtual keyboard's character map cannot type \"" + text + "\".");
            }

            foreach (var e in events)
            {
                AppHost.Activity?.DispatchKeyEvent(e);
            }
        }).GetAwaiter().GetResult();
    }

    /// <summary>Counts Android frames (called from the activity's frame callback).</summary>
    internal void OnAndroidFrame() => Interlocked.Increment(ref _frames);

    /// <summary>KeyCharacterMap.VIRTUAL_KEYBOARD: the device id of key events no physical keyboard sent.</summary>
    private const int VirtualKeyboardDeviceId = -1;

    private VirtualKeyModifiers _heldModifiers;

    private void DispatchKey(VirtualKey key, AKeyEventActions action)
    {
        var code = VirtualKeyHelper.ToKeyCode(key);
        if (code == AKeycode.Unknown)
        {
            throw new NotSupportedException("The virtual key " + key + " has no Android key code.");
        }

        var modifier = key switch
        {
            VirtualKey.Control or VirtualKey.LeftControl or VirtualKey.RightControl => VirtualKeyModifiers.Control,
            VirtualKey.Shift or VirtualKey.LeftShift or VirtualKey.RightShift => VirtualKeyModifiers.Shift,
            VirtualKey.Menu or VirtualKey.LeftMenu or VirtualKey.RightMenu => VirtualKeyModifiers.Menu,
            _ => VirtualKeyModifiers.None,
        };

        if (action == AKeyEventActions.Down)
        {
            _heldModifiers |= modifier;
        }

        var meta = VirtualKeyHelper.ToMetaState(_heldModifiers);
        if (action == AKeyEventActions.Up)
        {
            _heldModifiers &= ~modifier;
        }

        RunOnUIThreadAsync(() =>
        {
            var now = ASystemClock.UptimeMillis();
            var e = new AKeyEvent(now, now, action, code, 0, meta, VirtualKeyboardDeviceId, 0, 0, AInputSourceType.Keyboard);
            AppHost.Activity?.DispatchKeyEvent(e);
        }).GetAwaiter().GetResult();
    }

    private void Inject(uint pointerId, int x, int y, InjectedInputPointerOptions options)
    {
        var density = Panel().Density;
        RunInjection(injector => injector.InjectTouchInput(new[]
        {
            new InjectedInputTouchInfo
            {
                PointerInfo = new InjectedInputPointerInfo
                {
                    PointerId = pointerId,
                    PointerOptions = options,
                    PixelLocation = new InjectedInputPoint { PositionX = (int)Math.Round(x / density), PositionY = (int)Math.Round(y / density) },
                },
                Pressure = (options & InjectedInputPointerOptions.PointerUp) != 0 ? 0 : 1.0,
            },
        }));
    }

    private void RunInjection(Action<InputInjector> injection)
    {
        RunOnUIThreadAsync(() =>
        {
            _injector ??= InputInjector.TryCreate() ?? throw new InvalidOperationException("Core has no input injector target on the UI thread.");
            InputTrace.EnsureAttached();
            injection(_injector);
        }).GetAwaiter().GetResult();
    }

    private Task WaitForAndroidFramesAsync(int count)
    {
        var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var remaining = count;
        void Callback(long _)
        {
            if (--remaining <= 0)
            {
                done.TrySetResult();
            }
            else
            {
                AChoreographer.Instance!.PostFrameCallback(new FrameCallback(Callback));
            }
        }

        _main.Post(() =>
        {
            // Nothing may be waiting to draw: invalidate the window so a frame is produced.
            AppHost.Activity?.RootLayout?.Invalidate();
            AChoreographer.Instance!.PostFrameCallback(new FrameCallback(Callback));
        });
        return done.Task;
    }

    private sealed class FrameCallback : Java.Lang.Object, AChoreographer.IFrameCallback
    {
        private readonly Action<long> _callback;

        internal FrameCallback(Action<long> callback) => _callback = callback;

        public void DoFrame(long frameTimeNanos) => _callback(frameTimeNanos);
    }
}
