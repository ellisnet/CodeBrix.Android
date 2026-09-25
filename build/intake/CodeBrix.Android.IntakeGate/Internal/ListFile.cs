using System;
using System.Collections.Generic;
using System.IO;

namespace CodeBrix.Android.IntakeGate.Internal;

/// <summary>
/// Reads the plain-text list files used by the intake: one entry per line, blank lines
/// and lines starting with '#' ignored, surrounding white space trimmed.
/// </summary>
internal static class ListFile
{
    internal static IReadOnlyList<string> ReadLines(string path)
    {
        var lines = new List<string>();
        if (!File.Exists(path))
        {
            return lines;
        }

        foreach (var raw in File.ReadAllLines(path))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal))
            {
                continue;
            }

            lines.Add(line);
        }

        return lines;
    }
}
