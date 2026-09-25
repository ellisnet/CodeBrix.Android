using System;
using System.Collections.Generic;
using CodeBrix.Android.UI.Android;
using CodeBrix.Android.UI.Hosting;
using CodeBrix.Android.UI.Portable.Projection;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Shapes;
using ABreakStrategy = global::Android.Text.BreakStrategy;
using AComplexUnitType = global::Android.Util.ComplexUnitType;
using AGravityFlags = global::Android.Views.GravityFlags;
using AHyphenationFrequency = global::Android.Text.HyphenationFrequency;
using ARect = global::Android.Graphics.Rect;
using ATextView = global::Android.Widget.TextView;
using ATruncateAt = global::Android.Text.TextUtils.TruncateAt;
using ATypeface = global::Android.Graphics.Typeface;
using AView = global::Android.Views.View;
using AViewGroup = global::Android.Views.ViewGroup;

namespace CodeBrix.Android.UI.Projection;

/// <summary>
/// The AP1 projection viewer ("stub handlers" before the per-control handler seam): after
/// each Core layout tick it walks the window's visual tree and mirrors it into the
/// activity's <see cref="CodeBrixRootLayout.ContentLayer"/> as native views placed at Core's
/// layout slots (density-aware). TextBlocks and FontIcons become real TextViews (the
/// resolved font, size, colour, alignment and wrapping, so the native layout matches the
/// Core measure); backgrounds, borders and shape fills are drawn as boxes; every other
/// element the page declares becomes a labelled placeholder box (type name + key
/// properties); Images are crossed placeholder boxes (no image decoding before AP3a).
/// </summary>
/// <remarks>
/// Views are reused per element (a layout tick allocates nothing for unchanged elements) and
/// pooled when elements leave the tree. This is diagnostic scaffolding: the element handlers
/// of AP2 replace it, and <see cref="CodeBrixApplication.UseProjectionViewer"/> turns it off.
/// Not handled (by design, for AP1): render transforms, scroll offsets of scrolled content,
/// nested clipping other than scroll viewports, input.
/// </remarks>
internal sealed class ProjectionViewer : IDisposable
{
    private readonly AndroidXamlRootHost _host;
    private readonly ILogger _log;
    private readonly Dictionary<UIElement, Node> _nodes = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<Type, ProjectionRole> _roles = new();
    private readonly List<UIElement> _stale = new();
    private readonly Stack<ProjectionBoxView> _boxPool = new();
    private readonly Stack<ATextView> _textPool = new();
    private CodeBrixActivity _activity;
    private ProjectionLayer _layer;
    private float _density = 1;
    private int _generation;
    private int _order;
    private int _elementCount;
    private int _textCount;
    private int _boxCount;
    private int _labelCount;
    private int _lastTextCount = -1;
    private int _lastBoxCount = -1;
    private int _lastLabelCount = -1;
    private bool _disposed;

    internal ProjectionViewer(AndroidXamlRootHost host)
    {
        _host = host ?? throw new ArgumentNullException(nameof(host));
        _log = HostLog.For("CodeBrix.Android.UI.Projection");
        _host.LayoutUpdated += OnLayoutUpdated;
    }

    /// <summary>The number of native text views currently shown.</summary>
    internal int TextViewCount => _textCount;

    /// <summary>The number of box views currently shown (backgrounds, borders, placeholders).</summary>
    internal int BoxViewCount => _boxCount;

    /// <summary>The number of labelled placeholder boxes currently shown.</summary>
    internal int PlaceholderCount => _labelCount;

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _host.LayoutUpdated -= OnLayoutUpdated;
        ReleaseAll();
        if (_layer?.Parent is AViewGroup parent)
        {
            parent.RemoveView(_layer);
        }

