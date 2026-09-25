using System;
using System.Threading.Tasks;

namespace Pinta.Brix.Services;

/// <summary>
/// The seam between the application object's window-close handler and the
/// shell's save-prompt loop. The window is owned by <c>App</c> and the prompt
/// loop is owned by the shell's view model, and neither should hold a
/// reference to the other, so the loop is installed here and asked for here.
/// </summary>
public interface IShellCloseService
{
    /// <summary>
    /// The prompt loop the shell installs: it walks every dirty document,
    /// offering to save, discard or cancel, and answers false when the user
    /// cancelled. Null until a shell has installed one.
    /// Signature: <c>Func&lt;Task&lt;closeMayProceed&gt;&gt;</c>.
    /// </summary>
    Func<Task<bool>> ConfirmCloseApplicationAsync { get; set; }

    /// <summary>
    /// Runs the installed prompt loop and reports whether the close may go
    /// ahead. True when no loop has been installed, because there is then no
    /// shell holding unsaved work to ask about.
    /// </summary>
    Task<bool> ConfirmCloseAsync();
}
