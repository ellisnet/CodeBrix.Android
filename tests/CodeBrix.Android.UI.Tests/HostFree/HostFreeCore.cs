using System;
using CodeBrix.Android.UI.Composition.HostFree;
using CodeBrix.Android.UI.Dispatching.HostFree;
using CodeBrix.Platform.Foundation.Extensibility;
using CodeBrix.Platform.UI.Contracts;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;
using Windows.UI.Text;
using Xunit;

namespace CodeBrix.Android.UI.Tests.HostFree;

/// <summary>
/// Runs the extracted CodeBrix.Platform Core without a device: the net10.0 flavors of
/// CodeBrix.Android.UI.Dispatching (a managed pump the test drains) and
/// CodeBrix.Android.UI.Composition (the inert composition platform Android uses), plus test
/// doubles of the UI.Core text / font / rendering / focus contracts (deterministic text
/// metrics: every character is <see cref="CharWidthEm"/> em wide, a line is
/// <see cref="LineHeightEm"/> em high).
/// </summary>
internal static class HostFreeCore
{
    /// <summary>The width of one character in em (test text metrics).</summary>
    internal const double CharWidthEm = 0.5;

    /// <summary>The height of one line in em (test text metrics).</summary>
    internal const double LineHeightEm = 1.25;

    private static readonly object _gate = new();
    private static ManagedDispatcherPump _pump;

    /// <summary>Registers the host-free platform (once) and returns the dispatcher pump.</summary>
    internal static ManagedDispatcherPump EnsureInitialized()
    {
        lock (_gate)
        {
            if (_pump != null)
            {
                return _pump;
            }

            _pump = ManagedDispatcherPump.EnsureRegistered();
            HostFreeCompositionBootstrap.EnsureRegistered();

            var text = new FakeTextPlatform();
            var fonts = new FakeFontPlatform();
            var rendering = new FakeRenderingPlatform();
            var focus = new FakeFocusPlatform();
            var application = new FakeApplicationPlatform();
            ApiExtensibility.Register(typeof(IApplicationPlatform), _ => application);
            ApiExtensibility.Register(typeof(ITextPlatform), _ => text);
            ApiExtensibility.Register(typeof(IFontPlatform), _ => fonts);
            ApiExtensibility.Register(typeof(IRenderingPlatform), _ => rendering);
            ApiExtensibility.Register(typeof(IFocusPlatform), _ => focus);
            return _pump;
        }
    }

    /// <summary>Drains the UI work queued so far.</summary>
    internal static void RunPending() => _pump?.RunPending();

    /// <summary>Measures and arranges an element at a size, then drains the queued UI work.</summary>
    internal static void Layout(UIElement element, double width, double height)
    {
        element.Measure(new Size(width, height));
        element.Arrange(new Rect(0, 0, width, height));
        RunPending();
    }

    private sealed class FakeTextPlatform : ITextPlatform
    {
        public ITextLayout EmptyLayout { get; } = new FakeTextLayout();

        public ITextLayout CreateLayout(TextBlock textBlock, Size availableSize, out Size desiredSize)
        {
            var text = textBlock.Text ?? string.Empty;
            var lines = text.Length == 0 ? 0 : text.Split('\n').Length;
            var longest = 0;
            foreach (var line in text.Split('\n'))
            {
                longest = Math.Max(longest, line.Length);
            }

            desiredSize = new Size(longest * textBlock.FontSize * CharWidthEm, lines * textBlock.FontSize * LineHeightEm);
            return EmptyLayout;
        }

        public ITextBoxPlatform CreateTextBoxPlatform(TextBox textBox) => null;
    }

    private sealed class FakeTextLayout : ITextLayout
    {
        public bool IsBaseDirectionRightToLeft => false;

        public Rect GetRectForIndex(int adjustedIndex) => default;

        public int GetIndexAt(Point p, bool ignoreEndingNewLine, bool extendedSelection) => 0;

        public Hyperlink GetHyperlinkAt(Point point) => null;

        public (int, int) GetWordAt(int index, bool right) => (index, index);

        public (int, int, bool, bool, int) GetLineAt(int index) => (0, 0, false, false, 0);
    }

    private sealed class FakeFontPlatform : IFontPlatform
    {
        public System.Threading.Tasks.Task<bool> PreloadAsync(FontFamily family, FontWeight weight, FontStretch stretch, FontStyle style) =>
            System.Threading.Tasks.Task.FromResult(true);
    }

    private sealed class FakeRenderingPlatform : IRenderingPlatform
    {
        public void OnRenderFrameOpportunity(XamlRoot xamlRoot)
        {
        }

        public ICompositionTargetPlatform CreateCompositionTargetPlatform(Microsoft.UI.Xaml.Media.CompositionTarget target) => new FakeCompositionTarget();
    }

    private sealed class FakeCompositionTarget : ICompositionTargetPlatform
    {
        public bool CanRecordFrame() => false;

        public void RecordFrame()
        {
        }

        public void UpdateNativeElementsOrder()
        {
        }
    }

    private sealed class FakeApplicationPlatform : IApplicationPlatform
    {
        public void RegisterExtensions()
        {
        }
    }

    private sealed class FakeFocusPlatform : IFocusPlatform
    {
        public void FocusNative(UIElement element)
        {
        }
    }
}

/// <summary>The xUnit collection of every test that uses the host-free Core (Core's UI state is static: no parallel runs).</summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class HostFreeCoreCollection
{
    /// <summary>The collection name.</summary>
    public const string Name = "HostFreeCore";
}
