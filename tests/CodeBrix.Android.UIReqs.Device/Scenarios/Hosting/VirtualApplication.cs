using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace CodeBrix.Platform.UI.Core.UIReqs.Hosting;

/// <summary>
/// The application the scenarios run inside: one window, one root panel painted an opaque
/// known colour, and no XAML at all - every element a scenario shows is built in C#.
/// <para>
/// One process hosts exactly one of these (the window wrapper, the pointer source and the
/// dispatcher overrides are process-wide), so it is launched once by the test-run hook and
/// scenarios only swap what the root panel holds.
/// </para>
/// </summary>
public sealed class VirtualApplication : Application
{
	/// <summary>The name the root panel is registered under, so scenarios can talk about it.</summary>
	public const string RootElementName = "root";

	/// <summary>The text font the virtual application draws with.</summary>
	/// <remarks>
	/// The framework default is "Segoe UI", which no Linux machine has, and the family rule is
	/// no system-font fallback - so the application carries its own font exactly as every
	/// sample does. The symbols font needs no assignment: the framework default already points
	/// into the Fonts.Fluent package, and referencing that package is what puts the file where
	/// the URI resolves.
	/// </remarks>
	public const string TextFontFamily = "ms-appx:///CodeBrix.Platform.Fonts.OpenSans/Fonts/OpenSans.ttf";

	private static readonly Color LightBackground = Color.FromArgb(0xFF, 0xFF, 0xFF, 0xFF);
	private static readonly Color DarkBackground = Color.FromArgb(0xFF, 0x20, 0x20, 0x20);

	private Window? _window;
	private Grid? _root;

	/// <summary>Builds the application. The host calls this on the UI thread.</summary>
	public VirtualApplication()
	{
		// Both of these have to happen before the first string is measured: the font because the
		// framework default names a face no Linux machine has, and the engine because nothing
		// else in a head-free process loads the native library the text layout needs.
		global::CodeBrix.Platform.UI.FeatureConfiguration.Font.DefaultTextFontFamily = TextFontFamily;
		TextEngineBootstrap.Initialize();
		Resources.MergedDictionaries.Add(new XamlControlsResources());
		Running = this;
	}

	/// <summary>The instance the host built, or <c>null</c> before the application launches.</summary>
	public static VirtualApplication? Running { get; private set; }

	/// <summary>The instance the host built.</summary>
	/// <exception cref="InvalidOperationException">The application has not launched yet.</exception>
	public static VirtualApplication Instance =>
		Running ?? throw new InvalidOperationException(
			"The virtual application has not launched yet. TestRunHooks launches it before the first scenario.");

	/// <summary>The window the application activated.</summary>
	/// <exception cref="InvalidOperationException">The application has not launched yet.</exception>
	public Window Window =>
		_window ?? throw new InvalidOperationException("The virtual application has not launched yet.");

	/// <summary>
	/// The root panel: it fills the window, it is painted opaque, and it holds whatever the
	/// current scenario is showing.
	/// </summary>
	/// <exception cref="InvalidOperationException">The application has not launched yet.</exception>
	public Grid Root =>
		_root ?? throw new InvalidOperationException("The virtual application has not launched yet.");

	/// <summary>
	/// The colour the root panel is painted. Every pixel of a frame is opaque once the root
	/// has painted, so "blank", "ink" and "changed" all have this colour as their reference.
	/// </summary>
	public Color BackgroundColor { get; private set; } = LightBackground;

	/// <summary>Builds the window and its root panel. The host calls this on the UI thread.</summary>
	/// <param name="args">The launch arguments the host supplies.</param>
	protected override void OnLaunched(LaunchActivatedEventArgs args)
	{
		BackgroundColor = BackgroundColorFor(RequestedTheme);

		var root = new Grid
		{
			Name = RootElementName,
			Background = new SolidColorBrush(BackgroundColor),
			HorizontalAlignment = HorizontalAlignment.Stretch,
			VerticalAlignment = VerticalAlignment.Stretch,
		};

		var window = new Window
		{
			Content = root,
		};

		_root = root;
		_window = window;

		window.Activate();
	}

