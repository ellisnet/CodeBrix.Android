using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using CodeBrix.Platform.UI.Toolkit.Contracts;
using Microsoft.UI.Xaml;
using Windows.UI;

namespace CodeBrix.Android.UI.Toolkit.Android;

/// <summary>
/// The Android implementation of <see cref="IElevationPlatform"/> (UIElementExtensions
/// .SetElevation). A STUB until element handlers exist (AP2): the requested elevation and
/// shadow color are remembered per element, and applied to the element's native view
/// (ViewCompat.SetElevation plus the outline shadow colors) once handlers own views.
/// </summary>
internal sealed class ElevationAndroidPlatform : IElevationPlatform
{
    private readonly ConditionalWeakTableStore _requests = new();

    /// <summary>The number of elements with a remembered elevation request.</summary>
    internal int PendingCount => _requests.Count;

    /// <inheritdoc />
    public void SetElevation(UIElement element, double elevation, Color shadowColor)
    {
        if (element == null)
        {
            return;
        }

        _requests.Set(element, new ElevationRequest(elevation, shadowColor));
    }

    /// <summary>Returns the remembered request for an element, if any.</summary>
    internal bool TryGetRequest(UIElement element, out ElevationRequest request) => _requests.TryGet(element, out request);

    /// <summary>An elevation request (DIPs and shadow color).</summary>
    internal readonly record struct ElevationRequest(double Elevation, Color ShadowColor);

    private sealed class ConditionalWeakTableStore
    {
        private readonly ConditionalWeakTable<UIElement, Box> _table = new();
        private readonly object _gate = new();
        private readonly List<WeakReference<UIElement>> _keys = new();

        public int Count
        {
            get
            {
                lock (_gate)
                {
                    _keys.RemoveAll(k => !k.TryGetTarget(out _));
                    return _keys.Count;
                }
            }
        }

        public void Set(UIElement element, ElevationRequest request)
        {
            lock (_gate)
            {
                if (_table.TryGetValue(element, out var box))
                {
                    box.Value = request;
                    return;
                }

                _table.Add(element, new Box { Value = request });
                _keys.Add(new WeakReference<UIElement>(element));
            }
        }

        public bool TryGet(UIElement element, out ElevationRequest request)
        {
            lock (_gate)
            {
                if (element != null && _table.TryGetValue(element, out var box))
                {
                    request = box.Value;
                    return true;
                }
            }

            request = default;
            return false;
        }

        private sealed class Box
        {
            public ElevationRequest Value { get; set; }
        }
    }
}
