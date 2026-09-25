using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace JustBetweenUs.Controls;

/// <summary>
/// A Button control that displays an embedded-resource image alongside optional text.
/// Supports the embedded://AssemblyName/ResourceName URI scheme via <see cref="EmbeddedImage"/>,
/// as well as standard URIs (ms-appx:///, https://).
/// Set <see cref="ImageUriSource"/> and/or <see cref="Text"/> to control what is displayed.
/// </summary>
public sealed class EmbeddedImageButton : Button
{
    public EmbeddedImageButton()
    {
        DefaultStyleKey = typeof(Button);
        CornerRadius = new CornerRadius(4);
    }

    public static readonly DependencyProperty ImageUriSourceProperty =
        DependencyProperty.Register(
            nameof(ImageUriSource), typeof(string), typeof(EmbeddedImageButton),
            new PropertyMetadata(null, OnLayoutPropertyChanged));

    public static readonly DependencyProperty TextProperty =
        DependencyProperty.Register(
            nameof(Text), typeof(string), typeof(EmbeddedImageButton),
            new PropertyMetadata(null, OnLayoutPropertyChanged));

    public static readonly DependencyProperty ImagePositionProperty =
        DependencyProperty.Register(
            nameof(ImagePosition), typeof(ImagePosition), typeof(EmbeddedImageButton),
            new PropertyMetadata(ImagePosition.Left, OnLayoutPropertyChanged));

    public static readonly DependencyProperty SpacingProperty =
        DependencyProperty.Register(
            nameof(Spacing), typeof(double), typeof(EmbeddedImageButton),
            new PropertyMetadata(10.0, OnLayoutPropertyChanged));

    public static readonly DependencyProperty ImageWidthProperty =
        DependencyProperty.Register(
            nameof(ImageWidth), typeof(double), typeof(EmbeddedImageButton),
            new PropertyMetadata(double.NaN, OnLayoutPropertyChanged));

    public static readonly DependencyProperty ImageHeightProperty =
        DependencyProperty.Register(
            nameof(ImageHeight), typeof(double), typeof(EmbeddedImageButton),
            new PropertyMetadata(double.NaN, OnLayoutPropertyChanged));

    public static readonly DependencyProperty TextVerticalAlignmentProperty =
        DependencyProperty.Register(
            nameof(TextVerticalAlignment), typeof(VerticalAlignment), typeof(EmbeddedImageButton),
            new PropertyMetadata(VerticalAlignment.Center, OnLayoutPropertyChanged));

    public static readonly DependencyProperty TextHorizontalAlignmentProperty =
        DependencyProperty.Register(
            nameof(TextHorizontalAlignment), typeof(HorizontalAlignment), typeof(EmbeddedImageButton),
            new PropertyMetadata(HorizontalAlignment.Center, OnLayoutPropertyChanged));

    /// <summary>
    /// The URI of the image source. Supports embedded://AssemblyName/ResourceName
    /// for embedded resources, or standard URIs (ms-appx:///, https://).
    /// </summary>
    public string ImageUriSource
    {
        get => (string)GetValue(ImageUriSourceProperty);
        set => SetValue(ImageUriSourceProperty, value);
    }

    /// <summary>
    /// The text displayed on the button.
    /// </summary>
    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    /// <summary>
    /// The position of the image relative to the text.
    /// Defaults to <see cref="Controls.ImagePosition.Left"/>.
    /// </summary>
    public ImagePosition ImagePosition
    {
        get => (ImagePosition)GetValue(ImagePositionProperty);
        set => SetValue(ImagePositionProperty, value);
    }

    /// <summary>
    /// The spacing between the image and text.
    /// Defaults to 10.
    /// </summary>
    public double Spacing
    {
        get => (double)GetValue(SpacingProperty);
        set => SetValue(SpacingProperty, value);
    }

    /// <summary>
    /// The width of the image. Leave unset for automatic sizing.
    /// </summary>
    public double ImageWidth
    {
        get => (double)GetValue(ImageWidthProperty);
        set => SetValue(ImageWidthProperty, value);
    }

    /// <summary>
    /// The height of the image. Leave unset for automatic sizing.
    /// </summary>
    public double ImageHeight
    {
        get => (double)GetValue(ImageHeightProperty);
        set => SetValue(ImageHeightProperty, value);
    }

    /// <summary>
    /// The vertical alignment of the text within the button.
    /// Defaults to <see cref="VerticalAlignment.Center"/>.
    /// </summary>
    public VerticalAlignment TextVerticalAlignment
    {
        get => (VerticalAlignment)GetValue(TextVerticalAlignmentProperty);
        set => SetValue(TextVerticalAlignmentProperty, value);
    }

    /// <summary>
    /// The horizontal alignment of the text within the button.
    /// Defaults to <see cref="HorizontalAlignment.Center"/>.
    /// </summary>
    public HorizontalAlignment TextHorizontalAlignment
    {
        get => (HorizontalAlignment)GetValue(TextHorizontalAlignmentProperty);
        set => SetValue(TextHorizontalAlignmentProperty, value);
    }

    private static void OnLayoutPropertyChanged(
        DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        ((EmbeddedImageButton)d).UpdateContent();
    }

    /// <summary>
    /// Intercepts content set via XAML element content (e.g. <c>&lt;EmbeddedImageButton&gt;Decrypt&lt;/EmbeddedImageButton&gt;</c>)
    /// and treats string content as the <see cref="Text"/> property, so the image+text
    /// layout is preserved rather than being overwritten.
    /// </summary>
    protected override void OnContentChanged(object oldContent, object newContent)
    {
        base.OnContentChanged(oldContent, newContent);

        if (!_isUpdatingContent && newContent is string text)
        {
            text = text.Trim();
            if (text.Length > 0)
            {
                Text = text;
            }
        }
    }

    private bool _isUpdatingContent;

    private void UpdateContent()
    {
        _isUpdatingContent = true;
        try
        {
            var hasImage = !string.IsNullOrWhiteSpace(ImageUriSource);
            var hasText = !string.IsNullOrWhiteSpace(Text);

            if (!hasImage && !hasText)
            {
                Content = null;
                return;
            }

            if (hasImage && hasText)
            {
                var isHorizontal = ImagePosition is ImagePosition.Left or ImagePosition.Right;
                var imageFirst = ImagePosition is ImagePosition.Left or ImagePosition.Top;

                var panel = new StackPanel
                {
                    Orientation = isHorizontal ? Orientation.Horizontal : Orientation.Vertical,
                    Spacing = Spacing
                };

                panel.Children.Add(imageFirst ? CreateImage() : CreateTextBlock());
                panel.Children.Add(imageFirst ? CreateTextBlock() : CreateImage());

                Content = panel;
            }
            else if (hasImage)
            {
                Content = CreateImage();
            }
            else
            {
                Content = CreateTextBlock();
            }
        }
        finally
        {
            _isUpdatingContent = false;
        }
    }

    private EmbeddedImage CreateImage()
    {
        var image = new EmbeddedImage { UriSource = ImageUriSource };
        if (!double.IsNaN(ImageWidth)) image.Width = ImageWidth;
        if (!double.IsNaN(ImageHeight)) image.Height = ImageHeight;
        return image;
    }

    private TextBlock CreateTextBlock()
    {
        return new TextBlock
        {
            Text = Text,
            VerticalAlignment = TextVerticalAlignment,
            HorizontalAlignment = TextHorizontalAlignment
        };
    }
}
