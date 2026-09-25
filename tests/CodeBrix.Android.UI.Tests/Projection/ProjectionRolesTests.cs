using System;
using CodeBrix.Android.UI.Portable.Projection;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Shapes;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Android.UI.Tests.Projection;

public class ProjectionRolesTests
{
    [Theory]
    [InlineData(typeof(TextBlock), ProjectionRole.Text)]
    [InlineData(typeof(ImplicitTextBlock), ProjectionRole.Text)]
    [InlineData(typeof(FontIcon), ProjectionRole.Glyph)]
    [InlineData(typeof(Image), ProjectionRole.Image)]
    [InlineData(typeof(Grid), ProjectionRole.Container)]
    [InlineData(typeof(StackPanel), ProjectionRole.Container)]
    [InlineData(typeof(Border), ProjectionRole.Container)]
    [InlineData(typeof(ContentPresenter), ProjectionRole.Container)]
    [InlineData(typeof(ScrollContentPresenter), ProjectionRole.Container)]
    [InlineData(typeof(ScrollViewer), ProjectionRole.Container)]
    [InlineData(typeof(Page), ProjectionRole.Container)]
    [InlineData(typeof(Frame), ProjectionRole.Container)]
    [InlineData(typeof(UserControl), ProjectionRole.Container)]
    [InlineData(typeof(ContentControl), ProjectionRole.Container)]
    [InlineData(typeof(ContentDialog), ProjectionRole.Container)]
    [InlineData(typeof(Popup), ProjectionRole.Container)]
    [InlineData(typeof(Button), ProjectionRole.Control)]
    [InlineData(typeof(TextBox), ProjectionRole.Control)]
    [InlineData(typeof(ComboBox), ProjectionRole.Control)]
    [InlineData(typeof(ProgressBar), ProjectionRole.Control)]
    [InlineData(typeof(AnimatedVisualPlayer), ProjectionRole.Control)]
    [InlineData(typeof(Rectangle), ProjectionRole.Control)]
    [InlineData(typeof(PopupRoot), ProjectionRole.Container)]
    public void Classify_maps_element_types_to_roles(Type type, object expected)
    {
        //Act
        var role = ProjectionRoles.Classify(type);

        //Assert (the role enum is internal: xUnit passes it boxed)
        role.Should().Be((ProjectionRole)expected);
    }

    [Fact]
    public void Classify_treats_an_app_control_as_a_control()
    {
        //Act
        var role = ProjectionRoles.Classify(typeof(AppButton));

        //Assert
        role.Should().Be(ProjectionRole.Control);
    }

    [Fact]
    public void Classify_treats_an_app_page_as_a_container()
    {
        //Act
        var role = ProjectionRoles.Classify(typeof(AppPage));

        //Assert
        role.Should().Be(ProjectionRole.Container);
    }

    [Theory]
    [InlineData(ProjectionRole.Control, false, true)]
    [InlineData(ProjectionRole.Image, false, true)]
    [InlineData(ProjectionRole.Control, true, false)]
    [InlineData(ProjectionRole.Image, true, false)]
    [InlineData(ProjectionRole.Container, false, false)]
    [InlineData(ProjectionRole.Text, false, false)]
    [InlineData(ProjectionRole.Glyph, false, false)]
    public void IsLabelled_labels_declared_controls_and_images_only(object role, bool isTemplatePart, bool expected)
    {
        //Act
        var labelled = ProjectionRoles.IsLabelled((ProjectionRole)role, isTemplatePart);

        //Assert
        labelled.Should().Be(expected);
    }

    [Fact]
    public void Classify_rejects_a_null_type()
    {
        //Act
        Action act = () => ProjectionRoles.Classify(null);

        //Assert
        act.Should().Throw<ArgumentNullException>();
    }

    public sealed class AppButton : Button
    {
    }

    public sealed class AppPage : Page
    {
    }
}
