using System;
using System.Globalization;
using System.Windows.Input;
using CodeBrix.Platform.UI.Contracts;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Foundation;
using AEditText = AndroidX.AppCompat.Widget.AppCompatEditText;
using AGravityFlags = global::Android.Views.GravityFlags;
using AImeAction = global::Android.Views.InputMethods.ImeAction;
using AInputTypes = global::Android.Text.InputTypes;
using ALinearLayout = global::Android.Widget.LinearLayout;
using AMaterialButton = Google.Android.Material.Button.MaterialButton;
using APopupMenu = AndroidX.AppCompat.Widget.PopupMenu;
using ATextView = global::Android.Widget.TextView;
using AView = global::Android.Views.View;
using AViewStates = global::Android.Views.ViewStates;

namespace CodeBrix.Android.UI.Handlers;

/// <summary>The native view of a PagerControl: first / previous buttons, the page selector, next / last buttons.</summary>
internal sealed class PagerControlView : ALinearLayout
{
    /// <summary>Creates the view.</summary>
    /// <param name="context">A Material 3 context.</param>
    /// <param name="density">Pixels per DIP.</param>
    internal PagerControlView(global::Android.Content.Context context, double density)
        : base(context)
    {
        Orientation = global::Android.Widget.Orientation.Horizontal;
        SetGravity(AGravityFlags.CenterVertical);
        First = PagingWidgets.IconButton(context, PagingWidgets.First, "First page", density);
        Previous = PagingWidgets.IconButton(context, PagingWidgets.ChevronLeft, "Previous page", density);
        Prefix = new ATextView(context);
        Selector = new ALinearLayout(context) { Orientation = global::Android.Widget.Orientation.Horizontal };
        Selector.SetGravity(AGravityFlags.CenterVertical);
        Suffix = new ATextView(context);
        Next = PagingWidgets.IconButton(context, PagingWidgets.ChevronRight, "Next page", density);
        Last = PagingWidgets.IconButton(context, PagingWidgets.Last, "Last page", density);
        var gap = MaterialWidgets.Px(4, density);
        Prefix.SetPadding(gap, 0, gap, 0);
        Suffix.SetPadding(gap, 0, gap, 0);
        AddView(First);
        AddView(Previous);
        AddView(Prefix);
        AddView(Selector);
        AddView(Suffix);
        AddView(Next);
        AddView(Last);
    }

    /// <summary>The first-page button.</summary>
    internal AMaterialButton First { get; }

    /// <summary>The previous-page button.</summary>
    internal AMaterialButton Previous { get; }

    /// <summary>The prefix text ("Page").</summary>
    internal ATextView Prefix { get; }

    /// <summary>The selector host (drop-down button, number field or number buttons).</summary>
    internal ALinearLayout Selector { get; }

    /// <summary>The suffix text ("of N").</summary>
    internal ATextView Suffix { get; }

    /// <summary>The next-page button.</summary>
    internal AMaterialButton Next { get; }

    /// <summary>The last-page button.</summary>
    internal AMaterialButton Last { get; }
}

