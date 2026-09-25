using System.Collections.Generic;
using SKColor = CodeBrix.Android.UIReqs.Device.Canvas.PixelColor;

namespace CodeBrix.Platform.UI.Core.UIReqs.Canvas;

/// <summary>
/// ANDROID PORT (fix): the representative colour of a quantized colour bucket is the bucket's MOST
/// FREQUENT exact colour, not the first pixel met in scan order. The copied vocabulary took the
/// first pixel, which on Android is an anti-aliased glyph edge (text ink #FF0909 for a #FF0000
/// Foreground, 9 steps off - outside the matching tolerance); the pure colour is the bucket's
/// mode. FIXLIST: the same latent bug is in CodeBrix.Platform's src/UIReqs copy.
/// </summary>
public sealed class ModeColor
{
    private readonly Dictionary<uint, Dictionary<uint, int>> _exact = new();

    /// <summary>Counts one pixel of a bucket.</summary>
    public void Add(uint bucket, SKColor color)
    {
        if (!_exact.TryGetValue(bucket, out var colors))
        {
            _exact[bucket] = colors = new Dictionary<uint, int>();
        }

        var key = (uint)color;
        colors.TryGetValue(key, out var count);
        colors[key] = count + 1;
    }

    /// <summary>The most frequent exact colour of a bucket.</summary>
    public SKColor Of(uint bucket)
    {
        var best = 0u;
        var bestCount = -1;
        foreach (var pair in _exact[bucket])
        {
            if (pair.Value > bestCount || (pair.Value == bestCount && pair.Key < best))
            {
                best = pair.Key;
                bestCount = pair.Value;
            }
        }

        return new SKColor(best);
    }
}
