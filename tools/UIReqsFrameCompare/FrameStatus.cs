namespace UIReqsFrameCompare
{
	/// <summary>
	/// The outcome of comparing one saved UIReqs frame between the baseline and the current folder.
	/// </summary>
	internal enum FrameStatus
	{
		/// <summary>The two files are byte-identical, or decode to identical pixels.</summary>
		Same,

		/// <summary>Some pixels differ, but no more than the <c>--threshold</c> allows.</summary>
		SameWithinThreshold,

		/// <summary>More pixels differ than the <c>--threshold</c> allows.</summary>
		Different,

		/// <summary>The two frames decode to different pixel sizes.</summary>
		SizeDiffers,

		/// <summary>One of the two files could not be decoded as an image.</summary>
		Undecodable,

		/// <summary>The frame exists in the baseline folder only.</summary>
		MissingInCurrent,

		/// <summary>The frame exists in the current folder only.</summary>
		ExtraInCurrent,
	}
}
