namespace PdfSideBySide.PdfRender;

/// <summary>
/// Makes the <see cref="PdfComparison"/> a screen works with. Registered with the
/// dependency-injection container by <see cref="RegisterServices.AddPdfRender"/>, so a view model
/// asks for a comparison rather than constructing one itself.
/// </summary>
public interface IPdfComparisonFactory
{
    /// <summary>Creates a comparison with both sides empty and the view at fit-the-page.</summary>
    /// <returns>A new comparison.</returns>
    PdfComparison Create();
}
