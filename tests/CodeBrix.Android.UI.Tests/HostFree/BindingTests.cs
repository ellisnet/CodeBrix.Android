using System.ComponentModel;
using System.Runtime.CompilerServices;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Android.UI.Tests.HostFree;

[Collection(HostFreeCoreCollection.Name)]
public class BindingTests
{
    public BindingTests() => HostFreeCore.EnsureInitialized();

    [Fact]
    public void Binding_shows_the_view_model_value()
    {
        //Arrange
        var viewModel = new GreetingViewModel { Greeting = "Hello from the view model" };
        var textBlock = new TextBlock();
        textBlock.SetBinding(TextBlock.TextProperty, new Binding { Path = new PropertyPath(nameof(GreetingViewModel.Greeting)) });

        //Act
        textBlock.DataContext = viewModel;
        HostFreeCore.RunPending();

        //Assert
        textBlock.Text.Should().Be("Hello from the view model");
    }

    [Fact]
    public void Binding_follows_property_changes_of_the_view_model()
    {
        //Arrange
        var viewModel = new GreetingViewModel { Greeting = "before" };
        var textBlock = new TextBlock { DataContext = viewModel };
        textBlock.SetBinding(TextBlock.TextProperty, new Binding { Path = new PropertyPath(nameof(GreetingViewModel.Greeting)) });
        HostFreeCore.RunPending();

        //Act
        viewModel.Greeting = "after";
        HostFreeCore.RunPending();

        //Assert
        textBlock.Text.Should().Be("after");
    }

    [Fact]
    public void Page_data_context_is_inherited_by_its_content()
    {
        //Arrange
        var viewModel = new GreetingViewModel { Greeting = "inherited" };
        var textBlock = new TextBlock();
        textBlock.SetBinding(TextBlock.TextProperty, new Binding { Path = new PropertyPath(nameof(GreetingViewModel.Greeting)) });
        var page = new Page { Content = new StackPanel { Children = { textBlock } } };

        //Act
        page.DataContext = viewModel;
        HostFreeCore.Layout(page, 400, 300);

        //Assert
        textBlock.DataContext.Should().BeSameAs(viewModel);
        textBlock.Text.Should().Be("inherited");
    }

    [Fact]
    public void Two_way_binding_writes_the_target_value_back_to_the_view_model()
    {
        //Arrange
        var viewModel = new GreetingViewModel { Greeting = "start" };
        var checkBox = new CheckBox { DataContext = viewModel };
        checkBox.SetBinding(ToggleButtonIsChecked, new Binding { Path = new PropertyPath(nameof(GreetingViewModel.IsSelected)), Mode = BindingMode.TwoWay });
        HostFreeCore.RunPending();

        //Act
        checkBox.IsChecked = true;
        HostFreeCore.RunPending();

        //Assert
        viewModel.IsSelected.Should().BeTrue();
    }

    private static DependencyProperty ToggleButtonIsChecked => Microsoft.UI.Xaml.Controls.Primitives.ToggleButton.IsCheckedProperty;

    public sealed class GreetingViewModel : INotifyPropertyChanged
    {
        private string _greeting;
        private bool _isSelected;

        public event PropertyChangedEventHandler PropertyChanged;

        public string Greeting
        {
            get => _greeting;
            set => Set(ref _greeting, value);
        }

        public bool IsSelected
        {
            get => _isSelected;
            set => Set(ref _isSelected, value);
        }

        private void Set<T>(ref T field, T value, [CallerMemberName] string name = null)
        {
            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
