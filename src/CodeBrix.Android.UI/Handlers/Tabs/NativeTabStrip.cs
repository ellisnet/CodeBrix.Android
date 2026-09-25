using System;
using System.Collections.Generic;
using CodeBrix.Android.UI.Input;
using AContext = global::Android.Content.Context;
using AMotionEvent = global::Android.Views.MotionEvent;
using ATabLayout = global::Google.Android.Material.Tabs.TabLayout;
using ATextView = global::Android.Widget.TextView;
using ALinearLayout = global::Android.Widget.LinearLayout;
using AGravityFlags = global::Android.Views.GravityFlags;

namespace CodeBrix.Android.UI.Handlers;

/// <summary>One tab of a <see cref="NativeTabStrip"/>: its label and whether it shows a close button.</summary>
/// <param name="Text">The label.</param>
/// <param name="Closable">True when the tab has a close button.</param>
internal readonly record struct TabSpec(string Text, bool Closable);

/// <summary>
/// The Material tab row of TabView, Pivot and SelectorBar (plan 3: TabLayout, scrollable, start gravity; a
/// closable tab shows a close button; an optional trailing add button). The strip only REPORTS what a
/// finger did (<see cref="TabActivated"/>, <see cref="CloseRequested"/>, <see cref="AddRequested"/>): the
/// owning handler turns that into the control's own selection / close / add paths in Core, and Core's
/// selection comes back through <see cref="SelectIndex"/>. Every touch the strip receives is marked handled
/// for Core (NativeInput.MarkHandled), so the control's Core template under it does not act on the same
/// finger.
/// </summary>
internal sealed class NativeTabStrip : ALinearLayout
{
    private readonly List<TabSpec> _tabs = new();
    private readonly ATextView _addButton;
    private bool _updating;

    /// <summary>Creates the strip.</summary>
    /// <param name="context">The context (a Material theme).</param>
    internal NativeTabStrip(AContext context)
        : base(context)
    {
        Orientation = global::Android.Widget.Orientation.Horizontal;
        Tabs = new ATabLayout(context)
        {
            TabMode = ATabLayout.ModeScrollable,
            TabGravity = ATabLayout.GravityStart,
        };
        Tabs.TabSelected += OnTabSelected;
        AddView(Tabs, new LayoutParams(0, LayoutParams.MatchParent, 1f));
        _addButton = new ATextView(context)
        {
            Text = "+",
            Gravity = AGravityFlags.Center,
            Visibility = global::Android.Views.ViewStates.Gone,
            Clickable = true,
            ContentDescription = "Add tab",
        };
        _addButton.SetTextSize(global::Android.Util.ComplexUnitType.Sp, 22);
        _addButton.Click += (_, _) => AddRequested?.Invoke();
        var size = (int)Math.Round(48 * context.Resources.DisplayMetrics.Density);
        AddView(_addButton, new LayoutParams(size, LayoutParams.MatchParent));
    }

    /// <summary>A finger selected tab <c>index</c>.</summary>
    internal event Action<int> TabActivated;

    /// <summary>A finger pressed the close button of tab <c>index</c>.</summary>
    internal event Action<int> CloseRequested;

    /// <summary>A finger pressed the add button.</summary>
    internal event Action AddRequested;

    /// <summary>The Material TabLayout.</summary>
    internal ATabLayout Tabs { get; }

    /// <summary>The tabs shown now.</summary>
    internal IReadOnlyList<TabSpec> Current => _tabs;

    /// <summary>Shows or hides the trailing add button.</summary>
    internal bool ShowsAddButton
    {
        get => _addButton.Visibility == global::Android.Views.ViewStates.Visible;
        set => _addButton.Visibility = value ? global::Android.Views.ViewStates.Visible : global::Android.Views.ViewStates.Gone;
    }

