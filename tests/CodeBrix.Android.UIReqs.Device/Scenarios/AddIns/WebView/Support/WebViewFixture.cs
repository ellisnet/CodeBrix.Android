using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using CodeBrix.Platform.UI.Core.UIReqs.Hosting;
using CodeBrix.Platform.UI.Core.UIReqs.Support;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Web.WebView2.Core;
using WebView2 = Microsoft.UI.Xaml.Controls.WebView2;

namespace CodeBrix.Platform.UI.AddIn.WebView.UIReqs.Support;

/// <summary>
/// The ONE web view the scenarios of this assembly share, and everything the harness has to
/// remember about what it has been doing.
/// <para>
/// There is one of these per process on purpose. The engine's own thread runs a main loop that
/// has no shutdown path, and the object holding an engine view releases it in a finalizer, so a
/// fresh control per scenario would leave a view - and its web process - behind for every
/// scenario of the run. The add-in's own guidance is the same: keep one control alive and
/// re-navigate it, because starting the engine is the expensive step. The control is therefore
/// built once, taken off the tree by <see cref="DetachAsync"/> when a scenario ends, and put
/// back on it by the next scenario's first Given.
/// </para>
/// <para>
/// Everything the control reports is recorded here as it happens, because it happens on the UI
/// thread while the step that asked for it is waiting on another: a navigation's completion, the
/// URI a navigation was announced with, and every message the page posted. The counts are the
/// scenario's, cleared by <see cref="BeginScenario"/>.
/// </para>
/// </summary>
public static class WebViewFixture
{
	/// <summary>
	/// The name the harness records this control's events under when a scenario has not named it
	/// yet. A scenario always names it, so this is only ever seen in a failure message.
	/// </summary>
	public const string UnnamedElement = "the web view";

	private static readonly object Gate = new();
	private static readonly Dictionary<string, int> MessageCounts = new(StringComparer.Ordinal);

	private static WebView2? _browser;
	private static int _navigationsCompleted;
	private static int _navigationsStarted;
	private static bool _cancelNextNavigation;
	private static string? _lastNavigationStartUri;
	private static bool _lastNavigationSucceeded;

	/// <summary>
	/// Whether the engine has already produced a composited frame in this process. The first
	/// scenario to ask pays for the engine and its web process; every later one does not.
	/// </summary>
	public static bool IsEngineStarted { get; private set; }

	/// <summary>
	/// How long the engine took to put its first composited frame on the panel, in milliseconds,
	/// or <c>null</c> before it has. Reported once, by the step that waited for it.
	/// </summary>
	public static long? FirstFrameMilliseconds { get; private set; }

	/// <summary>How many navigations have completed since the scenario began.</summary>
	public static int NavigationsCompleted
	{
		get
		{
			lock (Gate)
			{
				return _navigationsCompleted;
			}
		}
	}

	/// <summary>How many navigations have been announced as starting since the scenario began.</summary>
	public static int NavigationsStarted
	{
		get
		{
			lock (Gate)
			{
				return _navigationsStarted;
			}
		}
	}

	/// <summary>
	/// The URI the most recent navigation was announced with, or <c>null</c> when none has been.
	/// This is the control's own idea of where it is going, which for a page handed over as text
	/// is that text as a <c>data:</c> document - not the URI the engine ends up reporting.
	/// </summary>
	public static string? LastNavigationStartUri
	{
		get
		{
			lock (Gate)
			{
				return _lastNavigationStartUri;
			}
		}
	}

	/// <summary>Whether the most recently completed navigation reported that it succeeded.</summary>
	public static bool LastNavigationSucceeded
	{
		get
		{
			lock (Gate)
			{
				return _lastNavigationSucceeded;
			}
		}
	}

	/// <summary>
	/// The shared control, built on first use. Call this on the UI thread: it builds a
	/// <see cref="WebView2"/>, which is a control.
	/// </summary>
	public static WebView2 Shared
	{
		get
		{
			if (_browser is { } existing)
			{
				return existing;
			}

			var browser = new WebView2();
			Watch(browser);
			_browser = browser;
			return browser;
		}
	}

