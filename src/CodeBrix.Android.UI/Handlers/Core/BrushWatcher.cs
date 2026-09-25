using System;
using System.Collections.Generic;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;

namespace CodeBrix.Android.UI.Handlers;

/// <summary>
/// Follows the inside of a brush a handler painted with (plan 2.5): apps re-point brush
/// colours at run time (a SolidColorBrush's Color, a gradient stop's Color/Offset, the
/// brush's Opacity) without replacing the brush, which raises no change on the element. The
/// callbacks are public DependencyObject.RegisterPropertyChangedCallback registrations,
/// released when the brush is replaced or the handler disconnects.
/// </summary>
internal sealed class BrushWatcher
{
    private readonly Action _changed;
    private readonly List<(DependencyObject Owner, DependencyProperty Property, long Token)> _tokens = new();
    private Brush _brush;

    /// <summary>Creates a watcher that calls <paramref name="changed"/> on any change inside the brush.</summary>
    /// <param name="changed">The repaint action.</param>
    internal BrushWatcher(Action changed)
    {
        _changed = changed ?? throw new ArgumentNullException(nameof(changed));
    }

    /// <summary>Watches <paramref name="brush"/> (null stops watching).</summary>
    /// <param name="brush">The brush now painted.</param>
    internal void Watch(Brush brush)
    {
        if (ReferenceEquals(_brush, brush))
        {
            return;
        }

        Clear();
        _brush = brush;
        if (brush == null)
        {
            return;
        }

        Register(brush, Brush.OpacityProperty);
        switch (brush)
        {
            case SolidColorBrush:
                Register(brush, SolidColorBrush.ColorProperty);
                break;
            case GradientBrush gradient:
                if (gradient.GradientStops is { } stops)
                {
                    foreach (var stop in stops)
                    {
                        Register(stop, GradientStop.ColorProperty);
                        Register(stop, GradientStop.OffsetProperty);
                    }
                }

                break;
        }
    }

    /// <summary>Stops watching.</summary>
    internal void Clear()
    {
        foreach (var (owner, property, token) in _tokens)
        {
            owner.UnregisterPropertyChangedCallback(property, token);
        }

        _tokens.Clear();
        _brush = null;
    }

    private void Register(DependencyObject owner, DependencyProperty property) =>
        _tokens.Add((owner, property, owner.RegisterPropertyChangedCallback(property, (_, _) => _changed())));
}
