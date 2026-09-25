using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Security;
using System.Text;
using CodeBrix.Platform.UI.Core.UIReqs.Hosting;
using CodeBrix.Android.UIReqs.TestTarget;
using Reqnroll;

namespace CodeBrix.Platform.UI.Core.UIReqs.Canvas;

/// <summary>
/// The opt-in review archive. When the <see cref="FolderVariable"/> environment variable names a
/// folder, every frame a scenario evaluates is written there as a PNG, in a folder named after
/// the feature file and beside a copy of that feature file, so that a person can read a
/// requirement and look at what it actually saw. When the variable is unset the harness behaves
/// exactly as it did before: nothing is read, nothing is created and nothing is written. A
/// folder that cannot be created or written to costs one line of output at the start of the run
/// and nothing else - no scenario ever fails because of the archive.
/// </summary>
/// <remarks>
/// This is not <see cref="FrameArchive"/>, which saves one PNG per FAILING scenario into the
/// test project's own TestResults folder and is always on. The two are independent.
/// </remarks>
public static class FrameReview
{
	/// <summary>The environment variable that names the folder, and by naming it turns saving on.</summary>
	public const string FolderVariable = "CODEBRIX_UIREQS_FRAME_SAVE";

	/// <summary>
	/// The assembly metadata key whose value is the folder the .feature sources live in. Both
	/// entry projects carry it, because a test assembly has no other way back to its sources.
	/// </summary>
	public const string FeatureRootMetadataKey = "CodeBrix.UIReqs.FeatureRoot";

	/// <summary>How many characters of a slugged title survive into a file name.</summary>
	public const int MaximumSlugLength = 60;

	private const string ProbeFileName = ".uireqs-frame-save-probe";
	private const string FeatureKeyword = "Feature:";
	private const string ScenarioKeyword = "Scenario:";
	private const string ScenarioOutlineKeyword = "Scenario Outline:";

	private static readonly Dictionary<string, string?> FeatureSources = new(StringComparer.Ordinal);
	private static readonly Dictionary<string, Dictionary<string, int>> ScenarioNumbers = new(StringComparer.Ordinal);
	private static readonly HashSet<string> PreparedFeatureFolders = new(StringComparer.Ordinal);
	private static readonly HashSet<string> ClearedScenarios = new(StringComparer.Ordinal);

	private static string? _featureRoot;
	private static string? _scenarioFolder;
	private static string? _scenarioPrefix;
	private static int _ordinal;

	/// <summary>The folder the environment variable named, or <c>null</c> when saving is off.</summary>
	public static string? RootDirectory { get; private set; }

	/// <summary>
	/// The folder THIS assembly writes under - the root plus the panel orientation, so the
	/// Landscape and the Portrait run never write to the same file even though they run at the
	/// same time. <c>null</c> when saving is off.
	/// </summary>
	public static string? OrientationDirectory { get; private set; }

	/// <summary>Whether frames are being saved at all.</summary>
	public static bool IsEnabled => OrientationDirectory is not null;

	/// <summary>How many PNGs this run has written.</summary>
	public static int SavedCount { get; private set; }

	/// <summary>The path <see cref="Save"/> wrote last, or <c>null</c> when it has written nothing.</summary>
	public static string? LastSavedPath { get; private set; }

