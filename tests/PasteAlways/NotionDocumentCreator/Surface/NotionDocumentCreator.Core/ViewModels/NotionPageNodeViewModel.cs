using CodeBrix.Platform.Simple;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using NotionDocumentCreator.CreateDocument.Models;
using NotionDocumentCreator.Helpers;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace NotionDocumentCreator.ViewModels;

/// <summary>
/// One row of the page tree: a Notion page (or database) with explicit checkbox
/// and expansion state. Selection is fully independent per node — checking a
/// parent never checks its children, and vice versa. Children load lazily on
/// first expand (a placeholder row keeps the expand chevron visible until then).
/// </summary>
[Microsoft.UI.Xaml.Data.Bindable]
public class NotionPageNodeViewModel : SimpleViewModel
{
    private readonly MainViewModel _owner;
    private bool _loadRequested; //Explicit field: methods outside the accessors read it

    public NotionPageNodeViewModel(NotionPageNode node, MainViewModel owner)
    {
        Node = node;
        _owner = owner;

        if (node?.HasChildren == true)
        {
            //A placeholder child keeps the expand chevron visible until the real
            //  children arrive on first expand
            Children.Add(new NotionPageNodeViewModel());
        }

        if (!string.IsNullOrEmpty(node?.IconUrl))
        {
            try { IconImageSource = new BitmapImage(new Uri(node.IconUrl)); }
            catch (Exception) { } //A malformed icon URL just falls back to the glyph
        }
    }

    private NotionPageNodeViewModel()
    {
        IsPlaceholder = true;
    }

    #region | Bindable properties |

    /// <summary>The underlying tree node (null for the loading placeholder).</summary>
    public NotionPageNode Node { get; }

    /// <summary>The Notion page/database ID.</summary>
    public string Id => Node?.Id ?? "";

    /// <summary>The row title.</summary>
    public string Title => IsPlaceholder ? "Loading…" : Node?.Title ?? "";

    /// <summary>True for the transient "Loading…" row shown before children arrive.</summary>
    public bool IsPlaceholder { get; }

    /// <summary>Child rows (one placeholder until the real children load).</summary>
    public ObservableCollection<NotionPageNodeViewModel> Children { get; } = new();

    public bool IsChecked
    {
        get;
        set
        {
            SetProperty(ref field, value);
            _owner?.OnNodeCheckedChanged();
        }
    }

    public bool IsExpanded
    {
        get;
        set
        {
            SetProperty(ref field, value);
            //A setter cannot await, so the one-shot child load is started through a helper
            //  that observes it rather than through a bare fire-and-forget discard.
            if (value) { BackgroundWork.StartAndObserve(EnsureChildrenLoadedAsync, ReportChildLoadFailure); }
        }
    }

    /// <summary>The checkbox hides on the placeholder row.</summary>
    public Visibility CheckBoxVisibility => GetVisibility(!IsPlaceholder);

    /// <summary>The page icon image, when Notion supplies an image icon.</summary>
    public ImageSource IconImageSource { get; }

    public Visibility IconImageVisibility => GetVisibility(IconImageSource is not null);

    public Visibility IconGlyphVisibility => GetVisibility(IconImageSource is null && !IsPlaceholder);

    /// <summary>Fluent glyph for the row: a document for pages, a stack for databases.</summary>
    public string KindGlyph => Node?.Kind == NotionSourceKind.Database ? "\uE8B7" : "\uE8A5";

    #endregion

    #region | Commands and their implementations |

    /// <summary>Tapping the row (not its checkbox) previews the page.</summary>
    public SimpleCommand SelectCommand => field ??= new SimpleCommand(() => _owner?.ShowPreview(this));

    #endregion

    /// <summary>Loads the real children on first expand (no-op afterwards).</summary>
    internal async System.Threading.Tasks.Task EnsureChildrenLoadedAsync()
    {
        if (IsPlaceholder || _loadRequested || Node?.HasChildren != true || _owner is null) { return; }
        _loadRequested = true;
        await _owner.LoadChildrenForNodeAsync(this);
    }

    /// <summary>Replaces the placeholder with the loaded children.</summary>
    internal void SetChildren(IEnumerable<NotionPageNodeViewModel> children)
    {
        Children.Clear();
        foreach (var child in children) { Children.Add(child); }
    }

    //The load path reports its own failures on the status line; this is the last resort for
    //  anything that gets past it, so an expand gesture can never surface an exception.
    private void ReportChildLoadFailure(Exception error)
    {
        var owner = _owner;
        if (owner is null) { return; }
        InvokeOnMainThread(() => owner.StatusText = $"Could not load child pages: {error.Message}");
    }
}
