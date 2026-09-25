using System;
using System.Globalization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;

namespace CodeBrix.Platform.UI.Core.UIReqs.Support;

/// <summary>
/// The range and progress controls a Range feature file may ask for, and the properties those
/// scenarios set. Registration goes through the element factory's public
/// <see cref="ElementFactory.RegisterKind"/> and <see cref="ElementFactory.RegisterProperty"/>
/// so that two coverage groups can add their controls at the same time.
/// </summary>
public static class RangeElements
{
	/// <summary>The event name a scenario asks about when a range control's Value changes.</summary>
	public const string ValueChangedEvent = "ValueChanged";

	private static bool _registered;

	/// <summary>
	/// Adds the Range controls and properties to the element factory. Calling it twice is
	/// harmless; the range steps call it once, before the first scenario.
	/// </summary>
	public static void Register()
	{
		if (_registered)
		{
			return;
		}

		_registered = true;

		ElementFactory.RegisterKind("Slider", BuildSlider);
		ElementFactory.RegisterKind("ProgressBar", () => Watch(new ProgressBar()));
		ElementFactory.RegisterKind("ProgressRing", () => new ProgressRing());
		ElementFactory.RegisterKind("ScrollBar", () => Watch(new ScrollBar()));
		ElementFactory.RegisterKind("RatingControl", BuildRatingControl);

		ElementFactory.RegisterProperty("Value", (element, value) => SetValue(element, GherkinValue.ToDouble(value)));
		ElementFactory.RegisterProperty("Minimum", (element, value) => SetMinimum(element, GherkinValue.ToDouble(value)));
		ElementFactory.RegisterProperty("Maximum", (element, value) => SetMaximum(element, GherkinValue.ToDouble(value)));
		ElementFactory.RegisterProperty("SmallChange", (element, value) =>
			Range(element, "SmallChange").SmallChange = GherkinValue.ToDouble(value));
		ElementFactory.RegisterProperty("LargeChange", (element, value) =>
			Range(element, "LargeChange").LargeChange = GherkinValue.ToDouble(value));
		ElementFactory.RegisterProperty("StepFrequency", (element, value) =>
			Sliding(element, "StepFrequency").StepFrequency = GherkinValue.ToDouble(value));
		ElementFactory.RegisterProperty("IsThumbToolTipEnabled", (element, value) =>
			Sliding(element, "IsThumbToolTipEnabled").IsThumbToolTipEnabled = ReadBoolean(value));
		ElementFactory.RegisterProperty("IsIndeterminate", (element, value) => SetIsIndeterminate(element, value));
		// ANDROID PORT (AP1.9, pin 1.0.268.12): the WPE1-1 C0g scenario's harness property, written here from the
		// scenario (the Platform's own RangeElements change was not copied: the Platform repository is closed to the
		// Android track since the engine pass). ShowPaused puts the ProgressBar in its Paused state.
		ElementFactory.RegisterProperty("ShowPaused", (element, value) =>
			Progress(element, "ShowPaused").ShowPaused = ReadBoolean(value));
		ElementFactory.RegisterProperty("IsActive", (element, value) =>
			Ring(element, "IsActive").IsActive = ReadBoolean(value));
		ElementFactory.RegisterProperty("ViewportSize", (element, value) =>
			Bar(element, "ViewportSize").ViewportSize = GherkinValue.ToDouble(value));
		ElementFactory.RegisterProperty("IndicatorMode", (element, value) =>
			Bar(element, "IndicatorMode").IndicatorMode = GherkinValue.ToEnum<ScrollingIndicatorMode>(value));
		ElementFactory.RegisterProperty("MaxRating", (element, value) =>
			Rating(element, "MaxRating").MaxRating = (int) Math.Round(GherkinValue.ToDouble(value)));
		ElementFactory.RegisterProperty("PlaceholderValue", (element, value) =>
			Rating(element, "PlaceholderValue").PlaceholderValue = GherkinValue.ToDouble(value));
		ElementFactory.RegisterProperty("IsClearEnabled", (element, value) =>
			Rating(element, "IsClearEnabled").IsClearEnabled = ReadBoolean(value));
		ElementFactory.RegisterProperty("InitialSetValue", (element, value) =>
			Rating(element, "InitialSetValue").InitialSetValue = (int) Math.Round(GherkinValue.ToDouble(value)));
	}

	/// <summary>The Value an element carries, whatever kind of range control it is.</summary>
	/// <param name="element">The element to read. Call this on the UI thread.</param>
	/// <returns>The value.</returns>
	public static double ValueOf(FrameworkElement element)
	{
		ArgumentNullException.ThrowIfNull(element);

		return element switch
		{
			RangeBase range => range.Value,
			RatingControl rating => rating.Value,
			ProgressRing ring => ring.Value,
			_ => throw Unsupported(element, "Value"),
		};
	}

