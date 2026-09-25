using Microsoft.UI.Xaml.Controls;
using CodeBrix.Android.UI.Tests.HostFree;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Android.UI.Tests.Handlers;

/// <summary>
/// AP3a fences for the Core entry points the ComboBox handler uses: a native row choice writes
/// SelectedIndex (SelectionChanged, SelectedItem), an editable ComboBox submits typed text through
/// ComboBox.RaiseTextSubmittedFromPlatform.
/// </summary>
[Collection(HostFreeCoreCollection.Name)]
public class SelectionSeamTests
{
    public SelectionSeamTests() => HostFreeCore.EnsureInitialized();

    [Fact]
    public void Writing_SelectedIndex_selects_the_item_and_raises_SelectionChanged()
    {
        //Arrange
        var combo = new ComboBox { ItemsSource = new[] { "Alpha", "Beta", "Gamma" } };
        var changes = 0;
        combo.SelectionChanged += (_, _) => changes++;

        //Act
        combo.SelectedIndex = 2;

        //Assert
        combo.SelectedItem.Should().Be("Gamma");
        changes.Should().Be(1);
    }

    [Fact]
    public void RaiseTextSubmittedFromPlatform_raises_TextSubmitted_with_the_text()
    {
        //Arrange
        var combo = new ComboBox { IsEditable = true, ItemsSource = new[] { "100%", "200%" } };
        string submitted = null;
        combo.TextSubmitted += (_, e) => submitted = e.Text;

        //Act
        combo.RaiseTextSubmittedFromPlatform("200%");

        //Assert
        submitted.Should().Be("200%");
    }
}
