namespace UIReqsFrameCompare
{
	/// <summary>
	/// Entry point: compares two folders of saved UIReqs frames.
	/// </summary>
	internal static class Program
	{
		private const string Usage = """
			Usage:
			  UIReqsFrameCompare <baseline-folder> <current-folder> [--threshold <n>] [--report <file>] [--informational <entry>]...
			  UIReqsFrameCompare --self-test <baseline-folder> [--work <folder>] --informational <Group> [--informational <entry>]...

			Compares every <Orientation>/<Group>/<feature>/Scenario<N>_<slug>_<capture>[_<name>].png frame of the
			baseline folder with the frame at the same relative path in the current folder.
			  byte-identical        -> same
			  otherwise             -> decoded and compared pixel by pixel; the differing-pixel count and bounding
			                           box are reported and a diff image (red on a faded copy) is written under
			                           <current-folder>/_diff/<same relative path>
			  --threshold <n>       a frame with at most <n> differing pixels still counts as same (default 0)
			  --report <file>       write the text report to <file> (default: standard output)
			  --informational <E>   <E> is a whole group (<Group>) or one feature of a group (<Group>/<feature file
			                        name without extension>); its frames are compared and reported but never affect
			                        the exit code, and its missing/extra frames are listed as informational (repeatable)
			  --self-test <folder>  run the tool's own self-test against a baseline folder (copied to --work)

			Exit code: 0 when every strict frame is same (within the threshold) and none is missing or extra,
			1 otherwise, 2 on a usage error.
			""";

		private static int Main(string[] args)
		{
			var options = new CompareOptions();
			var positional = new List<string>();

			for (var i = 0; i < args.Length; i++)
			{
				var arg = args[i];

				string NextValue()
				{
					if (i + 1 >= args.Length)
					{
						throw new ArgumentException($"{arg} needs a value.");
					}

					return args[++i];
				}

				try
				{
					switch (arg)
					{
						case "--threshold":
							options.Threshold = long.Parse(NextValue(), System.Globalization.CultureInfo.InvariantCulture);
							if (options.Threshold < 0)
							{
								throw new ArgumentException("--threshold must be 0 or more.");
							}
							break;
						case "--report":
							options.ReportFile = NextValue();
							break;
						case "--informational":
							var entry = NextValue().Trim().Trim('/');
							if (entry.Length == 0 || entry.Split('/').Length > 2)
							{
								throw new ArgumentException($"--informational takes <Group> or <Group>/<feature>, not '{entry}'.");
							}
							options.InformationalEntries.Add(entry);
							break;
						case "--self-test":
							options.SelfTestBaseline = NextValue();
							break;
						case "--work":
							options.SelfTestWorkFolder = NextValue();
							break;
						case "-h":
						case "--help":
							Console.WriteLine(Usage);
							return 0;
						default:
							if (arg.StartsWith("--", StringComparison.Ordinal))
							{
								throw new ArgumentException($"Unknown option {arg}.");
							}
							positional.Add(arg);
							break;
					}
				}
				catch (Exception e) when (e is ArgumentException or FormatException or OverflowException)
				{
					Console.Error.WriteLine(e.Message);
					Console.Error.WriteLine(Usage);
					return 2;
				}
			}

			if (options.SelfTestBaseline is not null)
			{
				if (positional.Count != 0 || !Directory.Exists(options.SelfTestBaseline))
				{
					Console.Error.WriteLine(Usage);
					return 2;
				}

				return SelfTest.Run(options);
			}

			if (positional.Count != 2)
			{
				Console.Error.WriteLine(Usage);
				return 2;
			}

			options.BaselineFolder = positional[0];
			options.CurrentFolder = positional[1];

			foreach (var folder in positional)
			{
				if (!Directory.Exists(folder))
				{
					Console.Error.WriteLine($"Folder not found: {folder}");
					return 2;
				}
			}

			var results = FrameComparer.Compare(options);

			if (options.ReportFile is null)
			{
				return FrameComparer.WriteReport(options, results, Console.Out);
			}

			int exitCode;
			using (var writer = new StreamWriter(options.ReportFile))
			{
				exitCode = FrameComparer.WriteReport(options, results, writer);
			}

			Console.WriteLine($"Report written to {options.ReportFile}: {(exitCode == 0 ? "PASS" : "FAIL")} (exit {exitCode})");
			return exitCode;
		}
	}
}
