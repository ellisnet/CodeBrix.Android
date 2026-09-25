using System.Collections.Generic;
using System.Linq;
using CodeBrix.Android.UI.Policy;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Android.UI.Tests.Policy;

public class ThemeKeyMapTests
{
    /// <summary>The 173 keys of XAML_CORPUS_2026-09-23.md section 3.1 (the Fluent control keys the corpus apps re-key).</summary>
    private static readonly string[] CorpusKeys =
    {
        "AccentButtonBackground", "AccentButtonBackgroundDisabled", "AccentButtonBackgroundPointerOver", "AccentButtonBackgroundPressed",
        "AccentButtonBorderBrush", "AccentButtonBorderBrushDisabled", "AccentButtonBorderBrushPointerOver", "AccentButtonBorderBrushPressed",
        "AccentButtonForeground", "AccentButtonForegroundDisabled", "AccentButtonForegroundPointerOver", "AccentButtonForegroundPressed",
        "ButtonBackground", "ButtonBackgroundDisabled", "ButtonBackgroundPointerOver", "ButtonBackgroundPressed",
        "ButtonBorderBrush", "ButtonBorderBrushDisabled", "ButtonBorderBrushPointerOver", "ButtonBorderBrushPressed",
        "ButtonForeground", "ButtonForegroundDisabled", "ButtonForegroundPointerOver", "ButtonForegroundPressed",
        "CheckBoxCheckBackgroundFillChecked", "CheckBoxCheckBackgroundFillCheckedDisabled", "CheckBoxCheckBackgroundFillCheckedPointerOver", "CheckBoxCheckBackgroundFillCheckedPressed",
        "CheckBoxCheckBackgroundFillUnchecked", "CheckBoxCheckBackgroundFillUncheckedDisabled", "CheckBoxCheckBackgroundFillUncheckedPointerOver", "CheckBoxCheckBackgroundFillUncheckedPressed",
        "CheckBoxCheckBackgroundStrokeChecked", "CheckBoxCheckBackgroundStrokeCheckedDisabled", "CheckBoxCheckBackgroundStrokeCheckedPointerOver", "CheckBoxCheckBackgroundStrokeCheckedPressed",
        "CheckBoxCheckBackgroundStrokeUnchecked", "CheckBoxCheckBackgroundStrokeUncheckedDisabled", "CheckBoxCheckBackgroundStrokeUncheckedPointerOver", "CheckBoxCheckBackgroundStrokeUncheckedPressed",
        "CheckBoxCheckGlyphForegroundChecked", "CheckBoxCheckGlyphForegroundCheckedDisabled", "CheckBoxCheckGlyphForegroundCheckedPointerOver", "CheckBoxCheckGlyphForegroundCheckedPressed",
        "CheckBoxForegroundChecked", "CheckBoxForegroundCheckedDisabled", "CheckBoxForegroundCheckedPointerOver", "CheckBoxForegroundCheckedPressed",
        "CheckBoxForegroundUnchecked", "CheckBoxForegroundUncheckedDisabled", "CheckBoxForegroundUncheckedPointerOver", "CheckBoxForegroundUncheckedPressed",
        "ComboBoxBackground", "ComboBoxBackgroundDisabled", "ComboBoxBackgroundFocused", "ComboBoxBackgroundPointerOver",
        "ComboBoxBackgroundPressed", "ComboBoxBackgroundUnfocused", "ComboBoxBorderBrush", "ComboBoxBorderBrushDisabled",
        "ComboBoxBorderBrushPointerOver", "ComboBoxBorderBrushPressed", "ComboBoxDropDownBackground", "ComboBoxDropDownBorderBrush",
        "ComboBoxDropDownForeground", "ComboBoxDropDownGlyphForeground", "ComboBoxDropDownGlyphForegroundDisabled", "ComboBoxDropDownGlyphForegroundFocused",
        "ComboBoxDropDownGlyphForegroundFocusedPressed", "ComboBoxForeground", "ComboBoxForegroundDisabled", "ComboBoxForegroundFocused",
        "ComboBoxForegroundFocusedPressed", "ComboBoxForegroundPointerOver", "ComboBoxForegroundPressed", "ComboBoxItemBackground",
        "ComboBoxItemBackgroundDisabled", "ComboBoxItemBackgroundPointerOver", "ComboBoxItemBackgroundPressed", "ComboBoxItemBackgroundSelected",
        "ComboBoxItemBackgroundSelectedPointerOver", "ComboBoxItemBackgroundSelectedPressed", "ComboBoxItemBackgroundSelectedUnfocused", "ComboBoxItemForeground",
        "ComboBoxItemForegroundDisabled", "ComboBoxItemForegroundPointerOver", "ComboBoxItemForegroundPressed", "ComboBoxItemForegroundSelected",
        "ComboBoxItemForegroundSelectedPointerOver", "ComboBoxItemForegroundSelectedPressed", "ComboBoxItemForegroundSelectedUnfocused", "ComboBoxItemPillFillBrush",
        "ComboBoxPlaceHolderForeground", "ContentDialogBackground", "ContentDialogBorderBrush", "ContentDialogForeground",
        "ContentDialogLightDismissOverlayBackground", "ContentDialogSeparatorBorderBrush", "ContentDialogSmokeFill", "ContentDialogTopOverlay",
        "ListViewItemBackground", "ListViewItemBackgroundPointerOver", "ListViewItemBackgroundPressed", "ListViewItemBackgroundSelected",
        "ListViewItemBackgroundSelectedPointerOver", "ListViewItemBackgroundSelectedPressed", "ListViewItemForeground", "ListViewItemForegroundPointerOver",
        "ListViewItemForegroundPressed", "ListViewItemForegroundSelected", "ListViewItemForegroundSelectedPointerOver", "ListViewItemForegroundSelectedPressed",
        "ProgressBarBackground", "ProgressBarBorderBrush", "ProgressBarForeground", "ScrollBarBackground",
        "ScrollBarBackgroundDisabled", "ScrollBarBackgroundPointerOver", "ScrollBarBorderBrush", "ScrollBarBorderBrushDisabled",
        "ScrollBarBorderBrushPointerOver", "ScrollBarButtonArrowForeground", "ScrollBarButtonArrowForegroundDisabled", "ScrollBarButtonArrowForegroundPointerOver",
        "ScrollBarButtonArrowForegroundPressed", "ScrollBarButtonBackground", "ScrollBarButtonBackgroundDisabled", "ScrollBarButtonBackgroundPointerOver",
        "ScrollBarButtonBackgroundPressed", "ScrollBarThumbFill", "ScrollBarThumbFillDisabled", "ScrollBarThumbFillPointerOver",
        "ScrollBarThumbFillPressed", "ScrollBarTrackFill", "ScrollBarTrackFillDisabled", "ScrollBarTrackFillPointerOver",
        "ScrollBarTrackStroke", "ScrollBarTrackStrokeDisabled", "ScrollBarTrackStrokePointerOver", "SliderThumbBackground",
        "SliderThumbBackgroundPointerOver", "SliderThumbBackgroundPressed", "SliderTrackFill", "SliderTrackFillPointerOver",
        "SliderTrackFillPressed", "SliderTrackValueFill", "SliderTrackValueFillPointerOver", "SliderTrackValueFillPressed",
        "TextControlBackground", "TextControlBackgroundDisabled", "TextControlBackgroundFocused", "TextControlBackgroundPointerOver",
        "TextControlBorderBrush", "TextControlBorderBrushDisabled", "TextControlBorderBrushFocused", "TextControlBorderBrushPointerOver",
        "TextControlButtonBackground", "TextControlButtonBackgroundPointerOver", "TextControlButtonBackgroundPressed", "TextControlButtonForeground",
        "TextControlButtonForegroundPointerOver", "TextControlButtonForegroundPressed", "TextControlElevationBorderBrush", "TextControlElevationBorderFocusedBrush",
        "TextControlForeground", "TextControlForegroundDisabled", "TextControlForegroundFocused", "TextControlForegroundPointerOver",
        "TextControlPlaceholderForeground", "TextControlPlaceholderForegroundDisabled", "TextControlPlaceholderForegroundFocused", "TextControlPlaceholderForegroundPointerOver",
        "TextControlSelectionHighlightColor",
    };

