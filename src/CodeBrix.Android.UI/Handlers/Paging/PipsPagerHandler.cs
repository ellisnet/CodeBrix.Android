// Technique from .NET MAUI, src/Core/src/Platform/Android/MauiPageControl.cs and
// src/Core/src/Handlers/IndicatorView/IndicatorViewHandler.Android.cs @ 828569a864 (a LinearLayout of indicator
// ImageViews with oval shape drawables, the selected one drawn in the selected colour, a click on an indicator
// selecting its page, a content description per indicator). Copyright (c) .NET Foundation and Contributors.
// Licensed under the MIT License. See THIRD-PARTY-NOTICES.txt.

using System.Collections.Generic;
using CodeBrix.Platform.UI.Contracts;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Foundation;
using AGravityFlags = global::Android.Views.GravityFlags;
using AImageView = global::Android.Widget.ImageView;
using ALinearLayout = global::Android.Widget.LinearLayout;
using AMaterialButton = Google.Android.Material.Button.MaterialButton;
using AOrientation = global::Android.Widget.Orientation;
using AViewStates = global::Android.Views.ViewStates;

namespace CodeBrix.Android.UI.Handlers;

/// <summary>The native view of a PipsPager: previous button, the pips, next button, in the pager's orientation.</summary>
internal sealed class PipsPagerView : ALinearLayout
{
    /// <summary>Creates the view.</summary>
    /// <param name="context">A Material 3 context.</param>
    /// <param name="density">Pixels per DIP.</param>
    internal PipsPagerView(global::Android.Content.Context context, double density)
        : base(context)
    {
        SetGravity(AGravityFlags.Center);
        Previous = PagingWidgets.IconButton(context, PagingWidgets.ChevronLeft, "Previous page", density);
        Pips = new ALinearLayout(context);
        Pips.SetGravity(AGravityFlags.Center);
        Next = PagingWidgets.IconButton(context, PagingWidgets.ChevronRight, "Next page", density);
        AddView(Previous);
        AddView(Pips);
        AddView(Next);
    }

    /// <summary>The previous-page button.</summary>
    internal AMaterialButton Previous { get; }

    /// <summary>The next-page button.</summary>
    internal AMaterialButton Next { get; }

    /// <summary>The row (or column) of pips.</summary>
    internal ALinearLayout Pips { get; }
}

/// <summary>
/// AP10-A: the handler of PipsPager (tsv row PipsPager: "MauiPageControl port"). The pager is native: a row (or
/// column, by Orientation) of Material pips - at most MaxVisiblePips, the selected page kept centred as WinUI
/// scrolls its pips (<see cref="PagingMath.PipWindow"/>) - between previous / next icon buttons shown per
/// Previous/NextButtonVisibility (VisibleOnPointerOver is shown: a finger has no hover), hidden at the ends unless
/// WrapMode is Wrap. A pip or button a finger presses sets SelectedPageIndex, and Core raises SelectedIndexChanged.
/// NormalPipStyle / SelectedPipStyle and the button styles are Fluent styles and are not applied (Material pips).
/// </summary>
internal sealed class PipsPagerHandler : ViewHandler<PipsPager, PipsPagerView>
{
    /// <summary>PipsPager's mapper.</summary>
    public static readonly PropertyMapper<PipsPager, PipsPagerHandler> Mapper = new(ViewMappers.ViewMapper)
    {
        [PipsPager.NumberOfPagesProperty] = MapPips,
        [PipsPager.SelectedPageIndexProperty] = MapPips,
        [PipsPager.MaxVisiblePipsProperty] = MapPips,
        [PipsPager.OrientationProperty] = MapPips,
        [PipsPager.WrapModeProperty] = MapPips,
        [PipsPager.PreviousButtonVisibilityProperty] = MapPips,
        [PipsPager.NextButtonVisibilityProperty] = MapPips,
        [Control.IsEnabledProperty] = MapPips,
    };

    private readonly List<AImageView> _pips = new();
    private int _first;

    /// <summary>Creates the handler.</summary>
    public PipsPagerHandler()
        : base(Mapper)
    {
    }

    /// <inheritdoc />
    public override ElementHandlerCapabilities Capabilities => ElementHandlerCapabilities.OwnsVisuals | ElementHandlerCapabilities.MeasuresNatively;

    /// <summary>The pips shown now (their page is <see cref="FirstPip"/> + their index).</summary>
    internal IReadOnlyList<AImageView> Pips => _pips;

    /// <summary>The page of the first pip shown.</summary>
    internal int FirstPip => _first;

    /// <summary>The handler, or the templated fallback for a re-templated pager.</summary>
    /// <param name="element">The PipsPager.</param>
    /// <returns>The handler.</returns>
    internal static IAndroidElementHandler Create(UIElement element) =>
        element is PipsPager pager && NativeControlPolicy.IsNative(pager, typeof(PipsPager), System.Array.Empty<string>(), out _)
            ? new PipsPagerHandler()
            : new TemplatedFallbackHandler();

