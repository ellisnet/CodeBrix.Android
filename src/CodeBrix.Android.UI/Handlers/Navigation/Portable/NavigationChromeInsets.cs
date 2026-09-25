using CodeBrix.Android.UI.Policy;
using CodeBrix.Android.UI.Portable.Layout;

namespace CodeBrix.Android.UI.Handlers;

/// <summary>
/// The room the chrome of a native navigation container takes and the room its content gets, with the window's
/// safe-area insets (system bars, display cutout) the NavigationView overlaps absorbed: the chrome runs edge to
/// edge and pads its destinations clear of the bars on the edges it touches; the content is laid out clear of the
/// bars on the other edges too - unless the content is a Page, which absorbs its own overlap (its background then
/// still runs under the bars, plan D-P10). All values in DIPs, left / top / right / bottom.
/// </summary>
internal static class NavigationChromeInsets
{
    /// <summary>The height of the bottom navigation bar, in dp (Material 3).</summary>
    internal const double BottomBarHeight = 80;

    /// <summary>The width of the navigation rail, in dp (Material 3).</summary>
    internal const double RailWidth = 80;

    /// <summary>The height of the top bar of the modal-drawer container, in dp (Material 3 small top app bar).</summary>
    internal const double TopBarHeight = 64;

    /// <summary>The room the chrome takes from each edge without insets.</summary>
    /// <param name="container">The container.</param>
    /// <param name="paneWidth">The persistent drawer's width.</param>
    /// <returns>Left, top, right, bottom.</returns>
    internal static SafeAreaPadding Chrome(NavigationContainer container, double paneWidth) => container switch
    {
        NavigationContainer.BottomBar => new SafeAreaPadding(0, 0, 0, BottomBarHeight),
        NavigationContainer.Rail => new SafeAreaPadding(RailWidth, 0, 0, 0),
        NavigationContainer.PersistentDrawer => new SafeAreaPadding(paneWidth, 0, 0, 0),
        NavigationContainer.ModalDrawer => new SafeAreaPadding(0, TopBarHeight, 0, 0),
        _ => SafeAreaPadding.Empty,
    };

    /// <summary>
    /// The chrome's padding: the safe-area inset of every edge the chrome view touches (a bottom bar: left, right
    /// and bottom; a rail or a drawer: left, top and bottom; the modal container's top bar: left, top and right).
    /// </summary>
    /// <param name="container">The container.</param>
    /// <param name="safeArea">The safe-area insets the NavigationView overlaps.</param>
    /// <returns>The padding of the bar, rail or drawer (for the modal container: of the top bar).</returns>
    internal static SafeAreaPadding ChromePadding(NavigationContainer container, SafeAreaPadding safeArea) => container switch
    {
        NavigationContainer.BottomBar => new SafeAreaPadding(safeArea.Left, 0, safeArea.Right, safeArea.Bottom),
        NavigationContainer.Rail or NavigationContainer.PersistentDrawer => new SafeAreaPadding(safeArea.Left, safeArea.Top, 0, safeArea.Bottom),
        NavigationContainer.ModalDrawer => new SafeAreaPadding(safeArea.Left, safeArea.Top, safeArea.Right, 0),
        _ => SafeAreaPadding.Empty,
    };

    /// <summary>
    /// The room the content leaves free on each edge: the chrome's extent plus the inset it absorbs on the edges
    /// the chrome takes, the safe-area inset on the others (none when <paramref name="contentAbsorbs"/>).
    /// </summary>
    /// <param name="container">The container.</param>
    /// <param name="paneWidth">The persistent drawer's width.</param>
    /// <param name="safeArea">The safe-area insets the NavigationView overlaps.</param>
    /// <param name="contentAbsorbs">True when the content absorbs its own overlap (a Page).</param>
    /// <returns>Left, top, right, bottom.</returns>
    internal static SafeAreaPadding Content(NavigationContainer container, double paneWidth, SafeAreaPadding safeArea, bool contentAbsorbs)
    {
        if (container == NavigationContainer.Template)
        {
            return SafeAreaPadding.Empty;
        }

        var chrome = Chrome(container, paneWidth);
        double Edge(double chromeEdge, double inset) => chromeEdge > 0 ? chromeEdge + inset : contentAbsorbs ? 0 : inset;
        return new SafeAreaPadding(
            Edge(chrome.Left, safeArea.Left),
            Edge(chrome.Top, safeArea.Top),
            Edge(chrome.Right, safeArea.Right),
            Edge(chrome.Bottom, safeArea.Bottom));
    }
}
