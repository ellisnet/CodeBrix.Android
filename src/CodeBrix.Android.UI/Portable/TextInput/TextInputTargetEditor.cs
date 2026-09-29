using System;

namespace CodeBrix.Android.UI.Portable.TextInput;

/// <summary>
/// What an Android input method does to a custom text-entry control's text, worked out on the control's
/// <see cref="ICoreTextInputTarget"/>: the text around the cursor, commit, composition (the composing region, shown
/// underlined by the control), delete around the cursor (in code units or code points, never splitting a surrogate
/// pair), set selection, the toolbar commands, and batch edits. It also decides when the input method must be told
/// that the selection moved (<see cref="Report"/>: after each of its own edits once no batch is open, and for every
/// change made by anything else - a hardware key, a finger, the application - which also ends a composition).
/// Pure C#: the Android input connection (Input/TextInput/CoreTextInputConnection) forwards the input method's calls
/// here, and the host-free tests fence it. UI thread.
/// </summary>
/// <remarks>
/// Typing that an input method does through a composition reaches the control as TYPED text where it can
/// (<see cref="Plan"/>: <see cref="TextInputEditKind.Typed"/> and <see cref="TextInputEditKind.Appended"/>), so the
/// control's own reactions to typing - its text-input events, completion, the line-break handling - happen for a soft
/// keyboard as they do for a hardware one; every other replacement is applied as it is.
/// </remarks>
internal sealed class TextInputTargetEditor
{
    private readonly ICoreTextInputTarget _target;
    private int _batch;
    private int _editing;
    private bool _pendingReport;
    private bool _detached;

    /// <summary>Creates the editor of <paramref name="target"/> (and listens to its changes until <see cref="Detach"/>).</summary>
    /// <param name="target">The control's text.</param>
    /// <exception cref="ArgumentNullException"><paramref name="target"/> is null.</exception>
    internal TextInputTargetEditor(ICoreTextInputTarget target)
    {
        _target = target ?? throw new ArgumentNullException(nameof(target));
        _target.Changed += OnTargetChanged;
    }

    /// <summary>
    /// Raised when the input method must hear about the selection: the argument is true when it must read the whole
    /// text again (restart its input: the control's text was replaced), false when a selection update is enough.
    /// </summary>
    internal event Action<bool> Report;

    /// <summary>The control's text.</summary>
    internal ICoreTextInputTarget Target => _target;

    /// <summary>The first offset of the composing region, or -1 when nothing is being composed.</summary>
    internal int ComposingStart { get; private set; } = -1;

    /// <summary>The offset after the composing region, or -1 when nothing is being composed.</summary>
    internal int ComposingEnd { get; private set; } = -1;

    /// <summary>True while an input method composes (a region of the text is its composition).</summary>
    internal bool IsComposing => ComposingStart >= 0;

    /// <summary>
    /// How a replacement of [<paramref name="start"/>, <paramref name="end"/>) by <paramref name="text"/> is applied,
    /// given the selection and what the range holds now; for <see cref="TextInputEditKind.Appended"/>,
    /// <paramref name="typed"/> is the part to type.
    /// </summary>
    /// <param name="start">The range's first offset.</param>
    /// <param name="end">The offset after the range.</param>
    /// <param name="text">The replacement.</param>
    /// <param name="selectionStart">The selection's lower end.</param>
    /// <param name="selectionEnd">The selection's upper end.</param>
    /// <param name="current">What the range holds now.</param>
    /// <param name="typed">The text to type (Typed and Appended), else null.</param>
    /// <returns>The kind of edit.</returns>
    internal static TextInputEditKind Plan(int start, int end, string text, int selectionStart, int selectionEnd, string current, out string typed)
    {
        typed = null;
        text ??= string.Empty;
        current ??= string.Empty;
        if (string.Equals(current, text, StringComparison.Ordinal))
        {
            return TextInputEditKind.None;
        }

        if (text.Length > 0 && start == selectionStart && end == selectionEnd)
        {
            typed = text;
            return TextInputEditKind.Typed;
        }

        if (start < end && end == selectionStart && selectionStart == selectionEnd
            && text.Length > current.Length && text.StartsWith(current, StringComparison.Ordinal)
            && !char.IsLowSurrogate(text[current.Length]))
        {
            typed = text.Substring(current.Length);
            return TextInputEditKind.Appended;
        }

        return TextInputEditKind.Replaced;
    }