/// <summary>
/// AP10-A: the handler of PagerControl (tsv row PagerControl: "composed MaterialButtons + TextView"). A native row of
/// Material icon buttons (first, previous, next, last; per their PagerControlButtonVisibility: disabled at the edge
/// when Visible, invisible at the edge when HiddenOnEdge, gone when Hidden) around the page selector of the
/// DisplayMode (<see cref="PagingMath.Selector"/>): a drop-down button listing the pages (a Material popup menu), a
/// number field, or a panel of page-number buttons with ellipses. PrefixText / SuffixText frame the drop-down and the
/// number field ("Page" / "of N" when empty, as the Fluent template's resources say). Every choice sets
/// SelectedPageIndex (Core raises SelectedIndexChanged) and the buttons also run their First/Previous/Next/Last
/// ButtonCommand, as the template's buttons do.
/// </summary>
internal sealed class PagerControlHandler : ViewHandler<PagerControl, PagerControlView>
{
    /// <summary>PagerControl's mapper.</summary>
    public static readonly PropertyMapper<PagerControl, PagerControlHandler> Mapper = new(ViewMappers.ViewMapper)
    {
        [PagerControl.NumberOfPagesProperty] = MapPager,
        [PagerControl.SelectedPageIndexProperty] = MapPager,
        [PagerControl.DisplayModeProperty] = MapPager,
        [PagerControl.PrefixTextProperty] = MapPager,
        [PagerControl.SuffixTextProperty] = MapPager,
        [PagerControl.FirstButtonVisibilityProperty] = MapPager,
        [PagerControl.PreviousButtonVisibilityProperty] = MapPager,
        [PagerControl.NextButtonVisibilityProperty] = MapPager,
        [PagerControl.LastButtonVisibilityProperty] = MapPager,
        [Control.IsEnabledProperty] = MapPager,
    };

    private PagerSelectorKind? _kind;
    private AMaterialButton _dropDown;
    private AEditText _field;

    /// <summary>Creates the handler.</summary>
    public PagerControlHandler()
        : base(Mapper)
    {
    }

    /// <inheritdoc />
    public override ElementHandlerCapabilities Capabilities => ElementHandlerCapabilities.OwnsVisuals | ElementHandlerCapabilities.MeasuresNatively;

    /// <summary>The selector shown now.</summary>
    internal PagerSelectorKind? SelectorKind => _kind;

    /// <summary>The drop-down button (DropDown mode).</summary>
    internal AMaterialButton DropDown => _dropDown;

    /// <summary>The number field (NumberField mode).</summary>
    internal AEditText Field => _field;

    /// <summary>The handler, or the templated fallback for a re-templated pager.</summary>
    /// <param name="element">The PagerControl.</param>
    /// <returns>The handler.</returns>
    internal static IAndroidElementHandler Create(UIElement element) =>
        element is PagerControl pager && NativeControlPolicy.IsNative(pager, typeof(PagerControl), Array.Empty<string>(), out _)
            ? new PagerControlHandler()
            : new TemplatedFallbackHandler();

    /// <summary>Maps every property.</summary>
    public static void MapPager(PagerControlHandler handler, PagerControl element)
    {
        handler.Refresh();
        element.InvalidateMeasure();
    }

    /// <summary>Chooses a page as a finger does (the drop-down menu, the field or a number button).</summary>
    /// <param name="page">The page index.</param>
    internal void Choose(int page)
    {
        if (Element is PagerControl pager && page >= 0 && (pager.NumberOfPages < 0 || page < pager.NumberOfPages))
        {
            pager.SelectedPageIndex = page;
        }
    }

    /// <inheritdoc />
    public override Size Measure(Size availableSize) => ViewHandlerExtensions.GetDesiredSizeFromView(PlatformView, availableSize, Density);

    /// <inheritdoc />
    protected override PagerControlView CreatePlatformView() => new(MaterialWidgets.Material3(Context), Density);

    /// <inheritdoc />
    protected override void ConnectHandler(PagerControlView platformView)
    {
        base.ConnectHandler(platformView);
        platformView.First.Click += OnFirst;
        platformView.Previous.Click += OnPrevious;
        platformView.Next.Click += OnNext;
        platformView.Last.Click += OnLast;
    }

    /// <inheritdoc />
    protected override void DisconnectHandler(PagerControlView platformView)
    {
        platformView.First.Click -= OnFirst;
        platformView.Previous.Click -= OnPrevious;
        platformView.Next.Click -= OnNext;
        platformView.Last.Click -= OnLast;
        platformView.Selector.RemoveAllViews();
        _dropDown = null;
        _field = null;
        _kind = null;
        base.DisconnectHandler(platformView);
    }

