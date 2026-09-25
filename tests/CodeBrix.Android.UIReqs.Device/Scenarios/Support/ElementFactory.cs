using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using CodeBrix.Platform.UI.Core.UIReqs.Hosting;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;

namespace CodeBrix.Platform.UI.Core.UIReqs.Support;

/// <summary>
/// Turns the nouns of a feature file into elements: "a Border named box 400 by 300 with
/// Background Red" becomes a real Border, registered under the name the scenario will keep
/// using. Elements are built on the UI thread, because that is the only thread that may build
/// them, and an element that raises an event a requirement talks about gets a recorder
/// attached as it is created.
/// <para>
/// A coverage group teaches the factory its own nouns through the public
/// <see cref="RegisterKind"/> and <see cref="RegisterProperty"/> API, called once from a
/// <c>[BeforeTestRun]</c> hook in that group's own steps class - never by editing this file.
/// That is the route because several groups add to the same two tables, and a name a group
/// registers is the name every feature file of the assembly then shares: registering a name
/// another group already took with a different implementation throws rather than silently
/// winning, so a group gives anything that is not universal a control-specific name.
/// </para>
/// </summary>
public static partial class ElementFactory
{
	private static readonly Dictionary<string, Func<FrameworkElement>> Kinds = BuildKinds();
	private static readonly Dictionary<string, Action<FrameworkElement, string>> Properties = BuildProperties();
	private static readonly Dictionary<string, List<TypedSetter>> TypedProperties =
		new(StringComparer.OrdinalIgnoreCase);

	private static readonly List<TypedReader> TextReaders = [];

	/// <summary>The element kinds a feature file may ask for, in alphabetical order.</summary>
	public static IReadOnlyCollection<string> KnownKinds => Sorted(Kinds.Keys);

	/// <summary>The properties a feature file may set, in alphabetical order.</summary>
	public static IReadOnlyCollection<string> KnownProperties =>
		Sorted(new HashSet<string>(Properties.Keys, StringComparer.OrdinalIgnoreCase)
			.Union(TypedProperties.Keys, StringComparer.OrdinalIgnoreCase));

	/// <summary>Registers an element kind a feature file may ask for.</summary>
	/// <param name="kind">The name a feature file uses.</param>
	/// <param name="factory">How to build one.</param>
	/// <exception cref="InvalidOperationException">
	/// The name is already registered with a different factory.
	/// </exception>
	public static void RegisterKind(string kind, Func<FrameworkElement> factory)
	{
		ArgumentException.ThrowIfNullOrEmpty(kind);
		ArgumentNullException.ThrowIfNull(factory);

		if (Kinds.TryGetValue(kind, out var existing) && !existing.Equals(factory))
		{
			throw AlreadyTaken("element kind", kind);
		}

		Kinds[kind] = factory;
	}

	/// <summary>Registers a property a feature file may set.</summary>
	/// <param name="property">The name a feature file uses.</param>
	/// <param name="setter">How to apply it.</param>
	/// <exception cref="InvalidOperationException">
	/// The name is already registered with a different setter.
	/// </exception>
	public static void RegisterProperty(string property, Action<FrameworkElement, string> setter)
	{
		ArgumentException.ThrowIfNullOrEmpty(property);
		ArgumentNullException.ThrowIfNull(setter);

		if (Properties.TryGetValue(property, out var existing) && !existing.Equals(setter))
		{
			throw AlreadyTaken("property", property);
		}

		Properties[property] = setter;
	}

	/// <summary>
	/// Registers a property a feature file may set on ONE kind of element. The universal words a
	/// requirement uses - Padding, Text, FontSize - mean different things on different elements,
	/// and a control that spells one of them its own way must not have to be given a
	/// control-specific name for it (nor may it silently take the word away from every other
	/// element). A typed setter is therefore consulted before the general table, and only for an
	/// element it actually applies to.
	/// </summary>
	/// <typeparam name="TElement">The element type this setter applies to.</typeparam>
	/// <param name="property">The name a feature file uses.</param>
	/// <param name="setter">How to apply it to an element of that type.</param>
	/// <exception cref="InvalidOperationException">
	/// That name is already registered for that same type with a different setter.
	/// </exception>
	public static void RegisterProperty<TElement>(string property, Action<TElement, string> setter)
		where TElement : FrameworkElement
	{
		ArgumentException.ThrowIfNullOrEmpty(property);
		ArgumentNullException.ThrowIfNull(setter);

		if (!TypedProperties.TryGetValue(property, out var handlers))
		{
			handlers = new List<TypedSetter>();
			TypedProperties[property] = handlers;
		}

		foreach (var handler in handlers)
		{
			if (handler.ElementType != typeof(TElement))
			{
				continue;
			}

			if (!handler.Setter.Equals(setter))
			{
				throw AlreadyTaken(
					string.Create(CultureInfo.InvariantCulture, $"{typeof(TElement).Name} property"), property);
			}

			return;
		}

		handlers.Add(new TypedSetter(
			typeof(TElement),
			setter,
			(element, value) => setter((TElement) element, value)));
	}

