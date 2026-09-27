using Microsoft.CodeAnalysis;

namespace CodeBrix.Android.Analyzers.Diagnostics;

/// <summary>
/// The CBAND diagnostics: CodeBrix.Platform constructs that compile unchanged for Android but that CodeBrix.Android
/// accepts and ignores (or shows differently) at run time. Every one is a WARNING, never an error (decision D-P12):
/// a pasted page always builds, and the build says what will not look or behave as on the desktop heads.
/// </summary>
public static class CbandDescriptors
{
    /// <summary>The diagnostic category.</summary>
    public const string Category = "CodeBrix.Android";

    /// <summary>CBAND0001: a ControlTemplate on a control CodeBrix.Android shows as a native Material control.</summary>
    /// <remarks>
    /// The wording follows the behaviour today (AP6 C6: app templates on native-handled controls are kept and their parts
    /// rendered natively; Jeremy has not ruled): the template does not style the native Material control.
    /// </remarks>
    public static readonly DiagnosticDescriptor ControlTemplateOnNativeControl = new(
        "CBAND0001",
        "ControlTemplate on a control that Android shows as a native control",
        "{0} sets a ControlTemplate on {1}: on Android the template is not used for the native control's visuals; its parts may be shown through the native control",
        Category,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "CodeBrix.Android shows this control as a native Material control. An app ControlTemplate does not restyle the native control; the control keeps its non-template properties (Background, Foreground, BorderBrush, Padding, FontFamily...), and parts of the template may be shown through the native control.");

    /// <summary>CBAND0002: template members used on a control CodeBrix.Android shows as a native control.</summary>
    public static readonly DiagnosticDescriptor TemplateMemberOnNativeControl = new(
        "CBAND0002",
        "Template member used on a control that Android shows as a native control",
        "{0} on {1}: on Android {1} is a native control, so OnApplyTemplate may not run, GetTemplateChild may return null and VisualStateManager.GoToState may return false",
        Category,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Lookless-template members have no native counterpart: code that relies on template parts or visual states of a natively shown control does not run as it does on the desktop heads.");

    /// <summary>CBAND0003: composition APIs, system backdrops, ThemeShadow and overlay-only presenters.</summary>
    public static readonly DiagnosticDescriptor CompositionIgnored = new(
        "CBAND0003",
        "Composition construct ignored on Android",
        "{0} is accepted and ignored on Android: the composition layer is inert (no composition visuals, effects, shadows or backdrops)",
        Category,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "The Android composition platform is inert. ElementCompositionPreview, Compositor, composition effects, SystemBackdrop, ThemeShadow shadows, MonochromaticOverlayPresenter and DiagnosticsOverlay are accepted and have no visible effect.");

    /// <summary>CBAND0004: 3-D projections and 3-D transforms.</summary>
    public static readonly DiagnosticDescriptor ProjectionIgnored = new(
        "CBAND0004",
        "3-D projection or transform ignored on Android",
        "{0} is accepted and ignored on Android: 3-D projections and 3-D transforms are not applied to native views",
        Category,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "UIElement.Projection (PlaneProjection, Matrix3DProjection) and UIElement.Transform3D (CompositeTransform3D, PerspectiveTransform3D) have no native mapping yet. 2-D render transforms, skew and matrix transforms included, are mapped.");

    /// <summary>CBAND0005: ScrollViewer zoom enabled.</summary>
    public static readonly DiagnosticDescriptor ZoomIgnored = new(
        "CBAND0005",
        "ScrollViewer zoom ignored on Android",
        "{0} enables ScrollViewer zoom, which is accepted and ignored on Android (the native scroll view does not zoom)",
        Category,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "ZoomMode=\"Disabled\" is the Android behaviour; ZoomMode=\"Enabled\" (and zoom factors) are accepted and ignored.");

    /// <summary>CBAND0006: a PasswordChar the native mask cannot show.</summary>
    public static readonly DiagnosticDescriptor PasswordCharIgnored = new(
        "CBAND0006",
        "PasswordChar that Android cannot show",
        "PasswordChar \"{0}\" is not a single character: Android masks with its first character, or with the default mask when it is empty",
        Category,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "The native password field masks every character with one mask character. A single-character PasswordChar is honoured; an empty or longer value is not shown as written.");

    /// <summary>CBAND0007: frame-buffer head options referenced from an Android build.</summary>
    public static readonly DiagnosticDescriptor FrameBufferOptionIgnored = new(
        "CBAND0007",
        "Frame-buffer head option has no effect on Android",
        "{0} is a frame-buffer head option and has no effect on Android (the system keyboard and the system file pickers are used)",
        Category,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "SoftwareKeyboardOptions, FilePickerOptions and FolderPickerOptions configure the Linux frame-buffer head. An Android app uses the system IME and the Storage Access Framework pickers.");

    /// <summary>CBAND0008: acrylic and Mica materials.</summary>
    public static readonly DiagnosticDescriptor MaterialFallback = new(
        "CBAND0008",
        "Acrylic or Mica material shown as a solid fallback on Android",
        "{0} is shown as its solid fallback colour on Android (acrylic and Mica materials are not rendered)",
        Category,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "AcrylicBrush draws its fallback colour; MicaBackdrop and DesktopAcrylicBackdrop draw nothing (the window background shows).");
}