    private void Refresh()
    {
        if (Element is not PagerControl pager || PlatformView is not { } view)
        {
            return;
        }

        var pages = pager.NumberOfPages;
        var selected = pager.SelectedPageIndex;
        var kind = PagingMath.Selector(pager.DisplayMode.ToString(), pages);
        if (kind != _kind)
        {
            _kind = kind;
            view.Selector.RemoveAllViews();
            _dropDown = null;
            _field = null;
            if (kind == PagerSelectorKind.DropDown)
            {
                var style = MaterialWidgets.AttrId(view.Context, "materialButtonOutlinedStyle");
                _dropDown = style != 0 ? new AMaterialButton(view.Context, null, style) : new AMaterialButton(view.Context);
                _dropDown.SetAllCaps(false);
                _dropDown.InsetTop = 0;
                _dropDown.InsetBottom = 0;
                _dropDown.Icon = IconDrawables.Create(new FontIconSource { Glyph = PagingWidgets.ChevronDown }, view.Context, Density, PagingWidgets.Role(view.Context, "colorOnSurfaceVariant", unchecked((int)0xFF49454F)), 12);
                _dropDown.IconGravity = AMaterialButton.IconGravityTextEnd;
                _dropDown.Click += OnDropDown;
                view.Selector.AddView(_dropDown);
            }
            else if (kind == PagerSelectorKind.NumberField)
            {
                _field = new AEditText(view.Context)
                {
                    InputType = AInputTypes.ClassNumber,
                    ImeOptions = AImeAction.Done,
                };
                _field.SetSingleLine(true);
                _field.SetMinEms(3);
                _field.Gravity = AGravityFlags.Center;
                _field.EditorAction += OnFieldAction;
                _field.FocusChange += OnFieldFocus;
                view.Selector.AddView(_field);
            }
        }

        var textual = kind != PagerSelectorKind.ButtonPanel;
        view.Prefix.Visibility = textual ? AViewStates.Visible : AViewStates.Gone;
        view.Suffix.Visibility = textual ? AViewStates.Visible : AViewStates.Gone;
        view.Prefix.Text = string.IsNullOrEmpty(pager.PrefixText) ? "Page" : pager.PrefixText;
        view.Suffix.Text = !string.IsNullOrEmpty(pager.SuffixText) ? pager.SuffixText
            : pages > 0 ? "of " + pages.ToString(CultureInfo.CurrentCulture) : string.Empty;

        var number = (selected + 1).ToString(CultureInfo.CurrentCulture);
        if (_dropDown != null)
        {
            _dropDown.Text = number;
            _dropDown.Enabled = pager.IsEnabled;
        }

        if (_field != null && !_field.HasFocus)
        {
            _field.Text = number;
            _field.Enabled = pager.IsEnabled;
        }

        if (kind == PagerSelectorKind.ButtonPanel)
        {
            FillPanel(view, pager, pages, selected);
        }

        var enabled = pager.IsEnabled;
        EdgeButton(view.First, pager.FirstButtonVisibility, selected > 0, enabled);
        EdgeButton(view.Previous, pager.PreviousButtonVisibility, selected > 0, enabled);
        EdgeButton(view.Next, pager.NextButtonVisibility, pages < 0 || selected < pages - 1, enabled);
        EdgeButton(view.Last, pager.LastButtonVisibility, pages > 0 && selected < pages - 1, enabled);
    }

    private void FillPanel(PagerControlView view, PagerControl pager, int pages, int selected)
    {
        foreach (var existing in Children(view.Selector))
        {
            if (existing is AMaterialButton old)
            {
                old.Click -= OnNumber;
            }
        }

        view.Selector.RemoveAllViews();
        var accent = PagingWidgets.Role(view.Context, "colorPrimary", unchecked((int)0xFF6750A4));
        foreach (var page in PagingMath.PanelNumbers(pages, selected))
        {
            if (page == PagingMath.Ellipsis)
            {
                view.Selector.AddView(new ATextView(view.Context) { Text = "…" });
                continue;
            }

            var button = PagingWidgets.TextButton(view.Context, (page + 1).ToString(CultureInfo.CurrentCulture), Density);
            button.Enabled = pager.IsEnabled;
            button.Selected = page == selected;
            button.ContentDescription = "Page " + (page + 1).ToString(CultureInfo.CurrentCulture);
            if (page == selected)
            {
                button.SetTextColor(new global::Android.Graphics.Color(accent));
                button.Typeface = global::Android.Graphics.Typeface.Create(button.Typeface, global::Android.Graphics.TypefaceStyle.Bold);
            }

            button.Click += OnNumber;
            view.Selector.AddView(button);
        }
    }

