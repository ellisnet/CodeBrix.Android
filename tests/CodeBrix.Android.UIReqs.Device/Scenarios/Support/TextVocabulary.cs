using System;
using System.Globalization;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using FontWeight = Windows.UI.Text.FontWeight;

namespace CodeBrix.Platform.UI.Core.UIReqs.Support;

/// <summary>
/// The nouns and adjectives the Text scenarios need: the text controls a feature file may ask
/// for, and the text properties it may set on them.
/// <para>
/// Registration goes through <see cref="ElementFactory.RegisterKind"/> and
/// <see cref="ElementFactory.RegisterProperty"/> for the same reason
/// <see cref="LayoutVocabulary"/> does: a partial method can only have one implementation, and
/// two coverage groups adding to the same table would collide on it. <see cref="Ensure"/> is
/// idempotent and is called from a scenario hook.
/// </para>
/// </summary>
public static class TextVocabulary
{
	private static readonly object RegistrationLock = new();

	private static bool _registered;

	/// <summary>Registers the Text kinds and properties, once per process.</summary>
	public static void Ensure()
	{
		lock (RegistrationLock)
		{
			if (_registered)
			{
				return;
			}

			_registered = true;
			RegisterKinds();
			RegisterProperties();
		}
	}

	private static void RegisterKinds()
	{
		ElementFactory.RegisterKind("TextBox", () => new TextBox());
		ElementFactory.RegisterKind("PasswordBox", () => new PasswordBox());
	}

	private static void RegisterProperties()
	{
		ElementFactory.RegisterProperty("TextAlignment", (element, value) => SetTextAlignment(element, value));
		ElementFactory.RegisterProperty("TextWrapping", (element, value) => SetTextWrapping(element, value));
		ElementFactory.RegisterProperty("TextTrimming", (element, value) => SetTextTrimming(element, value));
		ElementFactory.RegisterProperty("FontWeight", (element, value) => SetFontWeight(element, value));
		ElementFactory.RegisterProperty("PlaceholderText", (element, value) => TextBoxOf(element, "PlaceholderText").PlaceholderText = value);
		ElementFactory.RegisterProperty("MaxLength", (element, value) => TextBoxOf(element, "MaxLength").MaxLength = ToInt(value));
		ElementFactory.RegisterProperty("IsReadOnly", (element, value) => TextBoxOf(element, "IsReadOnly").IsReadOnly = ToBool(value));
		ElementFactory.RegisterProperty("AcceptsReturn", (element, value) => TextBoxOf(element, "AcceptsReturn").AcceptsReturn = ToBool(value));
		ElementFactory.RegisterProperty("Password", (element, value) => PasswordBoxOf(element).Password = value);
		ElementFactory.RegisterProperty("PasswordChar", (element, value) => PasswordBoxOf(element).PasswordChar = value);
	}

	private static int ToInt(string value) => (int) Math.Round(GherkinValue.ToDouble(value));

	private static bool ToBool(string value) => bool.Parse(GherkinValue.Unquote(value));

	private static TextBox TextBoxOf(FrameworkElement element, string property) =>
		element as TextBox ?? throw Unsupported(element, property);

	private static PasswordBox PasswordBoxOf(FrameworkElement element) =>
		element as PasswordBox ?? throw Unsupported(element, "a password");

	private static void SetTextAlignment(FrameworkElement element, string value)
	{
		var alignment = GherkinValue.ToEnum<TextAlignment>(value);
		switch (element)
		{
			case TextBlock text:
				text.TextAlignment = alignment;
				break;
			case TextBox box:
				box.TextAlignment = alignment;
				break;
			default:
				throw Unsupported(element, "TextAlignment");
		}
	}

	private static void SetTextWrapping(FrameworkElement element, string value)
	{
		var wrapping = GherkinValue.ToEnum<TextWrapping>(value);
		switch (element)
		{
			case TextBlock text:
				text.TextWrapping = wrapping;
				break;
			case TextBox box:
				box.TextWrapping = wrapping;
				break;
			default:
				throw Unsupported(element, "TextWrapping");
		}
	}

	private static void SetTextTrimming(FrameworkElement element, string value)
	{
		var trimming = GherkinValue.ToEnum<TextTrimming>(value);
		switch (element)
		{
			case TextBlock text:
				text.TextTrimming = trimming;
				break;
			default:
				throw Unsupported(element, "TextTrimming");
		}
	}

	private static void SetFontWeight(FrameworkElement element, string value)
	{
		var weight = ToFontWeight(value);
		switch (element)
		{
			case TextBlock text:
				text.FontWeight = weight;
				break;
			case Control control:
				control.FontWeight = weight;
				break;
			default:
				throw Unsupported(element, "FontWeight");
		}
	}

	private static FontWeight ToFontWeight(string value) => GherkinValue.Unquote(value).ToUpperInvariant() switch
	{
		"THIN" => FontWeights.Thin,
		"EXTRALIGHT" => FontWeights.ExtraLight,
		"LIGHT" => FontWeights.Light,
		"SEMILIGHT" => FontWeights.SemiLight,
		"NORMAL" => FontWeights.Normal,
		"MEDIUM" => FontWeights.Medium,
		"SEMIBOLD" => FontWeights.SemiBold,
		"BOLD" => FontWeights.Bold,
		"EXTRABOLD" => FontWeights.ExtraBold,
		"BLACK" => FontWeights.Black,
		"EXTRABLACK" => FontWeights.ExtraBlack,
		_ => throw new FormatException(
			$"\"{value}\" is not a font weight. Write Thin, ExtraLight, Light, SemiLight, Normal, Medium, SemiBold, Bold, ExtraBold, Black or ExtraBlack."),
	};

	private static NotSupportedException Unsupported(FrameworkElement element, string property) =>
		new(string.Create(CultureInfo.InvariantCulture,
			$"A {element.GetType().Name} named \"{element.Name}\" has no {property} the harness can set."));
}
