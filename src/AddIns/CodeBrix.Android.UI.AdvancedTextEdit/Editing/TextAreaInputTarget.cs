using System;
using System.Collections.Generic;
using System.Linq;
using CodeBrix.Android.UI.Portable.TextInput;
using CodeBrix.Platform.UI.AdvancedTextEdit.Document;
using CodeBrix.Platform.UI.AdvancedTextEdit.Editing;

namespace CodeBrix.Android.UI.AdvancedTextEdit.Editing;

/// <summary>
/// The text of an AdvancedTextEdit TextArea as a soft keyboard sees and edits it (the text target of CodeBrix.Android.UI's
/// input connection): the TextArea's document, its caret and selection, typing through the TextArea's own
/// <see cref="TextArea.PerformTextInput(string)"/> (its TextEntering/TextEntered events, the line-break and indentation
/// handling, code completion - the same path a hardware key's character takes), raw replacements through the document
/// (read-only sections honoured), the composition shown by <see cref="CompositionUnderline"/>, the editor's own
/// select-all / cut / copy / paste commands, and one undo step per batch (the document's update group). Everything is the
/// Core's: this class only connects it to the keyboard. UI thread.
/// </summary>
internal sealed class TextAreaInputTarget : ICoreTextInputTarget
{
    private readonly TextArea _area;
    private TextDocument _document;
    private CompositionUnderline _underline;
    private readonly Stack<TextDocument> _batches = new();
    private bool _disposed;

    /// <summary>Creates the target of <paramref name="area"/> (it listens to the area until disposed).</summary>
    /// <param name="area">The text area.</param>
    /// <exception cref="ArgumentNullException"><paramref name="area"/> is null.</exception>
    internal TextAreaInputTarget(TextArea area)
    {
        _area = area ?? throw new ArgumentNullException(nameof(area));
        _area.Caret.PositionChanged += OnSelectionMoved;
        _area.SelectionChanged += OnSelectionMoved;
        _area.DocumentChanged += OnDocumentReplaced;
        Listen(_area.Document);
    }

    /// <inheritdoc />
    public event EventHandler<CoreTextInputChange> Changed;

    /// <summary>The text area.</summary>
    internal TextArea Area => _area;

    /// <summary>The composition underline (null until an input method first composes).</summary>
    internal CompositionUnderline Underline => _underline;

    /// <inheritdoc />
    public int TextLength => _area.Document?.TextLength ?? 0;

    /// <inheritdoc />
    public int SelectionStart => _area.Selection.IsEmpty || _area.Selection.SurroundingSegment is not { } segment
        ? _area.Caret.Offset
        : segment.Offset;

    /// <inheritdoc />
    public int SelectionEnd => _area.Selection.IsEmpty || _area.Selection.SurroundingSegment is not { } segment
        ? _area.Caret.Offset
        : segment.EndOffset;

    /// <inheritdoc />
    public string GetText(int start, int length) => _area.Document?.GetText(start, length) ?? string.Empty;

    /// <inheritdoc />
    public bool CanEdit(int start, int end)
    {
        if (_area.Document == null)
        {
            return false;
        }

        var provider = _area.ReadOnlySectionProvider;
        if (end <= start)
        {
            return provider.CanInsert(start);
        }

        // The whole range must be deletable (one deletable segment covering it).
        return provider.GetDeletableSegments(new SimpleSegment(start, end - start))
            .Any(s => s.Offset <= start && s.EndOffset >= end);
    }

    /// <inheritdoc />
    public int Type(string text)
    {
        if (_area.Document == null || string.IsNullOrEmpty(text))
        {
            return -1;
        }

        _area.PerformTextInput(text);
        return _area.Caret.Offset;
    }

    /// <inheritdoc />
    public int Replace(int start, int end, string text)
    {
        if (_area.Document is not { } document)
        {
            return -1;
        }

        text ??= string.Empty;
        document.Replace(start, end - start, text);
        return start + text.Length;
    }

    /// <inheritdoc />
    public void Select(int start, int end)
    {
        if (_area.Document == null)
        {
            return;
        }

        if (start == end)
        {
            _area.ClearSelection();
        }
        else
        {
            _area.Selection = Selection.Create(_area, start, end);
        }

        _area.Caret.Offset = end;
    }

    /// <inheritdoc />
    public void ShowComposition(int start, int end)
    {
        if (start < 0 || end <= start)
        {
            _underline?.Hide();
            return;
        }

        _underline ??= new CompositionUnderline(_area.TextView, _area);
        _underline.Show(start, end);
    }

    /// <inheritdoc />
    public bool Perform(CoreTextInputCommand command)
    {
        if (_area.ActiveInputHandler is not TextAreaInputHandler handler)
        {
            return false;
        }

        var editorCommand = command switch
        {
            CoreTextInputCommand.SelectAll => EditorCommands.SelectAll,
            CoreTextInputCommand.Cut => EditorCommands.Cut,
            CoreTextInputCommand.Copy => EditorCommands.Copy,
            CoreTextInputCommand.Paste => EditorCommands.Paste,
            _ => null,
        };
        return editorCommand != null && handler.ExecuteCommand(editorCommand, null);
    }

    /// <inheritdoc />
    public void BeginBatch()
    {
        // The update group is ended on the document it was begun on (an edit may replace the text area's document).
        var document = _area.Document;
        document?.BeginUpdate();
        _batches.Push(document);
    }

    /// <inheritdoc />
    public void EndBatch()
    {
        if (_batches.Count == 0)
        {
            return;
        }

        if (_batches.Pop() is { IsInUpdate: true } document)
        {
            document.EndUpdate();
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        while (_batches.Count > 0)
        {
            EndBatch();
        }

        _area.Caret.PositionChanged -= OnSelectionMoved;
        _area.SelectionChanged -= OnSelectionMoved;
        _area.DocumentChanged -= OnDocumentReplaced;
        Listen(null);
        _underline?.Detach();
        _underline = null;
    }

    private void Listen(TextDocument document)
    {
        if (_document != null)
        {
            _document.Changed -= OnTextChanged;
        }

        _document = document;
        if (_document != null)
        {
            _document.Changed += OnTextChanged;
        }
    }

    private void OnSelectionMoved(object sender, EventArgs e) => Changed?.Invoke(this, CoreTextInputChange.Selection);

    private void OnTextChanged(object sender, DocumentChangeEventArgs e)
    {
        // The whole text replaced (the application set a new text): the input method must read it again.
        var whole = e.Offset == 0 && e.RemovalLength > 0 && e.InsertionLength == TextLength;
        Changed?.Invoke(this, whole ? CoreTextInputChange.Reset : CoreTextInputChange.Text);
    }

    private void OnDocumentReplaced(object sender, EventArgs e)
    {
        Listen(_area.Document);
        Changed?.Invoke(this, CoreTextInputChange.Reset);
    }
}