    [Fact]
    public void All_holds_exactly_the_173_corpus_keys()
    {
        //Act
        var keys = ThemeKeyMap.All.Select(e => e.Key).OrderBy(k => k, System.StringComparer.Ordinal).ToList();

        //Assert
        keys.Should().Equal(CorpusKeys.OrderBy(k => k, System.StringComparer.Ordinal));
        keys.Distinct().Count().Should().Be(173);
    }

    [Theory]
    [InlineData("Button", 12)]
    [InlineData("AccentButton", 12)]
    [InlineData("ComboBox", 41)]
    [InlineData("CheckBox", 28)]
    [InlineData("TextControl", 25)]
    [InlineData("ScrollBar", 24)]
    [InlineData("ListViewItem", 12)]
    [InlineData("Slider", 9)]
    [InlineData("ContentDialog", 7)]
    [InlineData("ProgressBar", 3)]
    public void Of_counts_each_family_as_the_corpus_does(string familyName, int count) =>
        ThemeKeyMap.Of(System.Enum.Parse<ThemeKeyFamily>(familyName)).Count().Should().Be(count);

    [Fact]
    public void HonoredCount_is_161_of_173()
    {
        //Act
        var notHonored = ThemeKeyMap.All.Where(e => !e.IsHonored).Select(e => e.Key).ToList();

        //Assert
        ThemeKeyMap.HonoredCount.Should().Be(161);
        notHonored.Should().BeEquivalentTo(new[]
        {
            "CheckBoxCheckBackgroundFillUnchecked", "CheckBoxCheckBackgroundFillUncheckedPointerOver", "CheckBoxCheckBackgroundFillUncheckedPressed", "CheckBoxCheckBackgroundFillUncheckedDisabled",
            "CheckBoxCheckBackgroundStrokeChecked", "CheckBoxCheckBackgroundStrokeCheckedPointerOver", "CheckBoxCheckBackgroundStrokeCheckedPressed", "CheckBoxCheckBackgroundStrokeCheckedDisabled",
            "TextControlElevationBorderBrush", "ContentDialogSeparatorBorderBrush", "ContentDialogTopOverlay", "ProgressBarBorderBrush",
        });
    }

    [Fact]
    public void Contains_and_Find_look_keys_up_by_exact_name()
    {
        //Assert
        ThemeKeyMap.Contains("ButtonBackgroundPointerOver").Should().BeTrue();
        ThemeKeyMap.Contains("buttonbackground").Should().BeFalse();
        ThemeKeyMap.Contains(null).Should().BeFalse();
        ThemeKeyMap.Find("SliderThumbBackground").Family.Should().Be(ThemeKeyFamily.Slider);
        ThemeKeyMap.Find("NoSuchKey").Should().BeNull();
    }

    [Fact]
    public void Every_entry_names_its_native_slot()
    {
        //Assert
        ThemeKeyMap.All.Should().OnlyContain(e => !string.IsNullOrWhiteSpace(e.Slot));
    }

    [Fact]
    public void The_template_families_are_honored_through_their_Fluent_template()
    {
        //Assert
        foreach (var family in new[] { ThemeKeyFamily.ComboBox, ThemeKeyFamily.ScrollBar, ThemeKeyFamily.ListViewItem })
        {
            ThemeKeyMap.Of(family).Should().OnlyContain(e => e.Path == ThemeKeyPath.FluentTemplate);
        }
    }
}
