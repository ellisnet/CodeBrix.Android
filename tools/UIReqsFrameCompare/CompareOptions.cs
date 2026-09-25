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
		/// (<c>&lt;Group&gt;/&lt;feature file name without extension&gt;</c>). Frames they cover are compared and
		/// reported but never affect the exit code.
		/// </summary>
		public HashSet<string> InformationalEntries { get; } = new(StringComparer.OrdinalIgnoreCase);

		/// <summary>
		/// True when the frame at <paramref name="relativePath"/> is covered by a whole-group or a
		/// feature-level informational entry.
		/// </summary>
		public bool IsInformational(string relativePath)
		{
			var parts = relativePath.Split('/');
			if (parts.Length < 3)
			{
				return false;
			}

			return InformationalEntries.Contains(parts[1])
				|| (parts.Length >= 4 && InformationalEntries.Contains(parts[1] + "/" + parts[2]));
		}

		/// <summary>When set, run the tool's self-test against this baseline folder instead of a comparison.</summary>
		public string? SelfTestBaseline { get; set; }

		/// <summary>The scratch folder the self-test copies the baseline into (default: a new temp folder).</summary>
		public string? SelfTestWorkFolder { get; set; }
	}
}
