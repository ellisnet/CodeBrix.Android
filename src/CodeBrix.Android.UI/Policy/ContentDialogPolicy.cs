#if __ANDROID__
using System;
using System.Runtime.CompilerServices;
using CodeBrix.Android.UI.Handlers;
using CodeBrix.Android.UI.Hosting;
using CodeBrix.Android.UI.Overlay;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using AColor = global::Android.Graphics.Color;
using AInsetDrawable = global::Android.Graphics.Drawables.InsetDrawable;
using AMaterialShapeDrawable = Google.Android.Material.Shape.MaterialShapeDrawable;
using ATextView = global::Android.Widget.TextView;
using AWindowManagerFlags = global::Android.Views.WindowManagerFlags;

namespace CodeBrix.Android.UI.Policy;

/// <summary>
/// The ContentDialog row of the adaptive table and the ContentDialog key family (plan 2.10, 2.11):
/// <list type="bullet">
/// <item>A dialog Core presents itself (XAML content: tier 1) is shown full screen in a Compact window
/// (<see cref="AdaptivePolicy.ContentDialog"/>: FullSizeDesired, so its card fills the window height, and at a
/// Compact width the Fluent card already takes the window's width) and as the basic centred card otherwise -
/// re-mapped live while it is open when the window changes class. A FullSizeDesired the app set itself is left
/// alone.</item>
/// <item>A text dialog shown as a Material dialog (AP4, tier 2; always basic) gets the app's re-keyed
/// ContentDialog* brushes: surface, stroke, title and message colour, and the scrim's dim amount - when it
/// appears and again when the app re-points them live.</item>
/// </list>
/// </summary>
internal static class ContentDialogPolicy
{
    private static readonly ConditionalWeakTable<ContentDialog, Tracked> _tracked = new();
    private static bool _installed;

    /// <summary>How many times a Material dialog was re-coloured from re-keyed brushes (diagnostics, tests).</summary>
    internal static int MaterialRecolorCount { get; private set; }

    /// <summary>Installs the Material-dialog colouring (idempotent).</summary>
    internal static void Install()
    {
        if (_installed)
        {
            return;
        }

        _installed = true;
        ThemeRefresh.Refreshed += RecolorMaterialDialogs;
    }

    /// <summary>A followed activity's window lost or gained focus (a dialog window appeared or went): colour new Material dialogs.</summary>
    internal static void OnWindowFocusChanged() => RecolorMaterialDialogs();

    /// <summary>
    /// Follows a Core-presented ContentDialog (its handler was just created, i.e. it entered the popup layer):
    /// applies the size class's form now and whenever the window changes class while it is open.
    /// </summary>
    /// <param name="dialog">The dialog.</param>
    internal static void Track(ContentDialog dialog)
    {
        if (dialog == null)
        {
            return;
        }

        if (!_tracked.TryGetValue(dialog, out var tracked))
        {
            tracked = new Tracked(dialog);
            _tracked.Add(dialog, tracked);
        }

        tracked.Start();
    }

    /// <summary>True when <paramref name="dialog"/>'s content is plain text (or nothing).</summary>
    /// <param name="dialog">The dialog.</param>
    /// <returns>True for text content.</returns>
    internal static bool HasTextContent(ContentDialog dialog) => dialog.Content is null or string || dialog.Content is not UIElement;

    /// <summary>The form the dialog is shown in now.</summary>
    /// <param name="dialog">The dialog.</param>
    /// <returns>The form.</returns>
    internal static DialogForm FormOf(ContentDialog dialog) =>
        AdaptivePolicy.ContentDialog(WindowSizeClassMonitor.For(dialog), HasTextContent(dialog));

    private static void RecolorMaterialDialogs()
    {
        foreach (var overlay in NativeOverlays.Open)
        {
            if (overlay is NativeContentDialog { IsShowing: true } native)
            {
                Recolor(native);
            }
        }
    }

