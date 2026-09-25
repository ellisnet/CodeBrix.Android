using CodeBrix.Platform.Simple;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using RedisSetupTool.Bridges;
using RedisSetupTool.DockerManagement;
using RedisSetupTool.DockerManagement.Exec;
using RedisSetupTool.Services;
using RedisSetupTool.TerminalView;
using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Threading.Tasks;

namespace RedisSetupTool.ViewModels;

/// <summary>
/// One console tab's status. The terminal control and the exec pump live in the page's
/// code-behind - <c>TerminalControl</c> declares no dependency properties, so it cannot be bound
/// or placed in a data template - and the grid size and session state the page learns there come
/// back through the <c>Apply</c> methods here.
/// </summary>
[Microsoft.UI.Xaml.Data.Bindable]
public class ConsoleTabViewModel : SimpleViewModel
{
    private readonly Action<ConsoleTabViewModel> _close;
    private readonly Action<ConsoleTabViewModel> _reopen;

    /// <summary>Creates a tab for one container.</summary>
    /// <param name="containerId">The container the console runs in.</param>
    /// <param name="containerName">The name to put on the tab.</param>
    /// <param name="close">Closes this tab.</param>
    /// <param name="reopen">Closes this tab and opens a fresh one on the same container.</param>
    public ConsoleTabViewModel(string containerId, string containerName,
        Action<ConsoleTabViewModel> close, Action<ConsoleTabViewModel> reopen)
    {
        ContainerId = containerId;
        ContainerName = string.IsNullOrWhiteSpace(containerName)
            ? Formatting.Trim(containerId, 12)
            : containerName;
        _close = close;
        _reopen = reopen;
    }

    #region | Bindable properties |

    /// <summary>The container the console runs in.</summary>
    public string ContainerId { get; }

    /// <summary>The tab's caption.</summary>
    public string ContainerName { get; }

    /// <summary>The shell the probe resolved, for example <c>/bin/sh</c>.</summary>
    public string ShellPath
    {
        get;
        private set => SetProperty(ref field, value);
    } = "resolving shell…";

    /// <summary>The grid size, for example <c>112 x 34</c>.</summary>
    public string GridText
    {
        get;
        private set => SetProperty(ref field, value);
    } = "—";

    /// <summary>The session state in words.</summary>
    [AffectsProperties(nameof(TabHeaderText))]
    public string StateText
    {
        get;
        private set => SetProperty(ref field, value);
    } = "starting";

    /// <summary>
    /// The caption the tab strip carries: the container's name on its own while the session is
    /// running, and the name with the state beside it once it is not.
    /// </summary>
    public string TabHeaderText =>
        IsRunning ? ContainerName : ContainerName + " · " + StateText;

    /// <summary>Why the console could not be opened, or an empty string while nothing has failed.</summary>
    public string FailureMessage
    {
        get;
        private set => SetProperty(ref field, value ?? string.Empty);
    } = string.Empty;

    /// <summary>The status strip's dot colour.</summary>
    public Brush StateBrush
    {
        get;
        private set => SetProperty(ref field, value);
    } = Palette.Warn;

    /// <summary>Whether the session is still running.</summary>
    [AffectsAllCommands]
    [AffectsProperties(nameof(TabHeaderText))]
    public bool IsRunning
    {
        get;
        private set => SetProperty(ref field, value);
    }

    /// <summary>Whether the Reopen button is offered, which it is once the session has ended.</summary>
    public Visibility ReopenVisibility { get; private set; } = Visibility.Collapsed;

    #endregion

    #region | Commands and their implementations |

    /// <summary>Closes this console.</summary>
    public SimpleCommand CloseCommand => field ??= new SimpleCommand(() => _close?.Invoke(this));

    /// <summary>Starts a fresh session on the same container.</summary>
    public SimpleCommand ReopenCommand => field ??= new SimpleCommand(() => _reopen?.Invoke(this));

    #endregion

