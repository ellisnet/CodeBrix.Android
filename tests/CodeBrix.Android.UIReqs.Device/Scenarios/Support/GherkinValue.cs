using System;
using System.Globalization;
using CodeBrix.Android.UIReqs.Device.TestTarget;
using Microsoft.UI.Xaml;
using Windows.System;

namespace CodeBrix.Platform.UI.Core.UIReqs.Support;

/// <summary>
/// How the words in a feature file become values. Everything is read with the invariant
/// culture, so a scenario reads the same on every machine, and anything unreadable is a
/// failure that says what was expected rather than a silently wrong default.
/// </summary>
public static class GherkinValue
{
	/// <summary>Reads a number written in a feature file.</summary>
	/// <param name="text">The text.</param>
	/// <returns>The number.</returns>
	/// <exception cref="FormatException">The text is not a number.</exception>
	public static double ToDouble(string text)
	{
		var value = Unquote(text);
		return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number)
			? number
			: throw new FormatException($"\"{text}\" is not a number.");
	}

	/// <summary>
	/// Reads a thickness: "8" for all four sides, "8,4" for left/right and top/bottom, or
	/// "1,2,3,4" for left, top, right and bottom.
	/// </summary>
	/// <param name="text">The text.</param>
	/// <returns>The thickness.</returns>
	/// <exception cref="FormatException">The text is not a thickness.</exception>
	public static Thickness ToThickness(string text)
	{
		var parts = Unquote(text).Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
		switch (parts.Length)
		{
			case 1:
				var all = ToDouble(parts[0]);
				return new Thickness(all);
			case 2:
				var horizontal = ToDouble(parts[0]);
				var vertical = ToDouble(parts[1]);
				return new Thickness(horizontal, vertical, horizontal, vertical);
			case 4:
				return new Thickness(ToDouble(parts[0]), ToDouble(parts[1]), ToDouble(parts[2]), ToDouble(parts[3]));
			default:
				throw new FormatException(
					$"\"{text}\" is not a thickness. Write one, two or four numbers separated by commas.");
		}
	}

	/// <summary>Reads a key name, as the VirtualKey enumeration spells it.</summary>
	/// <param name="text">The text.</param>
	/// <returns>The key.</returns>
	/// <exception cref="FormatException">The text names no key.</exception>
	public static VirtualKey ToVirtualKey(string text)
	{
		var value = Unquote(text);
		return Enum.TryParse<VirtualKey>(value, ignoreCase: true, out var key)
			? key
			: throw new FormatException(
				$"\"{text}\" is not a key. Write a VirtualKey name such as Enter, Tab, Escape or A.");
	}

	/// <summary>Reads a panel orientation.</summary>
	/// <param name="text">The text.</param>
	/// <returns>The orientation.</returns>
	/// <exception cref="FormatException">The text names no orientation.</exception>
	public static TestDisplayOrientation ToOrientation(string text)
	{
		var value = Unquote(text);
		return Enum.TryParse<TestDisplayOrientation>(value, ignoreCase: true, out var orientation)
			? orientation
			: throw new FormatException($"\"{text}\" is not a panel orientation. Write Landscape or Portrait.");
	}

	/// <summary>Reads a value of an enumeration, by the name the enumeration spells it with.</summary>
	/// <typeparam name="T">The enumeration.</typeparam>
	/// <param name="text">The text.</param>
	/// <returns>The value.</returns>
	/// <exception cref="FormatException">The text names no value of the enumeration.</exception>
	public static T ToEnum<T>(string text) where T : struct, Enum
	{
		var value = Unquote(text);
		return Enum.TryParse<T>(value, ignoreCase: true, out var parsed)
			? parsed
			: throw new FormatException(
				$"\"{text}\" is not a {typeof(T).Name}. Write one of: {string.Join(", ", Enum.GetNames<T>())}.");
	}

	/// <summary>Strips the quotation marks a feature file may have written around a value.</summary>
	/// <param name="text">The text.</param>
	/// <returns>The text without surrounding quotation marks or spaces.</returns>
	public static string Unquote(string text)
	{
		ArgumentNullException.ThrowIfNull(text);
		return text.Trim().Trim('"');
	}
}