    private static System.Collections.Generic.IEnumerable<AView> Children(ALinearLayout group)
    {
        for (var i = 0; i < group.ChildCount; i++)
        {
            yield return group.GetChildAt(i);
        }
    }

    private static void EdgeButton(AMaterialButton button, PagerControlButtonVisibility visibility, bool canMove, bool enabled)
    {
        button.Visibility = visibility switch
        {
            PagerControlButtonVisibility.Hidden => AViewStates.Gone,
            PagerControlButtonVisibility.HiddenOnEdge when !canMove => AViewStates.Invisible,
            _ => AViewStates.Visible,
        };
        button.Enabled = enabled && canMove;
    }

    private void OnNumber(object sender, EventArgs e)
    {
        if (sender is AMaterialButton button && int.TryParse(button.Text, NumberStyles.Integer, CultureInfo.CurrentCulture, out var number))
        {
            Choose(number - 1);
        }
    }

    private void OnDropDown(object sender, EventArgs e)
    {
        if (Element is not PagerControl pager || _dropDown == null)
        {
            return;
        }

        var menu = new APopupMenu(_dropDown.Context, _dropDown);
        for (var i = 0; i < Math.Max(0, pager.NumberOfPages); i++)
        {
            menu.Menu.Add(0, i, i, new Java.Lang.String((i + 1).ToString(CultureInfo.CurrentCulture)));
        }

        menu.MenuItemClick += (_, args) =>
        {
            args.Handled = true;
            Choose(args.Item.ItemId);
        };
        LastMenu = menu;
        menu.Show();
    }

    /// <summary>The page menu the drop-down button showed last (tests reach its items).</summary>
    internal APopupMenu LastMenu { get; private set; }

    private void OnFieldAction(object sender, ATextView.EditorActionEventArgs e)
    {
        e.Handled = false;
        if (e.ActionId == AImeAction.Done)
        {
            CommitField();
        }
    }

    private void OnFieldFocus(object sender, AView.FocusChangeEventArgs e)
    {
        if (!e.HasFocus)
        {
            CommitField();
        }
    }

    /// <summary>Commits the number field's text (as the IME's Done does).</summary>
    internal void CommitField()
    {
        if (_field == null || Element is not PagerControl pager)
        {
            return;
        }

        if (int.TryParse(_field.Text, NumberStyles.Integer, CultureInfo.CurrentCulture, out var number))
        {
            Choose(number - 1);
        }

        _field.Text = (pager.SelectedPageIndex + 1).ToString(CultureInfo.CurrentCulture);
    }

    private void OnFirst(object sender, EventArgs e) => Go(0, (Element as PagerControl)?.FirstButtonCommand);

    private void OnPrevious(object sender, EventArgs e) => Go(((Element as PagerControl)?.SelectedPageIndex ?? 0) - 1, (Element as PagerControl)?.PreviousButtonCommand);

    private void OnNext(object sender, EventArgs e) => Go(((Element as PagerControl)?.SelectedPageIndex ?? 0) + 1, (Element as PagerControl)?.NextButtonCommand);

    private void OnLast(object sender, EventArgs e) => Go(((Element as PagerControl)?.NumberOfPages ?? 0) - 1, (Element as PagerControl)?.LastButtonCommand);

    private void Go(int page, ICommand command)
    {
        Choose(page);
        if (command?.CanExecute(null) == true)
        {
            command.Execute(null);
        }
    }
}
