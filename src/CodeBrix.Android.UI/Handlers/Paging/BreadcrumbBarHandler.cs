using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using CodeBrix.Platform.UI.Contracts;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Foundation;
using AGravityFlags = global::Android.Views.GravityFlags;
using AHorizontalScrollView = global::Android.Widget.HorizontalScrollView;
using AImageView = global::Android.Widget.ImageView;
using ALinearLayout = global::Android.Widget.LinearLayout;
using AMaterialButton = Google.Android.Material.Button.MaterialButton;
using ATextView = global::Android.Widget.TextView;
using ATypeface = global::Android.Graphics.Typeface;
using ATypefaceStyle = global::Android.Graphics.TypefaceStyle;

namespace CodeBrix.Android.UI.Handlers;

/// <summary>The native view of a BreadcrumbBar: a horizontal scroll view over a row of crumbs and chevrons.</summary>
internal sealed class BreadcrumbBarView : AHorizontalScrollView
{
    /// <summary>Creates the view.</summary>
    /// <param name="context">A Material 3 context.</param>
    internal BreadcrumbBarView(global::Android.Content.Context context)
        : base(context)
    {
        HorizontalScrollBarEnabled = false;
        FillViewport = false;
        Row = new ALinearLayout(context) { Orientation = global::Android.Widget.Orientation.Horizontal };
        Row.SetGravity(AGravityFlags.CenterVertical);
        AddView(Row);
    }

    /// <summary>The row of crumbs.</summary>
    internal ALinearLayout Row { get; }
}

/// <summary>
/// AP10-A: the handler of BreadcrumbBar and its BreadcrumbBarItem (tsv rows BreadcrumbBar, BreadcrumbBarItem:
/// "HorizontalScrollView of text MaterialButtons with chevrons"). Every item of ItemsSource but the last is a Material
/// text button followed by a chevron; the last (the current location) is plain emphasised text; the row scrolls, kept
/// scrolled to its end (WinUI collapses the leading crumbs into an ellipsis flyout instead: the Material form scrolls).
/// A finger on a crumb raises ItemClicked with its item and index through Core (BreadcrumbBar.RaiseItemClickedEvent).
/// Collection changes re-fill the row. A bar with an ItemTemplate (element crumbs) keeps its Fluent template.
/// </summary>
internal sealed class BreadcrumbBarHandler : ViewHandler<BreadcrumbBar, BreadcrumbBarView>
{
    /// <summary>BreadcrumbBar's mapper.</summary>
    public static readonly PropertyMapper<BreadcrumbBar, BreadcrumbBarHandler> Mapper = new(ViewMappers.ViewMapper)
    {
        [BreadcrumbBar.ItemsSourceProperty] = MapItems,
        [Control.IsEnabledProperty] = MapItems,
    };

    private readonly List<object> _items = new();
    private readonly List<AMaterialButton> _buttons = new();
    private INotifyCollectionChanged _watched;

    /// <summary>Creates the handler.</summary>
    public BreadcrumbBarHandler()
        : base(Mapper)
    {
    }

    /// <inheritdoc />
    public override ElementHandlerCapabilities Capabilities => ElementHandlerCapabilities.OwnsVisuals | ElementHandlerCapabilities.MeasuresNatively;

    /// <summary>The crumb buttons shown now (every item but the last).</summary>
    internal IReadOnlyList<AMaterialButton> Buttons => _buttons;

    /// <summary>The text of every crumb shown, in order.</summary>
    internal IReadOnlyList<string> Texts
    {
        get
        {
            var texts = new List<string>();
            foreach (var item in _items)
            {
                texts.Add(TextOf(item));
            }

            return texts;
        }
    }

    /// <summary>The handler, or the templated fallback for a templated / re-templated bar.</summary>
    /// <param name="element">The BreadcrumbBar.</param>
    /// <returns>The handler.</returns>
    internal static IAndroidElementHandler Create(UIElement element) =>
        element is BreadcrumbBar bar && bar.ItemTemplate == null && NativeControlPolicy.IsNative(bar, typeof(BreadcrumbBar), Array.Empty<string>(), out _)
            ? new BreadcrumbBarHandler()
            : new TemplatedFallbackHandler();

