using System;
using System.Threading.Tasks;

namespace PdfSideBySide.Services;

/// <summary>
/// The one thing the main view model cannot do for itself: ask a person which PDF to open. Only a
/// head knows how to show a file dialog, so the page fills this in when it takes the view model as
/// its data context.
/// </summary>
public interface IPdfFileBridge
{
    /// <summary>
    /// Shows an "open file" dialog filtered to PDF documents and returns the full path the person
    /// chose, or <c>null</c> when they cancelled. A head with no file dialog leaves this null, and
    /// the view model says so instead of browsing.
    /// </summary>
    Func<Task<string>> PickPdfPathAsync { get; set; }
}
