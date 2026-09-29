using System;
using System.Collections.Generic;
using CodeBrix.Android.UI.Hosting;
using CodeBrix.Android.UI.Portable.TextInput;
using AContext = global::Android.Content.Context;
using AEditableFactory = global::Android.Text.EditableFactory;
using AIEditable = global::Android.Text.IEditable;
using AEditorInfo = global::Android.Views.InputMethods.EditorInfo;
using AFrameLayout = global::Android.Widget.FrameLayout;
using AGravityFlags = global::Android.Views.GravityFlags;
using AImeFlags = global::Android.Views.InputMethods.ImeFlags;
using AImportantForAutofill = global::Android.Views.ImportantForAutofill;
using AInputConnection = global::Android.Views.InputMethods.IInputConnection;
using AInputMethodManager = global::Android.Views.InputMethods.InputMethodManager;
using AInputTypes = global::Android.Text.InputTypes;
using ARect = global::Android.Graphics.Rect;
using AView = global::Android.Views.View;
using AViewGroup = global::Android.Views.ViewGroup;

namespace CodeBrix.Android.UI.Input.TextInput;

/// <summary>
/// The native view a soft keyboard types into while a custom text-entry control (a Core control reporting its focus
/// through SoftwareKeyboardFocus) has the focus: one per activity, a 1x1 view that draws nothing, in the root layout's
/// focus layer. While a session is open it is an editor (<see cref="OnCheckIsTextEditor"/>), holds the Android focus,
/// and its <see cref="CoreTextInputConnection"/> either edits the control's own text through its TARGET
/// (<see cref="TargetEditor"/>: the keyboard sees the text, composes in place, and hears when the selection moves) or,
/// for a control without one, turns the keyboard's text into key presses in Core (<see cref="Deliver"/>: the window's
/// keyboard source, so Core routes them to its focused element exactly as it routes a hardware key). Hardware keys never pass through it: the activity hands every key to Core first, and this
/// view is not a native text editor (ActivityInputRouter).
/// <para>
/// [AP8-S item L] While a session is open on a control whose add-in registered its CARET
/// (<see cref="CoreTextInput.RegisterCaret"/>), the view is laid out ON the caret (its size is the caret's, in the focus
/// layer's pixels) and follows it (caret moves, scrolls, re-layouts): Android's adjustPan brings the focused view's
/// rectangle above the soft keyboard, so the window pans the caret into view as it does a native text field's caret line.
/// Without a caret (and between sessions) the view is parked, 1x1, at the layer's origin.
/// </para>
/// </summary>
internal sealed class CoreTextInputView : AView
{
    // How much text on each side of the selection the input method is handed when it connects.
    private const int SurroundingTextLength = 1024;

    private AndroidKeyboardInputSource _source;
    private bool _reportPosted;
    private bool _restartPending;
    private ICoreTextInputCaret _caret;
    private bool _placePosted;
    private CaretPixels? _placed;
    private readonly int[] _layerLocation = new int[2];
    private readonly int[] _contentLocation = new int[2];

    /// <summary>Creates the view.</summary>
    /// <param name="context">The activity.</param>
    internal CoreTextInputView(AContext context)
        : base(context)
    {
        Focusable = true;
        FocusableInTouchMode = true;
        DefaultFocusHighlightEnabled = false;
        ImportantForAutofill = AImportantForAutofill.No;
        LayoutParameters = ParkedLayout();
    }

    /// <summary>
    /// The field the input method sees (the connections' editable, shared by every connection the view hands out, so a
    /// composition survives an input-method restart): empty except while a composition is in progress.
    /// </summary>
    internal AIEditable Editable { get; } = AEditableFactory.Instance.NewEditable(string.Empty);

    /// <summary>The profile of the open session (null: no session; the view is then no editor).</summary>
    internal CoreTextInputProfile Profile { get; private set; }

    /// <summary>How many key presses the view has delivered to Core (diagnostics and device fences).</summary>
    internal int DeliveredKeystrokes { get; private set; }

    /// <summary>The view of <paramref name="activity"/> (created and added to its focus layer on first use).</summary>
    /// <param name="activity">The activity.</param>
    /// <returns>The view, or null when the activity has no root layout.</returns>
    internal static CoreTextInputView For(CodeBrixActivity activity)
    {
        var layer = activity?.RootLayout?.FocusLayer;
        if (layer == null)
        {
            return null;
        }

        for (var i = 0; i < layer.ChildCount; i++)
        {
            if (layer.GetChildAt(i) is CoreTextInputView existing)
            {
                return existing;
            }
        }

        var view = new CoreTextInputView(activity);
        layer.AddView(view);
        return view;
    }

    /// <summary>
    /// The editor of the focused control's TEXT while its session is open (null: the control has no text target and
    /// the keyboard's text becomes key presses). The input connection forwards the input method's calls to it.
    /// </summary>
    internal TextInputTargetEditor TargetEditor { get; private set; }

    /// <summary>How many times the input method was told the selection moved (diagnostics and device fences).</summary>
    internal int SelectionUpdates { get; private set; }