	/// <summary>
	/// Reads the environment variable once, and prepares the orientation folder when it names
	/// one. Called from the test-run hook AFTER the application has launched, because the panel
	/// orientation is only known once it has. When the folder cannot be created or written to,
	/// one line says so and saving stays off for the whole run.
	/// </summary>
	public static void Initialize()
	{
		Reset();

		var configured = Environment.GetEnvironmentVariable(FolderVariable);
		if (string.IsNullOrWhiteSpace(configured))
		{
			return;
		}

		var requested = configured.Trim();
		string root;
		string orientation;

		try
		{
			root = Path.GetFullPath(requested);
			orientation = Path.Combine(root, TestTargetFixture.Orientation.ToString());
			Directory.CreateDirectory(orientation);

			// Creating a folder is not the same as being allowed to write in it, and the whole
			// point of the check is that the run finds out now rather than mid-scenario.
			var probe = Path.Combine(orientation, ProbeFileName);
			File.WriteAllText(probe, string.Empty);
			File.Delete(probe);
		}
		catch (Exception failure) when (IsFileSystemFailure(failure))
		{
			Console.Out.WriteLine(
				$"UIReqs: {FolderVariable} names \"{requested}\", which cannot be created or written to "
				+ $"({failure.GetType().Name}: {failure.Message}). Frames will not be saved this run.");
			return;
		}

		RootDirectory = root;
		OrientationDirectory = orientation;
		_featureRoot = ReadFeatureRoot();

		Console.Out.WriteLine(
			$"UIReqs: {FolderVariable} is set, so every frame a scenario evaluates is saved under {orientation}.");

		if (_featureRoot is null)
		{
			Console.Out.WriteLine(
				$"UIReqs: {TestTargetFixture.TestAssembly.GetName().Name} carries no \"{FeatureRootMetadataKey}\" "
				+ "assembly metadata, so the saved frames get no copy of the feature file they came from.");
		}
	}

	/// <summary>
	/// Names the scenario whose frames are about to be saved: works out the folder the feature
	/// writes into, puts a copy of the feature file there, and removes whatever THIS scenario
	/// left behind on an earlier run, so a re-run replaces its frames instead of piling more
	/// beside them. Does nothing at all when saving is off.
	/// </summary>
	/// <param name="featureContext">The current feature.</param>
	/// <param name="scenarioContext">The current scenario.</param>
	public static void BeginScenario(FeatureContext featureContext, ScenarioContext scenarioContext)
	{
		ArgumentNullException.ThrowIfNull(featureContext);
		ArgumentNullException.ThrowIfNull(scenarioContext);

		_scenarioFolder = null;
		_scenarioPrefix = null;
		_ordinal = 0;

		var orientation = OrientationDirectory;
		if (orientation is null)
		{
			return;
		}

		var featureInfo = featureContext.FeatureInfo;
		var scenarioTitle = scenarioContext.ScenarioInfo.Title;
		var domain = DomainOf(featureInfo.FolderPath);
		var source = FeatureSource(domain, featureInfo.Title);
		var featureFolderName = source is null
			? Slug(featureInfo.Title)
			: Path.GetFileNameWithoutExtension(source);
		var folder = Path.Combine(orientation, domain, featureFolderName);
		var prefix = string.Create(CultureInfo.InvariantCulture,
			$"Scenario{ScenarioNumber(source, scenarioTitle)}_{Slug(scenarioTitle)}");

		try
		{
			Directory.CreateDirectory(folder);
			CopyFeatureSource(folder, source);
			ClearPreviousFrames(folder, prefix);
		}
		catch (Exception failure) when (IsFileSystemFailure(failure))
		{
			return;
		}

		_scenarioFolder = folder;
		_scenarioPrefix = prefix;
	}

	/// <summary>
	/// Saves one frame the current scenario is about to evaluate. Called from the one place
	/// every such frame passes through, so "every frame a scenario looked at" is a fact rather
	/// than a promise each steps class has to keep. Frames the harness fetches for itself -
	/// the handshake at launch, before any scenario has begun - are not saved, because no
	/// scenario is current when they arrive.
	/// </summary>
	/// <param name="frame">The frame that was just retrieved.</param>
	/// <param name="label">
	/// The name the step gave the frame, or <c>null</c> when it asked for the scenario's
	/// unnamed "current" frame - in which case the capture's ordinal identifies it on its own.
	/// </param>
	/// <returns>The path written, or <c>null</c> when nothing was written.</returns>
	public static string? Save(TestFrame? frame, string? label = null)
	{
		var folder = _scenarioFolder;
		var prefix = _scenarioPrefix;
		if (frame is null || folder is null || prefix is null)
		{
			return null;
		}

		_ordinal++;

		var labelSlug = string.IsNullOrWhiteSpace(label) ? string.Empty : Slug(label);
		var name = string.Create(CultureInfo.InvariantCulture,
			$"{prefix}_{_ordinal:00}{(labelSlug.Length == 0 ? string.Empty : "_" + labelSlug)}");

		try
		{
			var path = Path.Combine(folder, name + ".png");
			var duplicate = 1;
			while (File.Exists(path))
			{
				// A scenario title that appears twice in one feature, or a file this run has
				// already written, keeps what is there and takes the next name along.
				duplicate++;
				path = Path.Combine(folder,
					string.Create(CultureInfo.InvariantCulture, $"{name}_{duplicate}.png"));
			}

			frame.SavePng(path);
			SavedCount++;
			LastSavedPath = path;
			return path;
		}
		catch (Exception failure) when (IsFileSystemFailure(failure))
		{
			return null;
		}
	}

