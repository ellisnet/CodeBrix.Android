using CodeBrix.Android.Services;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Android.UI.Tests.WinRT;

public class SaveWriteBackTests
{
    [Fact]
    public void a_closed_write_of_the_file_completes_it()
    {
        SaveWriteBack.IsFileComplete(SaveWriteBack.CloseWrite, "pain_diagram.png", "pain_diagram.png").Should().BeTrue();
    }

    [Fact]
    public void a_file_renamed_into_place_completes_it()
    {
        SaveWriteBack.IsFileComplete(SaveWriteBack.MovedTo, "book.pdf", "book.pdf").Should().BeTrue();
    }

    [Fact]
    public void another_file_in_the_folder_does_not_complete_it()
    {
        SaveWriteBack.IsFileComplete(SaveWriteBack.CloseWrite, "book.pdf.tmp", "book.pdf").Should().BeFalse();
        SaveWriteBack.IsFileComplete(SaveWriteBack.CloseWrite, "Book.pdf", "book.pdf").Should().BeFalse();
        SaveWriteBack.IsFileComplete(SaveWriteBack.CloseWrite, null, "book.pdf").Should().BeFalse();
    }

    [Fact]
    public void a_delete_or_an_open_does_not_complete_it()
    {
        // The placeholder deleted by desktop code before it writes its own file (inotify IN_DELETE = 0x200,
        // IN_OPEN = 0x020): nothing is copied until the new file is closed.
        SaveWriteBack.IsFileComplete(0x200, "pain_diagram.png", "pain_diagram.png").Should().BeFalse();
        SaveWriteBack.IsFileComplete(0x020, "pain_diagram.png", "pain_diagram.png").Should().BeFalse();
    }

    [Fact]
    public void the_bits_are_the_inotify_ones_file_observer_reports()
    {
        SaveWriteBack.CloseWrite.Should().Be(8);
        SaveWriteBack.MovedTo.Should().Be(128);
        SaveWriteBack.WatchedEvents.Should().Be(136);
    }
}
