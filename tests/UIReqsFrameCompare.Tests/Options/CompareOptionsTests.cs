using SilverAssertions;
using UIReqsFrameCompare;
using Xunit;

namespace UIReqsFrameCompare.Tests.Options;

// [AP7-B TerminalView RE-GATE 2] the informational entries may carry an orientation prefix
// (Landscape/ThemeFocus/Image: an emulator rendering flake seen in landscape only); the older
// whole-group and Group/feature forms keep covering both orientations.
public class CompareOptionsTests
{
    private const string LandscapeImageFrame = "Landscape/ThemeFocus/Image/Scenario3_uniform_01_uniform.png";
    private const string PortraitImageFrame = "Portrait/ThemeFocus/Image/Scenario3_uniform_01_uniform.png";

    private static CompareOptions With(params string[] entries)
    {
        var options = new CompareOptions();
        options.InformationalEntries.UnionWith(entries);
        return options;
    }

    [Fact]
    public void Whole_group_entry_covers_both_orientations()
    {
        //Arrange
        var options = With("ThemeFocus");

        //Act + Assert
        options.IsInformational(LandscapeImageFrame).Should().BeTrue();
        options.IsInformational(PortraitImageFrame).Should().BeTrue();
    }

    [Fact]
    public void Feature_entry_covers_both_orientations_and_only_that_feature()
    {
        //Arrange
        var options = With("ThemeFocus/Image");

        //Act + Assert
        options.IsInformational(LandscapeImageFrame).Should().BeTrue();
        options.IsInformational(PortraitImageFrame).Should().BeTrue();
        options.IsInformational("Landscape/ThemeFocus/Animation/Scenario1_x_01.png").Should().BeFalse();
    }

    [Fact]
    public void Oriented_feature_entry_covers_only_that_orientation()
    {
        //Arrange
        var options = With("Landscape/ThemeFocus/Image");

        //Act + Assert
        options.IsInformational(LandscapeImageFrame).Should().BeTrue();
        options.IsInformational(PortraitImageFrame).Should().BeFalse();
        options.IsInformational("Landscape/ThemeFocus/Animation/Scenario1_x_01.png").Should().BeFalse();
    }

    [Fact]
    public void Oriented_group_entry_covers_the_whole_group_in_that_orientation_only()
    {
        //Arrange
        var options = With("Portrait/Lottie");

        //Act + Assert
        options.IsInformational("Portrait/Lottie/LottiePlayback/Scenario1_x_01.png").Should().BeTrue();
        options.IsInformational("Landscape/Lottie/LottiePlayback/Scenario1_x_01.png").Should().BeFalse();
    }

    [Fact]
    public void Orientation_prefix_is_matched_without_regard_to_case()
    {
        //Arrange
        var options = With("landscape/themefocus/image");

        //Act + Assert
        options.IsInformational(LandscapeImageFrame).Should().BeTrue();
        options.IsInformational(PortraitImageFrame).Should().BeFalse();
    }

    [Fact]
    public void A_frame_directly_under_a_group_is_covered_by_group_entries_only()
    {
        //Arrange
        var frame = "Landscape/Harness/Scenario1_x_01.png";

        //Act + Assert
        With("Landscape/Harness").IsInformational(frame).Should().BeTrue();
        With("Harness").IsInformational(frame).Should().BeTrue();
        With("Portrait/Harness").IsInformational(frame).Should().BeFalse();
    }

    [Theory]
    [InlineData("ThemeFocus", true)]
    [InlineData("ThemeFocus/Image", true)]
    [InlineData("Landscape/ThemeFocus", true)]
    [InlineData("Landscape/ThemeFocus/Image", true)]
    [InlineData("Portrait/ThemeFocus/Image", true)]
    [InlineData("ThemeFocus/Image/Scenario3_uniform_01_uniform", true)]
    [InlineData("Landscape/ThemeFocus/Image/Scenario3_uniform_01_uniform", true)]
    [InlineData("Landscape", false)]
    [InlineData("ThemeFocus/Image/Scenario3_x_01/Extra", false)]
    [InlineData("Landscape/ThemeFocus/Image/Scenario3_x_01/Extra", false)]
    [InlineData("ThemeFocus//Image", false)]
    [InlineData("", false)]
    public void IsValidEntry_accepts_the_group_feature_and_frame_forms_only(string entry, bool expected)
    {
        //Act
        var valid = CompareOptions.IsValidEntry(entry);

        //Assert
        valid.Should().Be(expected);
    }

    // Coordinator 2026-09-27 03:41: an entry may name ONE frame ([<Orientation>/]<Group>/<feature>/<frame without .png>).
    [Fact]
    public void Oriented_frame_entry_covers_only_that_frame_in_that_orientation()
    {
        //Arrange
        var options = With("Portrait/Text/PasswordBox/Scenario2_masked_01_masked");

        //Act + Assert
        options.IsInformational("Portrait/Text/PasswordBox/Scenario2_masked_01_masked.png").Should().BeTrue();
        options.IsInformational("Landscape/Text/PasswordBox/Scenario2_masked_01_masked.png").Should().BeFalse();
        options.IsInformational("Portrait/Text/PasswordBox/Scenario2_masked_02_hashes.png").Should().BeFalse();
        options.IsInformational("Portrait/Text/TextBox/Scenario2_masked_01_masked.png").Should().BeFalse();
    }

    [Fact]
    public void Frame_entry_without_orientation_covers_that_frame_in_both_orientations()
    {
        //Arrange
        var options = With("Text/PasswordBox/Scenario2_masked_01_masked");

        //Act + Assert
        options.IsInformational("Portrait/Text/PasswordBox/Scenario2_masked_01_masked.png").Should().BeTrue();
        options.IsInformational("Landscape/Text/PasswordBox/Scenario2_masked_01_masked.png").Should().BeTrue();
        options.IsInformational("Landscape/Text/PasswordBox/Scenario2_masked_02_hashes.png").Should().BeFalse();
    }

    [Fact]
    public void Frame_entry_does_not_match_a_frame_whose_name_only_starts_with_it()
    {
        //Arrange
        var options = With("Portrait/Text/PasswordBox/Scenario2_masked_01");

        //Act + Assert
        options.IsInformational("Portrait/Text/PasswordBox/Scenario2_masked_01_masked.png").Should().BeFalse();
        options.IsInformational("Portrait/Text/PasswordBox/Scenario2_masked_01.png").Should().BeTrue();
    }
}
