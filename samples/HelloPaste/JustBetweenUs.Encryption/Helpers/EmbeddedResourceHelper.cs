/*
   Copyright 2025 Ellisnet - Jeremy Ellis (jeremy@ellisnet.com)
   Licensed under the Apache License, Version 2.0 (the "License");
   you may not use this file except in compliance with the License.
   You may obtain a copy of the License at
       http://www.apache.org/licenses/LICENSE-2.0
   Unless required by applicable law or agreed to in writing, software
   distributed under the License is distributed on an "AS IS" BASIS,
   WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
   See the License for the specific language governing permissions and
   limitations under the License.
*/

//FILE DATE/REVISION: 2025-01-18

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable UnusedMember.Global
// ReSharper disable CheckNamespace

public enum LineEndingFixType
{
    /// <summary>
    /// Make no changes to the line endings
    /// </summary>
    NoChange = 0,

    /// <summary>
    /// Replace all line-endings with carriage return (CR) + line feed (LF) - default for Windows
    /// </summary>
    CrLf,

    /// <summary>
    /// Replace all line-endings with line feed (LF) only (no carriage return) - default for Unix-based systems
    /// </summary>
    Lf,

    /// <summary>
    /// Replace all line-endings with CR+LF on Windows, and LF (only) on Unix-based systems - i.e. to match the OS default behavior
    /// </summary>
    OsDefault,

    /// <summary>
    /// Remove all end-of-line characters
    /// </summary>
    RemoveLineEndings,

    /// <summary>
    /// Remove all end-of-line characters, remove empty lines, and remove whitespace from beginning and end of each line
    /// </summary>
    RemoveLineEndingsAndWhiteSpace
}

public static class EmbeddedResourceHelper
{
    public static IList<string> FindResourceFileNames(
        string rootNamespace,
        string folderPath = null,
        string nameStartsWith = null,
        string extension = null,
        Assembly assembly = null)
    {
        assembly ??= Assembly.GetExecutingAssembly();

        IList<string> result = Array.Empty<string>();

        if (!string.IsNullOrWhiteSpace(rootNamespace))
        {
            var beginsWith = rootNamespace.Trim();

            if (!string.IsNullOrWhiteSpace(folderPath))
            {
                beginsWith += "." + folderPath
                    .Replace("/", ".")
                    .Replace("\\", ".")
                    .Trim();
            }

            if (!string.IsNullOrWhiteSpace(nameStartsWith))
            {
                beginsWith += ((beginsWith.EndsWith('.'))
                                  ? string.Empty
                                  : ".")
                              + nameStartsWith.Trim();
            }

            extension = (string.IsNullOrWhiteSpace(extension))
                ? null
                : extension.Trim().ToLowerInvariant();

            if (extension != null && (!extension.StartsWith('.')))
            {
                extension = $".{extension}";
            }

            var found = new List<string>();
            foreach (var file in assembly.GetManifestResourceNames())
            {
                if (file.StartsWith(beginsWith)
                    && (extension == null || file.ToLowerInvariant().EndsWith(extension)))
                {
                    found.Add(file);
                }
            }

            if (found.Count > 0) { result = found; }
        }

        return result;
    }

    public static async Task<string> GetResourceAsString(string exactResourceName,
        Assembly assembly = null,
        LineEndingFixType fixType = LineEndingFixType.NoChange,
        bool trimResult = true)
    {
        if (string.IsNullOrWhiteSpace(exactResourceName))
        {
            throw new ArgumentException("Value cannot be null or blank.", nameof(exactResourceName));
        }

        assembly ??= Assembly.GetExecutingAssembly();

        await using var stream = assembly.GetManifestResourceStream(exactResourceName.Trim());
        if (stream == null)
        {
            throw new ArgumentException($"The embedded resource '{exactResourceName}' does not appear to exist.",
                nameof(exactResourceName));
        }
        using var reader = new StreamReader(stream);
        var text = FixLineEndings((await reader.ReadToEndAsync()), fixType);

        return (trimResult) ? text?.Trim() : text;
    }

