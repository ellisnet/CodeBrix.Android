// STUB (paste always): InannaRosette.Reading.Services.PdfReportBuilder / PdfReportOptions (CodeBrix.Samples
// InannaRosette.Reading library); the real builder draws with CodeBrix.PdfDocuments and the library's embedded
// Merriweather fonts - a whole dependency tree this compile-only head does not need. Same public surface.
using System;
using InannaRosette.Reading.Models;

namespace InannaRosette.Reading.Services;

/// <summary>Options for <see cref="PdfReportBuilder"/>.</summary>
public sealed class PdfReportOptions
{
    /// <summary>US Letter instead of the default A4.</summary>
    public bool UseLetter { get; set; } = false;

    /// <summary>The value written to the PDF's Author field.</summary>
    public string Author { get; set; } = "Rosette of Inanna";
}

/// <summary>Stand-in for the PDF report builder (compile-only).</summary>
public sealed class PdfReportBuilder : IPdfReportBuilder
{
    private readonly PdfReportOptions _options;

    /// <summary>Creates a builder; <paramref name="options"/> defaults to A4.</summary>
    public PdfReportBuilder(PdfReportOptions? options = null)
    {
        _options = options ?? new PdfReportOptions();
    }

    /// <inheritdoc />
    public byte[] Build(ReadingInterpretation interpretation) =>
        throw new NotSupportedException($"PDF report ({(_options.UseLetter ? "Letter" : "A4")}) is not part of the paste-always compile head.");

    /// <inheritdoc />
    public string SuggestedFileName(ReadingInterpretation interpretation)
    {
        ArgumentNullException.ThrowIfNull(interpretation);
        return "Rosette-Reading.pdf";
    }
}
