using System.Runtime.InteropServices;
using System.Text;
using CodeBrix.Android.UIReqs.Protocol;

namespace UIReqsFrameCompare
{
	/// <summary>
	/// Compares every saved UIReqs frame (<c>*.png</c>) of a baseline folder with the frame at the same
	/// relative path in a current folder, and writes a diff image for every frame whose pixels differ.
	/// </summary>
	internal static class FrameComparer
	{
		/// <summary>The folder (under the current folder) that receives the diff images; never scanned.</summary>
		public const string DiffFolderName = "_diff";

		/// <summary>
		/// Runs one comparison and returns the per-frame results, sorted by relative path.
		/// </summary>
		public static List<FrameResult> Compare(CompareOptions options)
		{
			var baseline = ListFrames(options.BaselineFolder);
			var current = ListFrames(options.CurrentFolder);

			var diffRoot = Path.Combine(options.CurrentFolder, DiffFolderName);
			if (Directory.Exists(diffRoot))
			{
				// Stale diffs from an earlier run would otherwise be mistaken for this run's.
				Directory.Delete(diffRoot, recursive: true);
			}

			var results = new List<FrameResult>();

			foreach (var relativePath in baseline.Union(current, StringComparer.Ordinal).OrderBy(p => p, StringComparer.Ordinal))
			{
				var group = GetGroup(relativePath);
				var result = new FrameResult(relativePath, group, options.IsInformational(relativePath), FrameStatus.Same);

				if (!current.Contains(relativePath))
				{
					result.Status = FrameStatus.MissingInCurrent;
				}
				else if (!baseline.Contains(relativePath))
				{
					result.Status = FrameStatus.ExtraInCurrent;
				}
				else
				{
					CompareFrame(
						Path.Combine(options.BaselineFolder, relativePath),
						Path.Combine(options.CurrentFolder, relativePath),
						Path.Combine(diffRoot, relativePath),
						options.Threshold,
						result);
				}

				results.Add(result);
			}

			return results;
		}

		/// <summary>
		/// Writes the text report for <paramref name="results"/> and returns the exit code
		/// (0 when no strict frame failed, 1 otherwise).
		/// </summary>
		public static int WriteReport(CompareOptions options, List<FrameResult> results, TextWriter writer)
		{
			var failures = results.Count(r => r.IsFailure);

			writer.WriteLine("UIReqs frame compare");
			writer.WriteLine($"  baseline      : {Path.GetFullPath(options.BaselineFolder)}");
			writer.WriteLine($"  current       : {Path.GetFullPath(options.CurrentFolder)}");
			writer.WriteLine($"  threshold     : {options.Threshold} differing pixel(s)");
			writer.WriteLine($"  informational : {(options.InformationalEntries.Count == 0 ? "(none)" : string.Join(", ", options.InformationalEntries.OrderBy(g => g, StringComparer.Ordinal)))}");
			writer.WriteLine();

			var notSame = results.Where(r => r.Status != FrameStatus.Same).ToList();

			WriteSection(writer, "STRICT DIFFERENCES (these fail the run)", notSame.Where(r => r.IsFailure));
			WriteSection(writer, "WITHIN THRESHOLD (strict groups, not failing)", notSame.Where(r => !r.IsInformational && r.Status == FrameStatus.SameWithinThreshold));
			WriteSection(writer, "INFORMATIONAL (never failing)", notSame.Where(r => r.IsInformational));

			writer.WriteLine("SUMMARY BY GROUP (orientation/group: frames, same, not same)");
			foreach (var byGroup in results
				.GroupBy(r => GetOrientationAndGroup(r.RelativePath))
				.OrderBy(g => g.Key, StringComparer.Ordinal))
			{
				var informationalCount = byGroup.Count(r => r.IsInformational);
				var informational = informationalCount == 0 ? ""
					: informationalCount == byGroup.Count() ? "  [informational]"
					: $"  [{informationalCount} informational]";
				var groupNotSame = byGroup.Count(r => r.Status != FrameStatus.Same);
				writer.WriteLine($"  {byGroup.Key,-34} {byGroup.Count(),5} {byGroup.Count() - groupNotSame,5} {groupNotSame,5}{informational}");
			}

			writer.WriteLine();
			writer.WriteLine("TOTALS");
			foreach (FrameStatus status in Enum.GetValues<FrameStatus>())
			{
				var strict = results.Count(r => r.Status == status && !r.IsInformational);
				var informational = results.Count(r => r.Status == status && r.IsInformational);
				writer.WriteLine($"  {status,-20} strict={strict,5}  informational={informational,5}");
			}

			writer.WriteLine($"  frames compared: {results.Count}, strict failures: {failures}");
			writer.WriteLine();
			writer.WriteLine(failures == 0 ? "RESULT: PASS (exit 0)" : "RESULT: FAIL (exit 1)");

			return failures == 0 ? 0 : 1;
		}