	/// <summary>
	/// Registers how to read the text of ONE kind of element, so that the universal sentence
	/// "the Text of ... is ..." can be said about a control the core harness has never heard of.
	/// It is the mirror of <see cref="RegisterProperty{TElement}"/>: the reader is consulted
	/// before the harness's own switch, and only for an element it actually applies to, so a
	/// group teaching the word to its own control takes it away from nothing else.
	/// </summary>
	/// <typeparam name="TElement">The element type this reader applies to.</typeparam>
	/// <param name="reader">How to read the text of an element of that type.</param>
	/// <exception cref="InvalidOperationException">
	/// That type is already registered with a different reader.
	/// </exception>
	public static void RegisterTextReader<TElement>(Func<TElement, string> reader)
		where TElement : FrameworkElement
	{
		ArgumentNullException.ThrowIfNull(reader);

		foreach (var registered in TextReaders)
		{
			if (registered.ElementType != typeof(TElement))
			{
				continue;
			}

			if (!registered.Reader.Equals(reader))
			{
				throw AlreadyTaken(
					string.Create(CultureInfo.InvariantCulture, $"{typeof(TElement).Name} text reader"), "Text");
			}

			return;
		}

		TextReaders.Add(new TypedReader(typeof(TElement), reader, element => reader((TElement) element)));
	}

	/// <summary>
	/// Reads an element's text through the reader a coverage group registered for its type. The
	/// most-derived registration wins, as it does for a typed setter. Call this on the UI thread.
	/// </summary>
	/// <param name="element">The element to read.</param>
	/// <param name="text">The text, when a reader was registered for it.</param>
	/// <returns><c>true</c> when a reader answered; <c>false</c> when none applies.</returns>
	public static bool TryReadText(FrameworkElement element, out string text)
	{
		ArgumentNullException.ThrowIfNull(element);

		text = string.Empty;
		TypedReader? best = null;
		foreach (var reader in TextReaders)
		{
			if (reader.ElementType.IsInstanceOfType(element)
				&& (best is null || best.ElementType.IsAssignableFrom(reader.ElementType)))
			{
				best = reader;
			}
		}

		if (best is null)
		{
			return false;
		}

		text = best.Read(element) ?? string.Empty;
		return true;
	}

	/// <summary>
	/// Builds an element on the UI thread, registers it under its Gherkin name and applies the
	/// properties the scenario asked for.
	/// </summary>
	/// <param name="kind">The element kind, as a feature file spells it.</param>
	/// <param name="name">The name the scenario refers to it by.</param>
	/// <param name="properties">The properties to set, in the order they were written.</param>
	/// <returns>The element.</returns>
	public static async Task<FrameworkElement> CreateAsync(string kind, string name,
		IEnumerable<KeyValuePair<string, string>>? properties = null)
	{
		ArgumentException.ThrowIfNullOrEmpty(kind);
		ArgumentException.ThrowIfNullOrEmpty(name);

		var settings = properties is null
			? Array.Empty<KeyValuePair<string, string>>()
			: properties.ToArray();

		FrameworkElement element = null!;
		await TestTargetFixture.RunOnUIThreadAsync(() =>
		{
			element = Build(kind, name);
			foreach (var setting in settings)
			{
				Apply(element, setting.Key, setting.Value);
			}
		}).ConfigureAwait(false);

		return element;
	}

	/// <summary>Applies one property to an already-built element, on the UI thread.</summary>
	/// <param name="name">The Gherkin name of the element.</param>
	/// <param name="property">The property to set.</param>
	/// <param name="value">The value to set it to.</param>
	/// <returns>A task that completes once the UI thread has applied the change.</returns>
	public static Task ApplyAsync(string name, string property, string value)
	{
		ArgumentException.ThrowIfNullOrEmpty(name);

		return TestTargetFixture.RunOnUIThreadAsync(() =>
		{
			var element = ElementRegistry.Resolve(name);
			Apply(element, property, value);
			element.UpdateLayout();
		});
	}

