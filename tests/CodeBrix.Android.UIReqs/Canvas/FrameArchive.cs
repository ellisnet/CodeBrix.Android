using System;
using System.Globalization;
using System.IO;
using CodeBrix.Android.UIReqs.TestTarget;

namespace CodeBrix.Platform.UI.Core.UIReqs.Canvas;

/// <summary>
/// Where a frame goes when someone will want to look at it: one PNG per scenario, under
/// <c>TestResults/UIReqs/&lt;feature&gt;/&lt;scenario&gt;.png</c> in the test project's own
/// folder, so a failure is a file a person can open rather than a paragraph of numbers.
/// </summary>
public static class FrameArchive
{
	private const string ArchiveFolderName = "TestResults";
	private const string ArchiveSubfolderName = "UIReqs";

	private static string? _featureFolder;
	private static string? _scenarioFile;

	/// <summary>The folder every PNG of this run goes under.</summary>
	public static string RootDirectory { get; } =
		Path.Combine(FindProjectDirectory(), ArchiveFolderName, ArchiveSubfolderName);

	/// <summary>The path the current scenario's PNG has, whether or not it has been written.</summary>
	/// <exception cref="InvalidOperationException">No scenario has been started.</exception>
	public static string CurrentScenarioPath
	{
		get
		{
			if (_featureFolder is null || _scenarioFile is null)
			{
				throw new InvalidOperationException(
					"No scenario is current. ScenarioHooks calls BeginScenario before the first step.");
			}

			return Path.Combine(RootDirectory, _featureFolder, _scenarioFile + ".png");
		}
	}

	/// <summary>The last path <see cref="Save"/> wrote, or <c>null</c> when nothing has been saved.</summary>
	public static string? LastSavedPath { get; private set; }

	/// <summary>Names the scenario whose frames are about to be archived.</summary>
	/// <param name="featureTitle">The feature's Gherkin title.</param>
	/// <param name="scenarioTitle">The scenario's Gherkin title.</param>
	public static void BeginScenario(string featureTitle, string scenarioTitle)
	{
		_featureFolder = Sanitize(featureTitle);
		_scenarioFile = Sanitize(scenarioTitle);
		LastSavedPath = null;
	}

	/// <summary>Writes a frame to the current scenario's PNG path, replacing any earlier write.</summary>
	/// <param name="frame">The frame to save.</param>
	/// <returns>The path written.</returns>
	public static string Save(TestFrame frame)
	{
		ArgumentNullException.ThrowIfNull(frame);

		var path = CurrentScenarioPath;
		frame.SavePng(path);
		LastSavedPath = path;
		return path;
	}

	/// <summary>Writes a frame and never throws - a failure report must not fail.</summary>
	/// <param name="frame">The frame to save.</param>
	/// <returns>The path written, or <c>null</c> when it could not be written.</returns>
	public static string? TrySave(TestFrame? frame)
	{
		if (frame is null)
		{
			return null;
		}

		try
		{
			return Save(frame);
		}
		catch (IOException)
		{
			return null;
		}
		catch (UnauthorizedAccessException)
		{
			return null;
		}
		catch (InvalidOperationException)
		{
			return null;
		}
	}

	private static string Sanitize(string value)
	{
		if (string.IsNullOrWhiteSpace(value))
		{
			return "unnamed";
		}

		var invalid = Path.GetInvalidFileNameChars();
		var characters = value.Trim().ToCharArray();
		for (var i = 0; i < characters.Length; i++)
		{
			if (characters[i] == ' ' || Array.IndexOf(invalid, characters[i]) >= 0)
			{
				characters[i] = '_';
			}
		}

		return new string(characters);
	}

	private static string FindProjectDirectory()
	{
		// The Landscape and the Portrait entry project each want their own archive, and each
		// one's output folder sits below its own project folder.
		var directory = new DirectoryInfo(AppContext.BaseDirectory);
		while (directory is not null)
		{
			if (directory.GetFiles("*.csproj", SearchOption.TopDirectoryOnly).Length > 0)
			{
				return directory.FullName;
			}

			directory = directory.Parent;
		}

		return AppContext.BaseDirectory;
	}

	/// <summary>A one-line description of where the archive lives, for a report header.</summary>
	/// <returns>The description.</returns>
	public static string Describe() => string.Create(CultureInfo.InvariantCulture,
		$"frames are archived under {RootDirectory}");
}