    /// <summary>Colours a Material dialog from the app's re-keyed ContentDialog brushes (the framework's leave Material's own).</summary>
    /// <param name="native">The Material dialog.</param>
    internal static void Recolor(NativeContentDialog native)
    {
        var dialog = native?.Dialog;
        var window = native?.Native?.Window;
        if (dialog == null || window == null)
        {
            return;
        }

        try
        {
            var applied = false;
            var density = window.Context?.Resources?.DisplayMetrics?.Density ?? 1f;
            if (window.DecorView?.Background is { } background)
            {
                var shape = background is AInsetDrawable inset ? inset.Drawable as AMaterialShapeDrawable : background as AMaterialShapeDrawable;
                if (shape != null && ThemeKeys.IsAppKey(dialog, "ContentDialogBackground") && ThemeResources.FindColor(dialog, "ContentDialogBackground") is { } fill)
                {
                    shape.FillColor = StateColors.Single(fill);
                    applied = true;
                }

                if (shape != null && ThemeKeys.IsAppKey(dialog, "ContentDialogBorderBrush") && ThemeResources.FindColor(dialog, "ContentDialogBorderBrush") is { } stroke)
                {
                    shape.SetStroke(Math.Max(1f, density), StateColors.Single(stroke));
                    applied = true;
                }
            }

            if (ThemeKeys.IsAppKey(dialog, "ContentDialogForeground") && ThemeResources.FindColor(dialog, "ContentDialogForeground") is { } foreground)
            {
                var context = window.Context;
                var titleId = context?.Resources?.GetIdentifier("alertTitle", "id", context.PackageName) ?? 0;
                if (titleId != 0 && window.DecorView?.FindViewById(titleId) is ATextView title)
                {
                    title.SetTextColor(new AColor(foreground));
                }

                if (window.DecorView?.FindViewById(global::Android.Resource.Id.Message) is ATextView message)
                {
                    message.SetTextColor(new AColor(foreground));
                }

                applied = true;
            }

            var scrimKey = ThemeKeys.IsAppKey(dialog, "ContentDialogSmokeFill") ? "ContentDialogSmokeFill"
                : ThemeKeys.IsAppKey(dialog, "ContentDialogLightDismissOverlayBackground") ? "ContentDialogLightDismissOverlayBackground"
                : null;
            if (scrimKey != null && ThemeResources.FindColor(dialog, scrimKey) is { } scrim)
            {
                window.AddFlags(AWindowManagerFlags.DimBehind);
                window.SetDimAmount(((scrim >> 24) & 0xFF) / 255f);
                applied = true;
            }

            if (applied)
            {
                MaterialRecolorCount++;
            }
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            HostLog.For("CodeBrix.Android.UI.Policy").LogDebug(exception, "Colouring a Material dialog failed.");
        }
    }

    private sealed class Tracked
    {
        private readonly WeakReference<ContentDialog> _dialog;
        private bool _listening;
        private bool _setByPolicy;

        private FrameworkElement _card;
        private (double MaxWidth, double MaxHeight, HorizontalAlignment Horizontal, VerticalAlignment Vertical)? _cardDefaults;

        internal Tracked(ContentDialog dialog)
        {
            _dialog = new WeakReference<ContentDialog>(dialog);
            dialog.Opened += (_, _) => Apply();
            dialog.Closed += (_, _) => Stop();
        }

        internal void Start()
        {
            if (!_listening)
            {
                _listening = true;
                WindowSizeClassMonitor.Changed += OnChanged;
            }

            Apply();
        }

        private void Stop()
        {
            if (_listening)
            {
                _listening = false;
                WindowSizeClassMonitor.Changed -= OnChanged;
            }
        }

        private void OnChanged(object sender, WindowSizeClassChangedEventArgs e) => Apply();

        private void Apply()
        {
            if (!_dialog.TryGetTarget(out var dialog))
            {
                Stop();
                return;
            }

            // An app that set FullSizeDesired itself decides; otherwise the table does.
            if (!_setByPolicy && dialog.ReadLocalValue(ContentDialog.FullSizeDesiredProperty) != DependencyProperty.UnsetValue)
            {
                return;
            }

            if (FormOf(dialog) == DialogForm.FullScreen)
            {
                if (!dialog.FullSizeDesired)
                {
                    dialog.FullSizeDesired = true;
                }

                _setByPolicy = true;
                Card(dialog, fill: true);
            }
            else if (_setByPolicy)
            {
                dialog.ClearValue(ContentDialog.FullSizeDesiredProperty);
                _setByPolicy = false;
                Card(dialog, fill: false);
            }
        }

        /// <summary>
        /// Lets the dialog's card (the template's BackgroundElement) fill the window - FullSizeDesired alone stretches
        /// it only up to ContentDialogMaxHeight / MaxWidth - or gives it its template limits back.
        /// </summary>
        private void Card(ContentDialog dialog, bool fill)
        {
            _card ??= Find(dialog, "BackgroundElement") as FrameworkElement;
            if (_card == null)
            {
                return;
            }

            if (fill)
            {
                _cardDefaults ??= (_card.MaxWidth, _card.MaxHeight, _card.HorizontalAlignment, _card.VerticalAlignment);
                _card.MaxWidth = double.PositiveInfinity;
                _card.MaxHeight = double.PositiveInfinity;
                _card.HorizontalAlignment = HorizontalAlignment.Stretch;
                _card.VerticalAlignment = VerticalAlignment.Stretch;
            }
            else if (_cardDefaults is { } defaults)
            {
                _card.MaxWidth = defaults.MaxWidth;
                _card.MaxHeight = defaults.MaxHeight;
                _card.HorizontalAlignment = defaults.Horizontal;
                _card.VerticalAlignment = defaults.Vertical;
                _cardDefaults = null;
            }
        }

        private static DependencyObject Find(DependencyObject root, string name)
        {
            var count = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChildrenCount(root);
            for (var i = 0; i < count; i++)
            {
                var child = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChild(root, i);
                if (child is FrameworkElement { Name: var n } && n == name)
                {
                    return child;
                }

                if (Find(child, name) is { } nested)
                {
                    return nested;
                }
            }

            return null;
        }
    }
}
#endif