    /// <summary>Takes the composition underline away, stops listening to the target and disposes it (the session ended).</summary>
    internal void Detach()
    {
        if (_detached)
        {
            return;
        }

        ClearComposition();
        _detached = true;
        _target.Changed -= OnTargetChanged;
        _target.Dispose();
    }

    /// <summary>Up to <paramref name="length"/> code units before the selection (never half a surrogate pair).</summary>
    /// <param name="length">The most code units to return.</param>
    /// <returns>The text.</returns>
    internal string GetTextBeforeCursor(int length)
    {
        var caret = Clamp(_target.SelectionStart);
        var start = Math.Max(0, caret - Math.Max(0, length));
        if (start > 0 && start < caret && char.IsLowSurrogate(_target.GetText(start, 1)[0]))
        {
            start++;
        }

        return caret > start ? _target.GetText(start, caret - start) : string.Empty;
    }

    /// <summary>Up to <paramref name="length"/> code units after the selection (never half a surrogate pair).</summary>
    /// <param name="length">The most code units to return.</param>
    /// <returns>The text.</returns>
    internal string GetTextAfterCursor(int length)
    {
        var caret = Clamp(_target.SelectionEnd);
        var end = Math.Min(_target.TextLength, caret + Math.Max(0, length));
        if (end > caret && end < _target.TextLength && char.IsHighSurrogate(_target.GetText(end - 1, 1)[0]))
        {
            end--;
        }

        return end > caret ? _target.GetText(caret, end - caret) : string.Empty;
    }

    /// <summary>The selected text, or null when nothing is selected (what an input method expects).</summary>
    /// <returns>The text or null.</returns>
    internal string GetSelectedText()
    {
        var start = Clamp(_target.SelectionStart);
        var end = Clamp(_target.SelectionEnd);
        return end > start ? _target.GetText(start, end - start) : null;
    }

    /// <summary>Commits text in place of the composition (or the selection) and ends the composition.</summary>
    /// <param name="text">The text.</param>
    /// <param name="newCursorPosition">Where the cursor goes, the input method's way (1 = after the text).</param>
    /// <returns>True.</returns>
    internal bool Commit(string text, int newCursorPosition)
    {
        Edit(() =>
        {
            var (start, end) = ActiveRange();
            var after = Apply(start, end, text ?? string.Empty);
            ClearComposition();
            if (after >= 0)
            {
                PlaceCursor(start, after, newCursorPosition);
            }
        }, changesText: true);
        return true;
    }

    /// <summary>Replaces the composition (or the selection) with a new composition.</summary>
    /// <param name="text">The composing text (empty: the composition is removed).</param>
    /// <param name="newCursorPosition">Where the cursor goes, the input method's way (1 = after the text).</param>
    /// <returns>True.</returns>
    internal bool SetComposingText(string text, int newCursorPosition)
    {
        Edit(() =>
        {
            text ??= string.Empty;
            var (start, end) = ActiveRange();
            var after = Apply(start, end, text);
            if (after < 0)
            {
                ClearComposition();
                return;
            }

            SetComposition(start, text.Length == 0 ? start : after);
            PlaceCursor(start, after, newCursorPosition);
        }, changesText: true);
        return true;
    }

    /// <summary>Makes a range of the existing text the composition (an input method re-composing a word).</summary>
    /// <param name="start">One end.</param>
    /// <param name="end">The other end.</param>
    /// <returns>True.</returns>
    internal bool SetComposingRegion(int start, int end)
    {
        Edit(() =>
        {
            var a = Clamp(Math.Min(start, end));
            var b = Clamp(Math.Max(start, end));
            SetComposition(a, b);
        });
        return true;
    }

    /// <summary>Ends the composition, keeping its text.</summary>
    /// <returns>True.</returns>
    internal bool FinishComposingText()
    {
        Edit(ClearComposition);
        return true;
    }