    public static Task<string> GetResourceAsString(string embeddedResourcePath,
        string rootNamespace,
        Assembly assembly = null,
        LineEndingFixType fixType = LineEndingFixType.NoChange,
        bool trimResult = true)
    {
        if (string.IsNullOrWhiteSpace(embeddedResourcePath))
        {
            throw new ArgumentException("Value cannot be null or blank.", nameof(embeddedResourcePath));
        }

        var path = ((string.IsNullOrWhiteSpace(rootNamespace))
                       ? string.Empty
                       : ($"{rootNamespace.Trim()}."))
                   + embeddedResourcePath
                       .Replace("/", ".")
                       .Replace("\\", ".")
                       .Trim();

        return GetResourceAsString(path, assembly, fixType, trimResult);
    }

    public static async Task<byte[]> GetResourceAsBytes(string exactResourceName,
        Assembly assembly = null)
    {
        if (string.IsNullOrWhiteSpace(exactResourceName))
        {
            throw new ArgumentException("Value cannot be null or blank.", nameof(exactResourceName));
        }

        byte[] result;
        assembly ??= Assembly.GetExecutingAssembly();

        await using var stream = assembly.GetManifestResourceStream(exactResourceName.Trim());
        if (stream == null)
        {
            throw new ArgumentException($"The embedded resource '{exactResourceName}' does not appear to exist.",
                nameof(exactResourceName));
        }

        // ReSharper disable once ConvertToUsingDeclaration
        using (var br = new BinaryReader(stream))
        {
            result = br.ReadBytes((int)stream.Length);
        }

        return result;

    }

    public static Task<byte[]> GetResourceAsBytes(string embeddedResourcePath,
        string rootNamespace,
        Assembly assembly = null)
    {
        if (string.IsNullOrWhiteSpace(embeddedResourcePath))
        {
            throw new ArgumentException("Value cannot be null or blank.", nameof(embeddedResourcePath));
        }

        var path = ((string.IsNullOrWhiteSpace(rootNamespace))
                       ? string.Empty
                       : ($"{rootNamespace.Trim()}."))
                   + embeddedResourcePath
                       .Replace("/", ".")
                       .Replace("\\", ".")
                       .Trim();

        return GetResourceAsBytes(path, assembly);
    }

    public static string FixLineEndings(string text, LineEndingFixType fixType)
    {
        var result = text;

        if (fixType != LineEndingFixType.NoChange && (!string.IsNullOrWhiteSpace(text)))
        {
            if (fixType == LineEndingFixType.OsDefault && RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                fixType = LineEndingFixType.CrLf;
            }
            else if (fixType == LineEndingFixType.OsDefault)
            {
                fixType = LineEndingFixType.Lf;
            }

            switch (fixType)
            {
                // ReSharper disable UnreachableSwitchCaseDueToIntegerAnalysis

                case LineEndingFixType.NoChange:  //Should not be possible, see code above
                case LineEndingFixType.OsDefault:  //Should not be possible, see code directly above
                    break;

                // ReSharper restore UnreachableSwitchCaseDueToIntegerAnalysis

                case LineEndingFixType.CrLf:
                    result = text.Replace("\r\n", "\n").Replace("\n", "\r\n");
                    break;

                case LineEndingFixType.Lf:
                    result = text.Replace("\r\n", "\n");
                    break;

                case LineEndingFixType.RemoveLineEndings:
                    result = text.Replace("\r\n", "").Replace("\n", "");
                    break;

                case LineEndingFixType.RemoveLineEndingsAndWhiteSpace:
                    var lines = text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
                    var sb = new StringBuilder();

                    foreach (var line in lines.Where(w => !string.IsNullOrWhiteSpace(w)))
                    {
                        sb.Append(line.Trim());
                    }

                    result = sb.ToString();
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(fixType), fixType, "The specified FixType is not yet implemented.");
            }
        }

        return result;
    }
}