    /// <summary>Records the shell the probe found.</summary>
    /// <param name="shellPath">The resolved shell path.</param>
    public void ApplyShell(string shellPath) =>
        ShellPath = string.IsNullOrEmpty(shellPath) ? "no shell" : shellPath;

    /// <summary>Records the terminal's grid size.</summary>
    /// <param name="columns">How many columns the grid has.</param>
    /// <param name="rows">How many rows the grid has.</param>
    public void ApplyGrid(int columns, int rows) =>
        GridText = columns.ToString(CultureInfo.InvariantCulture) + " x "
            + rows.ToString(CultureInfo.InvariantCulture);

    /// <summary>Records the session's state.</summary>
    /// <param name="state">The state the pump reported.</param>
    /// <param name="exitCode">The exec's exit code, once it has one.</param>
    public void ApplyState(TerminalSessionState state, long exitCode = 0)
    {
        IsRunning = state == TerminalSessionState.Running;
        StateText = state switch
        {
            TerminalSessionState.Starting => "starting",
            TerminalSessionState.Running => "running",
            TerminalSessionState.Exited => "exited (code "
                + exitCode.ToString(CultureInfo.InvariantCulture) + ")",
            _ => "failed",
        };
        StateBrush = state switch
        {
            TerminalSessionState.Running => Palette.Good,
            TerminalSessionState.Starting => Palette.Warn,
            TerminalSessionState.Exited => Palette.Idle,
            _ => Palette.Bad,
        };
        ReopenVisibility = GetVisibility(state is TerminalSessionState.Exited
            or TerminalSessionState.Failed);
        NotifyPropertyChanged(nameof(ReopenVisibility));
    }

    /// <summary>Records that the console could not be opened at all.</summary>
    /// <param name="message">Why it could not.</param>
    public void ApplyFailure(string message)
    {
        FailureMessage = message;
        ShellPath = message;
        IsRunning = false;
        StateText = "failed";
        StateBrush = Palette.Bad;
        ReopenVisibility = Visibility.Visible;
        NotifyPropertyChanged(nameof(ReopenVisibility));
    }
}

/// <summary>
/// Section 5 — a tab strip of live terminal sessions inside running containers. The view model
/// owns the list of tabs and the container picker; the page's code-behind owns the terminals
/// themselves and mirrors this collection into the <c>TabView</c>.
/// </summary>
[Microsoft.UI.Xaml.Data.Bindable]
public class ConsolesViewModel : SectionViewModel, IConsoleTabsBridge
{
    private const string NoShellMessage = "No usable shell in this image.";

    private readonly IDockerManager _docker;

    /// <summary>Creates the consoles section.</summary>
    /// <param name="shell">The shell this section belongs to.</param>
    public ConsolesViewModel(IShellContext shell)
        : base(shell)
    {
        _docker = GetService<IDockerManager>();
    }

    #region | Bindable properties |

    /// <summary>The open console tabs. The page mirrors this into its tab strip.</summary>
    public ObservableCollection<ConsoleTabViewModel> Tabs { get; } = [];

    /// <summary>Whether there are no consoles open.</summary>
    public Visibility EmptyVisibility => GetVisibility(Tabs.Count == 0);

    /// <summary>Whether at least one console is open.</summary>
    public Visibility TabsVisibility => GetVisibility(Tabs.Count > 0);

    /// <summary>The running containers a console can be opened in.</summary>
    public ObservableCollection<ContainerRowViewModel> PickerRows { get; } = [];

    /// <summary>Whether the container picker is showing.</summary>
    public Visibility PickerVisibility => GetVisibility(IsPickerOpen);

    /// <summary>Whether the container picker is open.</summary>
    public bool IsPickerOpen
    {
        get;
        private set
        {
            SetProperty(ref field, value);
            NotifyPropertyChanged(nameof(PickerVisibility));
        }
    }

    /// <summary>The picker's free-text filter.</summary>
    public string PickerSearchText
    {
        get;
        set
        {
            SetProperty(ref field, value ?? string.Empty);
            RebuildPicker();
        }
    } = string.Empty;

    #endregion

    #region | Commands and their implementations |

