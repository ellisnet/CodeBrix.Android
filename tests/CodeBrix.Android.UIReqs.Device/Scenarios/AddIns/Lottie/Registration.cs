using System.Runtime.CompilerServices;
using CodeBrix.Platform.Foundation.Extensibility;
using Microsoft.UI.Xaml.Controls;
using LottieVisualSourceProvider = CodeBrix.Platform.UI.Lottie.LottieVisualSourceProvider;

namespace CodeBrix.Platform.UI.AddIn.Lottie.UIReqs;

/// <summary>
/// Turns the animation add-in on for this assembly's scenarios, the way an application head
/// does.
/// <para>
/// In an application the registration is generated into the App's code-behind: the XAML
/// generator walks the referenced assemblies for their extension attributes and emits one
/// registration call each. A UIReqs project has no XAML and no App, so nothing is generated.
/// The two animation sources are ordinary public classes and need none of this; the CONTROL
/// that resolves the provider through the registry does - the framework's own ProgressRing
/// asks for an <see cref="ILottieVisualSourceProvider"/> in its constructor, and without one
/// it draws a red warning line instead of a ring and only logs why.
/// </para>
/// <para>
/// It must run before the FIRST ProgressRing is constructed, and the element factory builds
/// one as soon as a scenario asks for a "ProgressRing", so a module initializer is the only
/// hook early enough.
/// </para>
/// </summary>
public static class Registration
{
	/// <summary>
	/// Registers the add-in's provider as the framework's animation-source provider, unless
	/// something has already registered one. The guard is load-bearing: registering twice
	/// throws, and a process that also loaded another coverage group's registration would fail
	/// on the very first line it ran.
	/// </summary>
	// ANDROID PORT (a): this repository treats warnings as errors; CA2255 (module initializer in a library-style
	// project) is suppressed here as the repository's own bootstraps do.
#pragma warning disable CA2255
	[ModuleInitializer]
#pragma warning restore CA2255
	internal static void RegisterTheLottieVisualSourceProvider()
	{
		if (!ApiExtensibility.IsRegistered<ILottieVisualSourceProvider>())
		{
			ApiExtensibility.Register(typeof(ILottieVisualSourceProvider),
				owner => new LottieVisualSourceProvider(owner));
		}
	}
}
