using System.Runtime.CompilerServices;
using CodeBrix.Platform.Foundation.Extensibility;
using CodeBrix.Platform.UI.Xaml.Media.Imaging.Svg;
using SvgProvider = CodeBrix.Platform.UI.Svg.SvgProvider;

namespace CodeBrix.Platform.UI.AddIn.CommandBar.UIReqs;

/// <summary>
/// Turns the SVG route on for this assembly's scenarios, the way an application head does.
/// <para>
/// Every icon a tool bar item shows is drawn through an <c>SvgImageSource</c>, and that source
/// asks for its provider in its own constructor. In an application the registration is generated
/// into the App's code-behind - the XAML generator walks the referenced assemblies for their
/// extension attributes and emits one registration call each. A UIReqs project has no XAML and no
/// App, so nothing is generated and every icon in the bar would quietly draw nothing at all,
/// which is a blank button rather than a failure that says why.
/// </para>
/// <para>
/// A module initializer is the only hook early enough: the element factory builds an icon source
/// as soon as a scenario asks for a button with an icon, and a source built before the provider
/// was registered caches the fact that it found none. The add-in's own host-free test project
/// makes the same call for the same reason.
/// </para>
/// </summary>
public static class Registration
{
	/// <summary>
	/// Registers the SVG add-in's provider as the framework's SVG provider, unless something has
	/// already registered one. The guard is load-bearing: registering twice throws, and a process
	/// that also loaded another coverage group's registration would fail on the very first line it
	/// ran.
	/// </summary>
	// ANDROID PORT (a): this repository treats warnings as errors; CA2255 (module initializer in a library-style
	// project) is suppressed here as the repository's own bootstraps do.
#pragma warning disable CA2255
	[ModuleInitializer]
#pragma warning restore CA2255
	internal static void RegisterTheSvgProvider()
	{
		if (!ApiExtensibility.IsRegistered<ISvgProvider>())
		{
			ApiExtensibility.Register(typeof(ISvgProvider), owner => new SvgProvider(owner));
		}
	}
}
