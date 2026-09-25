using System;
using System.Threading.Tasks;
using CodeBrix.Platform.UI.Core.UIReqs.Hosting;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace CodeBrix.Platform.UI.Core.UIReqs.Support;

/// <summary>
/// The two theme colours a requirement about a control's state has to be able to name. A
/// checked box, a progress indicator and a slider's filled part are all painted from the
/// theme's accent brush, and the glyph on top of them from the brush meant to be legible on
/// it; hard-coding those values in a feature file would make the scenario a statement about
/// one palette rather than about the control, so the names are bound to the brushes the
/// running application actually holds.
/// </summary>
public static class ThemeColors
{
	/// <summary>The name a feature file writes for the theme's accent fill.</summary>
	public const string AccentName = "Accent";

	/// <summary>The name a feature file writes for the colour drawn on top of the accent fill.</summary>
	public const string OnAccentName = "AccentText";

	private const string AccentResourceKey = "AccentFillColorDefaultBrush";
	private const string OnAccentResourceKey = "TextOnAccentFillColorPrimaryBrush";

	private static bool _registered;

	/// <summary>
	/// Binds the two names to the running application's brushes. Called once, after the
	/// application has launched and its resource dictionaries exist.
	/// </summary>
	/// <returns>A task that completes once the names are usable in a feature file.</returns>
	public static async Task RegisterAsync()
	{
		if (_registered)
		{
			return;
		}

		_registered = true;

		var accent = default(Color);
		var onAccent = default(Color);
		await TestTargetFixture.RunOnUIThreadAsync(() =>
		{
			accent = ResolveBrushColor(AccentResourceKey);
			onAccent = ResolveBrushColor(OnAccentResourceKey);
		}).ConfigureAwait(false);

		Colors.RegisterName(AccentName, accent);
		Colors.RegisterName(OnAccentName, onAccent);
	}

	private static Color ResolveBrushColor(string key)
	{
		var resources = Application.Current?.Resources
			?? throw new InvalidOperationException(
				"The application has no resources, so a theme brush cannot be read.");

		if (resources.TryGetValue(key, out var resource) && resource is SolidColorBrush brush)
		{
			return brush.Color;
		}

		throw new InvalidOperationException(
			$"The application's resources hold no SolidColorBrush called \"{key}\", so the "
			+ "feature files cannot name that theme colour.");
	}
}
