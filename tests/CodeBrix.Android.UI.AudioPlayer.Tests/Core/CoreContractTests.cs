using System;
using System.IO;
using System.Linq;
using System.Reflection;
using CodeBrix.Android.Tests.Shared;
using CodeBrix.Android.UI.AudioPlayer.Portable;
using CodeBrix.Platform.Foundation.Extensibility;
using CodeBrix.Platform.UI.AudioPlayer.Skia;
using CodeBrix.Platform.UI.AudioPlayer.Skia.Contracts;
using CodeBrix.Platform.UI.AudioPlayer.Skia.Internal;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Android.UI.AudioPlayer.Tests.Core;

public class CoreContractTests
{
    private const string AndroidName = "CodeBrix.Android.UI.AudioPlayer";
    private const string AudioCore = "CodeBrix.Platform.UI.AudioPlayer.Core";

    [Fact]
    public void The_AudioPlayer_Core_grants_its_internals_to_the_Android_assembly_name()
    {
        //Arrange
        //Act
        var grants = CoreMetadata.InternalsVisibleTo(AudioCore);

        //Assert
        grants.Should().Contain(AndroidName);
        grants.Should().Contain(AndroidName + ".Tests");
    }

    [Fact]
    public void The_AudioPlayer_Core_loads_the_Android_assembly_by_name_for_its_contracts()
    {
        //Arrange
        //Act
        var names = PlatformContract.PlatformAssemblyNames;

        //Assert
        names.Should().Contain(AndroidName);
    }

    [Fact]
    public void The_AudioPlayer_Core_carries_the_element_its_transport_and_the_three_platform_contracts()
    {
        //Arrange
        //Act
        var types = CoreMetadata.TypeNames(AudioCore);

        //Assert
        types.Should().Contain("CodeBrix.Platform.UI.AudioPlayer.Skia.AudioPlayer");
        types.Should().Contain("CodeBrix.Platform.UI.AudioPlayer.Skia.SoundEffect");
        types.Should().Contain("CodeBrix.Platform.UI.AudioPlayer.Skia.Engine.AudioTransport");
        types.Should().Contain("CodeBrix.Platform.UI.AudioPlayer.Skia.Contracts.IAudioPlayerPlatform");
        types.Should().Contain("CodeBrix.Platform.UI.AudioPlayer.Skia.Contracts.IAudioOutputPlatform");
        types.Should().Contain("CodeBrix.Platform.UI.AudioPlayer.Skia.Contracts.IAssetLocation");
    }

    [Fact]
    public void MidiPlayer_is_not_in_the_Core_so_the_Android_add_in_provides_it()
    {
        //Arrange
        //Act
        var types = CoreMetadata.TypeNames(AudioCore);

        //Assert
        types.Should().NotContain("CodeBrix.Platform.UI.AudioPlayer.Skia.MidiPlayer");
        types.Should().NotContain("CodeBrix.Platform.UI.AudioPlayer.Skia.MidiInstrumentKind");
        types.Should().Contain("CodeBrix.Platform.UI.AudioPlayer.Skia.Internal.AudioSourceResolver"); // what the port reuses
    }

    [Fact]
    public void The_AudioPlayer_Core_names_no_audio_engine()
    {
        //Arrange
        //Act
        var references = CoreMetadata.References(AudioCore);

        //Assert
        references.Should().NotContain("CodeBrix.Audio");
        references.Should().NotContain("CodeBrix.Audio.Engine");
        references.Should().NotContain("SkiaSharp");
    }

    [Fact]
    public void The_output_contract_is_the_members_the_Android_output_implements()
    {
        //Arrange
        //Act
        var player = Members(typeof(IAudioPlayerPlatform));
        var output = Members(typeof(IAudioOutputPlatform));
        var assets = Members(typeof(IAssetLocation));

        //Assert
        player.Should().Equal("Load(Stream)", "Load(String)", "Pause()", "Play()", "Seek(TimeSpan)", "Stop()", "add_PlaybackEnded(EventHandler)", "get_Duration()",
            "get_IsLooping()", "get_Position()", "get_Volume()", "remove_PlaybackEnded(EventHandler)", "set_IsLooping(Boolean)", "set_Volume(Single)");
        output.Should().Equal("ExplainFailure(String,String)", "LoadSoundEffect(Byte[])", "PlaySoundEffect(IDisposable,Single)", "PlaySoundEffectOnce(Stream,Single)");
        assets.Should().Equal("get_ApplicationAssembly()", "get_InstalledPath()");
    }

    [Fact]
    public void An_ms_appx_source_resolves_under_the_asset_copy_root_to_the_APK_asset_path()
    {
        //Arrange
        var root = Path.Combine(Path.GetTempPath(), "ap7c-audio-root", "codebrix-audio-assets");
        TestAssets.EnsureRegistered(root);

        //Act
        var (path, stream) = AudioSourceResolver.Resolve("ms-appx:///Assets/Music/My%20Song.ogg");
        var twoSlash = AudioSourceResolver.ResolveLocalPathOrNull("ms-appx://Assets/Music/Theme.mp3");

        //Assert
        stream.Should().BeNull();
        path.Should().Be(Path.Join(root, "Assets/Music/My Song.ogg"));
        PackagedAssetPaths.AssetPathOf(root, path).Should().Be("Assets/Music/My Song.ogg");
        PackagedAssetPaths.AssetPathOf(root, twoSlash).Should().Be("Assets/Music/Theme.mp3");
        PackagedAssetPaths.AssetPathOf(root, "/data/local/tmp/other.wav").Should().BeNull();
    }

    private static string[] Members(Type contract) =>
        contract.GetMethods().Select(m => $"{m.Name}({string.Join(",", m.GetParameters().Select(p => p.ParameterType.Name))})").OrderBy(s => s, StringComparer.Ordinal).ToArray();

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
