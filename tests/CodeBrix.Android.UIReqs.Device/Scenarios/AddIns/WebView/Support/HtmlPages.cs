using System;
using System.Collections.Generic;
using System.Linq;

namespace CodeBrix.Platform.UI.AddIn.WebView.UIReqs.Support;

/// <summary>
/// The pages the scenarios show. They are written here as text rather than fetched from
/// anywhere, so that what a scenario expects to see on the panel and what the page says cannot
/// drift apart, and so that the Portrait twin has nothing to link but source. Nothing here
/// reaches a network: a page given to the control this way arrives at the engine as a
/// <c>data:text/html</c> document.
/// <para>
/// Every page sets <c>margin:0</c> on the document, and no scenario asserts anything about
/// rendered text: a browser's default margins, its scrollbars and the faces it has installed
/// differ from machine to machine, whereas a flat colour block on a whole-pixel boundary is the
/// same everywhere. The colours are the ones the harness names, so a feature file can say "Red"
/// and mean exactly what the page painted.
/// </para>
/// </summary>
public static class HtmlPages
{
	/// <summary>
	/// The name of the page the engine is started on: a flat Gray document that shares no colour
	/// with any other page here, so that a later scenario asserting a colour is asserting about
	/// the page it navigated to rather than about whatever happened to be left on the panel.
	/// </summary>
	public const string Warmup = "Warmup";

	/// <summary>The name of the flat Red page: the whole viewport is one colour and nothing else.</summary>
	public const string Solid = "Solid";

	/// <summary>
	/// The name of the two-colour page: its left half Red and its right half Blue, both sized as
	/// a percentage of the viewport, so where the colours land says where the PAGE put them.
	/// </summary>
	public const string Halves = "Halves";

	/// <summary>
	/// The name of the page with a bar of a FIXED width: 100 CSS pixels of Red down the left of a
	/// Blue document. Fixed pixels are what tells a re-render from a stale frame stretched to a
	/// new size - a percentage would look the same either way.
	/// </summary>
	public const string Bar = "Bar";

	/// <summary>
	/// The name of the page that talks to its host: it posts "ready" as it loads and posts
	/// "tapped" and turns Green when it is touched. The two messages deliberately use the two
	/// different idioms a page may post with, so one fixture proves both routes.
	/// </summary>
	public const string Button = "Button";

	/// <summary>The name of the titled page, for what the engine reports as the document title.</summary>
	public const string Title = "Title";

	/// <summary>
	/// The name of the page holding one text box. The box sits in the middle quarter of a Yellow
	/// document rather than filling it, so that a scenario can see the page has painted before it
	/// types into it - a text box that filled the viewport would be white on a white panel.
	/// </summary>
	public const string Typing = "Typing";

	/// <summary>The title the <see cref="Title"/> page gives itself.</summary>
	public const string TitleText = "Hello UIReqs";

	private const string WarmupDocument =
		"""
		<!DOCTYPE html><html><head><meta charset="utf-8"><title>Warmup</title></head>
		<body style="margin:0;background:#808080"></body></html>
		""";

	private const string SolidDocument =
		"""
		<!DOCTYPE html><html><head><meta charset="utf-8"><title>Solid</title></head>
		<body style="margin:0;background:#FF0000"></body></html>
		""";

	private const string HalvesDocument =
		"""
		<!DOCTYPE html><html><head><meta charset="utf-8"><title>Halves</title>
		<style>html,body{margin:0;height:100%;overflow:hidden}
		div{position:absolute;top:0;height:100%;width:50%}</style></head>
		<body><div style="left:0;background:#FF0000"></div>
		<div style="left:50%;background:#0000FF"></div></body></html>
		""";

	private const string BarDocument =
		"""
		<!DOCTYPE html><html><head><meta charset="utf-8"><title>Bar</title>
		<style>html,body{margin:0;height:100%;overflow:hidden;background:#0000FF}</style></head>
		<body><div style="position:absolute;left:0;top:0;width:100px;height:100%;background:#FF0000"></div></body></html>
		""";

	private const string ButtonDocument =
		"""
		<!DOCTYPE html><html><head><meta charset="utf-8"><title>Button</title>
		<style>html,body{margin:0;height:100%;overflow:hidden}
		#pad{position:absolute;left:0;top:0;width:100%;height:100%;background:#FF0000}</style></head>
		<body><div id="pad"></div>
		<script>
		document.getElementById('pad').addEventListener('click', function () {
			document.getElementById('pad').style.background = '#008000';
			window.chrome.webview.postMessage('tapped');
		});
		window.addEventListener('load', function () {
			window.webkit.messageHandlers.codebrixWebView.postMessage('ready');
		});
		</script></body></html>
		""";

	private const string TitleDocument =
		"""
		<!DOCTYPE html><html><head><meta charset="utf-8"><title>Hello UIReqs</title></head>
		<body style="margin:0;background:#0000FF"></body></html>
		""";

	private const string TypingDocument =
		"""
		<!DOCTYPE html><html><head><meta charset="utf-8"><title>Typing</title>
		<style>html,body{margin:0;height:100%;overflow:hidden;background:#FFFF00}
		input{position:absolute;left:25%;top:25%;width:50%;height:50%;border:0;font-size:48px}</style></head>
		<body><input id="box" type="text" value=""></body></html>
		""";

	private static readonly Dictionary<string, string> Documents = new(StringComparer.OrdinalIgnoreCase)
	{
		[Warmup] = WarmupDocument,
		[Solid] = SolidDocument,
		[Halves] = HalvesDocument,
		[Bar] = BarDocument,
		[Button] = ButtonDocument,
		[Title] = TitleDocument,
		[Typing] = TypingDocument,
	};

	/// <summary>The page names a feature file may use, in alphabetical order.</summary>
	public static IReadOnlyCollection<string> Names
	{
		get
		{
			var names = new List<string>(Documents.Keys);
			names.Sort(StringComparer.OrdinalIgnoreCase);
			return names;
		}
	}

	/// <summary>The document a feature file named.</summary>
	/// <param name="name">The page name.</param>
	/// <returns>The page's HTML.</returns>
	/// <exception cref="NotSupportedException">No page of that name is written here.</exception>
	public static string Get(string name)
	{
		ArgumentException.ThrowIfNullOrEmpty(name);

		if (Documents.TryGetValue(name, out var document))
		{
			return document;
		}

		throw new NotSupportedException(
			$"There is no page named \"{name}\". The pages written for these scenarios are: "
			+ string.Join(", ", Names.Select(known => "\"" + known + "\"")) + ".");
	}
}
