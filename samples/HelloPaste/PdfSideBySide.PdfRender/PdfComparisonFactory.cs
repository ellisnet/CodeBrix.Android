namespace PdfSideBySide.PdfRender;

/// <summary>
/// The <see cref="IPdfComparisonFactory"/> the application registers: hands out a fresh
/// <see cref="PdfComparison"/> each time it is asked.
/// </summary>
public sealed class PdfComparisonFactory : IPdfComparisonFactory
{
    /// <inheritdoc/>
    public PdfComparison Create() => new();
}