    /// <summary>Maps ItemsSource / IsEnabled.</summary>
    public static void MapItems(BreadcrumbBarHandler handler, BreadcrumbBar element)
    {
        handler.Watch(element.ItemsSource as INotifyCollectionChanged);
        handler.Refill();
    }

    /// <inheritdoc />
    public override Size Measure(Size availableSize) =>
        ViewHandlerExtensions.GetDesiredSizeFromView(PlatformView, new Size(availableSize.Width, availableSize.Height), Density);

    /// <inheritdoc />
    protected override BreadcrumbBarView CreatePlatformView() => new(MaterialWidgets.Material3(Context));

    /// <inheritdoc />
    protected override void DisconnectHandler(BreadcrumbBarView platformView)
    {
        Watch(null);
        foreach (var button in _buttons)
        {
            button.Click -= OnCrumb;
        }

        _buttons.Clear();
        _items.Clear();
        platformView.Row.RemoveAllViews();
        base.DisconnectHandler(platformView);
    }

    private static string TextOf(object item) => item switch
    {
        null => string.Empty,
        string s => s,
        BreadcrumbBarItem crumb => crumb.Content?.ToString() ?? string.Empty,
        _ => item.ToString(),
    };

    private void Watch(INotifyCollectionChanged source)
    {
        if (ReferenceEquals(_watched, source))
        {
            return;
        }

        if (_watched != null)
        {
            _watched.CollectionChanged -= OnCollectionChanged;
        }

        _watched = source;
        if (source != null)
        {
            source.CollectionChanged += OnCollectionChanged;
        }
    }

    private void OnCollectionChanged(object sender, NotifyCollectionChangedEventArgs e) => PlatformView?.Post(Refill);

    private void Refill()
    {
        if (Element is not BreadcrumbBar bar || PlatformView is not { } view)
        {
            return;
        }

        foreach (var button in _buttons)
        {
            button.Click -= OnCrumb;
        }

        _buttons.Clear();
        _items.Clear();
        view.Row.RemoveAllViews();
        if (bar.ItemsSource is IEnumerable source)
        {
            foreach (var item in source)
            {
                _items.Add(item);
            }
        }

        var context = view.Context;
        var density = Density;
        var muted = PagingWidgets.Role(context, "colorOnSurfaceVariant", unchecked((int)0xFF49454F));
        var strong = PagingWidgets.Role(context, "colorOnSurface", unchecked((int)0xFF1B1B1F));
        for (var i = 0; i < _items.Count; i++)
        {
            var text = TextOf(_items[i]);
            if (i < _items.Count - 1)
            {
                var button = PagingWidgets.TextButton(context, text, density);
                button.Enabled = bar.IsEnabled;
                button.Click += OnCrumb;
                _buttons.Add(button);
                view.Row.AddView(button);
                var chevron = new AImageView(context);
                chevron.SetImageDrawable(IconDrawables.Create(new FontIconSource { Glyph = PagingWidgets.ChevronRight }, context, density, muted, 12));
                chevron.SetPadding(MaterialWidgets.Px(2, density), 0, MaterialWidgets.Px(2, density), 0);
                view.Row.AddView(chevron);
            }
            else
            {
                var current = new ATextView(context) { Text = text };
                current.SetTextColor(new global::Android.Graphics.Color(strong));
                current.Typeface = ATypeface.Create(current.Typeface, ATypefaceStyle.Bold);
                var h = MaterialWidgets.Px(8, density);
                current.SetPadding(h, 0, h, 0);
                view.Row.AddView(current);
            }
        }

        view.Post(() => view.FullScroll(global::Android.Views.FocusSearchDirection.Right));
        bar.InvalidateMeasure();
    }

    private void OnCrumb(object sender, EventArgs e)
    {
        if (sender is AMaterialButton button && _buttons.IndexOf(button) is var index and >= 0 && index < _items.Count && Element is BreadcrumbBar bar)
        {
            bar.RaiseItemClickedEvent(_items[index], index);
        }
    }
}
