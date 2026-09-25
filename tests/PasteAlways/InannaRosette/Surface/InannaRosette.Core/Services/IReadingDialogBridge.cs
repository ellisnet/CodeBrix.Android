using System;
using System.Threading.Tasks;

namespace InannaRosette.Services;

/// <summary>
/// The dialogs this application asks for by name. <c>SimpleViewModel</c>'s own
/// <c>ConfirmDialog</c> and <c>ShowError</c> helpers would do the job, but the temple styling —
/// gold on lapis, the app's own button styles, a "Report saved" panel with an Open button — is
/// a page concern, so the page supplies the three shapes the view model needs and keeps the
/// look with them. A head that cannot show a dialog leaves them null; the view model then
/// treats a confirmation as granted and reports failures in the status line only.
/// </summary>
public interface IReadingDialogBridge
{
    /// <summary>
    /// Asks a yes/no question and returns true when the person pressed the primary button.
    /// Signature: <c>Func&lt;title, message, primaryButtonCaption, Task&lt;bool&gt;&gt;</c>.
    /// </summary>
    Func<string, string, string, Task<bool>>? ConfirmAsync { get; set; }

    /// <summary>Tells the person something and waits for them to close it.</summary>
    Func<string, string, Task>? ShowMessageAsync { get; set; }

    /// <summary>
    /// Shows the "Report saved" panel for a finished PDF, whose Open button runs
    /// <see cref="ViewModels.MainViewModel.OpenSavedReportCommand"/>.
    /// </summary>
    Func<string, Task>? ShowReportSavedAsync { get; set; }
}
