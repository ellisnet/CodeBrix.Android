using System;
using Java.Lang;
using AGraphicsRect = global::Android.Graphics.Rect;
using ITransformationMethod = global::Android.Text.Method.ITransformationMethod;
using AView = global::Android.Views.View;

namespace CodeBrix.Android.UI.Platform.Text;

/// <summary>
/// Shows a password as a row of the PasswordBox's PasswordChar (WinUI default U+25CF): every character
/// is replaced, none is ever shown (unlike Android's own password transformation, which shows the last
/// typed character for a moment).
/// </summary>
internal sealed class PasswordCharTransformation : Java.Lang.Object, ITransformationMethod
{
    /// <summary>Creates the transformation.</summary>
    /// <param name="maskCharacter">The masking character.</param>
    internal PasswordCharTransformation(char maskCharacter) => MaskCharacter = maskCharacter;

    /// <summary>The masking character.</summary>
    internal char MaskCharacter { get; }

    /// <inheritdoc />
    public ICharSequence GetTransformationFormatted(ICharSequence source, AView view) => new MaskedSequence(source, MaskCharacter);

    /// <inheritdoc />
    public void OnFocusChanged(AView view, ICharSequence sourceText, bool focused, global::Android.Views.FocusSearchDirection direction, AGraphicsRect previouslyFocusedRect)
    {
    }

    private sealed class MaskedSequence : Java.Lang.Object, ICharSequence
    {
        private readonly ICharSequence _source;
        private readonly char _mask;

        internal MaskedSequence(ICharSequence source, char mask)
        {
            _source = source;
            _mask = mask;
        }

        public char CharAt(int index) => _mask;

        public int Length() => _source?.Length() ?? 0;

        public ICharSequence SubSequenceFormatted(int start, int end) => new Java.Lang.String(new string(_mask, System.Math.Max(0, end - start)));

        public override string ToString() => new(_mask, Length());

        public System.Collections.Generic.IEnumerator<char> GetEnumerator()
        {
            for (var i = 0; i < Length(); i++)
            {
                yield return _mask;
            }
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