		/// <summary>
		/// Decodes a PNG into straight-alpha RGBA8888, or returns null when it cannot be decoded.
		/// ANDROID PORT: the managed PNG codec of the UIReqs port (no SkiaSharp).
		/// </summary>
		public static FrameImage? Decode(string path)
		{
			try
			{
				var (rgba, width, height) = PngCodec.Decode(File.ReadAllBytes(path));
				return new FrameImage(rgba, width, height);
			}
			catch (Exception exception) when (exception is InvalidDataException or NotSupportedException or IOException or IndexOutOfRangeException)
			{
				return null;
			}
		}

		/// <summary>
		/// Encodes <paramref name="image"/> as a PNG file at <paramref name="path"/>, creating its folder.
		/// </summary>
		public static void SavePng(FrameImage image, string path)
		{
			Directory.CreateDirectory(Path.GetDirectoryName(path)!);
			File.WriteAllBytes(path, PngCodec.Encode(image.Rgba, image.Width, image.Height));
		}

		private static void CompareFrame(string baselinePath, string currentPath, string diffPath, long threshold, FrameResult result)
		{
			if (FilesAreIdentical(baselinePath, currentPath))
			{
				result.Status = FrameStatus.Same;
				return;
			}

			using var baseline = Decode(baselinePath);
			using var current = Decode(currentPath);

			if (baseline is null || current is null)
			{
				result.Status = FrameStatus.Undecodable;
				result.Detail = baseline is null ? "baseline frame could not be decoded" : "current frame could not be decoded";
				return;
			}

			if (baseline.Width != current.Width || baseline.Height != current.Height)
			{
				result.Status = FrameStatus.SizeDiffers;
				result.Detail = $"baseline {baseline.Width}x{baseline.Height}, current {current.Width}x{current.Height}";
				return;
			}

			var width = current.Width;
			var basePixels = MemoryMarshal.Cast<byte, uint>(baseline.GetPixelSpan());
			var currentPixels = MemoryMarshal.Cast<byte, uint>(current.GetPixelSpan());

			using var diff = new FrameImage(new byte[width * current.Height * 4], width, current.Height);
			var diffBytes = diff.GetPixelSpan();
			var currentBytes = current.GetPixelSpan();

			long count = 0;
			int left = int.MaxValue, top = int.MaxValue, right = -1, bottom = -1;

			for (var i = 0; i < currentPixels.Length; i++)
			{
				var o = i * 4;

				if (basePixels[i] != currentPixels[i])
				{
					count++;
					var x = i % width;
					var y = i / width;
					left = Math.Min(left, x);
					right = Math.Max(right, x);
					top = Math.Min(top, y);
					bottom = Math.Max(bottom, y);

					diffBytes[o] = 255;
					diffBytes[o + 1] = 0;
					diffBytes[o + 2] = 0;
					diffBytes[o + 3] = 255;
				}
				else
				{
					// Faded grey copy of the current frame: keeps the layout readable, lets red stand out.
					var luminance = (currentBytes[o] * 299 + currentBytes[o + 1] * 587 + currentBytes[o + 2] * 114) / 1000;
					var faded = (byte)(255 - ((255 - luminance) * 3 / 10));
					diffBytes[o] = faded;
					diffBytes[o + 1] = faded;
					diffBytes[o + 2] = faded;
					diffBytes[o + 3] = 255;
				}
			}

			result.DifferingPixels = count;

			if (count == 0)
			{
				// Different bytes (e.g. PNG metadata or compression), identical pixels.
				result.Status = FrameStatus.Same;
				return;
			}

			result.BoundingBox = (left, top, right, bottom);
			result.Status = count <= threshold ? FrameStatus.SameWithinThreshold : FrameStatus.Different;

			SavePng(diff, diffPath);
			result.DiffImagePath = diffPath;
		}

