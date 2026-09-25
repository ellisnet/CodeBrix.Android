using System;
using CodeBrix.Android.UI.Composition.Android;
using CodeBrix.Platform.UI.Contracts;
using CodeBrix.Platform.UI.Xaml.Media;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;
using AColor = global::Android.Graphics.Color;
using AComplexUnitType = global::Android.Util.ComplexUnitType;
using AContext = global::Android.Content.Context;
using AGradientDrawable = global::Android.Graphics.Drawables.GradientDrawable;
using AGravityFlags = global::Android.Views.GravityFlags;
using AImageView = global::Android.Widget.ImageView;
using AShapeableImageView = Google.Android.Material.ImageView.ShapeableImageView;
using AShapeAppearanceModel = Google.Android.Material.Shape.ShapeAppearanceModel;
using AShapeType = global::Android.Graphics.Drawables.ShapeType;
using ATextView = global::Android.Widget.TextView;
using AViewGroup = global::Android.Views.ViewGroup;
using AViewStates = global::Android.Views.ViewStates;

namespace CodeBrix.Android.UI.Handlers;

/// <summary>
/// AP10-B: the native view of a PersonPicture: a circle (Material container colour) holding the initials or the
/// contact / group glyph, the profile picture in a circular ShapeableImageView over it, and the badge (number, text or
/// glyph) on a smaller circle at the top right - WinUI's geometry (the badge is half the picture's diameter).
/// </summary>
internal sealed class PersonPictureView : AViewGroup
{
    /// <summary>Creates the view.</summary>
    /// <param name="context">A Material 3 context.</param>
    internal PersonPictureView(AContext context)
        : base(context)
    {
        Circle = new AGradientDrawable();
        Circle.SetShape(AShapeType.Oval);
        Background = Circle;
        Initials = new ATextView(context) { Gravity = AGravityFlags.Center };
        Initials.SetIncludeFontPadding(false);
        Initials.SetMaxLines(1);
        Picture = new AShapeableImageView(context) { Visibility = AViewStates.Gone };
        Picture.SetScaleType(AImageView.ScaleType.CenterCrop);
        Picture.ShapeAppearanceModel = AShapeAppearanceModel.InvokeBuilder().SetAllCornerSizes(new Google.Android.Material.Shape.RelativeCornerSize(0.5f)).Build();
        BadgeCircle = new AGradientDrawable();
        BadgeCircle.SetShape(AShapeType.Oval);
        Badge = new ATextView(context) { Gravity = AGravityFlags.Center, Visibility = AViewStates.Gone, Background = BadgeCircle };
        Badge.SetIncludeFontPadding(false);
        Badge.SetMaxLines(1);
        AddView(Initials);
        AddView(Picture);
        AddView(Badge);
    }

    /// <summary>The circle behind the initials.</summary>
    internal AGradientDrawable Circle { get; }

    /// <summary>The initials (or the contact / group glyph).</summary>
    internal ATextView Initials { get; }

    /// <summary>The profile picture.</summary>
    internal AShapeableImageView Picture { get; }

    /// <summary>The badge (number, text or glyph).</summary>
    internal ATextView Badge { get; }

    /// <summary>The circle behind the badge.</summary>
    internal AGradientDrawable BadgeCircle { get; }

    /// <inheritdoc />
    protected override void OnMeasure(int widthMeasureSpec, int heightMeasureSpec)
    {
        var width = MeasureSpec.GetSize(widthMeasureSpec);
        var height = MeasureSpec.GetSize(heightMeasureSpec);
        SetMeasuredDimension(width, height);
        var side = Math.Min(width, height);
        var exact = MeasureSpec.MakeMeasureSpec(side, global::Android.Views.MeasureSpecMode.Exactly);
        Initials.Measure(exact, exact);
        Picture.Measure(exact, exact);
        var badge = MeasureSpec.MakeMeasureSpec(side / 2, global::Android.Views.MeasureSpecMode.Exactly);
        Badge.Measure(badge, badge);
    }

    /// <inheritdoc />
    protected override void OnLayout(bool changed, int l, int t, int r, int b)
    {
        var width = r - l;
        var height = b - t;
        var side = Math.Min(width, height);
        var left = (width - side) / 2;
        var top = (height - side) / 2;
        Initials.Layout(left, top, left + side, top + side);
        Picture.Layout(left, top, left + side, top + side);
        Badge.Layout(left + side - (side / 2), top, left + side, top + (side / 2));
    }
}