	/// <summary>The background colour that belongs to a theme.</summary>
	/// <param name="theme">The application theme.</param>
	/// <returns>Opaque white for the light theme, opaque #FF202020 for the dark theme.</returns>
	public static Color BackgroundColorFor(ApplicationTheme theme) =>
		theme == ApplicationTheme.Dark ? DarkBackground : LightBackground;

	/// <summary>
	/// Shows one element, replacing whatever the root was holding, and lays the tree out.
	/// </summary>
	/// <param name="content">The element to show.</param>
	/// <returns>A task that completes once the UI thread has applied the change.</returns>
	public async Task SetContentAsync(UIElement content)
	{
		ArgumentNullException.ThrowIfNull(content);

		await TestTargetFixture.RunOnUIThreadAsync(() =>
		{
			Root.Children.Clear();
			Root.Children.Add(content);
			Root.UpdateLayout();
		}).ConfigureAwait(false);

		// ANDROID PORT (8): Core raises Loaded on the NEXT layout tick (RequestRaiseLoadedEventOnNextTick) and an
		// element is not hit-testable before it is loaded (CoerceHitTestVisibility), so a tap sent right after this
		// step could fall through to the panel ("first-tap race"): wait (at most ~1 s) until the content is loaded.
		for (var i = 0; i < 60; i++)
		{
			var loaded = true;
			await TestTargetFixture.RunOnUIThreadAsync(() => loaded = content is not FrameworkElement element || element.IsLoaded).ConfigureAwait(false);
			if (loaded)
			{
				break;
			}

			await Task.Delay(16).ConfigureAwait(false);
		}
	}

	/// <summary>Empties the root panel and lays the tree out.</summary>
	/// <returns>A task that completes once the UI thread has applied the change.</returns>
	public Task ClearContentAsync() =>
		TestTargetFixture.RunOnUIThreadAsync(() =>
		{
			Root.Children.Clear();
			Root.UpdateLayout();
		});

	/// <summary>
	/// Finds an element by its <see cref="FrameworkElement.Name"/> anywhere below the root,
	/// walking the visual tree rather than a namescope - elements built in C# and added to a
	/// panel are not registered in one.
	/// </summary>
	/// <param name="name">The element name to look for.</param>
	/// <returns>The element, or <c>null</c> when the tree holds no element of that name.</returns>
	public FrameworkElement? FindByName(string name)
	{
		ArgumentException.ThrowIfNullOrEmpty(name);

		if (_root is null)
		{
			return null;
		}

		if (string.Equals(_root.Name, name, StringComparison.Ordinal))
		{
			return _root;
		}

		return FindByName(_root, name);
	}

	private static FrameworkElement? FindByName(DependencyObject parent, string name)
	{
		var count = VisualTreeHelper.GetChildrenCount(parent);
		for (var i = 0; i < count; i++)
		{
			var child = VisualTreeHelper.GetChild(parent, i);
			if (child is FrameworkElement element
				&& string.Equals(element.Name, name, StringComparison.Ordinal))
			{
				return element;
			}

			var found = FindByName(child, name);
			if (found is not null)
			{
				return found;
			}
		}

		return null;
	}

	/// <summary>The names of every element currently below the root, for failure messages.</summary>
	/// <returns>The element names, in visual-tree order.</returns>
	public IReadOnlyList<string> NamedElements()
	{
		var names = new List<string>();
		if (_root is not null)
		{
			CollectNames(_root, names);
		}

		return names;
	}

	private static void CollectNames(DependencyObject parent, List<string> names)
	{
		if (parent is FrameworkElement element && !string.IsNullOrEmpty(element.Name))
		{
			names.Add(element.Name);
		}

		var count = VisualTreeHelper.GetChildrenCount(parent);
		for (var i = 0; i < count; i++)
		{
			CollectNames(VisualTreeHelper.GetChild(parent, i), names);
		}
	}
}