        _layer = null;
        _activity = null;
    }

    /// <summary>Walks the tree and updates the native views (also called by the layout tick).</summary>
    internal void Update()
    {
        if (_disposed || !EnsureLayer())
        {
            return;
        }

        var root = _host.RootElement;
        var scale = _host.XamlRoot?.RasterizationScale ?? 1;
        _density = scale > 0 ? (float)scale : 1f;
        _generation++;
        _order = 0;
        _elementCount = 0;
        _textCount = 0;
        _boxCount = 0;
        _labelCount = 0;

        if (root != null)
        {
            Walk(root, 0, 0, 1, PixelRect.Unbounded, 0);
        }

        RemoveStaleNodes();
        LogSummaryIfChanged();
    }

    private void OnLayoutUpdated(object sender, EventArgs e)
    {
        try
        {
            Update();
        }
        catch (Exception ex)
        {
            // Diagnostics must never take the app down.
            _log.LogError(ex, "The projection viewer failed to update.");
        }
    }

    private bool EnsureLayer()
    {
        var activity = _host.Wrapper.Activity;
        var content = activity?.RootLayout?.ContentLayer;
        if (content == null)
        {
            return false;
        }

        if (!ReferenceEquals(activity, _activity))
        {
            // A re-created activity: start over with views of the new context.
            ReleaseAll();
            if (_layer?.Parent is AViewGroup oldParent)
            {
                oldParent.RemoveView(_layer);
            }

            _boxPool.Clear();
            _textPool.Clear();
            _activity = activity;
            _layer = new ProjectionLayer(activity);
        }

        if (_layer.Parent == null)
        {
            content.AddView(_layer, new AViewGroup.LayoutParams(AViewGroup.LayoutParams.MatchParent, AViewGroup.LayoutParams.MatchParent));
        }

        return true;
    }

    private void Walk(UIElement element, double parentX, double parentY, double opacity, PixelRect clip, int depth)
    {
        if (element.Visibility != Visibility.Visible)
        {
            return;
        }

        var elementOpacity = opacity * element.Opacity;
        if (elementOpacity <= 0.001)
        {
            return;
        }

        var offset = element.ActualOffset;
        var x = parentX + offset.X;
        var y = parentY + offset.Y;
        var childClip = clip;

        if (element is FrameworkElement fe)
        {
            _elementCount++;
            var rect = PixelRect.FromDips(x, y, fe.ActualWidth, fe.ActualHeight, _density);
            if (!rect.Intersect(clip).IsEmpty)
            {
                Project(fe, rect, clip, elementOpacity);
            }

            if (element is ScrollContentPresenter)
            {
                childClip = clip.Intersect(rect);
                if (childClip.IsEmpty)
                {
                    return;
                }
            }
        }

        if (depth >= Diagnostics.VisualTreeDump.MaxDepth)
        {
            return;
        }

        var count = VisualTreeHelper.GetChildrenCount(element);
        for (var i = 0; i < count; i++)
        {
            if (VisualTreeHelper.GetChild(element, i) is UIElement child)
            {
                Walk(child, x, y, elementOpacity, childClip, depth + 1);
            }
        }
    }

    private void Project(FrameworkElement element, PixelRect rect, PixelRect clip, double opacity)
    {
        var type = element.GetType();
        if (!_roles.TryGetValue(type, out var role))
        {
            role = ProjectionRoles.Classify(type);
            _roles.Add(type, role);
        }

        if (!_nodes.TryGetValue(element, out var node))
        {
            node = new Node();
            _nodes.Add(element, node);
        }

        node.Generation = _generation;
        var labelled = ProjectionRoles.IsLabelled(role, element.GetTemplatedParent() != null);

        // The element's own background / border / fill, then its placeholder outline and label.
        GetBoxPaint(element, opacity, out var fill, out var stroke, out var strokeWidth, out var corner, out var isEllipse);
        if (labelled || fill != 0 || (stroke != 0 && strokeWidth > 0))
        {
            string label = null;
            if (labelled)
            {
                var key = GetLabelKey(element);
                if (node.Label == null || key != node.LabelKey)
                {
                    node.LabelKey = key;
                    node.Label = ProjectionLabel.Build(type.Name, element.Name, key);
                }

                label = node.Label;
                _labelCount++;
            }

            var box = node.Box ??= RentBox();
            box.Set(fill, stroke, strokeWidth * _density, corner * _density, isEllipse, labelled, role == ProjectionRole.Image, label, _density);
            if (!ReferenceEquals(node.DescribedLabel, label))
            {
                // Placeholder labels are also the boxes' content descriptions (uiautomator dumps, device checks).
                node.DescribedLabel = label;
                box.ContentDescription = label;
            }

            _layer.Place(box, rect, _order++);
            ApplyClip(box, ref node.BoxClip, rect, clip);
            _boxCount++;
        }
        else if (node.Box != null)
        {
            ReturnBox(node.Box);
            node.Box = null;
            node.DescribedLabel = null;
            node.BoxClip = default;
        }

        // Text: TextBlock text and FontIcon glyphs as real native text views.
        var hasText = role switch
        {
            ProjectionRole.Text => UpdateText(node, (TextBlock)element, rect, clip, opacity),
            ProjectionRole.Glyph => UpdateGlyph(node, (FontIcon)element, rect, clip, opacity),
            _ when element is TextBox textBox and not PasswordBox => UpdateTextBoxText(node, textBox, rect, clip, opacity),
            _ => false,
        };

        if (!hasText && node.Text != null)
        {
            ReturnText(node.Text);
            node.Text = null;
            node.ResetTextCache();
        }
    }

    private bool UpdateText(Node node, TextBlock textBlock, PixelRect rect, PixelRect clip, double opacity)
    {
        var text = textBlock.Text;
        if (string.IsNullOrEmpty(text))
        {
            return false;
        }

        var typeface = AndroidPlatformBootstrap.Fonts?.Resolve(textBlock.FontFamily, textBlock.FontWeight, textBlock.FontStyle, textBlock.FontStretch);
        var style = ProjectionTextStyle.From(textBlock.TextAlignment, textBlock.TextWrapping, textBlock.TextTrimming, textBlock.MaxLines);
        var color = BrushToArgb(textBlock.Foreground, 1, unchecked((int)0xFF000000));
        var letterSpacing = textBlock.CharacterSpacing / 1000f;
        ShowText(node, text, typeface, (float)(textBlock.FontSize * _density), color, style, letterSpacing, false, rect, clip, opacity);
        return true;
    }

    // A TextBox's text (its template shows it through a native-only text view in Core), placed inside
    // the box's border and padding, so bound TextBox values are readable in the projection.
    private bool UpdateTextBoxText(Node node, TextBox textBox, PixelRect rect, PixelRect clip, double opacity)
    {
        var text = textBox.Text;
        if (string.IsNullOrEmpty(text))
        {
            return false;
        }

        var inset = Add(textBox.BorderThickness, textBox.Padding);
        var inner = new PixelRect(
            rect.Left + (int)Math.Round(inset.Left * _density),
            rect.Top + (int)Math.Round(inset.Top * _density),
            rect.Right - (int)Math.Round(inset.Right * _density),
            rect.Bottom - (int)Math.Round(inset.Bottom * _density));
        if (inner.IsEmpty)
        {
            return false;
        }

        var typeface = AndroidPlatformBootstrap.Fonts?.Resolve(textBox.FontFamily, textBox.FontWeight, textBox.FontStyle, textBox.FontStretch);
        var style = ProjectionTextStyle.From(textBox.TextAlignment, textBox.TextWrapping, TextTrimming.None, 0);
        var color = BrushToArgb(textBox.Foreground, 1, unchecked((int)0xFF000000));
        var centered = textBox.VerticalContentAlignment == VerticalAlignment.Center;
        ShowText(node, text, typeface, (float)(textBox.FontSize * _density), color, style, textBox.CharacterSpacing / 1000f, centered, inner, clip, opacity);
        return true;
    }

    private static Thickness Add(Thickness a, Thickness b) => new(a.Left + b.Left, a.Top + b.Top, a.Right + b.Right, a.Bottom + b.Bottom);

    private bool UpdateGlyph(Node node, FontIcon icon, PixelRect rect, PixelRect clip, double opacity)
    {
        var glyph = icon.Glyph;
        if (string.IsNullOrEmpty(glyph))
        {
            return false;
        }

        var typeface = AndroidPlatformBootstrap.Fonts?.Resolve(icon.FontFamily, icon.FontWeight, icon.FontStyle, Windows.UI.Text.FontStretch.Normal);
        var style = new ProjectionTextStyle(ProjectionTextAlignment.Center, 1, true, false);
        var color = BrushToArgb(icon.Foreground, 1, unchecked((int)0xFF000000));
        ShowText(node, glyph, typeface, (float)(icon.FontSize * _density), color, style, 0, true, rect, clip, opacity);
        return true;
    }

    private void ShowText(Node node, string text, ATypeface typeface, float sizePx, int color, ProjectionTextStyle style,
        float letterSpacing, bool centerVertically, PixelRect rect, PixelRect clip, double opacity)
    {
        var view = node.Text ??= RentText();
        if (!ReferenceEquals(node.TextValue, text) && !string.Equals(node.TextValue, text, StringComparison.Ordinal))
        {
            node.TextValue = text;
            view.Text = text;
        }

        if (!ReferenceEquals(node.Typeface, typeface))
        {
            node.Typeface = typeface;
            view.Typeface = typeface;
        }

        if (node.TextSizePx != sizePx)
        {
            node.TextSizePx = sizePx;
            view.SetTextSize(AComplexUnitType.Px, sizePx);
        }

        if (node.TextColor != color)
        {
            node.TextColor = color;
            view.SetTextColor(new global::Android.Graphics.Color(color));
        }

        if (node.LetterSpacing != letterSpacing)
        {
            node.LetterSpacing = letterSpacing;
            view.LetterSpacing = letterSpacing;
        }

        if (!node.HasStyle || node.Style != style || node.CenterVertically != centerVertically)
        {
            node.HasStyle = true;
            node.Style = style;
            node.CenterVertically = centerVertically;
            var horizontal = style.Alignment switch
            {
                ProjectionTextAlignment.Center => AGravityFlags.CenterHorizontal,
                ProjectionTextAlignment.End => AGravityFlags.End,
                _ => AGravityFlags.Start,
            };
            view.Gravity = horizontal | (centerVertically ? AGravityFlags.CenterVertical : AGravityFlags.Top);
            view.SetHorizontallyScrolling(style.SingleLine);
            view.SetMaxLines(style.MaxLines);
            view.Ellipsize = style.Ellipsize ? ATruncateAt.End : null;
        }

        var alpha = (float)Math.Clamp(opacity, 0, 1);
        if (view.Alpha != alpha)
        {
            view.Alpha = alpha;
        }

        _layer.Place(view, rect, _order++);
        ApplyClip(view, ref node.TextClip, rect, clip);
        _textCount++;
    }

    private void GetBoxPaint(FrameworkElement element, double opacity, out int fill, out int stroke, out float strokeWidth, out float corner, out bool isEllipse)
    {
        fill = 0;
        stroke = 0;
        strokeWidth = 0;
        corner = 0;
        isEllipse = false;

        switch (element)
        {
            case Border border:
                fill = BrushToArgb(border.Background, opacity, 0);
                stroke = BrushToArgb(border.BorderBrush, opacity, 0);
                strokeWidth = (float)MaxSide(border.BorderThickness);
                corner = (float)border.CornerRadius.TopLeft;
                break;

            case Panel panel:
                fill = BrushToArgb(panel.Background, opacity, 0);
                break;

            case ContentPresenter presenter:
                fill = BrushToArgb(presenter.Background, opacity, 0);
                stroke = BrushToArgb(presenter.BorderBrush, opacity, 0);
                strokeWidth = (float)MaxSide(presenter.BorderThickness);
                corner = (float)presenter.CornerRadius.TopLeft;
                break;

            case UserControl userControl:
                // A Page / UserControl draws its own Background (it has no template to do it).
                fill = BrushToArgb(userControl.Background, opacity, 0);
                break;

            case Shape shape when shape is Rectangle or Ellipse:
                fill = BrushToArgb(shape.Fill, opacity, 0);
                stroke = BrushToArgb(shape.Stroke, opacity, 0);
                strokeWidth = (float)shape.StrokeThickness;
                isEllipse = shape is Ellipse;
                if (shape is Rectangle rectangle)
                {
                    corner = (float)rectangle.RadiusX;
                }

                break;
        }
    }

    private static ProjectionLabelKey GetLabelKey(FrameworkElement element)
    {
        var disabled = element is Control { IsEnabled: false };
        return element switch
        {
            PasswordBox => new ProjectionLabelKey(ProjectionLabelFormat.TypeOnly, IsDisabled: disabled),
            TextBox textBox => new ProjectionLabelKey(ProjectionLabelFormat.Text, textBox.Text, textBox.PlaceholderText, IsDisabled: disabled),
            Selector selector => new ProjectionLabelKey(ProjectionLabelFormat.Selection, Value: selector.Items.Count, Index: selector.SelectedIndex, IsDisabled: disabled),
            ProgressBar progress => new ProjectionLabelKey(ProjectionLabelFormat.Range, Value: progress.Value, Maximum: progress.Maximum, Flag: progress.IsIndeterminate, IsDisabled: disabled),
            RangeBase range => new ProjectionLabelKey(ProjectionLabelFormat.Range, Value: range.Value, Maximum: range.Maximum, IsDisabled: disabled),
            ToggleSwitch toggle => new ProjectionLabelKey(ProjectionLabelFormat.Toggle, Flag: toggle.IsOn, IsDisabled: disabled),
            ToggleButton toggleButton => new ProjectionLabelKey(ProjectionLabelFormat.Toggle, Flag: toggleButton.IsChecked == true, IsDisabled: disabled),
            ContentControl content => new ProjectionLabelKey(ProjectionLabelFormat.Content, content.Content as string ?? content.Content?.GetType().Name, IsDisabled: disabled),
            Image image => new ProjectionLabelKey(ProjectionLabelFormat.Source, DescribeSource(image.Source)),
            AnimatedVisualPlayer player => new ProjectionLabelKey(ProjectionLabelFormat.Source, player.Source?.GetType().Name),
            _ => new ProjectionLabelKey(ProjectionLabelFormat.TypeOnly, IsDisabled: disabled),
        };
    }

    private static string DescribeSource(ImageSource source) => source switch
    {
        null => null,
        BitmapImage { UriSource: { } uri } => uri.OriginalString,
        SvgImageSource { UriSource: { } uri } => uri.OriginalString,
        _ => source.GetType().Name,
    };

    private static int BrushToArgb(Brush brush, double opacity, int fallback)
    {
        if (brush is SolidColorBrush solid)
        {
            var c = solid.Color;
            return ProjectionColor.ToArgb(c.A, c.R, c.G, c.B, solid.Opacity * opacity);
        }

        return fallback;
    }

    private static double MaxSide(Thickness t) => Math.Max(Math.Max(t.Left, t.Right), Math.Max(t.Top, t.Bottom));

    private static void ApplyClip(AView view, ref PixelRect lastClip, PixelRect rect, PixelRect clip)
    {
        var visible = rect.IsInside(clip) ? rect : rect.Intersect(clip);
        if (visible == lastClip)
        {
            return;
        }

        lastClip = visible;
        view.ClipBounds = visible == rect
            ? null
            : new ARect(visible.Left - rect.Left, visible.Top - rect.Top, visible.Right - rect.Left, visible.Bottom - rect.Top);
    }

    private void RemoveStaleNodes()
    {
        _stale.Clear();
        foreach (var pair in _nodes)
        {
            if (pair.Value.Generation != _generation)
            {
                _stale.Add(pair.Key);
            }
        }

        foreach (var element in _stale)
        {
            var node = _nodes[element];
            _nodes.Remove(element);
            ReleaseViews(node);
        }

        _stale.Clear();
    }

    private void ReleaseAll()
    {
        foreach (var node in _nodes.Values)
        {
            ReleaseViews(node);
        }

        _nodes.Clear();
    }

    private void ReleaseViews(Node node)
    {
        if (node.Box != null)
        {
            ReturnBox(node.Box);
            node.Box = null;
            node.DescribedLabel = null;
        }

        if (node.Text != null)
        {
            ReturnText(node.Text);
            node.Text = null;
        }

        node.BoxClip = default;
        node.ResetTextCache();
    }

    private ProjectionBoxView RentBox() => _boxPool.Count > 0 ? _boxPool.Pop() : new ProjectionBoxView(_activity);

    private void ReturnBox(ProjectionBoxView box)
    {
        _layer?.Remove(box);
        box.ClipBounds = null;
        box.ContentDescription = null;
        _boxPool.Push(box);
    }

    private ATextView RentText()
    {
        if (_textPool.Count > 0)
        {
            return _textPool.Pop();
        }

        var view = new ATextView(_activity)
        {
            Focusable = false,
            Clickable = false,
            BreakStrategy = ABreakStrategy.Simple,
            HyphenationFrequency = AHyphenationFrequency.None,
        };
        view.SetIncludeFontPadding(false);
        view.SetPadding(0, 0, 0, 0);
        view.SetMinHeight(0);
        view.SetMinimumHeight(0);
        view.SetLineSpacing(0, 1);
        view.FallbackLineSpacing = false;
        return view;
    }

    private void ReturnText(ATextView view)
    {
        _layer?.Remove(view);
        view.ClipBounds = null;
        view.LetterSpacing = 0;
        _textPool.Push(view);
    }

    private void LogSummaryIfChanged()
    {
        if (_textCount == _lastTextCount && _boxCount == _lastBoxCount && _labelCount == _lastLabelCount)
        {
            return;
        }

        _lastTextCount = _textCount;
        _lastBoxCount = _boxCount;
        _lastLabelCount = _labelCount;
        if (_log.IsEnabled(LogLevel.Information))
        {
            _log.LogInformation(
                "Projection: {Texts} text views, {Boxes} boxes ({Placeholders} labelled placeholders) for {Elements} elements, density {Density}.",
                _textCount, _boxCount, _labelCount, _elementCount, _density);
        }

        if (_log.IsEnabled(LogLevel.Debug))
        {
            // The projected views in draw order (diagnostics only; allocates, and only when the projection changed).
            foreach (var node in _nodes.Values)
            {
                if (node.Text?.LayoutParameters is ProjectionLayer.ProjectionLayoutParams tlp)
                {
                    _log.LogDebug("  text \"{Text}\" at {Left},{Top} {Width}x{Height}", node.TextValue, tlp.Rect.Left, tlp.Rect.Top, tlp.Rect.Width, tlp.Rect.Height);
                }

                if (node.Label != null && node.Box?.LayoutParameters is ProjectionLayer.ProjectionLayoutParams blp)
                {
                    _log.LogDebug("  placeholder {Label} at {Left},{Top} {Width}x{Height}", node.Label, blp.Rect.Left, blp.Rect.Top, blp.Rect.Width, blp.Rect.Height);
                }
            }
        }
    }

    /// <summary>The native views and cached values of one projected element.</summary>
    private sealed class Node
    {
        public int Generation;
        public ProjectionBoxView Box;
        public PixelRect BoxClip;
        public ProjectionLabelKey LabelKey;
        public string Label;
        public ATextView Text;
        public PixelRect TextClip;
        public string TextValue;
        public ATypeface Typeface;
        public float TextSizePx;
        public int TextColor;
        public float LetterSpacing;
        public bool HasStyle;
        public ProjectionTextStyle Style;
        public bool CenterVertically;
        public string DescribedLabel;

        public void ResetTextCache()
        {
            TextClip = default;
            TextValue = null;
            Typeface = null;
            TextSizePx = 0;
            TextColor = 0;
            LetterSpacing = 0;
            HasStyle = false;
        }
    }
}
