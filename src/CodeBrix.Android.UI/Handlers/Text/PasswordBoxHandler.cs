// Technique from .NET MAUI, src/Core/src/Handlers/Entry/EntryHandler2.Android.cs (IsPassword) @ 828569a864.
// Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License. See THIRD-PARTY-NOTICES.txt.

using System;
using CodeBrix.Android.UI.Platform.Text;
using CodeBrix.Platform.UI.Contracts;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using AInputTypes = global::Android.Text.InputTypes;
using ATextInputLayout = Google.Android.Material.TextField.TextInputLayout;

namespace CodeBrix.Android.UI.Handlers;

/// <summary>
/// The PasswordBox handler (plan 3 row PasswordBox): the TextBox handler with a password input type, the
/// PasswordChar mask (every character replaced, WinUI default U+25CF) and the reveal button
/// (PasswordRevealMode Peek + IsPasswordRevealButtonEnabled -> an end icon that shows the text while
/// revealed; Visible -> plain text; Hidden -> no button). The editor's text is the Password: typing goes
/// through <c>PasswordBox.ApplyPasswordFromPlatform</c> (PasswordChanging, Password, PasswordChanged).
/// </summary>
internal sealed class PasswordBoxHandler : TextBoxHandler
{
    /// <summary>The default WinUI password masking character.</summary>
    internal const char DefaultPasswordChar = '●';

    /// <summary>PasswordBox's mapper (TextBox's plus the password properties).</summary>
    public static readonly PropertyMapper<PasswordBox, PasswordBoxHandler> PasswordMapper = new(Mapper)
    {
        [PasswordBox.PasswordProperty] = (h, e) => h.SetTextFromCore(e.Password),
        [PasswordBox.PasswordCharProperty] = (h, e) => h.RefreshFromCore(),
        [PasswordBox.PasswordRevealModeProperty] = (h, e) => h.RefreshFromCore(),
        [PasswordBox.IsPasswordRevealButtonEnabledProperty] = (h, e) => h.RefreshFromCore(),
    };

    private bool _revealed;

    /// <summary>Creates the handler.</summary>
    public PasswordBoxHandler()
        : base(PasswordMapper)
    {
    }

    /// <summary>The PasswordBox handler, or the templated fallback for one with a template of its own.</summary>
    internal static new IAndroidElementHandler Create(UIElement element) =>
        element is PasswordBox box && NativeControlPolicy.IsNative(box, typeof(PasswordBox), new[] { "DefaultPasswordBoxStyle" }, out _)
            ? new PasswordBoxHandler()
            : new TemplatedFallbackHandler();

    /// <inheritdoc />
    protected override void MapTextCore(TextBox element)
    {
        if (element is PasswordBox box)
        {
            SetTextFromCore(box.Password);
        }
    }

    /// <inheritdoc />
    protected override string ApplyToCore(TextBox element, string text, int selectionStart, int selectionLength) =>
        element is PasswordBox box ? box.ApplyPasswordFromPlatform(text) ?? text : text;

    /// <inheritdoc />
    protected override AInputTypes InputType(TextBox element) => InputScopeMapping.ForPassword(element.InputScope);

    /// <inheritdoc />
    protected override bool IsMultiLine(TextBox element) => false;

    /// <inheritdoc />
    protected override void ApplyTransformation(TextBox element)
    {
        if (element is not PasswordBox box)
        {
            return;
        }

        var show = box.PasswordRevealMode == PasswordRevealMode.Visible || _revealed;
        var mask = string.IsNullOrEmpty(box.PasswordChar) ? DefaultPasswordChar : box.PasswordChar[0];
        var current = EditText.TransformationMethod as PasswordCharTransformation;
        if (show)
        {
            if (EditText.TransformationMethod != null)
            {
                EditText.TransformationMethod = null;
            }
        }
        else if (current == null || current.MaskCharacter != mask)
        {
            EditText.TransformationMethod = new PasswordCharTransformation(mask);
        }
    }

    /// <inheritdoc />
    protected override void ApplyEndIcon(TextBox element)
    {
        var layout = PlatformView.Field;
        if (element is PasswordBox { PasswordRevealMode: PasswordRevealMode.Peek, IsPasswordRevealButtonEnabled: true })
        {
            if (layout.EndIconMode != ATextInputLayout.EndIconCustom)
            {
                layout.EndIconMode = ATextInputLayout.EndIconCustom;
                var eye = MaterialWidgets.DrawableId(layout.Context, "design_password_eye");
                if (eye != 0)
                {
                    layout.SetEndIconDrawable(eye);
                }

                layout.SetEndIconOnClickListener(new RevealClick(this));
            }

            OnEditorTextChanged();
        }
        else
        {
            layout.EndIconMode = ATextInputLayout.EndIconNone;
        }
    }

    /// <inheritdoc />
    protected override void OnEditorTextChanged()
    {
        // As the Fluent reveal button: only while the box has the focus and holds a password.
        var layout = PlatformView?.Field;
        if (layout != null && layout.EndIconMode == ATextInputLayout.EndIconCustom)
        {
            layout.EndIconVisible = EditText.HasFocus && EditText.Length() > 0;
        }
    }

    private void ToggleReveal()
    {
        _revealed = !_revealed;
        if (Element is PasswordBox box)
        {
            var start = EditText.SelectionStart;
            var end = EditText.SelectionEnd;
            ApplyTransformation(box);
            EditText.SetSelection(Math.Max(0, start), Math.Max(0, end));
        }
    }

    private sealed class RevealClick : Java.Lang.Object, global::Android.Views.View.IOnClickListener
    {
        private readonly WeakReference<PasswordBoxHandler> _handler;

        internal RevealClick(PasswordBoxHandler handler) => _handler = new WeakReference<PasswordBoxHandler>(handler);

        public void OnClick(global::Android.Views.View v)
        {
            if (_handler.TryGetTarget(out var handler))
            {
                handler.ToggleReveal();
            }
        }
    }
}
