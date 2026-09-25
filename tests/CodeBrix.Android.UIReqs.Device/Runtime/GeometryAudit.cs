using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using CodeBrix.Android.UI.Diagnostics;
using CodeBrix.Platform.UI.Core.UIReqs.Hosting;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;

namespace CodeBrix.Android.UIReqs.Device.Runtime;

/// <summary>
/// The Android-only geometry check of the UIReqs port: after every captured frame, each element
/// the scenario named must be shown by a native view whose on-screen rectangle equals the
/// element's Core rectangle within 1 pixel (layout replay). Elements under a render transform,
/// collapsed or zero-sized elements are not compared (their Core bounds are not a view rectangle).
/// </summary>
internal static class GeometryAudit
{
    /// <summary>The largest allowed difference in pixels.</summary>
    internal const int Tolerance = 1;

    /// <summary>Compares every registered element; throws with the list of mismatches.</summary>
    internal static async Task VerifyAsync()
    {
        var mismatches = new List<string>();
        var compared = 0;
        await TestTargetFixture.RunOnUIThreadAsync(() =>
        {
            foreach (var name in ElementRegistry.Names)
            {
                if (!ElementRegistry.TryResolve(name, out var element) || element is null)
                {
                    continue;
                }

                if (!IsComparable(element))
                {
                    continue;
                }

                var view = NativeViewLocator.ViewOf(element);
                if (view == null)
                {
                    continue;
                }

                var density = element.XamlRoot?.RasterizationScale ?? 1;
                var origin = element.TransformToVisual(null).TransformPoint(new Point(0, 0));
                var core = new Rect(origin.X * density, origin.Y * density, element.RenderSize.Width * density, element.RenderSize.Height * density);
                var location = new int[2];
                view.GetLocationInWindow(location);
                compared++;
                var dx = Math.Abs(location[0] - core.X);
                var dy = Math.Abs(location[1] - core.Y);
                var dw = Math.Abs(view.Width - core.Width);
                var dh = Math.Abs(view.Height - core.Height);
                if (dx > Tolerance || dy > Tolerance || dw > Tolerance || dh > Tolerance)
                {
                    mismatches.Add(string.Create(CultureInfo.InvariantCulture,
                        $"\"{name}\" Core ({core.X:0.#},{core.Y:0.#}) {core.Width:0.#} x {core.Height:0.#} vs native view ({location[0]},{location[1]}) {view.Width} x {view.Height}"));
                }
            }
        }).ConfigureAwait(false);

        LastCompared = compared;
        global::Android.Util.Log.Info("UIReqs.Geometry", string.Create(CultureInfo.InvariantCulture,
            $"audit: {compared} named element(s) compared, {mismatches.Count} off by more than {Tolerance} px"));
        if (mismatches.Count > 0)
        {
            throw new InvalidOperationException(
                "Layout replay: the native view of an element is not where Core laid it out (more than "
                + Tolerance + " pixel): " + string.Join("; ", mismatches));
        }
    }

    /// <summary>How many elements the last audit compared.</summary>
    internal static int LastCompared { get; private set; }

    private static bool IsComparable(FrameworkElement element)
    {
        if (element.RenderSize.Width <= 0 || element.RenderSize.Height <= 0)
        {
            return false;
        }

        for (DependencyObject? current = element; current is UIElement ui; current = VisualTreeHelper.GetParent(ui))
        {
            if (ui.Visibility != Visibility.Visible || ui.RenderTransform != null && ui.RenderTransform is not MatrixTransform { Matrix.IsIdentity: true })
            {
                return false;
            }
        }

        return true;
    }
}
