using System;
using CodeBrix.Android.UI.Handlers;
using CodeBrix.Android.UI.MediaPlayer.Hosting;
using CodeBrix.Android.UI.MediaPlayer.Portable;
using CodeBrix.Platform.UI.Contracts;
using AContext = global::Android.Content.Context;
using ATextureView = global::Android.Views.TextureView;
using AViewGroup = global::Android.Views.ViewGroup;

namespace CodeBrix.Android.UI.MediaPlayer.Handlers;

/// <summary>
/// The handler of a <see cref="VideoHostElement"/>: a view group (clipping to the element) holding the TextureView the
/// engine renders into, laid out at the picture's destination rectangle for the element's Stretch
/// (<see cref="VideoFit"/>) - Uniform leaves the presenter's black background showing as bars, UniformToFill cuts the
/// edges.
/// </summary>
internal sealed class VideoHostHandler : ViewHandler<VideoHostElement, VideoHostHandler.VideoSurfaceLayout>
{
    /// <summary>The host element's mapper (only what every view maps).</summary>
    public static readonly PropertyMapper<VideoHostElement, VideoHostHandler> Mapper = new(ViewMappers.ViewMapper);

    /// <summary>Creates the handler.</summary>
    public VideoHostHandler()
        : base(Mapper)
    {
    }

    /// <inheritdoc />
    public override ElementHandlerCapabilities Capabilities => ElementHandlerCapabilities.OwnsVisuals;

    /// <inheritdoc />
    protected override VideoSurfaceLayout CreatePlatformView() => new(Context);

    /// <inheritdoc />
    protected override void ConnectHandler(VideoSurfaceLayout platformView)
    {
        base.ConnectHandler(platformView);
        VirtualElement.StretchChanged += OnFitChanged;
        VirtualElement.EngineChanged += OnEngineChanged;
        OnEngineChanged(this, EventArgs.Empty);
    }

    /// <inheritdoc />
    protected override void DisconnectHandler(VideoSurfaceLayout platformView)
    {
        if (Element is VideoHostElement element)
        {
            element.StretchChanged -= OnFitChanged;
            element.EngineChanged -= OnEngineChanged;
            element.Engine?.AttachSurface(null);
        }

        platformView.Engine = null;
        base.DisconnectHandler(platformView);
    }

    private void OnEngineChanged(object sender, EventArgs e)
    {
        if (NativeView is not VideoSurfaceLayout view || Element is not VideoHostElement element)
        {
            return;
        }

        view.Engine = element.Engine;
        view.Stretch = element.Stretch;
        element.Engine?.AttachSurface(view.Surface);
    }

    private void OnFitChanged(object sender, EventArgs e)
    {
        if (NativeView is VideoSurfaceLayout view && Element is VideoHostElement element)
        {
            view.Stretch = element.Stretch;
        }
    }

    /// <summary>The native view: a clipping group laying its TextureView out at the picture's destination rectangle.</summary>
    internal sealed class VideoSurfaceLayout : AViewGroup
    {
        private Android.AndroidMediaPlayerExtension _engine;
        private VideoStretch _stretch = VideoStretch.Uniform;

        /// <summary>Creates the view.</summary>
        /// <param name="context">The activity context.</param>
        public VideoSurfaceLayout(AContext context)
            : base(context)
        {
            SetClipChildren(true);
            Surface = new ATextureView(context);
            AddView(Surface);
        }

        /// <summary>The TextureView the engine renders into.</summary>
        internal ATextureView Surface { get; }

        /// <summary>The engine (its picture size decides the layout).</summary>
        internal Android.AndroidMediaPlayerExtension Engine
        {
            get => _engine;
            set
            {
                if (_engine != null)
                {
                    _engine.VideoSizeChanged -= OnVideoSizeChanged;
                }

                _engine = value;
                if (value != null)
                {
                    value.VideoSizeChanged += OnVideoSizeChanged;
                }

                RequestLayout();
            }
        }

        /// <summary>How the picture is fitted.</summary>
        internal VideoStretch Stretch
        {
            get => _stretch;
            set
            {
                _stretch = value;
                RequestLayout();
            }
        }

        /// <inheritdoc />
        protected override void OnMeasure(int widthMeasureSpec, int heightMeasureSpec)
        {
            var width = MeasureSpec.GetSize(widthMeasureSpec);
            var height = MeasureSpec.GetSize(heightMeasureSpec);
            SetMeasuredDimension(width, height);
            var (_, _, w, h) = Destination(width, height);
            Surface.Measure(MeasureSpec.MakeMeasureSpec((int)Math.Round(w), global::Android.Views.MeasureSpecMode.Exactly),
                MeasureSpec.MakeMeasureSpec((int)Math.Round(h), global::Android.Views.MeasureSpecMode.Exactly));
        }

        /// <inheritdoc />
        protected override void OnLayout(bool changed, int l, int t, int r, int b)
        {
            var (x, y, w, h) = Destination(r - l, b - t);
            var left = (int)Math.Round(x);
            var top = (int)Math.Round(y);
            Surface.Layout(left, top, left + (int)Math.Round(w), top + (int)Math.Round(h));
        }

        private (double Left, double Top, double Width, double Height) Destination(int width, int height)
        {
            var size = _engine?.VideoSize ?? default;
            return VideoFit.Destination(size.Width, size.Height, width, height, _stretch);
        }

        private void OnVideoSizeChanged(object sender, EventArgs e) => RequestLayout();
    }
}
