using System;
using System.Collections.Generic;

namespace CodeBrix.Android.Analyzers.Surface;

/// <summary>
/// The CodeBrix.Platform types the CBAND rules look for, and the controls CodeBrix.Android shows as native Material
/// controls. Keep <see cref="NativeControls"/> in step with the native registrations of
/// src/CodeBrix.Android.UI/Handlers/CodeBrixHandlers.cs and the add-ins (the parity-score report lists them:
/// artifacts/parity/&lt;version&gt;/&lt;config&gt;/parity-declined.tsv).
/// </summary>
public static class AndroidSurface
{
    /// <summary>Controls shown by a native Material control when they look the way their default style says.</summary>
    public static readonly IReadOnlyCollection<string> NativeControls = new HashSet<string>(StringComparer.Ordinal)
    {
        "Microsoft.UI.Xaml.Controls.Button",
        "Microsoft.UI.Xaml.Controls.HyperlinkButton",
        "Microsoft.UI.Xaml.Controls.Primitives.RepeatButton",
        "Microsoft.UI.Xaml.Controls.Primitives.ToggleButton",
        "Microsoft.UI.Xaml.Controls.CheckBox",
        "Microsoft.UI.Xaml.Controls.RadioButton",
        "Microsoft.UI.Xaml.Controls.ToggleSwitch",
        "Microsoft.UI.Xaml.Controls.Slider",
        "Microsoft.UI.Xaml.Controls.ProgressBar",
        "Microsoft.UI.Xaml.Controls.ProgressRing",
        "Microsoft.UI.Xaml.Controls.TextBox",
        "Microsoft.UI.Xaml.Controls.PasswordBox",
        "Microsoft.UI.Xaml.Controls.NumberBox",
        "Microsoft.UI.Xaml.Controls.AutoSuggestBox",
        "Microsoft.UI.Xaml.Controls.ComboBox",
        "Microsoft.UI.Xaml.Controls.ListView",
        "Microsoft.UI.Xaml.Controls.GridView",
        "Microsoft.UI.Xaml.Controls.FlipView",
        "Microsoft.UI.Xaml.Controls.ScrollViewer",
        "Microsoft.UI.Xaml.Controls.Expander",
        "Microsoft.UI.Xaml.Controls.ColorPicker",
        "Microsoft.UI.Xaml.Controls.NavigationView",
        "Microsoft.UI.Xaml.Controls.CommandBar",
        "Microsoft.UI.Xaml.Controls.AppBarButton",
        "Microsoft.UI.Xaml.Controls.AppBarToggleButton",
        "Microsoft.UI.Xaml.Controls.SplitView",
        "Microsoft.UI.Xaml.Controls.PipsPager",
        "Microsoft.UI.Xaml.Controls.PagerControl",
        "Microsoft.UI.Xaml.Controls.BreadcrumbBar",
        "Microsoft.UI.Xaml.Controls.CalendarView",
        "Microsoft.UI.Xaml.Controls.CalendarDatePicker",
        "Microsoft.UI.Xaml.Controls.TabView",
        "Microsoft.UI.Xaml.Controls.Pivot",
        "Microsoft.UI.Xaml.Controls.SelectorBar",
        "Microsoft.UI.Xaml.Controls.InfoBar",
        "Microsoft.UI.Xaml.Controls.InfoBadge",
        "Microsoft.UI.Xaml.Controls.RatingControl",
        "Microsoft.UI.Xaml.Controls.PersonPicture",
        "Microsoft.UI.Xaml.Controls.RefreshContainer",
        "Microsoft.UI.Xaml.Controls.DropDownButton",
        "Microsoft.UI.Xaml.Controls.SplitButton",
        "Microsoft.UI.Xaml.Controls.ToggleSplitButton",
    };

    /// <summary>The Control type (declares Template).</summary>
    public const string Control = "Microsoft.UI.Xaml.Controls.Control";

    /// <summary>The UIElement type.</summary>
    public const string UIElement = "Microsoft.UI.Xaml.UIElement";