	/// <summary>A one-line description of the archive, for a report header.</summary>
	/// <returns>Where the frames go, or that they are not being saved.</returns>
	public static string Describe() => OrientationDirectory is null
		? string.Create(CultureInfo.InvariantCulture, $"frames are not being saved ({FolderVariable} is unset)")
		: string.Create(CultureInfo.InvariantCulture, $"frames are saved under {OrientationDirectory}");

	/// <summary>
	/// The slug a title becomes in a file name: every run of characters that is not a letter or
	/// a digit becomes one underscore, the result is lower case, has no leading or trailing
	/// underscore, and is no longer than <see cref="MaximumSlugLength"/> characters.
	/// </summary>
	/// <param name="value">The title to slug.</param>
	/// <returns>The slug, which is empty when the title holds nothing usable.</returns>
	public static string Slug(string? value)
	{
		if (string.IsNullOrWhiteSpace(value))
		{
			return string.Empty;
		}

		var builder = new StringBuilder(Math.Min(value.Length, MaximumSlugLength));
		var pending = false;

		foreach (var character in value)
		{
			if (char.IsAsciiLetterOrDigit(character))
			{
				if (pending && builder.Length > 0 && builder.Length < MaximumSlugLength)
				{
					builder.Append('_');
				}

				pending = false;
				if (builder.Length >= MaximumSlugLength)
				{
					break;
				}

				builder.Append(char.ToLowerInvariant(character));
			}
			else
			{
				pending = true;
			}
		}

		while (builder.Length > 0 && builder[^1] == '_')
		{
			builder.Length--;
		}

		return builder.ToString();
	}

	private static void Reset()
	{
		RootDirectory = null;
		OrientationDirectory = null;
		SavedCount = 0;
		LastSavedPath = null;
		_featureRoot = null;
		_scenarioFolder = null;
		_scenarioPrefix = null;
		_ordinal = 0;
		FeatureSources.Clear();
		ScenarioNumbers.Clear();
		PreparedFeatureFolders.Clear();
		ClearedScenarios.Clear();
	}

	private static bool IsFileSystemFailure(Exception failure) => failure
		is IOException or UnauthorizedAccessException or SecurityException
		or ArgumentException or NotSupportedException;

	private static string? ReadFeatureRoot()
	{
		// The assembly the scenarios are running from, not the one this class is compiled into:
		// an add-in coverage group is its own executable that takes this harness from a
		// reference, and the feature files being run are ITS files.
		foreach (var metadata in TestTargetFixture.TestAssembly.GetCustomAttributes<AssemblyMetadataAttribute>())
		{
			if (!string.Equals(metadata.Key, FeatureRootMetadataKey, StringComparison.Ordinal)
				|| string.IsNullOrWhiteSpace(metadata.Value))
			{
				continue;
			}

			try
			{
				// The value is written by MSBuild and reaches here with whatever separators the
				// project file used, and with the Portrait twin's "../" still in it.
				var root = Path.GetFullPath(metadata.Value.Trim());
				return Directory.Exists(root) ? root : null;
			}
			catch (Exception failure) when (IsFileSystemFailure(failure))
			{
				return null;
			}
		}

		return null;
	}

	private static string DomainOf(string? folderPath)
	{
		if (string.IsNullOrWhiteSpace(folderPath))
		{
			return "Features";
		}

		// Reqnroll gives the feature's folder relative to the project: "Features/Items" in the
		// Landscape project, "../Platform.UI.Core/Features/Items" in the twin that links it. The
		// last segment is the domain either way.
		var segments = folderPath.Split('/', '\\', StringSplitOptions.RemoveEmptyEntries
			| StringSplitOptions.TrimEntries);
		return segments.Length == 0 ? "Features" : segments[^1];
	}

