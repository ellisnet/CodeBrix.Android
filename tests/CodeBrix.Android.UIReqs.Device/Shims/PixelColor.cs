using System;
using System.Globalization;

namespace CodeBrix.Android.UIReqs.Device.Canvas;

/// <summary>
/// A 32-bit RGBA pixel: the colour struct of the copied canvas vocabulary (which was written
/// against SkiaSharp's SKColor; the Android harness carries no Skia, so the copies alias SKColor
/// to this type). Same members the vocabulary uses: channel bytes, the uint (0xAARRGGBB)
/// conversion and value equality.
/// </summary>
public readonly struct PixelColor : IEquatable<PixelColor>
{
    private readonly uint _argb;

    /// <summary>Creates a colour from channels.</summary>
    public PixelColor(byte red, byte green, byte blue, byte alpha = 255) =>
        _argb = ((uint)alpha << 24) | ((uint)red << 16) | ((uint)green << 8) | blue;

    /// <summary>Creates a colour from 0xAARRGGBB.</summary>
    public PixelColor(uint argb) => _argb = argb;

    /// <summary>Red.</summary>
    public byte Red => (byte)(_argb >> 16);

    /// <summary>Green.</summary>
    public byte Green => (byte)(_argb >> 8);

    /// <summary>Blue.</summary>
    public byte Blue => (byte)_argb;

    /// <summary>Alpha.</summary>
    public byte Alpha => (byte)(_argb >> 24);

    /// <summary>0xAARRGGBB.</summary>
    public static explicit operator uint(PixelColor color) => color._argb;

    /// <summary>From 0xAARRGGBB.</summary>
    public static implicit operator PixelColor(uint argb) => new(argb);

    /// <summary>Equality.</summary>
    public static bool operator ==(PixelColor left, PixelColor right) => left._argb == right._argb;

    /// <summary>Inequality.</summary>
    public static bool operator !=(PixelColor left, PixelColor right) => left._argb != right._argb;

    /// <inheritdoc />
    public bool Equals(PixelColor other) => _argb == other._argb;

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is PixelColor other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => (int)_argb;

    /// <inheritdoc />
    public override string ToString() => "#" + _argb.ToString("X8", CultureInfo.InvariantCulture);
}
