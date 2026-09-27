using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using CodeBrix.Android.ParityScore.Scanning;

namespace CodeBrix.Android.ParityScore.Reporting;

/// <summary>
/// Writes the parity report: a text summary and TSV files (one row per line, tab-separated, a header line first,
/// deterministic order so two reports diff cleanly).
/// </summary>
internal static class ParityReport
{
    /// <summary>The summary file name.</summary>
    internal const string SummaryFile = "parity-summary.txt";

    /// <summary>NotImplemented counts per Core type.</summary>
    internal const string NotImplementedFile = "parity-notimplemented.tsv";

    /// <summary>Every NotImplemented member.</summary>
    internal const string NotImplementedMembersFile = "parity-notimplemented-members.tsv";

    /// <summary>Declined counts per (element, handler).</summary>
    internal const string DeclinedFile = "parity-declined.tsv";

    /// <summary>Every property row (mapped / partial / explained / declined).</summary>
    internal const string DeclinedPropertiesFile = "parity-declined-properties.tsv";

    /// <summary>Registered elements served by the templated fallback or Core's path.</summary>
    internal const string TemplatedFile = "parity-templated.tsv";

    /// <summary>Writes every file into <paramref name="outputDirectory"/>.</summary>
    /// <param name="outputDirectory">The folder (created when missing).</param>
    /// <param name="header">Lines that describe the inputs (version, configuration, assemblies).</param>
    /// <param name="notImplemented">The Core scan.</param>
    /// <param name="coreTypes">The number of public Core types scanned.</param>
    /// <param name="declined">The declined calculation.</param>
    /// <returns>The summary text.</returns>
    internal static string Write(string outputDirectory, IReadOnlyList<string> header, IReadOnlyList<NotImplementedType> notImplemented, int coreTypes, DeclinedResult declined)
    {
        Directory.CreateDirectory(outputDirectory);

        var types = new StringBuilder("assembly\ttype\ttype_marked\tpublic_members\tnot_implemented\tmarked\tthrows\traises\n");
        var members = new StringBuilder("assembly\ttype\tmember\treasons\n");
        foreach (var type in notImplemented)
        {
            types.Append(CultureInfo.InvariantCulture, $"{type.Assembly}\t{type.Type}\t{(type.TypeMarked ? "yes" : "no")}\t{type.PublicMembers}\t{type.Members.Count}\t{type.Marked}\t{type.Throws}\t{type.Raises}\n");
            foreach (var member in type.Members)
            {
                members.Append(CultureInfo.InvariantCulture, $"{type.Assembly}\t{type.Type}\t{member.Member}\t{Reasons(member.Reason)}\n");
            }
        }

        var summaries = new StringBuilder("element\thandler\tmapper\tscope\tmapped\texplained\tdeclined\n");
        foreach (var summary in declined.Summaries)
        {
            summaries.Append(CultureInfo.InvariantCulture, $"{summary.Element}\t{summary.Handler}\t{summary.Mapper}\t{summary.Scope}\t{summary.Mapped}\t{summary.Explained}\t{summary.Declined}\n");
        }

        var rows = new StringBuilder("element\thandler\tproperty\tstatus\tnote\n");
        foreach (var row in declined.Rows)
        {
            rows.Append(CultureInfo.InvariantCulture, $"{row.Element}\t{row.Handler}\t{row.Property}\t{row.Status.ToString().ToLowerInvariant()}\t{row.Note}\n");
        }

        var templated = new StringBuilder("element\tkind\tassembly\n");
        foreach (var registration in declined.Templated)
        {
            templated.Append(CultureInfo.InvariantCulture, $"{registration.ElementType}\t{(registration.Kind == RegistrationKind.Fallback ? "templated-fallback" : "core-path")}\t{registration.Assembly}\n");
        }

        File.WriteAllText(Path.Combine(outputDirectory, NotImplementedFile), types.ToString());
        File.WriteAllText(Path.Combine(outputDirectory, NotImplementedMembersFile), members.ToString());
        File.WriteAllText(Path.Combine(outputDirectory, DeclinedFile), summaries.ToString());
        File.WriteAllText(Path.Combine(outputDirectory, DeclinedPropertiesFile), rows.ToString());
        File.WriteAllText(Path.Combine(outputDirectory, TemplatedFile), templated.ToString());

        var text = Summary(header, notImplemented, coreTypes, declined);
        File.WriteAllText(Path.Combine(outputDirectory, SummaryFile), text);
        return text;
    }

