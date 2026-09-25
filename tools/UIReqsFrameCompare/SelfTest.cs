
namespace UIReqsFrameCompare
{
	/// <summary>
	/// The tool's self-test: proves, against a real baseline folder, that identical folders pass, that a
	/// deliberately altered frame is found (failing in a strict group, reported but not failing in an
	/// informational group), that a missing frame is found, and that a feature-level informational entry
	/// (<c>&lt;Group&gt;/&lt;feature&gt;</c>) covers that one feature and nothing else of its group.
	/// </summary>
	internal static class SelfTest
	{
		private const int RectLeft = 10;
		private const int RectTop = 10;
		private const int RectWidth = 40;
		private const int RectHeight = 20;

		/// <summary>Runs every self-test step and returns 0 when all of them pass, 1 otherwise.</summary>
		public static int Run(CompareOptions options)
		{
			var baseline = Path.GetFullPath(options.SelfTestBaseline!);
			var work = Path.GetFullPath(options.SelfTestWorkFolder ?? Path.Combine(Path.GetTempPath(), "uireqs-frame-compare-selftest-" + Guid.NewGuid().ToString("N")));
			var current = Path.Combine(work, "current");

			if (!options.InformationalEntries.Any(e => !e.Contains('/', StringComparison.Ordinal)))
			{
				Console.Error.WriteLine("The self-test needs at least one whole-group --informational entry that exists in the baseline.");
				return 2;
			}

			Console.WriteLine($"Self-test baseline : {baseline}");
			Console.WriteLine($"Self-test work     : {work}");
			Console.WriteLine($"Informational      : {string.Join(", ", options.InformationalEntries.OrderBy(g => g, StringComparer.Ordinal))}");

			if (Directory.Exists(current))
			{
				Directory.Delete(current, recursive: true);
			}

			var failed = 0;

			// Step 1: the baseline against itself.
			failed += Check(options, "1 baseline vs baseline", baseline, baseline, work, expectedExit: 0, results =>
				results.All(r => r.Status == FrameStatus.Same)
					? null
					: $"{results.Count(r => r.Status != FrameStatus.Same)} frame(s) not same");

			CopyFolder(baseline, current);

			var frames = Directory.EnumerateFiles(baseline, "*.png", SearchOption.AllDirectories)
				.Select(f => Path.GetRelativePath(baseline, f).Replace('\\', '/'))
				.OrderBy(f => f, StringComparer.Ordinal)
				.ToList();

			var strictFrame = frames.FirstOrDefault(f => FrameComparer.GetGroup(f).Length > 0 && !options.IsInformational(f));
			var informationalFrame = frames.FirstOrDefault(f => options.InformationalEntries.Contains(FrameComparer.GetGroup(f)));

			if (strictFrame is null || informationalFrame is null)
			{
				Console.Error.WriteLine("The baseline must hold at least one strict and one informational frame.");
				return 2;
			}

			// Step 2: one altered frame in a strict group -> exit 1, exactly that frame reported.
			var expectedPixels = AlterFrame(Path.Combine(current, strictFrame));
			failed += Check(options, "2 altered frame, strict group", baseline, current, work, expectedExit: 1, results =>
				ExpectOnly(results, strictFrame, FrameStatus.Different, isInformational: false, expectedPixels));
			Restore(baseline, current, strictFrame);

			// Step 3: the same alteration in an informational group -> exit 0, reported as informational.
			expectedPixels = AlterFrame(Path.Combine(current, informationalFrame));
			failed += Check(options, "3 altered frame, informational group", baseline, current, work, expectedExit: 0, results =>
				ExpectOnly(results, informationalFrame, FrameStatus.Different, isInformational: true, expectedPixels));
			Restore(baseline, current, informationalFrame);

			// Step 4: a frame missing from a strict group -> exit 1, reported missing.
			File.Delete(Path.Combine(current, strictFrame));
			failed += Check(options, "4 missing frame, strict group", baseline, current, work, expectedExit: 1, results =>
				ExpectOnly(results, strictFrame, FrameStatus.MissingInCurrent, isInformational: false, expectedPixels: null));
			Restore(baseline, current, strictFrame);

			// Step 5: a frame missing from an informational group -> exit 0, reported as informational.
			File.Delete(Path.Combine(current, informationalFrame));
			failed += Check(options, "5 missing frame, informational group", baseline, current, work, expectedExit: 0, results =>
				ExpectOnly(results, informationalFrame, FrameStatus.MissingInCurrent, isInformational: true, expectedPixels: null));
			Restore(baseline, current, informationalFrame);

			// Steps 6 and 7: a feature-level entry <Group>/<feature> covers that feature only. Pick a strict
			// group with at least two strict features; list the first one at feature level.
			var byFeature = frames
				.Where(f => f.Split('/').Length >= 4 && !options.IsInformational(f))
				.GroupBy(f => string.Join('/', f.Split('/').Take(3)), StringComparer.Ordinal)
				.ToList();
			var pair = byFeature
				.GroupBy(g => g.Key.Split('/')[0] + "/" + g.Key.Split('/')[1], StringComparer.Ordinal)
				.FirstOrDefault(g => g.Count() >= 2);

			if (pair is null)
			{
				Console.Error.WriteLine("The baseline must hold a strict group with at least two strict features.");
				return 2;
			}

			var coveredFeature = pair.ElementAt(0);
			var siblingFeature = pair.ElementAt(1);
			var featureEntry = coveredFeature.Key.Split('/')[1] + "/" + coveredFeature.Key.Split('/')[2];
			var coveredFrame = coveredFeature.First();
			var siblingFrame = siblingFeature.First();
			Console.WriteLine($"Feature-level entry: {featureEntry} (sibling feature {siblingFeature.Key.Split('/')[2]} stays strict)");

			expectedPixels = AlterFrame(Path.Combine(current, coveredFrame));
			failed += Check(options, "6 altered frame, feature-level entry", baseline, current, work, expectedExit: 0, results =>
				ExpectOnly(results, coveredFrame, FrameStatus.Different, isInformational: true, expectedPixels), featureEntry);
			Restore(baseline, current, coveredFrame);

			expectedPixels = AlterFrame(Path.Combine(current, siblingFrame));
			failed += Check(options, "7 altered frame, sibling of a feature-level entry", baseline, current, work, expectedExit: 1, results =>
				ExpectOnly(results, siblingFrame, FrameStatus.Different, isInformational: false, expectedPixels), featureEntry);
			Restore(baseline, current, siblingFrame);

			if (failed == 0)
			{
				Directory.Delete(current, recursive: true);
			}

			Console.WriteLine(failed == 0 ? "SELF-TEST: PASS (7/7 steps)" : $"SELF-TEST: FAIL ({failed} step(s) failed; work folder kept)");
			return failed == 0 ? 0 : 1;
		}