    /// <summary>How many times the input method was told to read the whole text again (diagnostics and device fences).</summary>
    internal int InputRestarts { get; private set; }

    /// <summary>Opens a session: the keyboard's text goes to <paramref name="source"/> with <paramref name="profile"/>.</summary>
    /// <param name="profile">The focused control's profile.</param>
    /// <param name="source">The keyboard source of the control's window.</param>
    /// <param name="target">The control's text (null: the key-press path); the session owns it from now on.</param>
    /// <param name="caret">The control's caret (null: the view stays parked); the session owns it from now on.</param>
    internal void Open(CoreTextInputProfile profile, AndroidKeyboardInputSource source, ICoreTextInputTarget target = null, ICoreTextInputCaret caret = null)
    {
        if (!ReferenceEquals(_source, source) || !ReferenceEquals(Profile, profile))
        {
            Editable.Clear();
        }

        Profile = profile;
        _source = source;
        DetachTarget();
        if (target != null)
        {
            TargetEditor = new TextInputTargetEditor(target);
            TargetEditor.Report += OnTargetReport;
        }

        DetachCaret();
        if (caret != null)
        {
            _caret = caret;
            caret.Moved += OnCaretMoved;
            if (caret.Element is { } element)
            {
                element.LayoutUpdated += OnCaretElementLayoutUpdated;
            }

            // At once: the keyboard the controller shows next pans to the focused view where it is now.
            PlaceOnCaret();
        }
    }

    /// <summary>Closes the session (the view stops being an editor).</summary>
    internal void Close()
    {
        Profile = null;
        _source = null;
        Editable.Clear();
        DetachTarget();
        DetachCaret();
        Park();
    }

    private void DetachTarget()
    {
        if (TargetEditor is { } editor)
        {
            TargetEditor = null;
            editor.Report -= OnTargetReport;
            editor.Detach();
        }
    }

    /// <summary>[AP8-S item L] Where the view is laid out on the caret (layer pixels), or null while parked (diagnostics and device fences).</summary>
    internal CaretPixels? CaretPlace => _placed;

    /// <summary>[AP8-S item L] How many times the view was moved onto the caret (diagnostics and device fences).</summary>
    internal int CaretPlacements { get; private set; }

    private void DetachCaret()
    {
        if (_caret is { } caret)
        {
            _caret = null;
            caret.Moved -= OnCaretMoved;
            if (caret.Element is { } element)
            {
                element.LayoutUpdated -= OnCaretElementLayoutUpdated;
            }

            caret.Dispose();
        }
    }

    private void OnCaretMoved(object sender, EventArgs e) => SchedulePlacement();

    // The control (or anything around it) was laid out again: it may have moved on the window.
    private void OnCaretElementLayoutUpdated(object sender, object e) => SchedulePlacement();

    // Once per looper turn, after the control's own change (a caret move is raised before the text view re-lays its lines out).
    private void SchedulePlacement()
    {
        if (_placePosted || _caret == null)
        {
            return;
        }

        _placePosted = true;
        Post(() =>
        {
            _placePosted = false;
            PlaceOnCaret();
        });
    }

    // Lays the view out on the caret of the open session (in the focus layer: the content layer's origin + Core's DIPs x
    // the scale), or parks it when the caret is not in the visible part of its control.
    private void PlaceOnCaret()
    {
        if (_caret is not { } caret || caret.Element is not { XamlRoot: { } root } element || !caret.TryGetBounds(out var bounds))
        {
            Park();
            return;
        }

        CaretPixels pixels;
        try
        {
            var transform = element.TransformToVisual(null);
            var caretBox = transform.TransformBounds(bounds);
            var visibleBox = transform.TransformBounds(new global::Windows.Foundation.Rect(0, 0, element.ActualWidth, element.ActualHeight));
            var (originX, originY) = ContentOrigin();
            var density = root.RasterizationScale > 0 ? root.RasterizationScale : Resources?.DisplayMetrics?.Density ?? 1;
            if (!CaretPlacement.TryPlace(
                (caretBox.X, caretBox.Y, caretBox.Width, caretBox.Height),
                (visibleBox.X, visibleBox.Y, visibleBox.Width, visibleBox.Height),
                density,
                originX,
                originY,
                out pixels))
            {
                Park();
                return;
            }
        }
        catch (ArgumentException)
        {
            // The element left the tree between the caret's move and this turn.
            Park();
            return;
        }

        if (_placed == pixels)
        {
            return;
        }

        _placed = pixels;
        CaretPlacements++;
        LayoutParameters = new AFrameLayout.LayoutParams(pixels.Width, pixels.Height, AGravityFlags.Left | AGravityFlags.Top)
        {
            LeftMargin = pixels.Left,
            TopMargin = pixels.Top,
        };
    }

    private void Park()
    {
        if (_placed == null)
        {
            return;
        }

        _placed = null;
        LayoutParameters = ParkedLayout();
    }

