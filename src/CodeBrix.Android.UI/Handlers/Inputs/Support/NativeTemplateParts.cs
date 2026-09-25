using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;

namespace CodeBrix.Android.UI.Handlers;

/// <summary>
/// An invisible, input-transparent Core element that stands where a part of a NATIVE widget is drawn
/// (see <see cref="NativeTemplateParts"/>). It has no handler (Core's own path) and draws nothing.
/// </summary>
internal sealed class NativeTemplatePart : FrameworkElement
{
    /// <summary>Creates a part stand-in.</summary>
    /// <param name="name">The template-part name it answers to.</param>
    internal NativeTemplatePart(string name)
    {
        Name = name;
        IsHitTestVisible = false;
        IsTabStop = false;
    }
}

/// <summary>
/// Template-part stand-ins for native widgets (diagnostics / UI automation, OFF by default). A native
/// handler owns its control's visuals (the Fluent template is never materialized), so the parts a UI
/// test addresses by name - a CheckBox's "NormalRectangle", a ToggleSwitch's "SwitchKnob", a Slider's
/// "HorizontalThumb" - do not exist. When <see cref="Enabled"/> is set (the UIReqs scenario app sets
/// it), each native handler publishes, as invisible Core children of its control named like the
/// Fluent parts, the rectangles where the native widget actually DRAWS those parts (read from the
/// widget's drawables after it drew), so tree-based geometry queries (TransformToVisual, ActualWidth)
/// describe the native pixels.
/// </summary>
internal sealed class NativeTemplateParts
{
    private static readonly ConditionalWeakTable<UIElement, NativeTemplateParts> _byOwner = new();
    private readonly Dictionary<string, (NativeTemplatePart Part, Rect Rect)> _parts = new(StringComparer.Ordinal);
    private readonly UIElement _owner;

    private NativeTemplateParts(UIElement owner) => _owner = owner;

    /// <summary>
    /// The part set of a control (one per control for its lifetime, so a handler created on a later
    /// Enter finds the stand-ins its predecessor added).
    /// </summary>
    /// <param name="owner">The control whose parts are published.</param>
    /// <returns>The part set.</returns>
    internal static NativeTemplateParts For(UIElement owner)
    {
        ArgumentNullException.ThrowIfNull(owner);
        return _byOwner.GetValue(owner, o => new NativeTemplateParts(o));
    }

    /// <summary>True when native handlers publish their part stand-ins (default false).</summary>
    internal static bool Enabled { get; set; }

    /// <summary>
    /// Adds the stand-ins of the named parts (collapsed until their place is known). Handlers declare
    /// their parts when they connect, before Core's first layout of the control, so no child is added to
    /// the control once it is laid out (which would move it and its siblings a layout pass later).
    /// </summary>
    /// <param name="names">The template-part names.</param>
    internal void Declare(params string[] names)
    {
        if (!Enabled)
        {
            return;
        }

        foreach (var name in names)
        {
            if (!_parts.ContainsKey(name))
            {
                var part = new NativeTemplatePart(name) { Visibility = Visibility.Collapsed };
                _parts[name] = (part, Rect.Empty);
                _owner.AddChild(part, null);
            }
        }
    }

    /// <summary>
    /// Sets where a part is, relative to the control, in DIPs (a zero-width or zero-height rectangle is a
    /// real, empty part - a ProgressBar indicator at its Minimum); null collapses it. Asks Core to arrange
    /// the control again when the rectangle changed (arrange only: nothing else moves).
    /// </summary>
    /// <param name="name">The template-part name.</param>
    /// <param name="rect">The rectangle, or null.</param>
    internal void Set(string name, Rect? rect)
    {
        if (!Enabled)
        {
            return;
        }

        var value = rect is { IsEmpty: false } r ? new Rect(r.X, r.Y, Math.Max(0, r.Width), Math.Max(0, r.Height)) : Rect.Empty;
        if (_parts.TryGetValue(name, out var entry))
        {
            if (entry.Rect == value)
            {
                return;
            }

            _parts[name] = (entry.Part, value);
        }
        else
        {
            var part = new NativeTemplatePart(name);
            _parts[name] = (part, value);
            _owner.AddChild(part, null);
        }

        _owner.InvalidateArrange();
    }

    /// <summary>Lays every part out at its rectangle (the owner handler calls this after each Core arrange).</summary>
    internal void Arrange()
    {
        foreach (var (part, rect) in _parts.Values)
        {
            // Core takes a Control's first visual child for its template root when it releases the template
            // for the handler: a stand-in added before that is removed again - put it back.
            if (!ReferenceEquals(VisualTreeHelper.GetParent(part), _owner))
            {
                _owner.AddChild(part, null);
            }

            if (rect.IsEmpty)
            {
                part.Visibility = Visibility.Collapsed;
                continue;
            }

            part.Visibility = Visibility.Visible;
            part.Measure(new Size(rect.Width, rect.Height));
            part.Arrange(rect);
        }
    }
}

/// <summary>Lays a native view out at the size Core arranged it to, ahead of Android's own layout pass.</summary>
internal static class NativeLayoutNow
{
    /// <summary>
    /// Measures and lays <paramref name="view"/> out at <paramref name="size"/> (DIPs) now, so geometry that
    /// depends on the native layout (part stand-ins) is known in the same Core layout pass; the parent view
    /// places it again at its position in Android's layout pass.
    /// </summary>
    /// <param name="view">The view.</param>
    /// <param name="size">The arranged size in DIPs.</param>
    /// <param name="density">Pixels per DIP.</param>
    internal static void Ensure(global::Android.Views.View view, Windows.Foundation.Size size, double density)
    {
        if (view == null)
        {
            return;
        }

        var width = MaterialWidgets.Px(size.Width, density);
        var height = MaterialWidgets.Px(size.Height, density);
        if (view.Width == width && view.Height == height && !view.IsLayoutRequested)
        {
            return;
        }

        view.Measure(CodeBrix.Android.UI.Platform.MeasureSpecExtensions.Exactly(width), CodeBrix.Android.UI.Platform.MeasureSpecExtensions.Exactly(height));
        view.Layout(view.Left, view.Top, view.Left + width, view.Top + height);
    }
}
