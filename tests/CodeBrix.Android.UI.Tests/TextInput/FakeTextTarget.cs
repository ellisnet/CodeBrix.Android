using System;
using System.Collections.Generic;
using System.Text;
using CodeBrix.Android.UI.Portable.TextInput;

namespace CodeBrix.Android.UI.Tests.TextInput;

/// <summary>
/// A text target over a plain string for the host-free tests of <see cref="TextInputTargetEditor"/>: typing replaces the
/// selection (and is logged, so a test sees what reached the control's typing path), raw replacements are logged too,
/// a read-only range can be declared, and every change raises <see cref="Changed"/> as a control does.
/// </summary>
internal sealed class FakeTextTarget : ICoreTextInputTarget
{
    private readonly StringBuilder _text;
    private int _anchor;
    private int _active;

    internal FakeTextTarget(string text, int caret = -1)
    {
        _text = new StringBuilder(text ?? string.Empty);
        _anchor = _active = caret < 0 ? _text.Length : caret;
    }

    public event EventHandler<CoreTextInputChange> Changed;

    internal List<string> Log { get; } = new();

    internal (int Start, int End) Composition { get; private set; } = (-1, -1);

    internal (int Start, int End)? ReadOnly { get; set; }

    internal int Batches { get; private set; }

    internal bool Disposed { get; private set; }

    internal string Text => _text.ToString();

    public int TextLength => _text.Length;

    public int SelectionStart => Math.Min(_anchor, _active);

    public int SelectionEnd => Math.Max(_anchor, _active);

    public string GetText(int start, int length) => _text.ToString(start, length);

    public bool CanEdit(int start, int end) =>
        ReadOnly is not { } ro || end < ro.Start || start > ro.End || (end == start && (start <= ro.Start || start >= ro.End));

    public int Type(string text)
    {
        Log.Add("type:" + text);
        var start = SelectionStart;
        _text.Remove(start, SelectionEnd - start).Insert(start, text);
        _anchor = _active = start + text.Length;
        Changed?.Invoke(this, CoreTextInputChange.Text);
        Changed?.Invoke(this, CoreTextInputChange.Selection);
        return _active;
    }

    public int Replace(int start, int end, string text)
    {
        Log.Add($"replace:{start},{end},{text}");
        _text.Remove(start, end - start).Insert(start, text);
        Changed?.Invoke(this, CoreTextInputChange.Text);
        return start + text.Length;
    }

    public void Select(int start, int end)
    {
        _anchor = start;
        _active = end;
        Changed?.Invoke(this, CoreTextInputChange.Selection);
    }

    public void ShowComposition(int start, int end) => Composition = (start, end);

    public bool Perform(CoreTextInputCommand command)
    {
        Log.Add("command:" + command);
        return command == CoreTextInputCommand.Copy;
    }

    public void BeginBatch() => Batches++;

    public void EndBatch()
    {
    }

    public void Dispose() => Disposed = true;

    /// <summary>A change made by something else than the input method (a hardware key, a finger, the application).</summary>
    internal void TypeFromElsewhere(string text) => Type(text);

    /// <summary>The application sets a whole new text.</summary>
    internal void ResetFromElsewhere(string text)
    {
        _text.Clear().Append(text);
        _anchor = _active = 0;
        Changed?.Invoke(this, CoreTextInputChange.Reset);
    }
}
