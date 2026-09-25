namespace CodeBrix.Platform.UI.Core.UIReqs.Hosting;

/// <summary>
/// Loads the native library the text engine lays a string out with.
/// </summary>
/// <remarks>
/// An application head gets this from a module initializer its build generates; a project that
/// uses the framework WITHOUT being a head - this one, and the two host-free add-in suites in
/// this repository - has to make the call itself. Without it, measuring a TextBlock throws deep
/// inside the bidirectional pass, as ArgumentNullException(Parameter 'handle') out of
/// NativeLibrary.TryGetExport, and a scenario that asked "is there ink?" would be answered by an
/// exception rather than by the panel.
/// </remarks>
public static class TextEngineBootstrap
{
	private static bool _initialized;

	/// <summary>Loads the text engine once per process.</summary>
	public static void Initialize()
	{
		// ANDROID PORT: there is no Skia text engine to load on Android - text is measured and
		// drawn by native text views (the TextBlock handler) through CodeBrix.Android's platform
		// services, which the app's platform bootstrap registered before this runs.
		_initialized = true;
	}

	/// <summary>Whether Initialize ran.</summary>
	public static bool IsInitialized => _initialized;
}