    /// <summary>
    /// Deletes <paramref name="beforeLength"/> code units before the selection and <paramref name="afterLength"/>
    /// after it (the selection itself stays), never splitting a surrogate pair.
    /// </summary>
    /// <param name="beforeLength">Code units before.</param>
    /// <param name="afterLength">Code units after.</param>
    /// <returns>True.</returns>
    internal bool DeleteSurroundingText(int beforeLength, int afterLength)
    {
        Edit(() =>
        {
            var s0 = Clamp(_target.SelectionStart);
            var s1 = Clamp(_target.SelectionEnd);
            var afterEnd = Math.Min(_target.TextLength, s1 + Math.Max(0, afterLength));
            if (afterEnd > s1 && afterEnd < _target.TextLength && char.IsHighSurrogate(_target.GetText(afterEnd - 1, 1)[0]))
            {
                afterEnd++;
            }

            var beforeStart = Math.Max(0, s0 - Math.Max(0, beforeLength));
            if (beforeStart > 0 && beforeStart < s0 && char.IsLowSurrogate(_target.GetText(beforeStart, 1)[0]))
            {
                beforeStart--;
            }

            DeleteRanges(beforeStart, s0, s1, afterEnd);
        }, changesText: true);
        return true;
    }

    /// <summary>
    /// Deletes <paramref name="beforeLength"/> code points before the selection and <paramref name="afterLength"/>
    /// after it (a surrogate pair is one code point).
    /// </summary>
    /// <param name="beforeLength">Code points before.</param>
    /// <param name="afterLength">Code points after.</param>
    /// <returns>True.</returns>
    internal bool DeleteSurroundingTextInCodePoints(int beforeLength, int afterLength)
    {
        Edit(() =>
        {
            var s0 = Clamp(_target.SelectionStart);
            var s1 = Clamp(_target.SelectionEnd);
            var beforeStart = s0;
            for (var n = 0; n < beforeLength && beforeStart > 0; n++)
            {
                beforeStart--;
                if (beforeStart > 0 && char.IsLowSurrogate(_target.GetText(beforeStart, 1)[0])
                    && char.IsHighSurrogate(_target.GetText(beforeStart - 1, 1)[0]))
                {
                    beforeStart--;
                }
            }

            var afterEnd = s1;
            for (var n = 0; n < afterLength && afterEnd < _target.TextLength; n++)
            {
                afterEnd++;
                if (afterEnd < _target.TextLength && char.IsHighSurrogate(_target.GetText(afterEnd - 1, 1)[0])
                    && char.IsLowSurrogate(_target.GetText(afterEnd, 1)[0]))
                {
                    afterEnd++;
                }
            }

            DeleteRanges(beforeStart, s0, s1, afterEnd);
        }, changesText: true);
        return true;
    }

    /// <summary>Selects a range (clamped to the text); the composition stays.</summary>
    /// <param name="start">The anchor.</param>
    /// <param name="end">The active end.</param>
    /// <returns>True.</returns>
    internal bool SetSelection(int start, int end)
    {
        Edit(() => _target.Select(Clamp(start), Clamp(end)));
        return true;
    }

    /// <summary>Runs a toolbar command (a select-all selects the whole text even when the control has no command for it).</summary>
    /// <param name="command">The command.</param>
    /// <returns>True when it ran.</returns>
    internal bool Perform(CoreTextInputCommand command)
    {
        var done = false;
        Edit(() =>
        {
            ClearComposition();
            done = _target.Perform(command);
            if (!done && command == CoreTextInputCommand.SelectAll)
            {
                _target.Select(0, _target.TextLength);
                done = true;
            }
        }, changesText: true);
        return done;
    }

    /// <summary>
    /// Opens a batch: the input method hears about the selection once, when the last batch closes. (The control's own
    /// edit groups stay per call: an input method's batch spans several looper turns, and a control must never be left
    /// inside an open edit group while other work - a touch, a frame - runs in between.)
    /// </summary>
    /// <returns>True.</returns>
    internal bool BeginBatchEdit()
    {
        _batch++;
        return true;
    }

    /// <summary>Closes a batch <see cref="BeginBatchEdit"/> opened.</summary>
    /// <returns>True while a batch is still open, false when this closed the last one (the input method's contract).</returns>
    internal bool EndBatchEdit()
    {
        if (_batch == 0)
        {
            return false;
        }

        _batch--;
        if (_batch == 0)
        {
            FlushReport();
        }

        return _batch > 0;
    }