		private static int Check(CompareOptions template, string name, string baseline, string current, string work, int expectedExit, Func<List<FrameResult>, string?> verify, string? extraEntry = null)
		{
			var options = new CompareOptions
			{
				BaselineFolder = baseline,
				CurrentFolder = current,
				Threshold = template.Threshold,
			};
			options.InformationalEntries.UnionWith(template.InformationalEntries);
			if (extraEntry is not null)
			{
				options.InformationalEntries.Add(extraEntry);
			}

			var results = FrameComparer.Compare(options);

			Directory.CreateDirectory(work);
			var reportFile = Path.Combine(work, "selftest-step" + name.Split(' ')[0] + ".txt");
			int exit;
			using (var writer = new StreamWriter(reportFile))
			{
				exit = FrameComparer.WriteReport(options, results, writer);
			}

			var problem = exit != expectedExit ? $"exit {exit}, expected {expectedExit}" : verify(results);
			var notSame = results.Where(r => r.Status != FrameStatus.Same).ToList();
			var reported = notSame.Count == 0
				? "nothing reported"
				: string.Join("; ", notSame.Select(r => $"{r.Status}{(r.IsInformational ? " [informational]" : "")} {r.RelativePath}{(r.Status == FrameStatus.Different ? $" pixels={r.DifferingPixels} box=({r.BoundingBox!.Value.Left},{r.BoundingBox.Value.Top})-({r.BoundingBox.Value.Right},{r.BoundingBox.Value.Bottom})" : "")}"));

			Console.WriteLine($"  step {name}: {(problem is null ? "PASS" : "FAIL: " + problem)} (exit {exit}; {results.Count} frames; {reported})");
			Console.WriteLine($"      report: {reportFile}");
			return problem is null ? 0 : 1;
		}

		private static string? ExpectOnly(List<FrameResult> results, string frame, FrameStatus status, bool isInformational, long? expectedPixels)
		{
			var notSame = results.Where(r => r.Status != FrameStatus.Same).ToList();
			if (notSame.Count != 1)
			{
				return $"{notSame.Count} frame(s) reported, expected exactly 1";
			}

			var r = notSame[0];
			if (r.RelativePath != frame || r.Status != status || r.IsInformational != isInformational)
			{
				return $"reported {r.Status} {r.RelativePath} (informational={r.IsInformational}), expected {status} {frame} (informational={isInformational})";
			}

			if (expectedPixels is not null && r.DifferingPixels != expectedPixels)
			{
				return $"{r.DifferingPixels} differing pixels, expected {expectedPixels}";
			}

			if (status == FrameStatus.Different && r.DiffImagePath is null)
			{
				return "no diff image was written";
			}

			return null;
		}

		/// <summary>
		/// Draws an inverted rectangle into the frame and re-encodes it with the managed PNG codec. Inverting every
		/// channel changes every pixel in the rectangle, so the expected count is exactly its area.
		/// </summary>
		private static long AlterFrame(string path)
		{
			using var bitmap = FrameComparer.Decode(path) ?? throw new InvalidOperationException($"Cannot decode {path}");

			var right = Math.Min(bitmap.Width, RectLeft + RectWidth);
			var bottom = Math.Min(bitmap.Height, RectTop + RectHeight);
			var bytes = bitmap.GetPixelSpan();
			long changed = 0;

			for (var y = RectTop; y < bottom; y++)
			{
				for (var x = RectLeft; x < right; x++)
				{
					var o = (y * bitmap.Width + x) * 4;
					bytes[o] = (byte)(255 - bytes[o]);
					bytes[o + 1] = (byte)(255 - bytes[o + 1]);
					bytes[o + 2] = (byte)(255 - bytes[o + 2]);
					changed++;
				}
			}

			bitmap.NotifyPixelsChanged();
			FrameComparer.SavePng(bitmap, path);
			return changed;
		}

		private static void Restore(string baseline, string current, string frame)
			=> File.Copy(Path.Combine(baseline, frame), Path.Combine(current, frame), overwrite: true);

		private static void CopyFolder(string source, string destination)
		{
			foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
			{
				var relative = Path.GetRelativePath(source, file);
				if (relative.Replace('\\', '/').Split('/').Contains(FrameComparer.DiffFolderName, StringComparer.Ordinal))
				{
					continue;
				}

				var target = Path.Combine(destination, relative);
				Directory.CreateDirectory(Path.GetDirectoryName(target)!);
				File.Copy(file, target, overwrite: true);
			}
		}
	}
}
