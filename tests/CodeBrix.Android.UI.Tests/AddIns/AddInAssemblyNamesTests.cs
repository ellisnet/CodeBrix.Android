using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using CodeBrix.Android.UI.Portable;
using Microsoft.UI.Xaml;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Android.UI.Tests.AddIns;

public class AddInAssemblyNamesTests
{
    [Fact]
    public void Every_add_in_name_the_loader_loads_is_granted_by_UI_Core()
    {
        //Arrange
        var grants = typeof(UIElement).Assembly.GetCustomAttributes<InternalsVisibleToAttribute>()
            .Select(a => a.AssemblyName)
            .ToHashSet();

        //Act
        var ungranted = AddInAssemblyNames.All.Where(name => !grants.Contains(name)).ToList();

        //Assert
        ungranted.Should().BeEmpty();
    }

    [Fact]
    public void The_canvas_add_in_is_loaded_before_the_add_ins_that_draw_on_it()
    {
        //Arrange
        var names = AddInAssemblyNames.All.ToList();

        //Act
        var canvas = names.IndexOf("CodeBrix.Android.SkiaSharp.Views");

        //Assert
        canvas.Should().Be(0);
        names.Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void Every_add_in_name_is_granted_by_CodeBrix_Android_UI()
    {
        //Arrange
        var grants = typeof(AddInAssemblyNames).Assembly.GetCustomAttributes<InternalsVisibleToAttribute>()
            .Select(a => a.AssemblyName)
            .ToHashSet();

        //Act
        var ungranted = AddInAssemblyNames.All.Where(name => !grants.Contains(name)).ToList();

        //Assert
        ungranted.Should().BeEmpty();
    }
}
