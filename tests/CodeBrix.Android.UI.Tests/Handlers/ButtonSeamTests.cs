using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using CodeBrix.Android.UI.Tests.HostFree;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Android.UI.Tests.Handlers;

/// <summary>
/// AP3a fences for the Core entry points the Button-family handlers rely on (the handlers themselves need
/// a device): a native click raised through ButtonBase.RaiseClickFromPlatform is a Core click (Click,
/// Command, the ToggleButton/CheckBox/RadioButton state cycle).
/// </summary>
[Collection(HostFreeCoreCollection.Name)]
public class ButtonSeamTests
{
    public ButtonSeamTests() => HostFreeCore.EnsureInitialized();

    [Fact]
    public void RaiseClickFromPlatform_raises_Click_once()
    {
        //Arrange
        var button = new Button { Content = "Go" };
        var clicks = 0;
        button.Click += (_, _) => clicks++;

        //Act
        button.RaiseClickFromPlatform();

        //Assert
        clicks.Should().Be(1);
    }

    [Fact]
    public void RaiseClickFromPlatform_runs_the_Command_with_its_parameter()
    {
        //Arrange
        object received = null;
        var command = new TestCommand(p => received = p);
        var button = new Button { Command = command, CommandParameter = "save" };

        //Act
        button.RaiseClickFromPlatform();

        //Assert
        received.Should().Be("save");
    }

    [Fact]
    public void RaiseClickFromPlatform_cycles_a_three_state_CheckBox()
    {
        //Arrange
        var box = new CheckBox { IsThreeState = true, IsChecked = false };

        //Act
        box.RaiseClickFromPlatform();
        var first = box.IsChecked;
        box.RaiseClickFromPlatform();
        var second = box.IsChecked;
        box.RaiseClickFromPlatform();

        //Assert
        first.Should().Be(true);
        second.Should().BeNull();
        box.IsChecked.Should().Be(false);
    }

    [Fact]
    public void RaiseClickFromPlatform_toggles_a_ToggleButton_and_raises_Checked()
    {
        //Arrange
        var toggle = new ToggleButton();
        var checkedCount = 0;
        toggle.Checked += (_, _) => checkedCount++;

        //Act
        toggle.RaiseClickFromPlatform();

        //Assert
        toggle.IsChecked.Should().Be(true);
        checkedCount.Should().Be(1);
    }

    [Fact]
    public void Setting_IsOn_raises_Toggled_once()
    {
        //Arrange
        var toggle = new ToggleSwitch();
        var toggled = 0;
        toggle.Toggled += (_, _) => toggled++;

        //Act
        toggle.IsOn = true;
        HostFreeCore.RunPending();

        //Assert
        toggled.Should().Be(1);
    }

    private sealed class TestCommand : System.Windows.Input.ICommand
    {
        private readonly System.Action<object> _execute;

        public TestCommand(System.Action<object> execute) => _execute = execute;

        public event System.EventHandler CanExecuteChanged
        {
            add { }
            remove { }
        }

        public bool CanExecute(object parameter) => true;

        public void Execute(object parameter) => _execute(parameter);
    }
}