    // The window content's (XamlRoot's) origin in the focus layer, in pixels: the content layer's place relative to it.
    private (int X, int Y) ContentOrigin()
    {
        if (Parent is not AView layer || Context is not CodeBrixActivity { RootLayout.ContentLayer: { } content })
        {
            return (0, 0);
        }

        layer.GetLocationInWindow(_layerLocation);
        content.GetLocationInWindow(_contentLocation);
        return (_contentLocation[0] - _layerLocation[0], _contentLocation[1] - _layerLocation[1]);
    }

    private static AFrameLayout.LayoutParams ParkedLayout() => new(1, 1, AGravityFlags.Left | AGravityFlags.Top);

    /// <inheritdoc />
    protected override void OnLayout(bool changed, int left, int top, int right, int bottom)
    {
        base.OnLayout(changed, left, top, right, bottom);

        // The caret moved while the keyboard is up: ask the window to bring it into view again (with the keyboard down, or
        // in a mode that does not pan, the window ignores the request). The first pan, when the keyboard comes up, is the
        // window's own (it pans to the focused view's rectangle).
        if (changed && _placed != null && IsFocused)
        {
            using var rect = new ARect(0, 0, right - left, bottom - top);
            RequestRectangleOnScreen(rect, false);
        }
    }

    /// <summary>
    /// False while a UIReqs scenario drives the input connection itself (as the input method would): the reports still
    /// happen and are counted, but the real input method on the device does not hear of a composition it did not make
    /// (it would end it). True in every application.
    /// </summary>
    internal static bool ReportsReachInputMethod { get; set; } = true;

    // The control's text or selection changed (or this session's own edit ended): the input method hears of it - on
    // the next looper turn, never inside the control's own change event (a restart makes the input method finish its
    // composition through the connection at once, which would edit the control while it is still changing), and once
    // for all the changes of one turn (a restart covers a selection update).
    private void OnTargetReport(bool restart)
    {
        _restartPending |= restart;
        if (_reportPosted)
        {
            return;
        }

        _reportPosted = true;
        Post(DeliverReport);
    }

    private void DeliverReport()
    {
        _reportPosted = false;
        var restart = _restartPending;
        _restartPending = false;
        if (TargetEditor is not { } editor || Profile == null)
        {
            return;
        }

        if (restart)
        {
            InputRestarts++;
        }
        else
        {
            SelectionUpdates++;
        }

        if (!ReportsReachInputMethod || Context?.GetSystemService(AContext.InputMethodService) is not AInputMethodManager ime)
        {
            return;
        }

        if (restart)
        {
            ime.RestartInput(this);
            return;
        }

        ime.UpdateSelection(this, editor.Target.SelectionStart, editor.Target.SelectionEnd, editor.ComposingStart, editor.ComposingEnd);
    }

    /// <summary>Raises each key press in Core (a KeyDown with its character, then a KeyUp).</summary>
    /// <param name="strokes">The key presses.</param>
    internal void Deliver(IReadOnlyList<TextInputKeystroke> strokes)
    {
        if (_source is not { } source || strokes == null)
        {
            return;
        }

        foreach (var stroke in strokes)
        {
            source.InjectSoftwareKey(pressed: true, stroke.Key, stroke.Character);
            source.InjectSoftwareKey(pressed: false, stroke.Key, null);
            DeliveredKeystrokes++;
        }
    }

    /// <inheritdoc />
    public override bool OnCheckIsTextEditor() => Profile != null;

    /// <inheritdoc />
    public override AInputConnection OnCreateInputConnection(AEditorInfo outAttrs)
    {
        if (Profile is not { } profile || outAttrs == null)
        {
            return null;
        }

        if (profile.Suggestions)
        {
            outAttrs.InputType = AInputTypes.ClassText | (profile.MultiLine ? AInputTypes.TextFlagMultiLine : 0);
        }
        else
        {
            outAttrs.InputType = AInputTypes.ClassText | AInputTypes.TextVariationVisiblePassword | AInputTypes.TextFlagNoSuggestions
                | (profile.MultiLine ? AInputTypes.TextFlagMultiLine : 0);
        }

        outAttrs.ImeOptions = AImeFlags.NoFullscreen | AImeFlags.NoExtractUi | (profile.MultiLine ? AImeFlags.NoEnterAction : 0);
        if (TargetEditor is { } editor)
        {
            // The input method starts from the control's own selection and the text around it.
            var start = editor.Target.SelectionStart;
            var end = editor.Target.SelectionEnd;
            outAttrs.InitialSelStart = start;
            outAttrs.InitialSelEnd = end;
            var before = editor.GetTextBeforeCursor(SurroundingTextLength);
            var after = editor.GetTextAfterCursor(SurroundingTextLength);
            var selected = editor.GetSelectedText() ?? string.Empty;
            using var around = new Java.Lang.String(before + selected + after);
            outAttrs.SetInitialSurroundingSubText(around, start - before.Length);
        }
        else
        {
            outAttrs.InitialSelStart = 0;
            outAttrs.InitialSelEnd = 0;
        }

        return new CoreTextInputConnection(this);
    }
}
