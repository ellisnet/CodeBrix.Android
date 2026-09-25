using CodeBrix.Android.UI.Overlay;
using Microsoft.UI.Xaml.Controls;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Android.UI.Tests.Overlay;

public class DialogButtonSlotsTests
{
    [Fact]
    public void Primary_secondary_and_close_take_positive_neutral_and_negative()
    {
        //Act
        var slots = DialogButtonSlots.For("Delete", "Keep", "Cancel");

        //Assert
        slots.Positive.Should().Be(ContentDialogButton.Primary);
        slots.Neutral.Should().Be(ContentDialogButton.Secondary);
        slots.Negative.Should().Be(ContentDialogButton.Close);
    }

    [Fact]
    public void Without_a_close_button_the_secondary_button_is_the_negative_one()
    {
        //Act (a Yes / No confirmation)
        var slots = DialogButtonSlots.For("Yes", "No", null);

        //Assert
        slots.Positive.Should().Be(ContentDialogButton.Primary);
        slots.Negative.Should().Be(ContentDialogButton.Secondary);
        slots.Neutral.Should().Be(ContentDialogButton.None);
    }

    [Fact]
    public void A_close_button_alone_is_the_negative_button()
    {
        //Act
        var slots = DialogButtonSlots.For(string.Empty, null, "OK");

        //Assert
        slots.Positive.Should().Be(ContentDialogButton.None);
        slots.Negative.Should().Be(ContentDialogButton.Close);
        slots.Neutral.Should().Be(ContentDialogButton.None);
    }

    [Fact]
    public void A_dialog_without_button_texts_has_no_buttons()
    {
        //Act
        var slots = DialogButtonSlots.For(null, null, null);

        //Assert
        slots.Should().Be(new DialogButtonSlots(ContentDialogButton.None, ContentDialogButton.None, ContentDialogButton.None));
    }
}
