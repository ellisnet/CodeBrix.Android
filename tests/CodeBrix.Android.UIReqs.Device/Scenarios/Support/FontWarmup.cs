using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CodeBrix.Platform.UI.Core.UIReqs.Hosting;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;

namespace CodeBrix.Platform.UI.Core.UIReqs.Support;

/// <summary>
/// Lays one throwaway string out in a font before any scenario measures with it.
/// <para>
/// The text engine answers the FIRST measurement through a font it has not loaded yet with an
/// interim face, and finishes loading the real one on a continuation the frame handshake cannot
/// see - so a control measured in that moment is laid out to the wrong advance widths and the
/// frame that shows it is neither wrong enough to look broken nor right enough to assert on.
/// Warming a font is not a sleep and not a poll: it is one real layout pass on the UI thread,
/// awaited, followed by the harness's own idle drain.
/// </para>
/// </summary>
/// <remarks>
/// The warm-up lays out a <see cref="TextBlock"/>, which is the path every control's text goes
/// through. The text engine's own layout type lives in an add-in this project does not
/// reference, so it is not touched here; a coverage group that draws through that engine warms
/// its font the same way, through this class.
/// </remarks>
public static class FontWarmup
{
	/// <summary>The string that is laid out, chosen to touch ascenders, descenders and digits.</summary>
	public const string WarmupText = "Warming 0123 gjpqy";

	private static readonly object WarmedLock = new();
	private static readonly HashSet<string> WarmedUris = new(StringComparer.Ordinal);

	/// <summary>The font URIs this process has warmed, in the order they were warmed.</summary>
	public static IReadOnlyCollection<string> Warmed
	{
		get
		{
			lock (WarmedLock)
			{
				return new List<string>(WarmedUris);
			}
		}
	}

	/// <summary>Whether a font URI has already been warmed in this process.</summary>
	/// <param name="fontUri">The font URI, as a scenario or the application spells it.</param>
	/// <returns><c>true</c> when it has.</returns>
	public static bool IsWarm(string fontUri)
	{
		ArgumentException.ThrowIfNullOrEmpty(fontUri);

		lock (WarmedLock)
		{
			return WarmedUris.Contains(fontUri);
		}
	}

	/// <summary>
	/// Lays a throwaway string out in a font on the UI thread and waits for the dispatcher to
	/// drain, once per URI per process. A URI that is already warm costs nothing.
	/// </summary>
	/// <param name="fontUri">The font URI, such as the application's own text font.</param>
	/// <returns>A task that completes when the font has been laid out with at least once.</returns>
	public static async Task WarmAsync(string fontUri)
	{
		ArgumentException.ThrowIfNullOrEmpty(fontUri);

		lock (WarmedLock)
		{
			if (!WarmedUris.Add(fontUri))
			{
				return;
			}
		}

		var measured = default(Size);
		await TestTargetFixture.RunOnUIThreadAsync(() =>
		{
			// Off the tree on purpose: the warm-up happens once before the first scenario and
			// again whenever a scenario asks for another font, and neither moment may disturb
			// what the panel is showing.
			var probe = new TextBlock
			{
				FontFamily = new FontFamily(fontUri),
				Text = WarmupText,
			};

			probe.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
			measured = probe.DesiredSize;
			probe.Arrange(new Rect(0, 0, measured.Width, measured.Height));
		}).ConfigureAwait(false);

		await TestTargetFixture.WaitForIdleAsync().ConfigureAwait(false);

		if (measured.Width <= 0 || measured.Height <= 0)
		{
			// A font that measures to nothing is the silent disaster the family rule about system
			// fonts exists to prevent: every scenario that draws text would then assert about an
			// empty rectangle. Say so here, where the cause is still in view.
			throw new InvalidOperationException(
				$"The font \"{fontUri}\" laid \"{WarmupText}\" out to {measured.Width} x {measured.Height}, "
				+ "so nothing would be drawn with it. Is the .ttf (and its .ttf.manifest) beside the test "
				+ "executable at the path the URI resolves to?");
		}
	}
}
