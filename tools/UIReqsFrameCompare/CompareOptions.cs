namespace UIReqsFrameCompare
{
	/// <summary>
	/// The parsed command line of one comparison run.
	/// </summary>
	internal sealed class CompareOptions
	{
		/// <summary>The folder holding the baseline frames.</summary>
		public string BaselineFolder { get; set; } = "";

		/// <summary>The folder holding the current frames; diff images are written under its <c>_diff</c> folder.</summary>
		public string CurrentFolder { get; set; } = "";

		/// <summary>The maximum number of differing pixels for a frame to still count as same (default 0).</summary>
		public long Threshold { get; set; }

		/// <summary>The text report file, or null to write the report to standard output.</summary>
		public string? ReportFile { get; set; }

		/// <summary>
		/// The informational entries: a whole group (<c>&lt;Group&gt;</c>) or one feature of a group
		/// (<c>&lt;Group&gt;/&lt;feature file name without extension&gt;</c>) or ONE frame of a feature
		/// (<c>&lt;Group&gt;/&lt;feature&gt;/&lt;frame file name without .png&gt;</c>), in every orientation; each form may be
		/// prefixed with an orientation (<c>Landscape/&lt;Group&gt;</c>, <c>Portrait/&lt;Group&gt;/&lt;feature&gt;[/&lt;frame&gt;]</c>)
		/// to cover that orientation only. Frames they cover are compared and reported but never affect the exit code.
		/// </summary>
		public HashSet<string> InformationalEntries { get; } = new(StringComparer.OrdinalIgnoreCase);

		/// <summary>The orientation folders a frame path starts with (the prefix an orientation-aware entry may carry).</summary>
		public static readonly IReadOnlyList<string> Orientations = new[] { "Portrait", "Landscape" };

		/// <summary>
		/// True when <paramref name="entry"/> (already trimmed of surrounding slashes) is a valid informational
		/// entry: <c>[&lt;Orientation&gt;/]&lt;Group&gt;</c>, <c>[&lt;Orientation&gt;/]&lt;Group&gt;/&lt;feature&gt;</c> or
		/// <c>[&lt;Orientation&gt;/]&lt;Group&gt;/&lt;feature&gt;/&lt;frame&gt;</c> (the frame form: coordinator 2026-09-27 03:41,
		/// one frame whose instability has a named cause). [AP7-B TerminalView RE-GATE 2] the orientation-aware forms were
		/// added for a frame that is unstable in one orientation only (Landscape ThemeFocus/Image, the emulator's
		/// FitCenter edge filtering; FIXLIST AP2-M); an orientation alone is not an entry.
		/// </summary>
		public static bool IsValidEntry(string entry)
		{
			var parts = entry.Split('/');
			if (parts.Any(p => p.Length == 0))
			{
				return false;
			}

			var oriented = IsOrientation(parts[0]);
			return oriented ? parts.Length is >= 2 and <= 4 : parts.Length is >= 1 and <= 3;
		}

		/// <summary>
		/// True when the frame at <paramref name="relativePath"/> (<c>&lt;Orientation&gt;/&lt;Group&gt;/[&lt;feature&gt;/]&lt;file&gt;</c>)
		/// is covered by a whole-group or a feature-level informational entry, with or without the frame's orientation
		/// as a prefix.
		/// </summary>
		public bool IsInformational(string relativePath)
		{
			var parts = relativePath.Split('/');
			if (parts.Length < 3)
			{
				return false;
			}

			var orientation = parts[0];
			var group = parts[1];
			if (InformationalEntries.Contains(group) || InformationalEntries.Contains(orientation + "/" + group))
			{
				return true;
			}

			if (parts.Length < 4)
			{
				return false;
			}

			var feature = group + "/" + parts[2];
			if (InformationalEntries.Contains(feature) || InformationalEntries.Contains(orientation + "/" + feature))
			{
				return true;
			}

			// One frame: <Group>/<feature>/<frame file name without .png>, with or without the orientation.
			if (parts.Length != 4)
			{
				return false;
			}

			var file = parts[3];
			var stem = file.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ? file[..^4] : file;
			var frame = feature + "/" + stem;
			return InformationalEntries.Contains(frame) || InformationalEntries.Contains(orientation + "/" + frame);
		}

		private static bool IsOrientation(string part) =>
			Orientations.Any(o => string.Equals(o, part, StringComparison.OrdinalIgnoreCase));

		/// <summary>When set, run the tool's self-test against this baseline folder instead of a comparison.</summary>
		public string? SelfTestBaseline { get; set; }

		/// <summary>The scratch folder the self-test copies the baseline into (default: a new temp folder).</summary>
		public string? SelfTestWorkFolder { get; set; }
	}
}