    /// <summary>Maps every property: the pips and buttons are re-laid.</summary>
    public static void MapPips(PipsPagerHandler handler, PipsPager element)
    {
        handler.Refresh();
        element.InvalidateMeasure();
    }

    /// <inheritdoc />
    public override Size Measure(Size availableSize) => ViewHandlerExtensions.GetDesiredSizeFromView(PlatformView, availableSize, Density);

    /// <inheritdoc />
    protected override PipsPagerView CreatePlatformView() => new(MaterialWidgets.Material3(Context), Density);

    /// <inheritdoc />
    protected override void ConnectHandler(PipsPagerView platformView)
    {
        base.ConnectHandler(platformView);
        platformView.Previous.Click += OnPrevious;
        platformView.Next.Click += OnNext;
    }

    /// <inheritdoc />
    protected override void DisconnectHandler(PipsPagerView platformView)
    {
        platformView.Previous.Click -= OnPrevious;
        platformView.Next.Click -= OnNext;
        foreach (var pip in _pips)
        {
            pip.Click -= OnPip;
        }

        _pips.Clear();
        base.DisconnectHandler(platformView);
    }

    private void Refresh()
    {
        if (Element is not PipsPager pager || PlatformView is not { } view)
        {
            return;
        }

        var vertical = pager.Orientation == Orientation.Vertical;
        view.Orientation = vertical ? AOrientation.Vertical : AOrientation.Horizontal;
        view.Pips.Orientation = view.Orientation;
        var density = Density;
        var context = view.Context;
        view.Previous.Icon = IconDrawables.Create(new FontIconSource { Glyph = vertical ? PagingWidgets.ChevronUp : PagingWidgets.ChevronLeft }, context, density, PagingWidgets.Role(context, "colorOnSurfaceVariant", unchecked((int)0xFF49454F)), 16);
        view.Next.Icon = IconDrawables.Create(new FontIconSource { Glyph = vertical ? PagingWidgets.ChevronDown : PagingWidgets.ChevronRight }, context, density, PagingWidgets.Role(context, "colorOnSurfaceVariant", unchecked((int)0xFF49454F)), 16);

        var pages = pager.NumberOfPages;
        var selected = pager.SelectedPageIndex;
        var (first, count) = PagingMath.PipWindow(pages, pager.MaxVisiblePips, selected);
        _first = first;
        while (_pips.Count < count)
        {
            var pip = new AImageView(context) { Clickable = true };
            pip.SetScaleType(AImageView.ScaleType.Center);
            pip.Click += OnPip;
            var cell = MaterialWidgets.Px(16, density);
            view.Pips.AddView(pip, new ALinearLayout.LayoutParams(cell, cell));
            _pips.Add(pip);
        }

        while (_pips.Count > count)
        {
            var last = _pips[^1];
            last.Click -= OnPip;
            view.Pips.RemoveView(last);
            _pips.RemoveAt(_pips.Count - 1);
        }

        var normal = PagingWidgets.Role(context, "colorOnSurfaceVariant", unchecked((int)0xFF49454F));
        var accent = PagingWidgets.Role(context, "colorPrimary", unchecked((int)0xFF6750A4));
        for (var i = 0; i < _pips.Count; i++)
        {
            var page = first + i;
            var isSelected = page == selected;
            _pips[i].SetImageDrawable(PagingWidgets.Pip(MaterialWidgets.Px(isSelected ? 8 : 5, density), isSelected ? accent : normal));
            _pips[i].Selected = isSelected;
            _pips[i].ContentDescription = $"Page {page + 1} of {pages}" + (isSelected ? ", selected" : string.Empty);
            _pips[i].Enabled = pager.IsEnabled;
        }

        var wrap = pager.WrapMode == PipsPagerWrapMode.Wrap;
        Button(view.Previous, pager.PreviousButtonVisibility, PagingMath.Step(pages, selected, -1, wrap) != null, pager.IsEnabled);
        Button(view.Next, pager.NextButtonVisibility, PagingMath.Step(pages, selected, +1, wrap) != null, pager.IsEnabled);
    }

    private static void Button(AMaterialButton button, PipsPagerButtonVisibility visibility, bool canMove, bool enabled)
    {
        button.Visibility = visibility == PipsPagerButtonVisibility.Collapsed ? AViewStates.Gone
            : canMove ? AViewStates.Visible
            : AViewStates.Invisible;
        button.Enabled = enabled && canMove;
    }

    private void OnPip(object sender, System.EventArgs e)
    {
        if (sender is AImageView pip && _pips.IndexOf(pip) is var index and >= 0 && Element is PipsPager pager)
        {
            pager.SelectedPageIndex = _first + index;
        }
    }

    private void OnPrevious(object sender, System.EventArgs e) => Move(-1);

    private void OnNext(object sender, System.EventArgs e) => Move(+1);

    private void Move(int step)
    {
        if (Element is PipsPager pager && PagingMath.Step(pager.NumberOfPages, pager.SelectedPageIndex, step, pager.WrapMode == PipsPagerWrapMode.Wrap) is int page)
        {
            pager.SelectedPageIndex = page;
        }
    }
}
