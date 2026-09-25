using System.Collections.Generic;
using System.Linq;
using CodeBrix.Platform.UI.Core.UIReqs.Canvas;
using CodeBrix.Platform.UI.Core.UIReqs.Support;
using Reqnroll;
using SilverAssertions;
using SKColor = CodeBrix.Android.UIReqs.Device.Canvas.PixelColor;

namespace CodeBrix.Platform.UI.Core.UIReqs.Steps;

/// <summary>
/// ANDROID PORT (AP1.9, pin 1.0.268.12): the steps of the Harness scenario "The colour the harness reports for a
/// shade is its most common exact colour" (WPE1-5 B7, the Platform copy's adoption of this copy's
/// <see cref="ModeColor"/>). Written here from the scenario text: the Platform's step code was not copied (the
/// Platform repository is closed to the Android track since the engine pass). The pixels go through exactly what
/// <see cref="CanvasAssert.TryInkColor"/> does - quantize into shades, take the most common shade, report its
/// representative colour.
/// </summary>
[Binding]
public sealed class HarnessColourSteps
{
	private readonly List<SKColor> _pixels = new();
	private SKColor _reported;
	private int _reportedCount;

	/// <summary>Makes a run of pixels of one shade: a few of one exact colour first, then more of another.</summary>
	[Given("pixels of one colour shade: {int} of {string} first, then {int} of {string}")]
	public void Given_pixels_of_one_shade(int firstCount, string first, int thenCount, string then)
	{
		_pixels.Clear();
		_pixels.AddRange(Enumerable.Repeat(ColorMatch.ToSkia(Colors.Parse(first)), firstCount));
		_pixels.AddRange(Enumerable.Repeat(ColorMatch.ToSkia(Colors.Parse(then)), thenCount));

		var counts = new Dictionary<uint, int>();
		var representatives = new ModeColor();
		foreach (var pixel in _pixels)
		{
			var key = (uint) ColorMatch.Quantize(pixel);
			counts.TryGetValue(key, out var count);
			counts[key] = count + 1;
			representatives.Add(key, pixel);
		}

		var mode = counts.OrderByDescending(pair => pair.Value).First();
		_reported = representatives.Of(mode.Key);
		_reportedCount = mode.Value;
	}

	/// <summary>Asserts the exact colour the harness reports for the shade.</summary>
	[Then("the harness reports their colour as exactly {string}")]
	public void Then_the_harness_reports_exactly(string expected) =>
		((uint) _reported).Should().Be((uint) ColorMatch.ToSkia(Colors.Parse(expected)), "the most common exact colour of the shade is reported, not the first pixel met");

	/// <summary>Asserts how many pixels the reported shade holds.</summary>
	[Then("the harness reports that colour for {int} of them")]
	public void Then_the_harness_reports_the_count(int expected) => _reportedCount.Should().Be(expected);
}
