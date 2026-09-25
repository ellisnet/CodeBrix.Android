namespace CodeBrix.Android.UI.Platform.Recycler.Portable;

/// <summary>How a RecyclerView-backed items control lays its items out.</summary>
internal enum ItemsLayoutKind
{
    /// <summary>One line of items (ItemsStackPanel, VirtualizingStackPanel, StackPanel, StackLayout).</summary>
    Linear,

    /// <summary>
    /// Lines of uniform cells (ItemsWrapGrid, WrapGrid, UniformGridLayout): the cell size is the first item's
    /// size unless the panel fixes it.
    /// </summary>
    UniformGrid,

    /// <summary>One item per page, filling the viewport (FlipView).</summary>
    Pager,
}

/// <summary>
/// The layout of a RecyclerView-backed items control, read from its items panel or ItemsRepeater layout:
/// the kind, the SCROLL direction (a horizontal ItemsWrapGrid fills rows and scrolls vertically), a fixed
/// cell size (NaN = measured), the most lines across the cross axis (0 = no limit) and the spacing
/// between items (DIPs).
/// </summary>
internal sealed record ItemsLayoutSpec(
    ItemsLayoutKind Kind,
    bool ScrollsHorizontally,
    double ItemWidth = double.NaN,
    double ItemHeight = double.NaN,
    int MaximumLines = 0,
    double MainSpacing = 0,
    double CrossSpacing = 0,
    bool StretchItems = false)
{
    /// <summary>A vertical list (the ListView default).</summary>
    internal static ItemsLayoutSpec VerticalList { get; } = new(ItemsLayoutKind.Linear, false);

    /// <summary>A grid that fills rows and scrolls vertically (the GridView default).</summary>
    internal static ItemsLayoutSpec VerticalGrid { get; } = new(ItemsLayoutKind.UniformGrid, false);

    /// <summary>A horizontal pager (FlipView).</summary>
    internal static ItemsLayoutSpec HorizontalPager { get; } = new(ItemsLayoutKind.Pager, true);
}