	/// <summary>The Maximum an element carries, whatever kind of range control it is.</summary>
	/// <param name="element">The element to read. Call this on the UI thread.</param>
	/// <returns>The maximum.</returns>
	public static double MaximumOf(FrameworkElement element)
	{
		ArgumentNullException.ThrowIfNull(element);

		return element switch
		{
			RangeBase range => range.Maximum,
			ProgressRing ring => ring.Maximum,
			_ => throw Unsupported(element, "Maximum"),
		};
	}

	/// <summary>The Minimum an element carries, whatever kind of range control it is.</summary>
	/// <param name="element">The element to read. Call this on the UI thread.</param>
	/// <returns>The minimum.</returns>
	public static double MinimumOf(FrameworkElement element)
	{
		ArgumentNullException.ThrowIfNull(element);

		return element switch
		{
			RangeBase range => range.Minimum,
			ProgressRing ring => ring.Minimum,
			_ => throw Unsupported(element, "Minimum"),
		};
	}

	private static void SetValue(FrameworkElement element, double value)
	{
		switch (element)
		{
			case RangeBase range:
				range.Value = value;
				break;
			case RatingControl rating:
				rating.Value = value;
				break;
			case ProgressRing ring:
				ring.Value = value;
				break;
			default:
				throw Unsupported(element, "Value");
		}
	}

	private static void SetMinimum(FrameworkElement element, double value)
	{
		switch (element)
		{
			case RangeBase range:
				range.Minimum = value;
				break;
			case ProgressRing ring:
				ring.Minimum = value;
				break;
			default:
				throw Unsupported(element, "Minimum");
		}
	}

	private static void SetMaximum(FrameworkElement element, double value)
	{
		switch (element)
		{
			case RangeBase range:
				range.Maximum = value;
				break;
			case ProgressRing ring:
				ring.Maximum = value;
				break;
			default:
				throw Unsupported(element, "Maximum");
		}
	}

	private static void SetIsIndeterminate(FrameworkElement element, string value)
	{
		switch (element)
		{
			case ProgressBar bar:
				bar.IsIndeterminate = ReadBoolean(value);
				break;
			case ProgressRing ring:
				ring.IsIndeterminate = ReadBoolean(value);
				break;
			default:
				throw Unsupported(element, "IsIndeterminate");
		}
	}

	private static FrameworkElement BuildSlider()
	{
		var slider = new Slider
		{
			// The thumb tool tip is a popup that would paint over the panel during a drag and
			// has nothing to do with the requirement under test; a scenario that wants it can
			// switch it back on.
			IsThumbToolTipEnabled = false,
		};
		return Watch(slider);
	}

	private static FrameworkElement BuildRatingControl()
	{
		var rating = new RatingControl();
		rating.ValueChanged += (sender, _) => EventRecorder.Record(sender.Name, ValueChangedEvent);
		return rating;
	}

	// The name is read when the event fires rather than when the control is built, because the
	// element factory gives an element its name only after the factory has returned it.
	private static RangeBase Watch(RangeBase range)
	{
		range.ValueChanged += (sender, _) =>
			EventRecorder.Record(((FrameworkElement) sender).Name, ValueChangedEvent);
		return range;
	}

	private static bool ReadBoolean(string value) =>
		bool.TryParse(GherkinValue.Unquote(value), out var parsed)
			? parsed
			: throw new FormatException($"\"{value}\" is not True or False.");

	private static RangeBase Range(FrameworkElement element, string property) =>
		element as RangeBase ?? throw Unsupported(element, property);

	private static Slider Sliding(FrameworkElement element, string property) =>
		element as Slider ?? throw Unsupported(element, property);

	private static ProgressRing Ring(FrameworkElement element, string property) =>
		element as ProgressRing ?? throw Unsupported(element, property);

	private static ProgressBar Progress(FrameworkElement element, string property) =>
		element as ProgressBar ?? throw Unsupported(element, property);

	private static ScrollBar Bar(FrameworkElement element, string property) =>
		element as ScrollBar ?? throw Unsupported(element, property);

	private static RatingControl Rating(FrameworkElement element, string property) =>
		element as RatingControl ?? throw Unsupported(element, property);

	private static NotSupportedException Unsupported(FrameworkElement element, string property) =>
		new(string.Create(CultureInfo.InvariantCulture,
			$"A {element.GetType().Name} named \"{element.Name}\" has no {property} the harness can set."));
}
