using System.Runtime.CompilerServices;
using CodeBrix.Platform.Foundation.Extensibility;
using CodeBrix.Platform.UI.Xaml.Media.Imaging.Svg;
using SvgProvider = CodeBrix.Platform.UI.Svg.SvgProvider;

namespace CodeBrix.Platform.UI.AddIn.Svg.UIReqs;

/// <summary>
/// Turns the SVG add-in on for this assembly's scenarios, the way an application head does.
/// <para>
/// In an application the registration is generated into the App's code-behind: the XAML
/// generator walks the referenced assemblies for their extension attributes and emits one
/// registration call each. A UIReqs project has no XAML and no App, so nothing is generated and
/// every <c>SvgImageSource</c> would quietly draw nothing at all - which is a blank region, not
/// a failure that says why. This is the same fix the SVG add-in's own host-free test project
/// makes, for the same reason.
/// </para>
/// <para>
/// It must run before the FIRST <c>SvgImageSource</c> is constructed, because that constructor
/// is what asks for the provider; a module initializer is the only hook early enough, since the
/// element factory builds a source as soon as a scenario asks for an "SvgImage".
/// </para>
/// </summary>
public static class Registration
{
	/// <summary>
	/// Registers the add-in's provider as the framework's SVG provider, unless something has
	/// already registered one. The guard is load-bearing: registering twice throws, and a
	/// process that also loaded another coverage group's registration would fail on the very
	/// first line it ran.
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
