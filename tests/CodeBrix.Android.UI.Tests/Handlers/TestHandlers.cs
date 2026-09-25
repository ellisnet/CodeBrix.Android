using System.Collections.Generic;
using CodeBrix.Android.UI.Handlers;
using CodeBrix.Platform.UI.Contracts;
using Microsoft.UI.Xaml;

namespace CodeBrix.Android.UI.Tests.Handlers;

/// <summary>A platform-free handler that records what the infrastructure does to it.</summary>
internal sealed class RecordingHandler : ElementHandler<UIElement, RecordingView>
{
    internal RecordingHandler(IPropertyMapper mapper, CommandMapper commandMapper = null)
        : base(mapper, commandMapper)
    {
    }

    internal List<string> Log { get; } = new();

    internal int CreatedViews { get; private set; }

    public override ElementHandlerCapabilities Capabilities => ElementHandlerCapabilities.OwnsChildren;

    protected override RecordingView CreatePlatformElement()
    {
        CreatedViews++;
        Log.Add("create");
        return new RecordingView();
    }

    protected override void ConnectHandler(RecordingView platformView) => Log.Add("connect-view");

    protected override void DisconnectHandler(RecordingView platformView) => Log.Add("disconnect-view");

    protected override void OnConnected() => Log.Add("connected");

    protected override void OnArranged(Windows.Foundation.Rect finalRect, bool changed) => Log.Add(changed ? "arranged-changed" : "arranged-same");
}

/// <summary>The stand-in platform view of <see cref="RecordingHandler"/>.</summary>
internal sealed class RecordingView
{
}