    private void Edit(Action action, bool changesText = false)
    {
        _editing++;
        if (changesText)
        {
            _target.BeginBatch();
        }

        try
        {
            action();
        }
        finally
        {
            if (changesText)
            {
                _target.EndBatch();
            }

            _editing--;
            _pendingReport = true;
            if (_editing == 0 && _batch == 0)
            {
                FlushReport();
            }
        }
    }

    private void FlushReport()
    {
        if (!_pendingReport || _detached)
        {
            return;
        }

        _pendingReport = false;
        Report?.Invoke(false);
    }

    private void OnTargetChanged(object sender, CoreTextInputChange change)
    {
        if (_editing > 0)
        {
            return;
        }

        // Changed by something else (a hardware key, a finger, the application): a composition cannot survive it.
        ClearComposition();
        if (change == CoreTextInputChange.Reset)
        {
            _pendingReport = false;
            Report?.Invoke(true);
            return;
        }

        _pendingReport = true;
        if (_batch == 0)
        {
            FlushReport();
        }
    }

    private (int Start, int End) ActiveRange()
    {
        if (IsComposing)
        {
            return (Clamp(ComposingStart), Clamp(ComposingEnd));
        }

        return (Clamp(_target.SelectionStart), Clamp(_target.SelectionEnd));
    }

    private int Apply(int start, int end, string text)
    {
        if (!_target.CanEdit(start, end))
        {
            return -1;
        }

        var current = end > start ? _target.GetText(start, end - start) : string.Empty;
        switch (Plan(start, end, text, Clamp(_target.SelectionStart), Clamp(_target.SelectionEnd), current, out var typed))
        {
            case TextInputEditKind.None:
                return end;
            case TextInputEditKind.Typed:
                return _target.Type(typed);
            case TextInputEditKind.Appended:
                return _target.Type(typed);
            default:
                return _target.Replace(start, end, text);
        }
    }

    private void DeleteRanges(int beforeStart, int s0, int s1, int afterEnd)
    {
        var removedBefore = 0;
        if (afterEnd > s1 && _target.CanEdit(s1, afterEnd))
        {
            _target.Replace(s1, afterEnd, string.Empty);
            ShiftComposition(s1, afterEnd - s1);
        }

        if (s0 > beforeStart && _target.CanEdit(beforeStart, s0))
        {
            _target.Replace(beforeStart, s0, string.Empty);
            ShiftComposition(beforeStart, s0 - beforeStart);
            removedBefore = s0 - beforeStart;
        }

        _target.Select(s0 - removedBefore, s1 - removedBefore);
    }

    // The composition after [start, start + length) was removed: offsets past the range move back, a region that
    // loses all its text ends.
    private void ShiftComposition(int start, int length)
    {
        if (!IsComposing || length <= 0)
        {
            return;
        }

        var a = Shift(ComposingStart, start, length);
        var b = Shift(ComposingEnd, start, length);
        if (b <= a)
        {
            ClearComposition();
            return;
        }

        SetComposition(a, b);
    }

    private static int Shift(int offset, int start, int length) =>
        offset <= start ? offset : offset >= start + length ? offset - length : start;

    private void PlaceCursor(int start, int after, int newCursorPosition)
    {
        var target = newCursorPosition > 0 ? after + newCursorPosition - 1 : start + newCursorPosition;
        target = Clamp(target);
        if (_target.SelectionStart != target || _target.SelectionEnd != target)
        {
            _target.Select(target, target);
        }
    }

    private void SetComposition(int start, int end)
    {
        if (end <= start)
        {
            ClearComposition();
            return;
        }

        ComposingStart = start;
        ComposingEnd = end;
        _target.ShowComposition(start, end);
    }

    private void ClearComposition()
    {
        if (!IsComposing)
        {
            return;
        }

        ComposingStart = -1;
        ComposingEnd = -1;
        _target.ShowComposition(-1, -1);
    }

    private int Clamp(int offset) => Math.Clamp(offset, 0, _target.TextLength);
}