    /// <summary>The ScrollViewer type.</summary>
    public const string ScrollViewer = "Microsoft.UI.Xaml.Controls.ScrollViewer";

    /// <summary>The PasswordBox type.</summary>
    public const string PasswordBox = "Microsoft.UI.Xaml.Controls.PasswordBox";

    /// <summary>The VisualStateManager type.</summary>
    public const string VisualStateManager = "Microsoft.UI.Xaml.VisualStateManager";

    /// <summary>The ElementCompositionPreview type.</summary>
    public const string ElementCompositionPreview = "Microsoft.UI.Xaml.Hosting.ElementCompositionPreview";

    /// <summary>The SystemBackdrop base type.</summary>
    public const string SystemBackdrop = "Microsoft.UI.Xaml.Media.SystemBackdrop";

    /// <summary>The composition namespace (every type in it is inert on Android).</summary>
    public const string CompositionNamespace = "Microsoft.UI.Composition";

    /// <summary>CBAND0003: types that are accepted and ignored (composition-backed).</summary>
    public static readonly IReadOnlyCollection<string> CompositionTypes = new HashSet<string>(StringComparer.Ordinal)
    {
        "Microsoft.UI.Xaml.Media.ThemeShadow",
        "Microsoft.UI.Xaml.Controls.Primitives.MonochromaticOverlayPresenter",
        "CodeBrix.Platform.Diagnostics.UI.DiagnosticsOverlay",
    };

    /// <summary>CBAND0004: 3-D projections and transforms.</summary>
    public static readonly IReadOnlyCollection<string> ProjectionTypes = new HashSet<string>(StringComparer.Ordinal)
    {
        "Microsoft.UI.Xaml.Media.PlaneProjection",
        "Microsoft.UI.Xaml.Media.Matrix3DProjection",
        "Microsoft.UI.Xaml.Media.Media3D.CompositeTransform3D",
        "Microsoft.UI.Xaml.Media.Media3D.PerspectiveTransform3D",
    };

    /// <summary>CBAND0004: the UIElement properties that take them.</summary>
    public static readonly IReadOnlyCollection<string> ProjectionProperties = new HashSet<string>(StringComparer.Ordinal)
    {
        "Projection", "Transform3D",
    };

    /// <summary>CBAND0008: acrylic and Mica materials.</summary>
    public static readonly IReadOnlyCollection<string> MaterialTypes = new HashSet<string>(StringComparer.Ordinal)
    {
        "Microsoft.UI.Xaml.Media.AcrylicBrush",
        "Microsoft.UI.Xaml.Media.MicaBackdrop",
        "Microsoft.UI.Xaml.Media.DesktopAcrylicBackdrop",
    };

    /// <summary>CBAND0007: frame-buffer head option types (matched by simple name: they live in the head packages).</summary>
    public static readonly IReadOnlyCollection<string> FrameBufferOptionNames = new HashSet<string>(StringComparer.Ordinal)
    {
        "SoftwareKeyboardOptions", "FilePickerOptions", "FolderPickerOptions",
    };

    /// <summary>The namespaces the default (presentation) XAML namespace maps to, in lookup order.</summary>
    public static readonly IReadOnlyList<string> DefaultXamlNamespaces = new[]
    {
        "Microsoft.UI.Xaml.Controls",
        "Microsoft.UI.Xaml.Controls.Primitives",
        "Microsoft.UI.Xaml",
        "Microsoft.UI.Xaml.Media",
        "Microsoft.UI.Xaml.Media.Media3D",
        "Microsoft.UI.Xaml.Shapes",
        "Microsoft.UI.Xaml.Media.Animation",
        "Microsoft.UI.Xaml.Documents",
    };

    /// <summary>The simple name of a full type name.</summary>
    /// <param name="fullName">The full name.</param>
    /// <returns>The part after the last dot.</returns>
    public static string SimpleName(string fullName)
    {
        var dot = fullName.LastIndexOf('.');
        return dot >= 0 ? fullName.Substring(dot + 1) : fullName;
    }
}