	private static string? FeatureSource(string domain, string featureTitle)
	{
		if (FeatureSources.TryGetValue(featureTitle, out var cached))
		{
			return cached;
		}

		var found = FindFeatureSource(domain, featureTitle);
		FeatureSources[featureTitle] = found;
		return found;
	}

	private static string? FindFeatureSource(string domain, string featureTitle)
	{
		var root = _featureRoot;
		if (root is null)
		{
			return null;
		}

		try
		{
			// The domain folder first, because that is where the feature must be; the whole tree
			// afterwards, so a feature that moves is still found by its title.
			var domainFolder = Path.Combine(root, domain);
			if (Directory.Exists(domainFolder))
			{
				var inDomain = MatchByTitle(domainFolder, SearchOption.TopDirectoryOnly, featureTitle);
				if (inDomain is not null)
				{
					return inDomain;
				}
			}

			return MatchByTitle(root, SearchOption.AllDirectories, featureTitle);
		}
		catch (Exception failure) when (IsFileSystemFailure(failure))
		{
			return null;
		}
	}

	private static string? MatchByTitle(string folder, SearchOption depth, string featureTitle)
	{
		foreach (var candidate in Directory.EnumerateFiles(folder, "*.feature", depth))
		{
			if (string.Equals(TitleOf(candidate), featureTitle, StringComparison.Ordinal))
			{
				return candidate;
			}
		}

		return null;
	}

	private static string? TitleOf(string featureFile)
	{
		foreach (var line in File.ReadLines(featureFile))
		{
			var trimmed = line.Trim();
			if (trimmed.StartsWith(FeatureKeyword, StringComparison.Ordinal))
			{
				return trimmed[FeatureKeyword.Length..].Trim();
			}
		}

		return null;
	}

	private static int ScenarioNumber(string? source, string scenarioTitle)
	{
		if (source is null)
		{
			return 0;
		}

		if (!ScenarioNumbers.TryGetValue(source, out var numbers))
		{
			numbers = ReadScenarioNumbers(source);
			ScenarioNumbers[source] = numbers;
		}

		return numbers.TryGetValue(scenarioTitle, out var number) ? number : 0;
	}

	private static Dictionary<string, int> ReadScenarioNumbers(string source)
	{
		var numbers = new Dictionary<string, int>(StringComparer.Ordinal);
		var position = 0;

		try
		{
			foreach (var line in File.ReadLines(source))
			{
				var trimmed = line.Trim();
				string title;

				if (trimmed.StartsWith(ScenarioOutlineKeyword, StringComparison.Ordinal))
				{
					title = trimmed[ScenarioOutlineKeyword.Length..].Trim();
				}
				else if (trimmed.StartsWith(ScenarioKeyword, StringComparison.Ordinal))
				{
					title = trimmed[ScenarioKeyword.Length..].Trim();
				}
				else
				{
					continue;
				}

				position++;

				// A title used twice keeps the first position; the file names of the second one
				// then take the "_2" that Save appends.
				if (!numbers.ContainsKey(title))
				{
					numbers[title] = position;
				}
			}
		}
		catch (Exception failure) when (IsFileSystemFailure(failure))
		{
			numbers.Clear();
		}

		return numbers;
	}

	private static void CopyFeatureSource(string folder, string? source)
	{
		if (source is null || !PreparedFeatureFolders.Add(folder))
		{
			return;
		}

		File.Copy(source, Path.Combine(folder, Path.GetFileName(source)), overwrite: true);
	}

	private static void ClearPreviousFrames(string folder, string prefix)
	{
		// Once per scenario per run: a feature that gives two scenarios the same title must not
		// have the second one delete the first one's frames.
		if (!ClearedScenarios.Add(Path.Combine(folder, prefix)))
		{
			return;
		}

		foreach (var stale in Directory.EnumerateFiles(folder, prefix + "_*.png", SearchOption.TopDirectoryOnly))
		{
			File.Delete(stale);
		}
	}
}
