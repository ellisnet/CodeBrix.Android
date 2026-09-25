using InannaRosette.Reading.Models;

namespace InannaRosette.Reading.Services;

/// <summary>Renders a finished interpretation as a printable PDF. No UI, no file system.</summary>
public interface IPdfReportBuilder
{
    /// <summary>Builds the whole report and returns the PDF bytes.</summary>
    byte[] Build(ReadingInterpretation interpretation);

    /// <summary>A safe, descriptive file name such as <c>Rosette-Reading-Jeremy-2026-09-13.pdf</c>.</summary>
    string SuggestedFileName(ReadingInterpretation interpretation);
}
