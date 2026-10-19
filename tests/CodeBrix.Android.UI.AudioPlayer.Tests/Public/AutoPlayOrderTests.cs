using System;
using System.IO;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Android.UI.AudioPlayer.Tests.Public;

/// <summary>
/// The ported MidiPlayer (the framework element re-provided on Android) keeps the framework's AutoPlay rule: AutoPlay set to
/// true after a load completed, while not playing, starts playback, so the order of AutoPlay and Source and Instrument in XAML does not
/// matter. A source fence (the element needs the device to be instantiated): the dependency property registers the change
/// callback and the callback plays a loaded, not-playing source. A port that re-syncs from the framework without it
/// brings back the "AutoPlay before Source" pitfall the AGENT-README no longer documents.
/// </summary>
public class AutoPlayOrderTests
{
    [Fact]
    public void auto_play_set_after_the_load_starts_the_loaded_source()
    {
        //Arrange
        var source = File.ReadAllText(Path.Combine(RepoRoot(), "src", "AddIns", "CodeBrix.Android.UI.AudioPlayer", "Public", "MidiPlayer.cs")).Replace("\r\n", "\n", StringComparison.Ordinal);

        //Assert
        source.Should().Contain("new PropertyMetadata(false, (o, e) => ((MidiPlayer)o).OnAutoPlayChanged((bool)e.NewValue))");
        source.Should().Contain("private void OnAutoPlayChanged(bool autoPlay)\n\t{\n\t\tif (autoPlay && _isSourceLoaded && !IsPlaying)\n\t\t{\n\t\t\tPlay();\n\t\t}\n\t}");
    }

    private static string RepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "CodeBrix.Android.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("The repository root was not found.");
    }
}
