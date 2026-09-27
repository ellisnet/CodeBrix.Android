using System;
using System.Collections.Generic;
using System.IO;

namespace CodeBrix.Android.ParityScore.Scanning;

/// <summary>One line of the explained list: a DependencyProperty a handler does not map on purpose, and why.</summary>
/// <param name="DeclaringType">Full name of the type declaring the property (or <c>*</c> for any type).</param>
/// <param name="Property">The property member name (<c>WidthProperty</c>).</param>
/// <param name="Category">The explanation category (e.g. <c>core-layout</c>).</param>
/// <param name="Reason">One sentence.</param>
internal sealed record Explanation(string DeclaringType, string Property, string Category, string Reason);

/// <summary>
/// The explained list (tools/CodeBrix.Android.ParityScore/declined-explained.tsv): the written policy for the
/// DependencyProperties no handler maps because Core, the policy layer or the platform answers for them. A property the
/// list explains is reported as "explained", not "declined". Format: tab-separated
/// <c>declaring type | property | category | reason</c>; <c>#</c> starts a comment line.
/// </summary>
internal sealed class ExplainedList
{
    private readonly Dictionary<string, Explanation> _byId = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Explanation> _anyType = new(StringComparer.Ordinal);

    /// <summary>The number of explanations.</summary>
    internal int Count => _byId.Count + _anyType.Count;

    /// <summary>Reads the list.</summary>
    /// <param name="path">The TSV file (a missing file = an empty list).</param>
    /// <returns>The list.</returns>
    internal static ExplainedList Load(string path) => Parse(path != null && File.Exists(path) ? File.ReadAllLines(path) : Array.Empty<string>());

    /// <summary>Parses the list's lines.</summary>
    /// <param name="lines">The lines.</param>
    /// <returns>The list.</returns>
    /// <exception cref="FormatException">A line does not have four fields.</exception>
    internal static ExplainedList Parse(IEnumerable<string> lines)
    {
        var list = new ExplainedList();
        var number = 0;
        foreach (var raw in lines)
        {
            number++;
            var line = raw.TrimEnd('\r');
            if (line.Trim().Length == 0 || line.TrimStart().StartsWith('#'))
            {
                continue;
            }

            var fields = line.Split('\t');
            if (fields.Length != 4)
            {
                throw new FormatException($"declined-explained.tsv line {number}: expected 4 tab-separated fields, found {fields.Length}.");
            }

            var explanation = new Explanation(fields[0].Trim(), fields[1].Trim(), fields[2].Trim(), fields[3].Trim());
            if (explanation.DeclaringType == "*")
            {
                list._anyType[explanation.Property] = explanation;
            }
            else
            {
                list._byId[explanation.DeclaringType + "." + explanation.Property] = explanation;
            }
        }

        return list;
    }

    /// <summary>The explanation of a property id (<c>Namespace.Type.NameProperty</c>), or null.</summary>
    /// <param name="propertyId">The property id.</param>
    /// <returns>The explanation, or null.</returns>
    internal Explanation Find(string propertyId)
    {
        if (_byId.TryGetValue(propertyId, out var explanation))
        {
            return explanation;
        }

        var dot = propertyId.LastIndexOf('.');
        return dot >= 0 && _anyType.TryGetValue(propertyId.Substring(dot + 1), out explanation) ? explanation : null;
    }
}
