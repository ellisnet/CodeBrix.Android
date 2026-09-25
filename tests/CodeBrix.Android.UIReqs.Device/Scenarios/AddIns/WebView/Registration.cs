using System;
using System.Runtime.CompilerServices;
using CodeBrix.Platform.Foundation.Extensibility;
using CodeBrix.Platform.UI.Xaml.Controls;
using Microsoft.Web.WebView2.Core;
// ANDROID PORT (c): the using alias of the Linux provider (WpeNativeWebViewProvider) is removed with its registration.

namespace CodeBrix.Platform.UI.AddIn.WebView.UIReqs;

/// <summary>
/// Turns the WebView add-in on for this assembly's scenarios, the way an application head does.
/// <para>
/// In an application the registration is generated into the App's code-behind: the XAML
/// generator walks the referenced assemblies for their extension attributes and emits one
/// registration call each. A UIReqs project has no XAML and no App, so nothing is generated,
/// and a <c>WebView2</c> would apply its template, find no provider for the native view, and
/// then sit there as an empty rectangle - which is a blank region, not a failure that says why.
/// </para>
/// <para>
/// The shape of the call is the one the add-in's own <c>[assembly: ApiExtension]</c> attribute
/// declares: the extension is the native web view provider, its owner is the
/// <see cref="CoreWebView2"/> that asks for it, and it applies on Linux only. The default style
/// needs nothing here - the template whose <c>ContentPresenter</c> the provider is handed comes
/// from the resources the core harness's application already merges.
/// </para>
/// </summary>
public static class Registration
{
	/// <summary>
	/// Registers the add-in's native web view provider, unless something has already registered
	/// one. The guard is load-bearing: registering twice throws, and a process that also loaded
	/// another coverage group's registration would fail on the very first line it ran.
	/// <para>
	/// It must run before the first <see cref="CoreWebView2"/> applies its owner's template,
	/// because that is when the provider is asked for; a module initializer is the only hook
	/// early enough, since the element factory builds the control as soon as a scenario asks
	/// for a "WebView".
	/// </para>
	/// </summary>
	// ANDROID PORT (c): the Linux engine's provider (WpeNativeWebViewProvider, registered here on Linux) does not exist on
	// Android; the Android WebView add-in (CodeBrix.Android.UI.WebView) registers its provider from its own module
	// initializer, which the framework runs at start-up. Nothing is left to register here.
	internal static void RegisterTheNativeWebViewProvider()
	{
	}
}
