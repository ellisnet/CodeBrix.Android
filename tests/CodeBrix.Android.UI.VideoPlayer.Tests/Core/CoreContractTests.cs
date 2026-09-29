using System;
using System.IO;
using System.Linq;
using System.Reflection;
using CodeBrix.Android.Tests.Shared;
using CodeBrix.Android.UI.VideoPlayer.Portable;
using CodeBrix.Platform.Foundation.Extensibility;
using CodeBrix.Platform.UI.VideoPlayer.Skia;
using CodeBrix.Platform.UI.VideoPlayer.Skia.Contracts;
using CodeBrix.Platform.UI.VideoPlayer.Skia.Internal;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Android.UI.VideoPlayer.Tests.Core;

public class CoreContractTests
{
    private const string AndroidName = "CodeBrix.Android.UI.VideoPlayer";
    private const string VideoCore = "CodeBrix.Platform.UI.VideoPlayer.Core";

    [Fact]
    public void The_VideoPlayer_Core_grants_its_internals_to_the_Android_assembly_name()
    {
        //Arrange
        //Act
        var grants = CoreMetadata.InternalsVisibleTo(VideoCore);
        var ui = CoreMetadata.InternalsVisibleTo("CodeBrix.Platform.UI.Core");

        //Assert
        grants.Should().Contain(AndroidName);
        grants.Should().Contain(AndroidName + ".Tests");
        ui.Should().Contain(AndroidName, "the surface element overrides UI.Core's internal CreateElementVisual");
    }

    [Fact]
    public void The_VideoPlayer_Core_loads_the_Android_assembly_by_name_for_its_asset_location()
    {
        //Arrange
        //Act
        var names = PlatformContract.PlatformAssemblyNames;

        //Assert
        names.Should().Contain(AndroidName);
    }

    [Fact]
    public void The_element_is_not_in_the_Core_so_the_Android_add_in_provides_it()
    {
        //Arrange
        //Act
        var types = CoreMetadata.TypeNames(VideoCore);

        //Assert
        types.Should().NotContain("CodeBrix.Platform.UI.VideoPlayer.Skia.VideoPlayer");
        types.Should().NotContain("CodeBrix.Platform.UI.VideoPlayer.Skia.IVideoLayer");
        types.Should().NotContain("CodeBrix.Platform.UI.VideoPlayer.Skia.VideoComposingEventArgs");
        types.Should().Contain("CodeBrix.Platform.UI.VideoPlayer.Skia.VideoPlayerFailedEventArgs");
        types.Should().Contain("CodeBrix.Platform.UI.VideoPlayer.Skia.Internal.VideoSourceResolver");
        types.Should().Contain("CodeBrix.Platform.UI.VideoPlayer.Skia.Internal.VideoPlayerRules");
        types.Should().Contain("CodeBrix.Platform.UI.VideoPlayer.Skia.Contracts.IAssetLocation");
    }

    [Fact]
    public void The_VideoPlayer_Core_names_neither_the_video_engine_nor_Skia()
    {
        //Arrange
        //Act
        var references = CoreMetadata.References(VideoCore);

        //Assert
        references.Should().NotContain("CodeBrix.VideoPlayback");
        references.Should().NotContain("SkiaSharp");
        references.Should().NotContain("CodeBrix.Audio");
    }

    [Fact]
    public void An_ms_appx_video_resolves_under_the_asset_copy_root_to_the_APK_asset_path()
    {
        //Arrange
        var root = Path.Combine(Path.GetTempPath(), "ap7c-video-root", "codebrix-audio-assets");
        TestAssets.EnsureRegistered(root);

        //Act
        var (path, stream) = VideoSourceResolver.Resolve("ms-appx:///Assets/Clips/My%20Clip.cbv");

        //Assert
        stream.Should().BeNull();
        path.Should().Be(Path.Join(root, "Assets/Clips/My Clip.cbv"));
        PackagedAssetPaths.AssetPathOf(root, path).Should().Be("Assets/Clips/My Clip.cbv");
    }

    [Fact]
    public void An_http_source_and_a_plain_path_are_left_to_the_engine()
    {
        //Arrange
        var root = Path.Combine(Path.GetTempPath(), "ap7c-video-root", "codebrix-audio-assets");
        TestAssets.EnsureRegistered(root);

        //Act
        var (web, _) = VideoSourceResolver.Resolve("https://example.org/clip.webm");
        var (file, _) = VideoSourceResolver.Resolve("/sdcard/Movies/clip.webm");

        //Assert
        web.Should().Be("https://example.org/clip.webm");
        PackagedAssetPaths.AssetPathOf(root, file).Should().BeNull();
    }

    private sealed class TestAssets : IAssetLocation
    {
        private static readonly object Gate = new();
        private static TestAssets _registered;

        private TestAssets(string root) => InstalledPath = root;

        public string InstalledPath { get; }

        public Assembly ApplicationAssembly => typeof(TestAssets).Assembly;

        internal static void EnsureRegistered(string root)
        {
            lock (Gate)
            {
                if (_registered == null)
                {
                    _registered = new TestAssets(root);
                    ApiExtensibility.Register(typeof(IAssetLocation), _ => _registered);
                }
            }
        }
    }
}
