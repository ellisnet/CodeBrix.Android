using CodeBrix.Platform.Simple;
using Pinta.Brix.Bridges;
using Pinta.Brix.Engine;
using Pinta.Brix.Services;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace Pinta.Brix.ViewModels;

/// <summary>
/// The shell's view model: the status bar's three readouts, the zoom control's
/// presets and commands, and the save-prompt loop the application's window
/// close asks for. The document model itself lives in the engine, so this
/// mirrors the parts of it the shell chrome shows.
/// </summary>
[Microsoft.UI.Xaml.Data.Bindable]
public class MainViewModel : SimpleViewModel, IShellCloseBridge
{
    private bool isLive;
    private bool updatingZoomSelection;
    private DocumentWorkspace watchedWorkspace;

    public MainViewModel()
    {
        if (IsDesignMode(true)) { return; } //Leave as the first line of constructor

        Debug.WriteLine("Main view model startup.");

        //The drop-down offers the workspace's own preset list, largest first.
        foreach (double percent in DocumentWorkspace.ZoomPresets.Reverse())
        {
            ZoomPresets.Add($"{percent:0.#}%");
        }

        //App's window-close handler resolves this service and asks it whether
        //the close may proceed; the page installs the prompt loop on the
        //bridge below, and this is what forwards it.
        IShellCloseService closeService = GetService<IShellCloseService>();
        if (closeService != null)
        {
            closeService.ConfirmCloseApplicationAsync = ConfirmCloseAsync;
        }

        ChromeManager chrome = PintaCore.Chrome;
        chrome.StatusBarTextChanged += OnStatusBarTextChanged;
        chrome.LastCanvasCursorPointChanged += OnCursorPointChanged;

        WorkspaceManager workspace = PintaCore.Workspace;
        workspace.ActiveDocumentChanged += OnActiveDocumentChanged;
        workspace.DocumentActivated += OnOpenDocumentsChanged;
        workspace.DocumentClosed += OnOpenDocumentsChanged;
        workspace.SelectionChanged += OnSelectionChanged;

        isLive = true;
    }

    #region | Bindable properties |

    /// <summary>The starter page's greeting, kept from the application template.</summary>
    public string Greeting => "Hello from Pinta.Brix!";

    /// <summary>
    /// The status bar's message, as the model last set it. The shape tools set
    /// several lines of it, which is why the bar pins itself to one.
    /// </summary>
    public string StatusText
    {
        get;
        private set => SetProperty(ref field, value ?? string.Empty);
    } = string.Empty;

    /// <summary>The canvas cursor's position, in image coordinates.</summary>
    public string CursorPositionText
    {
        get;
        private set => SetProperty(ref field, value ?? string.Empty);
    } = string.Empty;

    /// <summary>The selection's size, blank when nothing is selected.</summary>
    public string SelectionSizeText
    {
        get;
        private set => SetProperty(ref field, value ?? string.Empty);
    } = string.Empty;

    /// <summary>Whether any document is open; gates the zoom commands.</summary>
    [AffectsCommands(nameof(ZoomInCommand), nameof(ZoomOutCommand))]
    public bool HasOpenDocuments
    {
        get;
        private set => SetProperty(ref field, value);
    }

    /// <summary>The zoom percentages the drop-down offers, largest first.</summary>
    public IList<string> ZoomPresets { get; } = [];

    /// <summary>
    /// The current zoom, shown as the drop-down's placeholder. The list keeps
    /// nothing selected, so the placeholder is what the user reads.
    /// </summary>
    public string ZoomDisplayText
    {
        get;
        private set => SetProperty(ref field, value ?? string.Empty);
    } = string.Empty;

    /// <summary>
    /// The preset the user picked. Choosing one zooms to it; the zoom then
    /// clears the selection again so the placeholder takes over, which is also
    /// what keeps a manual zoom from looking like a preset.
    /// </summary>
    public string SelectedZoomPreset
    {
        get;
        set
        {
            SetProperty(ref field, value);
            ApplyZoomPreset(value);
        }
    }

    #endregion

    #region | Bridges |

    /// <inheritdoc />
    public Func<Task<bool>> ConfirmCloseApplicationAsync { get; set; }

    #endregion

    #region | Commands and their implementations |

    private SimpleCommand _zoomInCommand;
    /// <summary>Zooms the active document in one preset step.</summary>
    public SimpleCommand ZoomInCommand =>
        (_zoomInCommand ??= new SimpleCommand(CanZoom, DoZoomIn));

    private SimpleCommand _zoomOutCommand;
    /// <summary>Zooms the active document out one preset step.</summary>
    public SimpleCommand ZoomOutCommand =>
        (_zoomOutCommand ??= new SimpleCommand(CanZoom, DoZoomOut));

    private bool CanZoom() => HasOpenDocuments;