	/// <summary>Applies one property. Call this on the UI thread.</summary>
	/// <param name="element">The element to change.</param>
	/// <param name="property">The property to set.</param>
	/// <param name="value">The value to set it to.</param>
	/// <exception cref="NotSupportedException">The harness knows no such property.</exception>
	public static void Apply(FrameworkElement element, string property, string value)
	{
		ArgumentNullException.ThrowIfNull(element);
		ArgumentException.ThrowIfNullOrEmpty(property);

		// A setter registered for this element's own type wins over the general one: the general
		// "Padding" knows a Border, a Control and a TextBlock, and a panel that has a Padding of
		// its own is neither of those.
		if (TryTypedSetter(element, property, out var typed))
		{
			typed(element, GherkinValue.Unquote(value));
			return;
		}

		if (!Properties.TryGetValue(property, out var setter))
		{
			throw new NotSupportedException(
				$"The harness cannot set \"{property}\". It knows: {string.Join(", ", KnownProperties)}. "
				+ "Add the property in an ElementFactory partial file rather than in a step.");
		}

		setter(element, GherkinValue.Unquote(value));
	}

	private static bool TryTypedSetter(FrameworkElement element, string property,
		out Action<FrameworkElement, string> setter)
	{
		setter = null!;
		if (!TypedProperties.TryGetValue(property, out var handlers))
		{
			return false;
		}

		TypedSetter? best = null;
		foreach (var handler in handlers)
		{
			if (!handler.ElementType.IsInstanceOfType(element))
			{
				continue;
			}

			if (best is null || best.ElementType.IsAssignableFrom(handler.ElementType))
			{
				// The most-derived registration wins, so a group that teaches the word to a base
				// class and another that teaches it to one of its subclasses both get what they
				// asked for.
				best = handler;
				continue;
			}

			if (!handler.ElementType.IsAssignableFrom(best.ElementType))
			{
				var subject = string.Create(CultureInfo.InvariantCulture,
					$"A {element.GetType().Name} named \"{element.Name}\"");
				var registrations = string.Create(CultureInfo.InvariantCulture,
					$"\"{property}\" ({best.ElementType.Name} and {handler.ElementType.Name})");

				throw new InvalidOperationException(
					$"{subject} matches two unrelated registrations of {registrations}, so there is no "
					+ "way to know which one the scenario meant.");
			}
		}

		if (best is null)
		{
			return false;
		}

		setter = best.Apply;
		return true;
	}

	private sealed class TypedSetter(Type elementType, object setter, Action<FrameworkElement, string> apply)
	{
		public Type ElementType { get; } = elementType;

		public object Setter { get; } = setter;

		public Action<FrameworkElement, string> Apply { get; } = apply;
	}

	private sealed class TypedReader(Type elementType, object reader, Func<FrameworkElement, string> read)
	{
		public Type ElementType { get; } = elementType;

		public object Reader { get; } = reader;

		public Func<FrameworkElement, string> Read { get; } = read;
	}

	private static FrameworkElement Build(string kind, string name)
	{
		if (!Kinds.TryGetValue(kind, out var factory))
		{
			throw new NotSupportedException(
				$"The harness cannot build a \"{kind}\". It knows: {string.Join(", ", KnownKinds)}. "
				+ "Add the kind in an ElementFactory partial file rather than in a step.");
		}

		var element = factory();
		element.Name = name;

		// An element a scenario shows on its own is expected at its natural size in the middle
		// of the panel; a scenario that wants it anywhere else says so with a property.
		element.HorizontalAlignment = HorizontalAlignment.Center;
		element.VerticalAlignment = VerticalAlignment.Center;

		if (element is ButtonBase button)
		{
			button.Click += (_, _) => EventRecorder.Record(name, "Click");
		}

		if (element is ToggleButton toggle)
		{
			toggle.Checked += (_, _) => EventRecorder.Record(name, "Checked");
			toggle.Unchecked += (_, _) => EventRecorder.Record(name, "Unchecked");
		}

		ElementRegistry.Register(name, element);
		return element;
	}

	private static Dictionary<string, Func<FrameworkElement>> BuildKinds()
	{
		var kinds = new Dictionary<string, Func<FrameworkElement>>(StringComparer.OrdinalIgnoreCase)
		{
			["Border"] = () => new Border(),
			["Grid"] = () => new Grid(),
			["StackPanel"] = () => new StackPanel(),
			["TextBlock"] = () => new TextBlock(),
			["Button"] = () => new Button(),
		};

		return kinds;
	}