    /// <summary>Opens the container picker.</summary>
    public SimpleCommand OpenPickerCommand => field ??= new SimpleCommand(() =>
    {
        RebuildPicker();
        IsPickerOpen = true;
    });

    /// <summary>Closes the container picker.</summary>
    public SimpleCommand ClosePickerCommand => field ??=
        new SimpleCommand(() => IsPickerOpen = false);

    #endregion

    /// <inheritdoc />
    public override void ApplySnapshot()
    {
        if (IsPickerOpen)
        {
            RebuildPicker();
        }
    }

    /// <summary>
    /// Opens a console tab on a container. The page notices the new tab through the collection
    /// and does the rest: it builds a terminal, asks for the tab's session and starts the pump.
    /// </summary>
    /// <param name="containerId">The container to open a shell in.</param>
    /// <param name="containerName">The name to put on the tab.</param>
    /// <returns>The new tab.</returns>
    public ConsoleTabViewModel OpenConsole(string containerId, string containerName)
    {
        if (string.IsNullOrEmpty(containerId)) { return null; }

        IsPickerOpen = false;
        var tab = new ConsoleTabViewModel(containerId, containerName, CloseTab, Reopen);
        Tabs.Add(tab);
        NotifyPropertyChanged(nameof(EmptyVisibility));
        NotifyPropertyChanged(nameof(TabsVisibility));
        Shell?.LogAutomation("console open " + Formatting.Trim(containerId, 12));
        return tab;
    }

    /// <summary>Closes a console tab, which disposes its session.</summary>
    /// <param name="tab">The tab to close.</param>
    public void CloseTab(ConsoleTabViewModel tab)
    {
        if (tab is null) { return; }

        Tabs.Remove(tab);
        NotifyPropertyChanged(nameof(EmptyVisibility));
        NotifyPropertyChanged(nameof(TabsVisibility));
    }

    /// <inheritdoc />
    public async Task<IExecSession> StartSessionAsync(ConsoleTabViewModel tab, int columns,
        int rows)
    {
        if (tab is null) { return null; }

        try
        {
            var probe = await _docker.ProbeShellAsync(tab.ContainerId).ConfigureAwait(true);
            if (!probe.Found)
            {
                tab.ApplyFailure(probe.Message ?? NoShellMessage);
                return null;
            }

            tab.ApplyShell(probe.ShellPath);
            return await _docker.OpenShellAsync(tab.ContainerId, new ExecSessionOptions
            {
                Rows = rows,
                Columns = columns,
            }).ConfigureAwait(true);
        }
        catch (Exception exception)
        {
            //Nothing here is worth a dialog: the tab's own status strip carries the reason and
            //  the page writes it into the terminal the user is already looking at.
            tab.ApplyFailure(exception.Message);
            return null;
        }
    }

    /// <inheritdoc />
    /// <remarks>It exists for the unattended verification script; nothing in the UI uses it.</remarks>
    public Action<ConsoleTabViewModel, string> SendInput { get; set; }

    private void Reopen(ConsoleTabViewModel tab)
    {
        if (tab is null) { return; }

        //Closing and opening is the whole of it: the page is watching the collection, so it
        //  tears the old terminal down and builds a fresh one from these two moves alone.
        var containerId = tab.ContainerId;
        var containerName = tab.ContainerName;
        CloseTab(tab);
        OpenConsole(containerId, containerName);
    }

    private void RebuildPicker()
    {
        PickerRows.Clear();
        var snapshot = State?.Containers;
        if (snapshot is null) { return; }

        foreach (var container in snapshot)
        {
            if (!container.IsRunning) { continue; }
            if (!string.IsNullOrWhiteSpace(PickerSearchText)
                && (container.Name ?? string.Empty)
                    .IndexOf(PickerSearchText.Trim(), StringComparison.OrdinalIgnoreCase) < 0)
            {
                continue;
            }

            PickerRows.Add(new ContainerRowViewModel(container,
                row => OpenConsole(row.Id, row.Name)));
        }
    }
}
