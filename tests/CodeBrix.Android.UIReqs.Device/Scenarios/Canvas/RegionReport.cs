using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using SKColor = CodeBrix.Android.UIReqs.Device.Canvas.PixelColor;

namespace CodeBrix.Platform.UI.Core.UIReqs.Canvas;

/// <summary>
/// What a person needs in front of them when a pixel assertion fails: how big the region was,
/// what was actually in it, where the ink sat, and the PNG to open.
/// </summary>
public static class RegionReport
{
	/// <summary>How many distinct colours a report lists.</summary>
	public const int ReportedColorCount = 5;

	/// <summary>The report for one region, ending with the path of the archived frame.</summary>
	/// <param name="region">The region the assertion was about.</param>
	/// <param name="pngPath">The archived frame, when one was written.</param>
	/// <returns>The report, as several lines of text.</returns>
	public static string Describe(Region region, string? pngPath = null)
	{
		ArgumentNullException.ThrowIfNull(region);

		var builder = new StringBuilder();
		builder.Append(CultureInfo.InvariantCulture,
			$"  region      : {region.Description} at {region.Bounds} ({region.PixelCount} pixels)");
		builder.AppendLine();
		builder.Append(CultureInfo.InvariantCulture,
			$"  frame       : sequence {region.Frame.Sequence}, render generation {region.Frame.RenderGeneration}, {region.Frame.Width} x {region.Frame.Height}");
		builder.AppendLine();
		builder.Append(CultureInfo.InvariantCulture, $"  background  : {ColorMatch.Describe(CanvasAssert.Background)}");
		builder.AppendLine();

		foreach (var line in TopColors(region))
		{
			builder.Append(CultureInfo.InvariantCulture, $"  {line}");
			builder.AppendLine();
		}

		builder.Append(CultureInfo.InvariantCulture, $"  ink bounds  : {DescribeInkBounds(region)}");
		builder.AppendLine();
		builder.Append(CultureInfo.InvariantCulture, $"  png         : {pngPath ?? "(not archived)"}");

		return builder.ToString();
	}

	/// <summary>The most common colours of a region, most common first.</summary>
	/// <param name="region">The region to count.</param>
	/// <returns>One line per colour, with its share of the region.</returns>
	public static IReadOnlyList<string> TopColors(Region region)
	{
		ArgumentNullException.ThrowIfNull(region);

		var counts = new Dictionary<uint, int>();
		foreach (var pixel in region.Pixels())
		{
			var key = (uint) pixel;
			counts.TryGetValue(key, out var count);
			counts[key] = count + 1;
		}

		var total = (double) region.PixelCount;
		return counts
			.OrderByDescending(pair => pair.Value)
			.Take(ReportedColorCount)
			.Select((pair, index) => string.Create(CultureInfo.InvariantCulture,
				$"colour {index + 1}    : {ColorMatch.Describe(new SKColor(pair.Key))} {pair.Value / total * 100:0.00}% ({pair.Value} pixels)"))
			.ToArray();
	}

	private static string DescribeInkBounds(Region region) =>
		CanvasAssert.TryInkBounds(region, out var bounds)
			? string.Create(CultureInfo.InvariantCulture,
				$"{bounds} ({CanvasAssert.InkFraction(region) * 100:0.00}% of the region is ink)")
			: "none - every pixel is the background";
}
