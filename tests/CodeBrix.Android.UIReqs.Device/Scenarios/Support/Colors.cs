using System;
using System.Collections.Generic;
using System.Globalization;
using Windows.UI;

namespace CodeBrix.Platform.UI.Core.UIReqs.Support;

/// <summary>
/// The colours a feature file may name. A scenario writes either a name ("Red") or an exact
/// value ("#FFFF0000" / "#FF0000"); everything else is a failure with the list of names in it,
/// because a silently mis-parsed colour would make a pixel assertion lie.
/// <para>
/// A coverage group adds the names it needs through the public <see cref="RegisterName"/> API,
/// called once from a <c>[BeforeTestRun]</c> hook in that group's own steps class - never by
/// editing this file. The table is shared by the whole assembly, so registering a name another
/// group already gave a different colour throws rather than silently repainting that group's
/// scenarios.
/// </para>
/// </summary>
public static partial class Colors
{
	private static readonly Dictionary<string, Color> Named = BuildNamedColors();

	/// <summary>Opaque white.</summary>
	public static Color White => Color.FromArgb(0xFF, 0xFF, 0xFF, 0xFF);

	/// <summary>Opaque black.</summary>
	public static Color Black => Color.FromArgb(0xFF, 0x00, 0x00, 0x00);

	/// <summary>Fully transparent.</summary>
	public static Color Transparent => Color.FromArgb(0x00, 0x00, 0x00, 0x00);

	/// <summary>The names a feature file may use, in alphabetical order.</summary>
	public static IReadOnlyCollection<string> Names
	{
		get
		{
			var names = new List<string>(Named.Keys);
			names.Sort(StringComparer.OrdinalIgnoreCase);
			return names;
		}
	}

	/// <summary>Reads a colour written in a feature file.</summary>
	/// <param name="text">A colour name, "#AARRGGBB" or "#RRGGBB".</param>
	/// <returns>The colour.</returns>
	/// <exception cref="FormatException">The text names no colour this harness knows.</exception>
	public static Color Parse(string text)
	{
		if (TryParse(text, out var color))
		{
			return color;
		}

		throw new FormatException(
			$"\"{text}\" is not a colour. Write #AARRGGBB, #RRGGBB, or one of: "
			+ string.Join(", ", Names) + ".");
	}

	/// <summary>Reads a colour written in a feature file, without throwing.</summary>
	/// <param name="text">A colour name, "#AARRGGBB" or "#RRGGBB".</param>
	/// <param name="color">The colour, when the text names one.</param>
	/// <returns><c>true</c> when the text names a colour.</returns>
	public static bool TryParse(string text, out Color color)
	{
		color = default;
		if (string.IsNullOrWhiteSpace(text))
		{
			return false;
		}

		var value = text.Trim().Trim('"');
		if (value.StartsWith('#'))
		{
			return TryParseHex(value.AsSpan(1), out color);
		}

		return Named.TryGetValue(value, out color);
	}

	/// <summary>Registers a name a feature file may use.</summary>
	/// <param name="name">The name.</param>
	/// <param name="color">The colour it stands for.</param>
	/// <exception cref="InvalidOperationException">
	/// The name already stands for a different colour.
	/// </exception>
	public static void RegisterName(string name, Color color)
	{
		ArgumentException.ThrowIfNullOrEmpty(name);

		// One name means one colour to every feature file in the assembly. Re-registering the
		// same colour is harmless (a registration hook may run more than once); re-registering
		// a DIFFERENT colour would silently change what another group's scenarios assert.
		if (Named.TryGetValue(name, out var existing) && !existing.Equals(color))
		{
			throw new InvalidOperationException(
				$"The colour name \"{name}\" already stands for {Describe(existing)}, so it cannot also "
				+ $"stand for {Describe(color)}. Give this colour a name of its own instead of \"{name}\".");
		}

		Named[name] = color;
	}

	/// <summary>A colour as the "#AARRGGBB" a feature file would write.</summary>
	/// <param name="color">The colour.</param>
	/// <returns>The description.</returns>
	public static string Describe(Color color) => string.Create(CultureInfo.InvariantCulture,
		$"#{color.A:X2}{color.R:X2}{color.G:X2}{color.B:X2}");

	private static bool TryParseHex(ReadOnlySpan<char> digits, out Color color)
	{
		color = default;
		if (digits.Length is not (6 or 8))
		{
			return false;
		}

		Span<byte> channels = stackalloc byte[4];
		var offset = digits.Length == 8 ? 0 : 1;
		channels[0] = 0xFF;

		for (var i = 0; i < digits.Length / 2; i++)
		{
			if (!byte.TryParse(digits.Slice(i * 2, 2), NumberStyles.HexNumber,
				CultureInfo.InvariantCulture, out var channel))
			{
				return false;
			}

			channels[i + offset] = channel;
		}

		color = Color.FromArgb(channels[0], channels[1], channels[2], channels[3]);
		return true;
	}

	private static Dictionary<string, Color> BuildNamedColors()
	{
		// The names and values are the ones a person reading a requirement expects, and they
		// match the framework's own well-known colours.
		var named = new Dictionary<string, Color>(StringComparer.OrdinalIgnoreCase)
		{
			["Transparent"] = Color.FromArgb(0x00, 0x00, 0x00, 0x00),
			["Black"] = Color.FromArgb(0xFF, 0x00, 0x00, 0x00),
			["White"] = Color.FromArgb(0xFF, 0xFF, 0xFF, 0xFF),
			["Red"] = Color.FromArgb(0xFF, 0xFF, 0x00, 0x00),
			["Green"] = Color.FromArgb(0xFF, 0x00, 0x80, 0x00),
			["Lime"] = Color.FromArgb(0xFF, 0x00, 0xFF, 0x00),
			["Blue"] = Color.FromArgb(0xFF, 0x00, 0x00, 0xFF),
			["Navy"] = Color.FromArgb(0xFF, 0x00, 0x00, 0x80),
			["Yellow"] = Color.FromArgb(0xFF, 0xFF, 0xFF, 0x00),
			["Cyan"] = Color.FromArgb(0xFF, 0x00, 0xFF, 0xFF),
			["Magenta"] = Color.FromArgb(0xFF, 0xFF, 0x00, 0xFF),
			["Orange"] = Color.FromArgb(0xFF, 0xFF, 0xA5, 0x00),
			["Purple"] = Color.FromArgb(0xFF, 0x80, 0x00, 0x80),
			["Brown"] = Color.FromArgb(0xFF, 0xA5, 0x2A, 0x2A),
			["Teal"] = Color.FromArgb(0xFF, 0x00, 0x80, 0x80),
			["Olive"] = Color.FromArgb(0xFF, 0x80, 0x80, 0x00),
			["Maroon"] = Color.FromArgb(0xFF, 0x80, 0x00, 0x00),
			["Silver"] = Color.FromArgb(0xFF, 0xC0, 0xC0, 0xC0),
			["Gray"] = Color.FromArgb(0xFF, 0x80, 0x80, 0x80),
			["Grey"] = Color.FromArgb(0xFF, 0x80, 0x80, 0x80),
			["DarkGray"] = Color.FromArgb(0xFF, 0xA9, 0xA9, 0xA9),
			["LightGray"] = Color.FromArgb(0xFF, 0xD3, 0xD3, 0xD3),
			["PanelDark"] = Color.FromArgb(0xFF, 0x20, 0x20, 0x20),
		};

		return named;
	}
}
