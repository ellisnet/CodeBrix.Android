// STUB (paste always): CodeBrix.Platform.UI.TerminalView.TerminalControl (assembly CodeBrix.Platform.UI.TerminalView, package CodeBrix.Platform.TerminalView.ApacheLicenseForever); the TerminalView add-in has no Android flavor yet.
using System;
using Microsoft.UI.Xaml.Controls;
using SkiaSharp;

namespace CodeBrix.Platform.UI.TerminalView;

public sealed partial class TerminalControl : Control
{
    public event Action<string> InputEmitted;

    public event Action<int, int> GridResized;

    public event Action<string> TitleChanged;

    public event Action<string> CopyRequested;

    public int Columns { get; } = 80;

    public int Rows { get; } = 24;

    public SKColor ForegroundColor { get; set; } = new SKColor(0xFF, 0xFF, 0xFF);

    public SKColor BackgroundColor { get; set; } = new SKColor(0x00, 0x00, 0x00);

    public SKColor SelectionColor { get; set; } = new SKColor(0x4D, 0x8B, 0xD8, 0x66);

    public bool ConvertEol { get; set; }

    public int Scrollback { get; set; } = 1000;

    public string TerminalFontFamily { get; set; }

    public float TerminalFontSize { get; set; } = 14f;

    public void Feed(string data)
    {
    }

    public void Feed(byte[] data, int length)
    {
    }

    public void Reset()
    {
    }

    public void GrabFocus()
    {
    }

    private void RaiseForCompleteness()
    {
        InputEmitted?.Invoke(string.Empty);
        GridResized?.Invoke(Columns, Rows);
        TitleChanged?.Invoke(string.Empty);
        CopyRequested?.Invoke(string.Empty);
    }
}