	/// <summary>Forgets everything the previous scenario's page did.</summary>
	public static void BeginScenario()
	{
		lock (Gate)
		{
			MessageCounts.Clear();
			_navigationsCompleted = 0;
			_navigationsStarted = 0;
			_cancelNextNavigation = false;
			_lastNavigationStartUri = null;
			_lastNavigationSucceeded = false;
		}
	}

	/// <summary>
	/// Refuses the next navigation the control announces, once. The refusal is made in the
	/// control's own <c>NavigationStarting</c> handler, which is the only place a navigation can
	/// be refused.
	/// </summary>
	public static void RefuseTheNextNavigation()
	{
		lock (Gate)
		{
			_cancelNextNavigation = true;
		}
	}

	/// <summary>Records that the engine has painted for the first time, and how long it took.</summary>
	/// <param name="milliseconds">How long the wait for that first frame lasted.</param>
	public static void RecordFirstFrame(long milliseconds)
	{
		if (!IsEngineStarted)
		{
			FirstFrameMilliseconds = milliseconds;
			IsEngineStarted = true;
		}
	}

	/// <summary>How often the page has posted one message to its host this scenario.</summary>
	/// <param name="message">The message text.</param>
	/// <returns>The count, which is zero when the page has never posted it.</returns>
	public static int MessageCount(string message)
	{
		ArgumentNullException.ThrowIfNull(message);

		lock (Gate)
		{
			MessageCounts.TryGetValue(message, out var count);
			return count;
		}
	}

	/// <summary>Every message the page has posted this scenario, as "message = count".</summary>
	/// <returns>The messages, for a failure message.</returns>
	public static IReadOnlyCollection<string> RecordedMessages()
	{
		lock (Gate)
		{
			var recorded = new List<string>();
			foreach (var pair in MessageCounts)
			{
				recorded.Add(string.Create(CultureInfo.InvariantCulture, $"\"{pair.Key}\" = {pair.Value}"));
			}

			recorded.Sort(StringComparer.Ordinal);
			return recorded;
		}
	}

	/// <summary>
	/// Takes the shared control off whatever was holding it, so the next scenario can put it
	/// somewhere else. The harness empties the root panel between scenarios, which is enough for
	/// a control the root itself was holding - but a scenario that put this one inside a layout
	/// of its own would leave it parented to a panel that is no longer on the tree, and the next
	/// scenario's attempt to show it would fail with "already has a parent".
	/// </summary>
	/// <returns>A task that completes once the control has no parent.</returns>
	public static async Task DetachAsync()
	{
		if (_browser is null || !TestTargetFixture.IsLaunched)
		{
			return;
		}

		await TestTargetFixture.RunOnUIThreadAsync(() =>
		{
			switch (_browser.Parent)
			{
				case Panel panel:
					panel.Children.Remove(_browser);
					break;
				case Border border when ReferenceEquals(border.Child, _browser):
					border.Child = null;
					break;
				case ContentControl content when ReferenceEquals(content.Content, _browser):
					content.Content = null;
					break;
				default:
					break;
			}
		}).ConfigureAwait(false);
	}

	private static void Watch(WebView2 browser)
	{
		browser.NavigationStarting += (sender, args) =>
		{
			var refuse = false;
			lock (Gate)
			{
				_navigationsStarted++;
				_lastNavigationStartUri = args.Uri;
				if (_cancelNextNavigation)
				{
					_cancelNextNavigation = false;
					refuse = true;
				}
			}

			args.Cancel = refuse;
			Record(sender, "NavigationStarting");
		};

		browser.NavigationCompleted += (sender, args) =>
		{
			lock (Gate)
			{
				_navigationsCompleted++;
				_lastNavigationSucceeded = args.IsSuccess;
			}

			Record(sender, "NavigationCompleted");
			Record(sender, args.IsSuccess ? "NavigationSucceeded" : "NavigationFailed");
		};

		browser.WebMessageReceived += (sender, args) =>
		{
			var message = args.TryGetWebMessageAsString();
			lock (Gate)
			{
				MessageCounts.TryGetValue(message, out var count);
				MessageCounts[message] = count + 1;
			}

			Record(sender, "WebMessageReceived");
		};
	}

	private static void Record(WebView2 browser, string eventName)
	{
		var name = browser.Name;
		EventRecorder.Record(string.IsNullOrEmpty(name) ? UnnamedElement : name, eventName);
	}
}
