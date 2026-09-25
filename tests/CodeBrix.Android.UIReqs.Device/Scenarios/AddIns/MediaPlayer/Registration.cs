using System.Runtime.CompilerServices;
using CodeBrix.Platform.Foundation.Extensibility;
using CodeBrix.Platform.Media.Playback;
using Microsoft.UI.Xaml.Controls;
using MediaPlayerEngine = Windows.Media.Playback.MediaPlayer;

namespace CodeBrix.Platform.UI.AddIn.MediaPlayer.UIReqs;

/// <summary>
/// Turns the media add-in on for this assembly's scenarios, the way an application head does.
/// <para>
/// In an application the two registrations below are generated into the App's code-behind: the
/// XAML generator walks the referenced assemblies for their extension attributes and emits one
/// registration call each, gated on the operating system. A UIReqs project has no XAML and no
/// App, so nothing is generated - and the failure that causes is the quietest one in this whole
/// suite. The element still builds, its template still applies, its transport controls still
/// draw, and every Play, Pause and Position is a silent no-op against a player that has no
/// engine behind it. That is why the group's registration hook ASSERTS the registration rather
/// than trusting it.
/// </para>
/// <para>
/// Both must run before the first element is built: the player extension is created from the
/// <c>MediaPlayer</c> constructor, which the element runs as it applies its template, and the
/// presenter extension from the <c>MediaPlayerPresenter</c> constructor, which is part of that
/// same template. A module initializer is the only hook early enough.
/// </para>
/// </summary>
public static class Registration
{
	/// <summary>
	/// Registers the add-in's playback engine and its video presenter, unless something has
	/// already registered them. The guard is load-bearing: registering twice throws, and the
	/// registry is process-wide.
	/// </summary>
	// ANDROID PORT (c): the Linux engine (LibVLC through SkiaMediaPlayerExtension / SkiaMediaPlayerPresenterExtension)
	// does not exist on Android; the Android MediaPlayer add-in (CodeBrix.Android.UI.MediaPlayer, Media3 ExoPlayer)
	// registers both extensions from its own module initializer, which the framework runs at start-up.
	internal static void RegisterTheMediaExtensions()
	{
	}
}
