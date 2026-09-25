using System;
using System.Globalization;
using System.Threading.Tasks;
using CodeBrix.Platform.UI.Core.UIReqs.Hosting;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace CodeBrix.Platform.UI.Core.UIReqs.Support;

/// <summary>
/// What the ThemeFocus group's requirements need from the application itself: the one place a
/// theme can be changed after launch, and the colours a control's template resolved out of that
/// theme.
/// <para>
/// The application's own <c>RequestedTheme</c> cannot be set once the application is running,
/// so a scenario that is about the theme asks the ROOT element for one instead - which is the
/// documented way, and which the framework turns into an application-wide theme change because
/// the root is the window's content. A scenario that asks must put the theme back, and the
/// steps class does it in a hook as well, so one failing scenario cannot leave the next one on
/// the wrong palette.
/// </para>
/// </summary>
public static class ThemeElements
{
	private static bool _registered;
	private static ApplicationTheme? _launchTheme;

	/// <summary>Adds the ThemeFocus controls and properties to the element factory.</summary>
	public static void Register()
	{
		if (_registered)
		{
			return;
		}

		_registered = true;

		ElementFactory.RegisterKind("Image", () => new Image());
		ElementFactory.RegisterProperty("Stretch", (element, value) =>
			Picture(element, "Stretch").Stretch = GherkinValue.ToEnum<Stretch>(value));
	}

	/// <summary>
	/// Asks the root element for a theme, which is how a running application changes one.
	/// </summary>
	/// <param name="theme">The theme to ask for.</param>
	/// <returns>A task that completes once the UI thread has applied the change.</returns>
	public static Task SetRootThemeAsync(ElementTheme theme) =>
		TestTargetFixture.RunOnUIThreadAsync(() =>
		{
			_launchTheme ??= Application.Current.RequestedTheme;
			VirtualApplication.Instance.Root.RequestedTheme = theme;
		});

	/// <summary>
	/// Puts the theme back to the one the application launched with, if a scenario changed it.
	/// </summary>
	/// <returns>A task that completes once the UI thread has put it back.</returns>
	public static async Task RestoreRootThemeAsync()
	{
		if (_launchTheme is not { } launched || !TestTargetFixture.IsLaunched)
		{
			return;
		}

		_launchTheme = null;
		await TestTargetFixture.RunOnUIThreadAsync(() =>
			VirtualApplication.Instance.Root.RequestedTheme =
				launched == ApplicationTheme.Dark ? ElementTheme.Dark : ElementTheme.Light).ConfigureAwait(false);
		await TestTargetFixture.WaitForIdleAsync().ConfigureAwait(false);
	}

	/// <summary>The theme the root element is actually drawn in.</summary>
	/// <returns>The theme.</returns>
	public static async Task<ElementTheme> RootThemeAsync()
	{
		var theme = ElementTheme.Default;
		await TestTargetFixture.RunOnUIThreadAsync(() =>
			theme = VirtualApplication.Instance.Root.ActualTheme).ConfigureAwait(false);

		return theme;
	}

	/// <summary>The theme the whole application is on.</summary>
	/// <returns>The theme.</returns>
	public static async Task<ApplicationTheme> ApplicationThemeAsync()
	{
		var theme = ApplicationTheme.Light;
		await TestTargetFixture.RunOnUIThreadAsync(() =>
			theme = Application.Current.RequestedTheme).ConfigureAwait(false);

		return theme;
	}

	/// <summary>
	/// The colour a control's template resolved for its own Background. Reading it off the tree
	/// is what makes the pixel assertion a statement about the control rather than about one
	/// palette's exact values.
	/// </summary>
	/// <param name="name">The Gherkin name of the control.</param>
	/// <returns>The colour.</returns>
	public static async Task<Color> BackgroundColorAsync(string name)
	{
		var color = default(Color);
		await TestTargetFixture.RunOnUIThreadAsync(() =>
		{
			var element = ElementRegistry.Resolve(name);
			color = SolidColor(element, Brush(element, background: true), "Background");
		}).ConfigureAwait(false);

		return color;
	}

	/// <summary>The colour a control's template resolved for its own Foreground.</summary>
	/// <param name="name">The Gherkin name of the control.</param>
	/// <returns>The colour.</returns>
	public static async Task<Color> ForegroundColorAsync(string name)
	{
		var color = default(Color);
		await TestTargetFixture.RunOnUIThreadAsync(() =>
		{
			var element = ElementRegistry.Resolve(name);
			color = SolidColor(element, Brush(element, background: false), "Foreground");
		}).ConfigureAwait(false);

		return color;
	}

	private static Brush? Brush(FrameworkElement element, bool background) => element switch
	{
		Control control => background ? control.Background : control.Foreground,
		Border border when background => border.Background,
		Panel panel when background => panel.Background,
		TextBlock text when !background => text.Foreground,
		_ => null,
	};

	private static Color SolidColor(FrameworkElement element, Brush? brush, string what) => brush switch
	{
		SolidColorBrush solid => solid.Color,
		null => throw new NotSupportedException(string.Create(CultureInfo.InvariantCulture,
			$"A {element.GetType().Name} named \"{element.Name}\" has no {what} the harness can read.")),
		_ => throw new NotSupportedException(string.Create(CultureInfo.InvariantCulture,
			$"The {what} of \"{element.Name}\" is a {brush.GetType().Name}, not one flat colour.")),
	};

	private static Image Picture(FrameworkElement element, string what) =>
		element as Image ?? throw new NotSupportedException(string.Create(CultureInfo.InvariantCulture,
			$"A {element.GetType().Name} named \"{element.Name}\" has no {what} the harness can set."));
}
