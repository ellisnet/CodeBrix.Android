namespace UIReqsFrameCompare
{
	/// <summary>
	/// The comparison result for one frame, identified by its path relative to the compared folders
	/// (<c>&lt;Orientation&gt;/&lt;Group&gt;/&lt;feature&gt;/Scenario&lt;N&gt;_....png</c>).
	/// </summary>
	internal sealed class FrameResult
	{
		/// <summary>Creates a result for the frame at <paramref name="relativePath"/>.</summary>
		public FrameResult(string relativePath, string group, bool isInformational, FrameStatus status)
		{
			RelativePath = relativePath;
			Group = group;
			IsInformational = isInformational;
			Status = status;
		}

		/// <summary>The frame's path relative to the compared folders, with '/' separators.</summary>
		public string RelativePath { get; }

		/// <summary>The frame's group: the folder directly under the orientation folder (e.g. <c>Buttons</c>).</summary>
		public string Group { get; }

		/// <summary>True when the frame's group was named with <c>--informational</c>: reported, never failing.</summary>
		public bool IsInformational { get; }

		/// <summary>The outcome of the comparison.</summary>
		public FrameStatus Status { get; set; }

		/// <summary>The number of differing pixels (only meaningful when both frames decoded to the same size).</summary>
		public long DifferingPixels { get; set; }

		/// <summary>The smallest rectangle holding every differing pixel, as (left, top, right, bottom) inclusive.</summary>
		public (int Left, int Top, int Right, int Bottom)? BoundingBox { get; set; }

		/// <summary>A free-text detail (sizes, decode failures), or null.</summary>
		public string? Detail { get; set; }

		/// <summary>The diff image written for this frame, or null when none was written.</summary>
		public string? DiffImagePath { get; set; }

		/// <summary>True when this result counts as a failure (a strict group, and not same).</summary>
		public bool IsFailure => !IsInformational && Status is not (FrameStatus.Same or FrameStatus.SameWithinThreshold);
	}
}