    /// <summary>Replaces the tabs when they differ from what is shown, then selects <paramref name="selected"/>.</summary>
    internal void SetTabs(IReadOnlyList<TabSpec> tabs, int selected)
    {
        tabs ??= Array.Empty<TabSpec>();
        var same = tabs.Count == _tabs.Count;
        for (var i = 0; same && i < tabs.Count; i++)
        {
            same = tabs[i] == _tabs[i];
        }

        if (!same)
        {
            _updating = true;
            try
            {
                Tabs.RemoveAllTabs();
                _tabs.Clear();
                for (var i = 0; i < tabs.Count; i++)
                {
                    var spec = tabs[i];
                    var tab = Tabs.NewTab().SetText(spec.Text ?? string.Empty);
                    if (spec.Closable)
                    {
                        tab.SetCustomView(CreateClosableTabView(spec.Text));
                    }

                    Tabs.AddTab(tab, false);
                    var pad = (int)Math.Round(8 * Context.Resources.DisplayMetrics.Density);
                    tab.View?.SetPaddingRelative(pad, 0, pad, 0);
                    _tabs.Add(spec);
                }
            }
            finally
            {
                _updating = false;
            }
        }

        SelectIndex(selected);
    }

    /// <summary>Shows tab <paramref name="index"/> as selected (no <see cref="TabActivated"/>).</summary>
    internal void SelectIndex(int index)
    {
        if (index < 0 || index >= Tabs.TabCount || Tabs.SelectedTabPosition == index)
        {
            return;
        }

        _updating = true;
        try
        {
            Tabs.GetTabAt(index)?.Select();
        }
        finally
        {
            _updating = false;
        }
    }

    /// <inheritdoc />
    protected override void OnMeasure(int widthMeasureSpec, int heightMeasureSpec)
    {
        // Scrollable (tabs at their natural width, start-aligned) when they fit the row the control's template
        // gives them, else fixed (the row shared equally): the Core layout decides the row, not the tabs.
        var available = global::Android.Views.View.MeasureSpec.GetSize(widthMeasureSpec) - (ShowsAddButton ? _addButton.LayoutParameters.Width : 0);
        if (Tabs.TabMode != ATabLayout.ModeScrollable)
        {
            Tabs.TabMode = ATabLayout.ModeScrollable;
            Tabs.TabGravity = ATabLayout.GravityStart;
        }

        Tabs.Measure(global::Android.Views.View.MeasureSpec.MakeMeasureSpec(0, global::Android.Views.MeasureSpecMode.Unspecified), heightMeasureSpec);
        var natural = Tabs.GetChildAt(0)?.MeasuredWidth ?? Tabs.MeasuredWidth;
        if (natural > available && Tabs.TabCount > 0)
        {
            Tabs.TabMode = ATabLayout.ModeFixed;
            Tabs.TabGravity = ATabLayout.GravityFill;
        }

        base.OnMeasure(widthMeasureSpec, heightMeasureSpec);
    }

    /// <inheritdoc />
    public override bool DispatchTouchEvent(AMotionEvent e)
    {
        var handled = base.DispatchTouchEvent(e);

        // The strip acts on this finger: the Core template under it must not (plan 2.15).
        NativeInput.MarkHandled();
        return handled;
    }

    private void OnTabSelected(object sender, ATabLayout.TabSelectedEventArgs e)
    {
        if (!_updating && e.Tab != null)
        {
            TabActivated?.Invoke(e.Tab.Position);
        }
    }

    private ALinearLayout CreateClosableTabView(string text)
    {
        var context = Context;
        var row = new ALinearLayout(context) { Orientation = global::Android.Widget.Orientation.Horizontal };
        row.SetGravity(AGravityFlags.CenterVertical);
        var label = new ATextView(context) { Text = text ?? string.Empty };
        label.SetMaxLines(1);
        row.AddView(label);
        var close = new ATextView(context)
        {
            Text = "×",
            Clickable = true,
            ContentDescription = "Close tab",
            Gravity = AGravityFlags.Center,
        };
        close.SetTextSize(global::Android.Util.ComplexUnitType.Sp, 18);
        var pad = (int)Math.Round(8 * context.Resources.DisplayMetrics.Density);
        close.SetPadding(pad, 0, 0, 0);
        close.Click += (_, _) =>
        {
            var position = -1;
            for (var i = 0; i < Tabs.TabCount; i++)
            {
                if (ReferenceEquals(Tabs.GetTabAt(i)?.CustomView, row))
                {
                    position = i;
                    break;
                }
            }

            if (position >= 0)
            {
                CloseRequested?.Invoke(position);
            }
        };
        row.AddView(close);
        return row;
    }
}