/// <summary>
/// AP10-B: the handler of PersonPicture (tsv row PersonPicture: "ShapeableImageView (circle) + initials TextView").
/// ProfilePicture (any ImageSource Core can open: the view shows the Android bitmap Core decoded for it), Initials /
/// DisplayName (the WinUI initials rule, <see cref="StatusMath.Initials"/>), IsGroup (the group glyph), the contact
/// glyph when there is nothing else, and the badge (BadgeText, BadgeNumber, BadgeGlyph / BadgeImageSource's glyph).
/// Material colours: the circle is colorSecondaryContainer with colorOnSecondaryContainer text, the badge colorPrimary
/// with colorOnPrimary - unless the app set Background / Foreground itself. Core sizes the control (100 x 100 when
/// nothing constrains it, as the Fluent style does).
/// </summary>
internal sealed class PersonPictureHandler : ViewHandler<PersonPicture, PersonPictureView>
{
    private const string ContactGlyph = "\uE77B";
    private const string GroupGlyph = "\uE716";

    /// <summary>PersonPicture's mapper.</summary>
    public static readonly PropertyMapper<PersonPicture, PersonPictureHandler> Mapper = new(ViewMappers.ViewMapper)
    {
        [PersonPicture.DisplayNameProperty] = MapContent,
        [PersonPicture.InitialsProperty] = MapContent,
        [PersonPicture.IsGroupProperty] = MapContent,
        [PersonPicture.ProfilePictureProperty] = MapPicture,
        [PersonPicture.BadgeNumberProperty] = MapContent,
        [PersonPicture.BadgeTextProperty] = MapContent,
        [PersonPicture.BadgeGlyphProperty] = MapContent,
        [Control.BackgroundProperty] = MapContent,
        [Control.ForegroundProperty] = MapContent,
        [Control.FontFamilyProperty] = MapContent,
    };

    private IDisposable _pictureSubscription;
    private ImageSource _subscribed;

    /// <summary>Creates the handler.</summary>
    public PersonPictureHandler()
        : base(Mapper)
    {
    }

    /// <inheritdoc />
    public override ElementHandlerCapabilities Capabilities => ElementHandlerCapabilities.OwnsVisuals | ElementHandlerCapabilities.MeasuresNatively;

    /// <summary>The handler, or the templated fallback for a re-templated picture.</summary>
    /// <param name="element">The PersonPicture.</param>
    /// <returns>The handler.</returns>
    internal static IAndroidElementHandler Create(UIElement element) =>
        element is PersonPicture picture && NativeControlPolicy.IsNative(picture, typeof(PersonPicture), new[] { "DefaultPersonPictureStyle" }, out _)
            ? new PersonPictureHandler()
            : new TemplatedFallbackHandler();