		private static bool FilesAreIdentical(string a, string b)
		{
			var infoA = new FileInfo(a);
			var infoB = new FileInfo(b);
			if (infoA.Length != infoB.Length)
			{
				return false;
			}

			return File.ReadAllBytes(a).AsSpan().SequenceEqual(File.ReadAllBytes(b));
		}

		private static HashSet<string> ListFrames(string root)
		{
			var frames = new HashSet<string>(StringComparer.Ordinal);

			foreach (var file in Directory.EnumerateFiles(root, "*.png", SearchOption.AllDirectories))
			{
				var relative = Path.GetRelativePath(root, file).Replace('\\', '/');
				if (relative.Split('/').Contains(DiffFolderName, StringComparer.Ordinal))
				{
					continue;
				}

				frames.Add(relative);
			}

			return frames;
		}

		/// <summary>
		/// Returns the frame's group: the second path segment (<c>&lt;Orientation&gt;/&lt;Group&gt;/...</c>),
		/// or an empty string for a frame that does not sit in that layout.
		/// </summary>
		public static string GetGroup(string relativePath)
		{
			var parts = relativePath.Split('/');
			return parts.Length >= 3 ? parts[1] : "";
		}

		private static string GetOrientationAndGroup(string relativePath)
		{
			var parts = relativePath.Split('/');
			return parts.Length >= 3 ? parts[0] + "/" + parts[1] : "(outside the <Orientation>/<Group> layout)";
		}

		private static void WriteSection(TextWriter writer, string title, IEnumerable<FrameResult> results)
		{
			var list = results.ToList();
			writer.WriteLine($"{title}: {list.Count}");

			foreach (var r in list)
			{
				var line = new StringBuilder();
				line.Append("  ").Append(StatusText(r.Status)).Append("  ").Append(r.RelativePath);

				if (r.Status is FrameStatus.Different or FrameStatus.SameWithinThreshold)
				{
					var box = r.BoundingBox!.Value;
					line.Append($"  pixels={r.DifferingPixels}  box=({box.Left},{box.Top})-({box.Right},{box.Bottom}) {box.Right - box.Left + 1}x{box.Bottom - box.Top + 1}");
				}

				if (r.Detail is not null)
				{
					line.Append("  (").Append(r.Detail).Append(')');
				}

				writer.WriteLine(line.ToString());

				if (r.DiffImagePath is not null)
				{
					writer.WriteLine($"      diff: {r.DiffImagePath}");
				}
			}

			writer.WriteLine();
		}

		private static string StatusText(FrameStatus status) => status switch
		{
			FrameStatus.Same => "same     ",
			FrameStatus.SameWithinThreshold => "within   ",
			FrameStatus.Different => "DIFFERENT",
			FrameStatus.SizeDiffers => "SIZE     ",
			FrameStatus.Undecodable => "UNREADABLE",
			FrameStatus.MissingInCurrent => "MISSING  ",
			FrameStatus.ExtraInCurrent => "EXTRA    ",
			_ => status.ToString(),
		};
	}
}
