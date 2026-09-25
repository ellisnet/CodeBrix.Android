using Microsoft.UI.Xaml.Media;
using System;
using System.Globalization;
using System.Text;
using Windows.UI;

namespace InannaRosette.Controls;

/// <summary>
/// Palette tokens, typography helpers and procedural geometry shared by the card view,
/// the altar ornament and the panels. Everything here is pure: no UI state.
/// </summary>
public static class Ornament
{
    // ---------------------------------------------------------------- palette
    public const string Night = "#0F0A1E";
    public const string Night2 = "#1A1030";
    public const string Kohl = "#07050D";
    public const string Lapis = "#1F3A93";
    public const string LapisLight = "#2B4CB8";
    public const string LapisGlow = "#4C6FE0";
    public const string Gold = "#E6C476";
    public const string GoldDeep = "#C9A14A";
    public const string GoldPale = "#F3DFA2";
    public const string GoldShadow = "#8A6A1F";
    public const string Carnelian = "#B6402E";
    public const string CarnelianLight = "#D9634E";
    public const string Ivory = "#F4EBD9";
    public const string Parchment = "#EFE3C8";
    public const string Ink = "#2A1E12";
    public const string Malachite = "#2E8B6B";

    /// <summary>Parse "#RGB", "#RRGGBB" or "#AARRGGBB" into a Color. Falls back to gold.</summary>
    public static Color ToColor(string? hex)
    {
        if (string.IsNullOrWhiteSpace(hex)) { return ToColor(Gold); }
        var s = hex.Trim().TrimStart('#');
        try
        {
            if (s.Length == 3)
            {
                s = string.Concat(s[0], s[0], s[1], s[1], s[2], s[2]);
            }
            if (s.Length == 6)
            {
                return Color.FromArgb(255,
                    byte.Parse(s.Substring(0, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture),
                    byte.Parse(s.Substring(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture),
                    byte.Parse(s.Substring(4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
            }
            if (s.Length == 8)
            {
                return Color.FromArgb(
                    byte.Parse(s.Substring(0, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture),
                    byte.Parse(s.Substring(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture),
                    byte.Parse(s.Substring(4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture),
                    byte.Parse(s.Substring(6, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
            }
        }
        catch (Exception)
        {
            // fall through
        }
        return Color.FromArgb(255, 0xE6, 0xC4, 0x76);
    }

    public static SolidColorBrush Brush(string hex, double opacity = 1.0) =>
        new SolidColorBrush(ToColor(hex)) { Opacity = opacity };

    /// <summary>Mix two colours; <paramref name="t"/> 0 = a, 1 = b.</summary>
    public static Color Mix(Color a, Color b, double t)
    {
        t = Math.Clamp(t, 0, 1);
        return Color.FromArgb(
            (byte)(a.A + (b.A - a.A) * t),
            (byte)(a.R + (b.R - a.R) * t),
            (byte)(a.G + (b.G - a.G) * t),
            (byte)(a.B + (b.B - a.B) * t));
    }

    /// <summary>Lighten a colour towards white.</summary>
    public static Color Lighten(Color c, double t) => Mix(c, Color.FromArgb(255, 255, 255, 255), t);

    // ---------------------------------------------------------------- typography
    private const char ThinSpace = ' ';
    private const char HairSpace = ' ';

    /// <summary>
    /// TextBlock.CharacterSpacing is ignored on this head, so "tracked caps" are faked by
    /// inserting thin spaces between letters. Existing spaces become a wider gap.
    /// </summary>
    public static string Track(string? text, bool upper = true)
    {
        if (string.IsNullOrEmpty(text)) { return string.Empty; }
        var source = upper ? text.ToUpperInvariant() : text;
        var sb = new StringBuilder(source.Length * 2);
        for (int i = 0; i < source.Length; i++)
        {
            var ch = source[i];
            if (i > 0)
            {
                sb.Append(char.IsWhiteSpace(ch) || char.IsWhiteSpace(source[i - 1]) ? HairSpace : ThinSpace);
            }
            sb.Append(ch == ' ' ? ' ' : ch);
        }
        return sb.ToString();
    }

    /// <summary>Light tracking (hair spaces only) for longer strings that must still fit.</summary>
    public static string TrackLight(string? text, bool upper = true)
    {
        if (string.IsNullOrEmpty(text)) { return string.Empty; }
        var source = upper ? text.ToUpperInvariant() : text;
        var sb = new StringBuilder(source.Length * 2);
        for (int i = 0; i < source.Length; i++)
        {
            if (i > 0) { sb.Append(HairSpace); }
            sb.Append(source[i]);
        }
        return sb.ToString();
    }

    // ---------------------------------------------------------------- geometry
    private static string N(double v) => v.ToString("0.###", CultureInfo.InvariantCulture);

    /// <summary>Point on a circle; angle in degrees measured clockwise from straight up.</summary>
    public static (double X, double Y) Polar(double cx, double cy, double radius, double angleDegrees)
    {
        var rad = angleDegrees * Math.PI / 180.0;
        return (cx + radius * Math.Sin(rad), cy - radius * Math.Cos(rad));
    }

    /// <summary>
    /// One elongated bezier petal pointing along <paramref name="angleDegrees"/> (clockwise from up),
    /// starting at the centre ring radius and reaching out to <paramref name="length"/>.
    /// </summary>
    public static string PetalPath(double cx, double cy, double inner, double length, double width, double angleDegrees)
    {
        var rad = angleDegrees * Math.PI / 180.0;
        double ux = Math.Sin(rad), uy = -Math.Cos(rad);      // outward unit vector
        double px = Math.Cos(rad), py = Math.Sin(rad);       // perpendicular unit vector

        (double x, double y) P(double along, double across) =>
            (cx + ux * along + px * across, cy + uy * along + py * across);

        var a = P(inner, 0);
        var c1 = P(inner + length * 0.22, -width);
        var c2 = P(inner + length * 0.74, -width * 0.72);
        var tip = P(inner + length, 0);
        var c3 = P(inner + length * 0.74, width * 0.72);
        var c4 = P(inner + length * 0.22, width);

        var sb = new StringBuilder();
        sb.Append("M ").Append(N(a.x)).Append(',').Append(N(a.y));
        sb.Append(" C ").Append(N(c1.x)).Append(',').Append(N(c1.y))
          .Append(' ').Append(N(c2.x)).Append(',').Append(N(c2.y))
          .Append(' ').Append(N(tip.x)).Append(',').Append(N(tip.y));
        sb.Append(" C ").Append(N(c3.x)).Append(',').Append(N(c3.y))
          .Append(' ').Append(N(c4.x)).Append(',').Append(N(c4.y))
          .Append(' ').Append(N(a.x)).Append(',').Append(N(a.y));
        sb.Append(" Z");
        return sb.ToString();
    }

    /// <summary>A straight hairline from <paramref name="inner"/> to <paramref name="outer"/> radius.</summary>
    public static string RadialLine(double cx, double cy, double inner, double outer, double angleDegrees)
    {
        var a = Polar(cx, cy, inner, angleDegrees);
        var b = Polar(cx, cy, outer, angleDegrees);
        return $"M {N(a.X)},{N(a.Y)} L {N(b.X)},{N(b.Y)}";
    }

    /// <summary>An n-pointed star in a box of the given size, centred, as path mini-language.</summary>
    public static string StarPath(double cx, double cy, double outer, double inner, int points, double rotationDegrees = 0)
    {
        var sb = new StringBuilder();
        for (int i = 0; i < points * 2; i++)
        {
            var r = (i % 2 == 0) ? outer : inner;
            var angle = rotationDegrees + i * (360.0 / (points * 2));
            var p = Polar(cx, cy, r, angle);
            sb.Append(i == 0 ? "M " : " L ").Append(N(p.X)).Append(',').Append(N(p.Y));
        }
        sb.Append(" Z");
        return sb.ToString();
    }

    /// <summary>
    /// A circle as four cubic beziers. This head's path parser rejects some arc segments
    /// ("the arc must be based on a circle"), so circles are written as beziers throughout.
    /// </summary>
    public static string CirclePath(double cx, double cy, double r)
    {
        double k = r * 0.5522847498307936;
        var sb = new StringBuilder();
        sb.Append("M ").Append(N(cx - r)).Append(',').Append(N(cy));
        sb.Append(" C ").Append(N(cx - r)).Append(',').Append(N(cy - k))
          .Append(' ').Append(N(cx - k)).Append(',').Append(N(cy - r))
          .Append(' ').Append(N(cx)).Append(',').Append(N(cy - r));
        sb.Append(" C ").Append(N(cx + k)).Append(',').Append(N(cy - r))
          .Append(' ').Append(N(cx + r)).Append(',').Append(N(cy - k))
          .Append(' ').Append(N(cx + r)).Append(',').Append(N(cy));
        sb.Append(" C ").Append(N(cx + r)).Append(',').Append(N(cy + k))
          .Append(' ').Append(N(cx + k)).Append(',').Append(N(cy + r))
          .Append(' ').Append(N(cx)).Append(',').Append(N(cy + r));
        sb.Append(" C ").Append(N(cx - k)).Append(',').Append(N(cy + r))
          .Append(' ').Append(N(cx - r)).Append(',').Append(N(cy + k))
          .Append(' ').Append(N(cx - r)).Append(',').Append(N(cy));
        sb.Append(" Z");
        return sb.ToString();
    }

    /// <summary>
    /// Rewrite every absolute elliptical-arc command (A) in a path as cubic beziers.
    /// This head's path parser only accepts circular arcs ("the arc must be based on a circle,
    /// not an ellipse"), and the deck art contains real ellipses, so arcs are flattened before
    /// they reach it. Returns the original string when there is nothing to do, or when the path
    /// uses relative commands (which are never produced by the deck art or by this class).
    /// </summary>
    public static string ArcsToBeziers(string data)
    {
        if (string.IsNullOrWhiteSpace(data) || data.IndexOf('A') < 0) { return data; }
        foreach (var relative in "mlhvcsqtaz")
        {
            if (data.IndexOf(relative) >= 0) { return data; }
        }

        var tokens = System.Text.RegularExpressions.Regex.Matches(
            data, @"([MLHVCSQTAZ])([^MLHVCSQTAZ]*)");
        if (tokens.Count == 0) { return data; }

        var output = new StringBuilder(data.Length * 2);
        double cx = 0, cy = 0, startX = 0, startY = 0;

        foreach (System.Text.RegularExpressions.Match token in tokens)
        {
            var command = token.Groups[1].Value[0];
            var numbers = ParseNumbers(token.Groups[2].Value);

            if (command != 'A')
            {
                output.Append(token.Value.Trim()).Append(' ');
                switch (command)
                {
                    case 'M' when numbers.Count >= 2:
                        cx = numbers[^2]; cy = numbers[^1]; startX = numbers[0]; startY = numbers[1];
                        break;
                    case 'L' when numbers.Count >= 2:
                    case 'T' when numbers.Count >= 2:
                        cx = numbers[^2]; cy = numbers[^1];
                        break;
                    case 'H' when numbers.Count >= 1: cx = numbers[^1]; break;
                    case 'V' when numbers.Count >= 1: cy = numbers[^1]; break;
                    case 'C' when numbers.Count >= 6:
                    case 'S' when numbers.Count >= 4:
                    case 'Q' when numbers.Count >= 4:
                        cx = numbers[^2]; cy = numbers[^1];
                        break;
                    case 'Z':
                        cx = startX; cy = startY;
                        break;
                }
                continue;
            }

            for (int i = 0; i + 6 < numbers.Count + 1 && i + 7 <= numbers.Count; i += 7)
            {
                var rx = numbers[i];
                var ry = numbers[i + 1];
                var rotation = numbers[i + 2];
                var largeArc = numbers[i + 3] != 0;
                var sweep = numbers[i + 4] != 0;
                var x2 = numbers[i + 5];
                var y2 = numbers[i + 6];
                output.Append(ArcSegment(cx, cy, rx, ry, rotation, largeArc, sweep, x2, y2));
                cx = x2; cy = y2;
            }
        }

        return output.ToString().Trim();
    }

    private static List<double> ParseNumbers(string text)
    {
        var list = new List<double>();
        foreach (System.Text.RegularExpressions.Match m in
                 System.Text.RegularExpressions.Regex.Matches(text, @"-?\d*\.?\d+(?:[eE][-+]?\d+)?"))
        {
            if (double.TryParse(m.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var v))
            {
                list.Add(v);
            }
        }
        return list;
    }

    /// <summary>SVG endpoint-to-centre arc parameterisation, emitted as up to four cubics.</summary>
    private static string ArcSegment(double x1, double y1, double rx, double ry, double rotationDegrees,
                                     bool largeArc, bool sweep, double x2, double y2)
    {
        if (rx == 0 || ry == 0 || (Math.Abs(x1 - x2) < 1e-9 && Math.Abs(y1 - y2) < 1e-9))
        {
            return $"L {N(x2)},{N(y2)} ";
        }

        rx = Math.Abs(rx);
        ry = Math.Abs(ry);
        var phi = rotationDegrees * Math.PI / 180.0;
        double cosPhi = Math.Cos(phi), sinPhi = Math.Sin(phi);

        var dx = (x1 - x2) / 2.0;
        var dy = (y1 - y2) / 2.0;
        var x1p = cosPhi * dx + sinPhi * dy;
        var y1p = -sinPhi * dx + cosPhi * dy;

        var lambda = (x1p * x1p) / (rx * rx) + (y1p * y1p) / (ry * ry);
        if (lambda > 1)
        {
            var scale = Math.Sqrt(lambda);
            rx *= scale;
            ry *= scale;
        }

        var numerator = rx * rx * ry * ry - rx * rx * y1p * y1p - ry * ry * x1p * x1p;
        var denominator = rx * rx * y1p * y1p + ry * ry * x1p * x1p;
        var coefficient = (largeArc == sweep ? -1 : 1) * Math.Sqrt(Math.Max(0, numerator / denominator));
        var cxp = coefficient * rx * y1p / ry;
        var cyp = coefficient * -ry * x1p / rx;

        var centreX = cosPhi * cxp - sinPhi * cyp + (x1 + x2) / 2.0;
        var centreY = sinPhi * cxp + cosPhi * cyp + (y1 + y2) / 2.0;

        var theta1 = Math.Atan2((y1p - cyp) / ry, (x1p - cxp) / rx);
        var theta2 = Math.Atan2((-y1p - cyp) / ry, (-x1p - cxp) / rx);
        var delta = theta2 - theta1;
        if (!sweep && delta > 0) { delta -= 2 * Math.PI; }
        else if (sweep && delta < 0) { delta += 2 * Math.PI; }

        var segments = Math.Max(1, (int)Math.Ceiling(Math.Abs(delta) / (Math.PI / 2)));
        var step = delta / segments;
        var handle = 4.0 / 3.0 * Math.Tan(step / 4.0);

        (double X, double Y) Point(double t) => (
            centreX + rx * Math.Cos(t) * cosPhi - ry * Math.Sin(t) * sinPhi,
            centreY + rx * Math.Cos(t) * sinPhi + ry * Math.Sin(t) * cosPhi);

        (double X, double Y) Derivative(double t) => (
            -rx * Math.Sin(t) * cosPhi - ry * Math.Cos(t) * sinPhi,
            -rx * Math.Sin(t) * sinPhi + ry * Math.Cos(t) * cosPhi);

        var sb = new StringBuilder();
        for (int i = 0; i < segments; i++)
        {
            var a = theta1 + i * step;
            var b = a + step;
            var pa = Point(a);
            var pb = Point(b);
            var da = Derivative(a);
            var db = Derivative(b);
            sb.Append("C ")
              .Append(N(pa.X + handle * da.X)).Append(',').Append(N(pa.Y + handle * da.Y)).Append(' ')
              .Append(N(pb.X - handle * db.X)).Append(',').Append(N(pb.Y - handle * db.Y)).Append(' ')
              .Append(N(pb.X)).Append(',').Append(N(pb.Y)).Append(' ');
        }
        return sb.ToString();
    }

    /// <summary>A small rosette: <paramref name="petals"/> rounded lobes around a centre.</summary>
    public static string RosettePath(double cx, double cy, double radius, int petals = 8)
    {
        var sb = new StringBuilder();
        double lobe = radius * 0.44;
        for (int i = 0; i < petals; i++)
        {
            var angle = i * (360.0 / petals);
            var centre = Polar(cx, cy, radius - lobe * 0.8, angle);
            sb.Append(CirclePath(centre.X, centre.Y, lobe)).Append(' ');
        }
        sb.Append(CirclePath(cx, cy, radius * 0.34));
        return sb.ToString().Trim();
    }
}
