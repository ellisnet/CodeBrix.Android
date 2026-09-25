#if __ANDROID__
using System;
using System.Collections.Generic;
using System.Linq;
using CodeBrix.Android.UI.Hosting;
using AAttribute = global::Android.Resource.Attribute;
using ABottomSheetDialog = global::Google.Android.Material.BottomSheet.BottomSheetDialog;
using ACheckedTextView = global::Android.Widget.CheckedTextView;
using AColor = global::Android.Graphics.Color;
using AComplexUnitType = global::Android.Util.ComplexUnitType;
using AGravityFlags = global::Android.Views.GravityFlags;
using ALinearLayout = global::Android.Widget.LinearLayout;
using AMaterialDivider = global::Google.Android.Material.Divider.MaterialDivider;
using ANestedScrollView = global::AndroidX.Core.Widget.NestedScrollView;
using AOrientation = global::Android.Widget.Orientation;
using ATextUtilsTruncateAt = global::Android.Text.TextUtils.TruncateAt;
using ATypedValue = global::Android.Util.TypedValue;
using AView = global::Android.Views.View;
using AViewGroup = global::Android.Views.ViewGroup;

namespace CodeBrix.Android.UI.Overlay;

/// <summary>
/// The Compact-window form of a menu (plan 2.9): a modal Material bottom sheet listing the entries as 48 dp
/// rows - a check mark for a toggle or radio entry, a chevron for a sub-menu (which replaces the list, with a
/// row that goes back up), a divider for a separator group break. Choosing a row calls the chooser and closes
/// the sheet; cancelling (a swipe down, a tap on the scrim, back) raises <see cref="Dismissed"/> only.
/// </summary>
internal sealed class MenuSheet
{
    private const int RowHeightDips = 48;
    private const int RowPaddingDips = 24;

    private readonly CodeBrixActivity _activity;
    private readonly IReadOnlyList<MenuEntry> _root;
    private readonly Action<MenuEntry> _choose;
    private readonly Stack<(string Title, IReadOnlyList<MenuEntry> Entries)> _path = new();
    private readonly ABottomSheetDialog _dialog;
    private readonly ALinearLayout _list;
    private readonly float _density;

    /// <summary>Creates the sheet (not shown yet).</summary>
    internal MenuSheet(CodeBrixActivity activity, IReadOnlyList<MenuEntry> entries, Action<MenuEntry> choose)
    {
        _activity = activity;
        _root = entries;
        _choose = choose;
        _density = activity.Resources?.DisplayMetrics?.Density ?? 1f;
        _dialog = new ABottomSheetDialog(activity);
        _list = new ALinearLayout(activity) { Orientation = AOrientation.Vertical };
        _list.SetPadding(0, Px(8), 0, Px(24));
        var scroll = new ANestedScrollView(activity);
        scroll.AddView(_list, new AViewGroup.LayoutParams(AViewGroup.LayoutParams.MatchParent, AViewGroup.LayoutParams.WrapContent));
        _dialog.SetContentView(scroll);
        _dialog.DismissEvent += (_, _) => Dismissed?.Invoke();
        Fill(_root, null);
    }

    /// <summary>Raised once the sheet is gone (chosen, cancelled or closed from Core).</summary>
    internal event Action Dismissed;

    /// <summary>The rows shown now (for tests: text and enabled state).</summary>
    internal IEnumerable<AView> Rows => Enumerable.Range(0, _list.ChildCount).Select(_list.GetChildAt);

    /// <summary>The bottom sheet dialog.</summary>
    internal ABottomSheetDialog Dialog => _dialog;

    /// <summary>Shows the sheet.</summary>
    internal void Show() => _dialog.Show();

    /// <summary>Closes the sheet (no choice).</summary>
    internal void Dismiss() => _dialog.Dismiss();

    /// <summary>Cancels the sheet as a swipe down / scrim tap does.</summary>
    internal void Cancel() => _dialog.Cancel();

    /// <summary>Clicks the row whose text is <paramref name="text"/>, descending into sub-menus to find it.</summary>
    /// <returns>False when no enabled row has that text.</returns>
    internal bool PerformItem(string text)
    {
        var path = FindPath(_root, text);
        if (path == null)
        {
            return false;
        }

        foreach (var step in path)
        {
            var row = Rows.OfType<ACheckedTextView>().FirstOrDefault(r => r.Tag is EntryTag tag && ReferenceEquals(tag.Entry, step));
            if (row is not { Enabled: true } || !row.PerformClick())
            {
                return false;
            }
        }

        return true;
    }

