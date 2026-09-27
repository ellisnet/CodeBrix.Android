using System.Collections.Generic;
using Windows.System;

namespace CodeBrix.Android.UI.Portable.TextInput;

/// <summary>
/// One key press a soft keyboard's text turns into for a CUSTOM text-entry control (a Core control that reads
/// KeyDown, such as the TerminalView and AdvancedTextEdit add-ins): the virtual key and the character it types (null
/// for a key that types none, such as Enter or Backspace). It is raised in Core as a KeyDown carrying the character,
/// then a KeyUp without one - the shape the Platform's own software keyboard injects.
/// </summary>
/// <param name="Key">The virtual key (<see cref="VirtualKey.None"/> for a character no key of a US layout names).</param>
/// <param name="Character">The character the key types, or null.</param>
internal readonly record struct TextInputKeystroke(VirtualKey Key, char? Character);

/// <summary>
/// Turns what an Android input method hands an editor (committed text, a request to delete around the cursor) into
/// the key presses a custom text-entry control understands. Pure C#: the Android input connection
/// (Input/TextInput/CoreTextInputConnection) calls it, and the host-free tests fence it.
/// </summary>
/// <remarks>
/// The character-to-key table is the Platform software keyboard's (letters, digits and the space bar carry their
/// key; every other character is <see cref="VirtualKey.None"/> with the character), plus the three characters an
/// input method commits for keys: a line break is Enter (CR, LF and a CR LF pair are ONE Enter), a tab is Tab, and
/// a DEL character is Backspace. The keys that type no character carry none.
/// </remarks>
internal static class TextInputKeystrokes
{
    /// <summary>The key presses that type <paramref name="text"/>, in order.</summary>
    /// <param name="text">Committed text (null or empty types nothing).</param>
    /// <returns>The key presses.</returns>
    internal static IReadOnlyList<TextInputKeystroke> ForText(string text)
    {
        var strokes = new List<TextInputKeystroke>();
        if (string.IsNullOrEmpty(text))
        {
            return strokes;
        }

        for (var i = 0; i < text.Length; i++)
        {
            var character = text[i];
            switch (character)
            {
                case '\r':
                    strokes.Add(new TextInputKeystroke(VirtualKey.Enter, null));
                    if (i + 1 < text.Length && text[i + 1] == '\n')
                    {
                        i++;
                    }

                    break;

                case '\n':
                    strokes.Add(new TextInputKeystroke(VirtualKey.Enter, null));
                    break;

                case '\t':
                    strokes.Add(new TextInputKeystroke(VirtualKey.Tab, null));
                    break;

                case '\b':
                case '\x7f':
                    strokes.Add(new TextInputKeystroke(VirtualKey.Back, null));
                    break;

                default:
                    strokes.Add(new TextInputKeystroke(KeyFor(character), character));
                    break;
            }
        }

        return strokes;
    }

    /// <summary>
    /// The key presses that delete <paramref name="before"/> characters before the cursor (Backspace) and
    /// <paramref name="after"/> characters after it (Delete) - what an input method's "delete surrounding text" means to a
    /// control whose text the input method cannot see.
    /// </summary>
    /// <param name="before">Characters before the cursor (negative counts as none).</param>
    /// <param name="after">Characters after the cursor (negative counts as none).</param>
    /// <returns>The key presses.</returns>
    internal static IReadOnlyList<TextInputKeystroke> ForDeletion(int before, int after)
    {
        var strokes = new List<TextInputKeystroke>();
        for (var i = 0; i < before; i++)
        {
            strokes.Add(new TextInputKeystroke(VirtualKey.Back, null));
        }

        for (var i = 0; i < after; i++)
        {
            strokes.Add(new TextInputKeystroke(VirtualKey.Delete, null));
        }

        return strokes;
    }

    /// <summary>The virtual key of a character (the Platform software keyboard's table).</summary>
    /// <param name="character">The character.</param>
    /// <returns>Its key, or <see cref="VirtualKey.None"/>.</returns>
    internal static VirtualKey KeyFor(char character) => character switch
    {
        >= 'a' and <= 'z' => VirtualKey.A + (character - 'a'),
        >= 'A' and <= 'Z' => VirtualKey.A + (character - 'A'),
        >= '0' and <= '9' => VirtualKey.Number0 + (character - '0'),
        ' ' => VirtualKey.Space,
        _ => VirtualKey.None,
    };
}
