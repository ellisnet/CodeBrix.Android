using System.Collections.Generic;
using CodeBrix.Android.UI.Hosting;
using CodeBrix.Android.UI.Portable.TextInput;
using AContext = global::Android.Content.Context;
using AEditableFactory = global::Android.Text.EditableFactory;
using AIEditable = global::Android.Text.IEditable;
using AEditorInfo = global::Android.Views.InputMethods.EditorInfo;
using AImeFlags = global::Android.Views.InputMethods.ImeFlags;
using AImportantForAutofill = global::Android.Views.ImportantForAutofill;
using AInputConnection = global::Android.Views.InputMethods.IInputConnection;
using AInputTypes = global::Android.Text.InputTypes;
using AView = global::Android.Views.View;
using AViewGroup = global::Android.Views.ViewGroup;

namespace CodeBrix.Android.UI.Input.TextInput;

/// <summary>
/// The native view a soft keyboard types into while a custom text-entry control (a Core control reporting its focus
/// through SoftwareKeyboardFocus) has the focus: one per activity, a 1x1 view that draws nothing, in the root layout's
/// focus layer. While a session is open it is an editor (<see cref="OnCheckIsTextEditor"/>), holds the Android focus,
/// and its <see cref="CoreTextInputConnection"/> turns the keyboard's text into key presses in Core
/// (<see cref="Deliver"/>: the window's keyboard source, so Core routes them to its focused element exactly as it
/// routes a hardware key). Hardware keys never pass through it: the activity hands every key to Core first, and this
/// view is not a native text editor (ActivityInputRouter).
/// </summary>
internal sealed class CoreTextInputView : AView
{
    private AndroidKeyboardInputSource _source;

    /// <summary>Creates the view.</summary>
    /// <param name="context">The activity.</param>
    internal CoreTextInputView(AContext context)
        : base(context)
    {
        Focusable = true;
        FocusableInTouchMode = true;
        DefaultFocusHighlightEnabled = false;
        ImportantForAutofill = AImportantForAutofill.No;
        LayoutParameters = new AViewGroup.LayoutParams(1, 1);
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

    /// <summary>Opens a session: the keyboard's text goes to <paramref name="source"/> with <paramref name="profile"/>.</summary>
    /// <param name="profile">The focused control's profile.</param>
    /// <param name="source">The keyboard source of the control's window.</param>
    internal void Open(CoreTextInputProfile profile, AndroidKeyboardInputSource source)
    {
        if (!ReferenceEquals(_source, source) || !ReferenceEquals(Profile, profile))
        {
            Editable.Clear();
        }

        Profile = profile;
        _source = source;
    }

    /// <summary>Closes the session (the view stops being an editor).</summary>
    internal void Close()
    {
        Profile = null;
        _source = null;
        Editable.Clear();
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
        outAttrs.InitialSelStart = 0;
        outAttrs.InitialSelEnd = 0;
        return new CoreTextInputConnection(this);
    }
}
