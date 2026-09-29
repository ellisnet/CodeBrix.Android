using System;
using System.Threading;
using System.Threading.Tasks;
using CodeBrix.Android.UI.Input;
using CodeBrix.Android.UI.Input.TextInput;
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
    private bool _softKeyboardSessionWasOpen;
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

    /// <summary>Waits through two Android frame callbacks without an adb screenshot round trip.</summary>
    /// <param name="timeout">Maximum time allowed for the frame callbacks.</param>
    /// <returns>A task that completes after the preceding frame has rendered.</returns>
    public Task WaitForRenderAsync(TimeSpan timeout) =>
        WaitForAndroidFramesAsync(2).WaitAsync(timeout, StepServer.SessionToken);

    /// <summary>
    /// Waits until Android has drawn two more frames after everything queued (the native views
    /// replayed the layout Core just did), then asks the host for a screencap.
    /// </summary>
    public async Task<TestFrame> RequestFrameAsync(TimeSpan timeout)
    {
        await ClipboardCaptureWait.WaitAsync(StepServer.SessionToken).ConfigureAwait(false);
        await WaitForSoftKeyboardSettledAsync().ConfigureAwait(false);
        await RunOnUIThreadAsync(HideNativeCarets).ConfigureAwait(false);
        await WaitForAndroidFramesAsync(2).ConfigureAwait(false);

        // SurfaceFlinger composes the app's last buffer on the next vsync; give it that.
        await Task.Delay(50).ConfigureAwait(false);
        // A clipboard notification queued with the UI work above may have arrived after the first check.
        await ClipboardCaptureWait.WaitAsync(StepServer.SessionToken).ConfigureAwait(false);
        int[]? imeRect = null;
        await RunOnUIThreadAsync(() => imeRect = GetImeScreenRect()).ConfigureAwait(false);
        var sequence = Interlocked.Increment(ref _sequence);
        using var cancel = new CancellationTokenSource(timeout + TimeSpan.FromSeconds(10));
        return await HostChannel.CaptureAsync(sequence, Interlocked.Read(ref _frames), cancel.Token, imeRect).ConfigureAwait(false);
    }

    /// <summary>
    /// [AP7-B TerminalView RE-GATE 2, coordinator 01:28 + 02:31] The text caret of a native editor (the EditText a TextBox,
    /// PasswordBox, AutoSuggestBox or any other editor handler shows) blinks, and its phase at capture time cannot be observed:
    /// the same frame showed the caret in every touched-groups run and not in any full pass. Before every capture, every EditText
    /// in the activity's window gets a caret drawable and an insertion-handle drawable that draw NOTHING, with the originals'
    /// intrinsic sizes (TextView.setTextCursorDrawable / setTextSelectHandle, API 29+): the Editor's blink and bring-into-view
    /// logic keep running. NOT CursorVisible=false: that stops the EditText re-positioning its text after a theme switch
    /// (ThemeFocus/Theme Scenario3 02_dark drew its text ~14 px lower; FIXLIST 02:27). MEASURED (dev3/dev4): the insertion
    /// handle disappears, but a 2-3 px caret column is STILL drawn in the Material TextInputEditText (not through this
    /// drawable); FIXLIST 02:4x. [AP8-S item 3] That column was the Editor's CACHED cursor drawable (cached at its first caret
    /// draw, never replaced afterwards): <see cref="InstallCaretHook"/> now sets the draw-nothing drawables on every EditText's
    /// first layout, before any caret is drawn; this pre-capture pass stays for views created and focused within one frame.
    /// Harness only; no UIReqs scenario asserts a native caret. UI thread.
    /// </summary>
    /// <summary>
    /// [AP8-S item 3] Installs the caret hiding BEFORE any caret is drawn: the Editor of an EditText caches its cursor
    /// drawable the first time it draws the caret (Editor.loadCursorDrawable only loads it while its cache is empty), so a
    /// draw-nothing drawable set later - at capture time, after the field took the focus and blinked - never replaces the
    /// cached one: that is the 2-3 px caret column the capture-time replacement could not hide. A global-layout listener on
    /// the window gives every EditText the draw-nothing drawables on its first layout, before it is focused and drawn.
    /// UI thread; once per activity.
    /// </summary>
    internal static void InstallCaretHook(global::Android.App.Activity activity)
    {
        if (activity?.Window?.DecorView is not { } decor || decor.GetTag(CaretHookTag) != null)
        {
            return;
        }

        decor.SetTag(CaretHookTag, Java.Lang.Boolean.True);
        decor.ViewTreeObserver!.GlobalLayout += (_, _) => HideCarets(decor);
    }

    private const int CaretHookTag = 0x7f0cb0a1;

    private static void HideNativeCarets()
    {
        if (AppHost.Activity?.Window?.DecorView is { } decor)
        {
            HideCarets(decor);
        }
    }

    private static void HideCarets(global::Android.Views.View view)
    {
        if (view is global::Android.Widget.EditText edit)
        {
            // A drawable that draws NOTHING whatever tint the Material TextInputLayout applies to it (a transparent
            // GradientDrawable was re-tinted to the cursor colour and drawn), with the original's intrinsic size, for the
            // caret and for the insertion handle (the teardrop under the caret, its own popup; same size keeps its layout).
            if (edit.TextCursorDrawable is not EmptyCaretDrawable)
            {
                edit.TextCursorDrawable = new EmptyCaretDrawable(edit.TextCursorDrawable);
            }

            if (edit.TextSelectHandle is not EmptyCaretDrawable)
            {
                edit.TextSelectHandle = new EmptyCaretDrawable(edit.TextSelectHandle);
            }

            return;
        }

        if (view is global::Android.Views.ViewGroup group)
        {
            for (var i = 0; i < group.ChildCount; i++)
            {
                if (group.GetChildAt(i) is { } child)
                {
                    HideCarets(child);
                }
            }
        }
    }

    /// <summary>
    /// [AP7-B TerminalView RE-GATE 2] While the soft keyboard is visible (RootWindowInsets ime(); the settle wait has
    /// already run), its rectangle in SCREEN pixels as [left, top, right, bottom) - the window's full width, the bottom
    /// <c>ime().bottom</c> pixels of the window, from the window's bounds on the screen (both orientations; a pan does not move it) -
    /// so the host can mask it in the frame it saves (the keyboard's content is system UI and not deterministic). Null
    /// when the keyboard is not visible. UI thread.
    /// </summary>
    private static int[]? GetImeScreenRect()
    {
        var decor = AppHost.Activity?.Window?.DecorView;
        var insets = decor?.RootWindowInsets;
        if (decor == null || insets == null || !insets.IsVisible(global::Android.Views.WindowInsets.Type.Ime()))
        {
            return null;
        }

        var bottom = insets.GetInsets(global::Android.Views.WindowInsets.Type.Ime()).Bottom;
        if (bottom <= 0)
        {
            return null;
        }

        // [AP8-S item K] From the window's own bounds on the screen (its metrics), not the decor view's GetLocationOnScreen:
        // a window panned for the keyboard (the default adjustPan) reports its views higher by the pan, the keyboard does
        // not move. Unpanned, both give the same rectangle.
        var window = AppHost.Activity!.WindowManager!.CurrentWindowMetrics.Bounds;
        return new[] { window.Left, window.Bottom - bottom, window.Right, window.Bottom };
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

    /// <summary>
    /// [AP7-B TerminalView RE-GATE] The soft keyboard is system UI: its show/hide request and animation do not move Core's
    /// render generation, so a capture could catch it half-way (stability run 1, Landscape TerminalView "the live tail":
    /// no keyboard yet, the next frames with it). Before a capture: while a custom control's soft-keyboard session is open
    /// (CoreTextInput) or the focused Android view is an editor the input method is serving (a native TextBox/PasswordBox;
    /// coordinator 22:42) and the keyboard is not yet visible, just after such a session closed while it is still visible,
    /// or while an IME insets animation runs, wait for it to settle - bounded (2 s), then capture anyway.
    /// </summary>
    private async Task WaitForSoftKeyboardSettledAsync()
    {
        var started = System.Diagnostics.Stopwatch.StartNew();
        var sessionOpen = false;
        while (true)
        {
            var settled = true;
            await RunOnUIThreadAsync(() =>
            {
                var activity = AppHost.Activity;
                var insets = activity?.Window?.DecorView?.RootWindowInsets;
                var focused = activity?.CurrentFocus;
                var imm = activity?.GetSystemService(global::Android.Content.Context.InputMethodService) as global::Android.Views.InputMethods.InputMethodManager;
                sessionOpen = CoreTextInputController.Current?.View is { Profile: not null }
                    || (focused != null && focused.OnCheckIsTextEditor() && imm != null && ImeServes(imm, focused));
                var visible = insets != null && insets.IsVisible(global::Android.Views.WindowInsets.Type.Ime());
                var animating = activity?.InsetsListener?.IsImeAnimating == true;
                settled = !animating
                    && !(sessionOpen && !visible)
                    && !(!sessionOpen && _softKeyboardSessionWasOpen && visible);
            }).ConfigureAwait(false);
            if (settled || started.ElapsedMilliseconds >= 2000)
            {
                break;
            }

            await WaitForAndroidFramesAsync(1).ConfigureAwait(false);
        }

        _softKeyboardSessionWasOpen = sessionOpen;
    }

    // InputMethodManager.isActive(View) is not bound (only the parameterless IsActive property is): call it through JNI.
    private static bool ImeServes(global::Android.Views.InputMethods.InputMethodManager imm, global::Android.Views.View view)
    {
        var method = global::Android.Runtime.JNIEnv.GetMethodID(imm.Class.Handle, "isActive", "(Landroid/view/View;)Z");
        return global::Android.Runtime.JNIEnv.CallBooleanMethod(imm.Handle, method, new global::Android.Runtime.JValue(view));
    }

    /// <summary>[AP7-B TerminalView RE-GATE 2] A drawable of a given intrinsic size that draws nothing (harness caret hiding).</summary>
    private sealed class EmptyCaretDrawable : global::Android.Graphics.Drawables.Drawable
    {
        private readonly int _width;
        private readonly int _height;

        internal EmptyCaretDrawable(global::Android.Graphics.Drawables.Drawable? original)
        {
            _width = original?.IntrinsicWidth is > 0 and var w ? w : 2;
            _height = original?.IntrinsicHeight ?? -1;
        }

        public override int IntrinsicWidth => _width;

        public override int IntrinsicHeight => _height;

        public override int Opacity => (int)global::Android.Graphics.Format.Transparent;

        public override void Draw(global::Android.Graphics.Canvas canvas)
        {
        }

        public override void SetAlpha(int alpha)
        {
        }

        public override void SetColorFilter(global::Android.Graphics.ColorFilter? colorFilter)
        {
        }
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