    private void DoZoomIn()
    {
        if (!CanZoom()) { return; }

        //Through the action model, so the button, the menu item and the
        //keyboard shortcut all run the one handler.
        PintaCore.Actions.View.ZoomIn.Activate();
    }

    private void DoZoomOut()
    {
        if (!CanZoom()) { return; }
        PintaCore.Actions.View.ZoomOut.Activate();
    }

    private void ApplyZoomPreset(string preset)
    {
        //The zoom itself clears the selection; that write must not zoom again.
        if (updatingZoomSelection) { return; }

        if (string.IsNullOrEmpty(preset) || !PintaCore.Workspace.HasOpenDocuments) { return; }

        if (double.TryParse(preset.TrimEnd('%'), out double percent))
        {
            PintaCore.Workspace.ActiveWorkspace.ZoomManually(percent / 100.0);
        }
    }

    #endregion

    #region | Model event handlers |

    private void OnStatusBarTextChanged(object sender, TextChangedEventArgs args) =>
        StatusText = args.Text;

    private void OnCursorPointChanged(object sender, EventArgs args)
    {
        PointI point = PintaCore.Chrome.LastCanvasCursorPoint;
        CursorPositionText = $"{point.X}, {point.Y}";
    }

    private void OnOpenDocumentsChanged(object sender, DocumentEventArgs args) =>
        RefreshDocumentState();

    private void OnSelectionChanged(object sender, EventArgs args) =>
        UpdateSelectionSizeText();

    private void OnActiveDocumentChanged(object sender, EventArgs args)
    {
        if (watchedWorkspace != null)
        {
            watchedWorkspace.ZoomChanged -= OnZoomChanged;
            watchedWorkspace = null;
        }

        if (PintaCore.Workspace.HasOpenDocuments)
        {
            watchedWorkspace = PintaCore.Workspace.ActiveWorkspace;
            watchedWorkspace.ZoomChanged += OnZoomChanged;
        }

        RefreshDocumentState();
        OnZoomChanged(this, EventArgs.Empty);
    }

    private void OnZoomChanged(object sender, EventArgs args)
    {
        if (!PintaCore.Workspace.HasOpenDocuments) { return; }

        updatingZoomSelection = true;
        ZoomDisplayText = $"{PintaCore.Workspace.ActiveWorkspace.Scale * 100:0.#}%";
        SelectedZoomPreset = null;
        updatingZoomSelection = false;
    }

    private void RefreshDocumentState()
    {
        HasOpenDocuments = PintaCore.Workspace.HasOpenDocuments;
        UpdateSelectionSizeText();
    }

    /// <summary>
    /// Upstream shows the selection's size beside the cursor position; it is
    /// blank when nothing is selected.
    /// </summary>
    private void UpdateSelectionSizeText()
    {
        if (!PintaCore.Workspace.HasOpenDocuments)
        {
            SelectionSizeText = string.Empty;
            return;
        }

        Document document = PintaCore.Workspace.ActiveDocument;

        if (!document.Selection.Visible)
        {
            SelectionSizeText = string.Empty;
            return;
        }

        RectangleI bounds = document.Selection.GetBounds().ToInt();
        SelectionSizeText = $"{bounds.Width} x {bounds.Height}";
    }

    #endregion

    #region | Close prompt |

    /// <summary>
    /// What <see cref="IShellCloseService"/> calls: the page's prompt loop when
    /// one has been installed, and otherwise a plain "go ahead", because with
    /// no shell there is no unsaved work to ask about.
    /// </summary>
    private async Task<bool> ConfirmCloseAsync()
    {
        Func<Task<bool>> prompt = ConfirmCloseApplicationAsync;

        if (prompt == null) { return true; }

        return await prompt();
    }

    #endregion

    /// <summary>
    /// Lets go of the model events and the delegate the page handed over, and
    /// disposes the commands the bindings held.
    /// </summary>
    /// <param name="disposing">True when called from <c>Dispose()</c>.</param>
    protected override void Dispose(bool disposing)
    {
        if (disposing && isLive)
        {
            isLive = false;

            ChromeManager chrome = PintaCore.Chrome;
            chrome.StatusBarTextChanged -= OnStatusBarTextChanged;
            chrome.LastCanvasCursorPointChanged -= OnCursorPointChanged;

            WorkspaceManager workspace = PintaCore.Workspace;
            workspace.ActiveDocumentChanged -= OnActiveDocumentChanged;
            workspace.DocumentActivated -= OnOpenDocumentsChanged;
            workspace.DocumentClosed -= OnOpenDocumentsChanged;
            workspace.SelectionChanged -= OnSelectionChanged;

            if (watchedWorkspace != null)
            {
                watchedWorkspace.ZoomChanged -= OnZoomChanged;
                watchedWorkspace = null;
            }

            ConfirmCloseApplicationAsync = null;

            _zoomInCommand?.Dispose();
            _zoomOutCommand?.Dispose();
        }

        base.Dispose(disposing);
    }
}