	private static Dictionary<string, Action<FrameworkElement, string>> BuildProperties()
	{
		var properties = new Dictionary<string, Action<FrameworkElement, string>>(StringComparer.OrdinalIgnoreCase)
		{
			["Width"] = (element, value) => element.Width = GherkinValue.ToDouble(value),
			["Height"] = (element, value) => element.Height = GherkinValue.ToDouble(value),
			["Opacity"] = (element, value) => element.Opacity = GherkinValue.ToDouble(value),
			["Margin"] = (element, value) => element.Margin = GherkinValue.ToThickness(value),
			["Visibility"] = (element, value) =>
				element.Visibility = GherkinValue.ToEnum<Visibility>(value),
			["HorizontalAlignment"] = (element, value) =>
				element.HorizontalAlignment = GherkinValue.ToEnum<HorizontalAlignment>(value),
			["VerticalAlignment"] = (element, value) =>
				element.VerticalAlignment = GherkinValue.ToEnum<VerticalAlignment>(value),
			["IsEnabled"] = (element, value) => SetIsEnabled(element, value),
			["Background"] = (element, value) => SetBackground(element, Brush(value)),
			["Foreground"] = (element, value) => SetForeground(element, Brush(value)),
			["Text"] = (element, value) => SetText(element, value),
			["Content"] = (element, value) => SetContent(element, value),
			["FontSize"] = (element, value) => SetFontSize(element, GherkinValue.ToDouble(value)),
			["Padding"] = (element, value) => SetPadding(element, GherkinValue.ToThickness(value)),
		};

		return properties;
	}

	private static SolidColorBrush Brush(string value) => new(Colors.Parse(value));

	private static void SetBackground(FrameworkElement element, Brush brush)
	{
		switch (element)
		{
			case Panel panel:
				panel.Background = brush;
				break;
			case Border border:
				border.Background = brush;
				break;
			case Control control:
				control.Background = brush;
				break;
			default:
				throw Unsupported(element, "Background");
		}
	}

	private static void SetForeground(FrameworkElement element, Brush brush)
	{
		switch (element)
		{
			case TextBlock text:
				text.Foreground = brush;
				break;
			case Control control:
				control.Foreground = brush;
				break;
			default:
				throw Unsupported(element, "Foreground");
		}
	}

	private static void SetText(FrameworkElement element, string value)
	{
		switch (element)
		{
			case TextBlock text:
				text.Text = value;
				break;
			case TextBox box:
				box.Text = value;
				break;
			default:
				throw Unsupported(element, "Text");
		}
	}

	private static void SetContent(FrameworkElement element, string value)
	{
		if (element is ContentControl content)
		{
			content.Content = value;
			return;
		}

		throw Unsupported(element, "Content");
	}

	private static void SetFontSize(FrameworkElement element, double size)
	{
		switch (element)
		{
			case TextBlock text:
				text.FontSize = size;
				break;
			case Control control:
				control.FontSize = size;
				break;
			default:
				throw Unsupported(element, "FontSize");
		}
	}

	private static void SetPadding(FrameworkElement element, Thickness padding)
	{
		switch (element)
		{
			case Border border:
				border.Padding = padding;
				break;
			case Control control:
				control.Padding = padding;
				break;
			case TextBlock text:
				text.Padding = padding;
				break;
			default:
				throw Unsupported(element, "Padding");
		}
	}

	private static void SetIsEnabled(FrameworkElement element, string value)
	{
		if (element is Control control)
		{
			control.IsEnabled = bool.Parse(GherkinValue.Unquote(value));
			return;
		}

		throw Unsupported(element, "IsEnabled");
	}

	// Two coverage groups share one kind table and one property table. Overwriting an entry that
	// another group registered would make that group's scenarios build the wrong control (or
	// set the wrong property) with no failure anywhere near the cause, so a name that is
	// already taken by a DIFFERENT implementation is a hard error. Registering the very same
	// delegate again is not: a registration hook may legitimately run more than once.
	private static InvalidOperationException AlreadyTaken(string what, string name) =>
		new($"The {what} \"{name}\" is already registered with a different implementation. "
			+ "One name means one thing to the whole assembly, so a coverage group cannot re-use a "
			+ $"name another group has taken: give this one a control-specific name instead of \"{name}\".");

	private static NotSupportedException Unsupported(FrameworkElement element, string property) =>
		new(string.Create(CultureInfo.InvariantCulture,
			$"A {element.GetType().Name} named \"{element.Name}\" has no {property} the harness can set."));

	private static IReadOnlyCollection<string> Sorted(IEnumerable<string> values)
	{
		var names = new List<string>(values);
		names.Sort(StringComparer.OrdinalIgnoreCase);
		return names;
	}
}