    private static List<MenuEntry> FindPath(IReadOnlyList<MenuEntry> entries, string text)
    {
        foreach (var entry in entries)
        {
            if (entry.Kind == MenuEntryKind.SubMenu)
            {
                if (FindPath(entry.Children, text) is { } below)
                {
                    below.Insert(0, entry);
                    return below;
                }
            }
            else if (entry.Kind != MenuEntryKind.Separator && entry.Text == text)
            {
                return new List<MenuEntry> { entry };
            }
        }

        return null;
    }

    private void Fill(IReadOnlyList<MenuEntry> entries, string parentTitle)
    {
        _list.RemoveAllViews();
        if (parentTitle != null)
        {
            var up = CreateRow("‹  " + parentTitle, enabled: true, checkMark: 0);
            up.Click += (_, _) =>
            {
                _path.Pop();
                var (title, above) = _path.Count > 0 ? _path.Peek() : (null, _root);
                Fill(above, title);
            };
            _list.AddView(up);
            _list.AddView(new AMaterialDivider(_activity));
        }

        var placed = MenuLayout.Place(entries);
        var checkedRadios = MenuLayout.CheckedRadioEntries(placed);
        var lastGroup = placed.Count > 0 ? placed[0].GroupId : 0;
        foreach (var p in placed)
        {
            if (p.GroupId != lastGroup)
            {
                _list.AddView(new AMaterialDivider(_activity));
                lastGroup = p.GroupId;
            }

            var entry = p.Entry;
            var checkMark = entry.Kind switch
            {
                MenuEntryKind.Toggle => AAttribute.ListChoiceIndicatorMultiple,
                MenuEntryKind.Radio => AAttribute.ListChoiceIndicatorSingle,
                _ => 0,
            };
            var text = entry.Kind == MenuEntryKind.SubMenu ? entry.Text + "  ›" : entry.Text;
            var row = CreateRow(text, entry.IsEnabled, checkMark);
            row.Tag = new EntryTag(entry);
            row.Checked = entry.Kind == MenuEntryKind.Toggle ? entry.IsChecked : checkedRadios.Contains(entry);
            row.Click += (_, _) => OnRowClick(entry);
            _list.AddView(row);
        }
    }

    private void OnRowClick(MenuEntry entry)
    {
        if (entry.Kind == MenuEntryKind.SubMenu)
        {
            _path.Push((entry.Text, entry.Children));
            Fill(entry.Children, entry.Text);
            return;
        }

        _choose(entry);
        _dialog.Dismiss();
    }

    private ACheckedTextView CreateRow(string text, bool enabled, int checkMark)
    {
        var row = new ACheckedTextView(_activity)
        {
            Text = text,
            Enabled = enabled,
            Clickable = true,
            Focusable = true,
            Gravity = AGravityFlags.CenterVertical | AGravityFlags.Start,
        };
        row.SetMinHeight(Px(RowHeightDips));
        row.SetPadding(Px(RowPaddingDips), 0, Px(RowPaddingDips), 0);
        row.SetTextSize(AComplexUnitType.Sp, 16);
        row.SetSingleLine(true);
        row.Ellipsize = ATextUtilsTruncateAt.End;
        if (ResolveColor(AAttribute.TextColorPrimary) is { } color)
        {
            row.SetTextColor(color);
        }

        if (checkMark != 0 && ResolveResource(checkMark) is { } drawable)
        {
            row.SetCheckMarkDrawable(drawable);
        }

        if (ResolveResource(AAttribute.SelectableItemBackground) is { } background)
        {
            row.SetBackgroundResource(background);
        }

        if (!enabled)
        {
            row.Alpha = 0.38f;
        }

        row.LayoutParameters = new AViewGroup.LayoutParams(AViewGroup.LayoutParams.MatchParent, AViewGroup.LayoutParams.WrapContent);
        return row;
    }

    private int Px(int dips) => (int)Math.Round(dips * _density);

    private int? ResolveResource(int attribute)
    {
        var value = new ATypedValue();
        return _activity.Theme?.ResolveAttribute(attribute, value, true) == true && value.ResourceId != 0 ? value.ResourceId : null;
    }

    private global::Android.Content.Res.ColorStateList ResolveColor(int attribute)
    {
        var value = new ATypedValue();
        if (_activity.Theme?.ResolveAttribute(attribute, value, true) != true)
        {
            return null;
        }

        return value.ResourceId != 0
            ? _activity.GetColorStateList(value.ResourceId)
            : global::Android.Content.Res.ColorStateList.ValueOf(new AColor(value.Data));
    }

    private sealed class EntryTag : global::Java.Lang.Object
    {
        internal EntryTag(MenuEntry entry) => Entry = entry;

        internal MenuEntry Entry { get; }
    }
}
#endif
