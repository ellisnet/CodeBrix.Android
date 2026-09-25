using RedisSetupTool.DockerManagement.Exec;
using RedisSetupTool.ViewModels;
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

namespace RedisSetupTool.Bridges;

/// <summary>
/// The seam between the console tabs a view model owns and the page that runs a terminal for
/// each one. <c>TerminalControl</c> declares no dependency properties, so a terminal cannot be
/// bound or placed in a data template and the page has to build one per tab in code; this
/// interface is everything that crossing takes. The page watches <see cref="Tabs"/>, asks
/// <see cref="StartSessionAsync"/> for the shell behind a new tab, and fills
/// <see cref="SendInput"/> in so a caller that holds no terminal can still type into one.
/// </summary>
public interface IConsoleTabsBridge
{
    /// <summary>The open console tabs, which the page mirrors into its tab strip.</summary>
    ObservableCollection<ConsoleTabViewModel> Tabs { get; }

    /// <summary>
    /// Sends text to a console's shell, as though it had been typed. The page fills this in,
    /// because only the page holds the terminal pump, and it is null until the page does.
    /// </summary>
    Action<ConsoleTabViewModel, string> SendInput { get; set; }

    /// <summary>
    /// Opens the exec session behind a tab: it probes the container for a usable shell, records
    /// what the probe found on the tab, and opens the shell at the terminal's grid size.
    /// </summary>
    /// <param name="tab">The tab the session belongs to.</param>
    /// <param name="columns">The terminal's column count.</param>
    /// <param name="rows">The terminal's row count.</param>
    /// <returns>
    /// The open session, or null when the console could not be opened, in which case the reason
    /// is already on the tab in <see cref="ConsoleTabViewModel.FailureMessage"/>.
    /// </returns>
    Task<IExecSession> StartSessionAsync(ConsoleTabViewModel tab, int columns, int rows);
}