    /// <summary>Formats the summary.</summary>
    /// <param name="header">The input description lines.</param>
    /// <param name="notImplemented">The Core scan.</param>
    /// <param name="coreTypes">The number of public Core types scanned.</param>
    /// <param name="declined">The declined calculation.</param>
    /// <returns>The text.</returns>
    internal static string Summary(IReadOnlyList<string> header, IReadOnlyList<NotImplementedType> notImplemented, int coreTypes, DeclinedResult declined)
    {
        var text = new StringBuilder();
        text.Append("CodeBrix.Android parity score\n");
        foreach (var line in header)
        {
            text.Append(line).Append('\n');
        }

        var memberTotal = notImplemented.Sum(t => t.Members.Count);
        text.Append('\n').Append("(a) Core NotImplemented\n");
        text.Append(CultureInfo.InvariantCulture, $"  public Core types scanned:              {coreTypes}\n");
        text.Append(CultureInfo.InvariantCulture, $"  types with NotImplemented members:      {notImplemented.Count(t => t.Members.Count > 0)}\n");
        text.Append(CultureInfo.InvariantCulture, $"  types marked NotImplemented as a whole: {notImplemented.Count(t => t.TypeMarked)}\n");
        text.Append(CultureInfo.InvariantCulture, $"  NotImplemented members (total):         {memberTotal}\n");
        text.Append(CultureInfo.InvariantCulture, $"    marked NotImplemented:                {notImplemented.Sum(t => t.Marked)}\n");
        text.Append(CultureInfo.InvariantCulture, $"    body throws NotImplementedException:  {notImplemented.Sum(t => t.Throws)}\n");
        text.Append(CultureInfo.InvariantCulture, $"    body raises TryRaiseNotImplemented:   {notImplemented.Sum(t => t.Raises)}\n");
        text.Append("  by assembly:\n");
        foreach (var group in notImplemented.GroupBy(t => t.Assembly).OrderBy(g => g.Key, StringComparer.Ordinal))
        {
            text.Append(CultureInfo.InvariantCulture, $"    {group.Key,-58} {group.Sum(t => t.Members.Count),6} members in {group.Count(t => t.Members.Count > 0)} types\n");
        }

        var elements = declined.Summaries.Where(s => s.Element != DeclinedCalculator.BaseRow).ToList();
        var baseRow = declined.Summaries.FirstOrDefault(s => s.Element == DeclinedCalculator.BaseRow);
        text.Append('\n').Append("(b) Android declined list (public DependencyProperties a native handler does not map and the explained list does not explain)\n");
        text.Append(CultureInfo.InvariantCulture, $"  native (element, handler) pairs:        {elements.Count}\n");
        text.Append(CultureInfo.InvariantCulture, $"  element properties in scope:            {elements.Sum(s => s.Scope)}\n");
        text.Append(CultureInfo.InvariantCulture, $"    mapped:                               {elements.Sum(s => s.Mapped)}\n");
        text.Append(CultureInfo.InvariantCulture, $"    explained:                            {elements.Sum(s => s.Explained)}\n");
        text.Append(CultureInfo.InvariantCulture, $"    declined:                             {elements.Sum(s => s.Declined)}\n");
        if (baseRow != null)
        {
            text.Append(CultureInfo.InvariantCulture, $"  base row (UIElement + FrameworkElement, {baseRow.Scope} properties): mapped by at least one handler {baseRow.Mapped}, explained {baseRow.Explained}, declined {baseRow.Declined}\n");
        }

        text.Append(CultureInfo.InvariantCulture, $"  registered elements on the templated / Core path (no native mapper): {declined.Templated.Count}\n");
        text.Append("  per element (declined / scope):\n");
        foreach (var summary in elements.OrderByDescending(s => s.Declined).ThenBy(s => s.Element, StringComparer.Ordinal))
        {
            text.Append(CultureInfo.InvariantCulture, $"    {summary.Element,-64} {summary.Declined,4} / {summary.Scope,-4} ({Short(summary.Handler)})\n");
        }

        return text.ToString();
    }

    private static string Short(string typeName)
    {
        var dot = typeName.LastIndexOf('.');
        return dot >= 0 ? typeName.Substring(dot + 1) : typeName;
    }

    private static string Reasons(NotImplementedReason reason)
    {
        var parts = new List<string>();
        if ((reason & NotImplementedReason.Marked) != 0)
        {
            parts.Add("marked");
        }

        if ((reason & NotImplementedReason.Throws) != 0)
        {
            parts.Add("throws");
        }

        if ((reason & NotImplementedReason.Raises) != 0)
        {
            parts.Add("raises");
        }

        return string.Join(",", parts);
    }
}
