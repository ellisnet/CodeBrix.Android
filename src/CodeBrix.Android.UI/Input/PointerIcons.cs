using Windows.UI.Core;
using APointerIcon = global::Android.Views.PointerIcon;
using APointerIconType = global::Android.Views.PointerIconType;
using AView = global::Android.Views.View;

namespace CodeBrix.Android.UI.Input;

/// <summary>
/// WinUI cursors (ProtectedCursor / InputSystemCursor, through Core's CoreCursor) to Android system pointer
/// icons (plan 2.15 desktop mode: a docked phone or a Chromebook-class device shows a mouse pointer).
/// </summary>
internal static class PointerIcons
{
    /// <summary>The Android system icon type of a cursor type.</summary>
    internal static APointerIconType ToIconType(CoreCursorType type) => type switch
    {
        CoreCursorType.Cross => APointerIconType.Crosshair,
        CoreCursorType.Hand => APointerIconType.Hand,
        CoreCursorType.Help => APointerIconType.Help,
        CoreCursorType.IBeam => APointerIconType.Text,
        CoreCursorType.SizeAll => APointerIconType.AllScroll,
        CoreCursorType.SizeNortheastSouthwest => APointerIconType.TopRightDiagonalDoubleArrow,
        CoreCursorType.SizeNorthSouth => APointerIconType.VerticalDoubleArrow,
        CoreCursorType.SizeNorthwestSoutheast => APointerIconType.TopLeftDiagonalDoubleArrow,
        CoreCursorType.SizeWestEast => APointerIconType.HorizontalDoubleArrow,
        CoreCursorType.UniversalNo => APointerIconType.NoDrop,
        CoreCursorType.Wait => APointerIconType.Wait,
        CoreCursorType.UpArrow => APointerIconType.Arrow,
        _ => APointerIconType.Arrow,
    };

    /// <summary>Shows the cursor's icon for the mouse over <paramref name="view"/> (the activity's root layout).</summary>
    internal static void Apply(AView view, CoreCursor cursor)
    {
        if (view?.Context is not { } context)
        {
            return;
        }

        view.PointerIcon = cursor == null ? null : APointerIcon.GetSystemIcon(context, ToIconType(cursor.Type));
    }
}