    /// <summary>Maps the initials / glyph, the colours and the badge.</summary>
    public static void MapContent(PersonPictureHandler handler, PersonPicture element)
    {
        var view = handler.PlatformView;
        var context = view.Context;
        var density = handler.Density;
        var diameter = handler.HasArranged ? Math.Min(handler.ArrangedRect.Width, handler.ArrangedRect.Height) : 100;
        var fill = ThemeResources.IsLocal(element, Control.BackgroundProperty) && ThemeResources.ColorOf(element.Background) is int b
            ? b
            : PagingWidgets.Role(context, "colorSecondaryContainer", unchecked((int)0xFFE8DEF8));
        var foreground = ThemeResources.IsLocal(element, Control.ForegroundProperty) && ThemeResources.ColorOf(element.Foreground) is int f
            ? f
            : PagingWidgets.Role(context, "colorOnSecondaryContainer", unchecked((int)0xFF1D192B));
        view.Circle.SetColor(new AColor(fill));

        var initials = element.IsGroup ? string.Empty : StatusMath.Initials(element.Initials, element.DisplayName);
        var glyph = initials.Length == 0;
        var symbolFont = ThemeResources.TryFind(element, "SymbolThemeFontFamily", out var symbol) && symbol is FontFamily family ? family : new FontFamily("Segoe Fluent Icons");
        view.Initials.Text = glyph ? (element.IsGroup ? GroupGlyph : ContactGlyph) : initials;
        view.Initials.Typeface = global::CodeBrix.Android.UI.Android.AndroidPlatformBootstrap.Fonts?.Resolve(glyph ? symbolFont : element.FontFamily, Microsoft.UI.Text.FontWeights.SemiBold, Windows.UI.Text.FontStyle.Normal, Windows.UI.Text.FontStretch.Normal);
        view.Initials.SetTextColor(new AColor(foreground));
        view.Initials.SetTextSize(AComplexUnitType.Px, (float)(StatusMath.InitialsFontSize(diameter) * density));
        view.ContentDescription = string.IsNullOrEmpty(element.DisplayName) ? (element.IsGroup ? "Group" : "Person") : element.DisplayName;

        var badgeText = StatusMath.PictureBadgeText(element.BadgeText, element.BadgeNumber);
        var badgeGlyph = badgeText == null && !string.IsNullOrEmpty(element.BadgeGlyph) ? element.BadgeGlyph : null;
        if (badgeText == null && badgeGlyph == null)
        {
            view.Badge.Visibility = AViewStates.Gone;
        }
        else
        {
            view.Badge.Visibility = AViewStates.Visible;
            view.BadgeCircle.SetColor(new AColor(PagingWidgets.Role(context, "colorPrimary", unchecked((int)0xFF6750A4))));
            view.Badge.SetTextColor(new AColor(PagingWidgets.Role(context, "colorOnPrimary", unchecked((int)0xFFFFFFFF))));
            view.Badge.Text = badgeText ?? badgeGlyph;
            view.Badge.Typeface = global::CodeBrix.Android.UI.Android.AndroidPlatformBootstrap.Fonts?.Resolve(badgeGlyph != null ? symbolFont : element.FontFamily, Microsoft.UI.Text.FontWeights.SemiBold, Windows.UI.Text.FontStyle.Normal, Windows.UI.Text.FontStretch.Normal);
            view.Badge.SetTextSize(AComplexUnitType.Px, (float)(diameter * 0.25 * density));
        }

        view.RequestLayout();
    }

    /// <summary>Maps ProfilePicture: the bitmap Core opens for it.</summary>
    public static void MapPicture(PersonPictureHandler handler, PersonPicture element) => handler.Follow(element.ProfilePicture);

    /// <summary>True while a profile picture is shown.</summary>
    internal bool ShowsPicture => PlatformView?.Picture.Visibility == AViewStates.Visible;

    /// <inheritdoc />
    public override Size Measure(Size availableSize)
    {
        var width = double.IsInfinity(availableSize.Width) ? 100 : availableSize.Width;
        var height = double.IsInfinity(availableSize.Height) ? 100 : availableSize.Height;
        var side = Math.Min(width, height);
        return new Size(side, side);
    }

    /// <inheritdoc />
    protected override PersonPictureView CreatePlatformView() => new(MaterialWidgets.Material3(Context));

    /// <inheritdoc />
    protected override void DisconnectHandler(PersonPictureView platformView)
    {
        Follow(null);
        base.DisconnectHandler(platformView);
    }

    /// <inheritdoc />
    protected override void OnArrangedSizeChanged(Size size)
    {
        base.OnArrangedSizeChanged(size);
        if (Element is PersonPicture element)
        {
            MapContent(this, element);
        }
    }

    private void Follow(ImageSource source)
    {
        if (ReferenceEquals(source, _subscribed))
        {
            return;
        }

        _pictureSubscription?.Dispose();
        _pictureSubscription = null;
        _subscribed = source;
        ShowBitmap(null);
        if (source != null)
        {
            // Core opens the source (BitmapImage URI, stream, WriteableBitmap) into a composition surface whose platform
            // object is the Android bitmap (IImagingPlatform, BitmapCompositionSurfacePlatform).
            _pictureSubscription = source.Subscribe(OnPictureOpened);
        }
    }

    private void OnPictureOpened(ImageData data)
    {
        var bitmap = data.CompositionSurface is PlatformCompositionSurface { Platform: BitmapCompositionSurfacePlatform { Bitmap: { } b } } && b.Handle != IntPtr.Zero ? b : null;
        ShowBitmap(bitmap);
    }

    private void ShowBitmap(global::Android.Graphics.Bitmap bitmap)
    {
        if (PlatformView is not { } view)
        {
            return;
        }

        view.Picture.SetImageBitmap(bitmap);
        view.Picture.Visibility = bitmap == null ? AViewStates.Gone : AViewStates.Visible;
    }
}
