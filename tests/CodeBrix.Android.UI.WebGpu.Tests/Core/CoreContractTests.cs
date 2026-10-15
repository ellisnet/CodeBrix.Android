using System.Linq;
using CodeBrix.Android.Tests.Shared;
using CodeBrix.Platform.UI.WebGpu.Contracts;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Android.UI.WebGpu.Tests.Core;

public class CoreContractTests
{
    private const string AndroidName = "CodeBrix.Android.UI.WebGpu";
    private const string WebGpuCore = "CodeBrix.Platform.UI.WebGpu.Core";

    [Fact]
    public void The_WebGpu_Core_grants_its_internals_to_the_Android_assembly_name()
    {
        //Arrange
        //Act
        var grants = CoreMetadata.InternalsVisibleTo(WebGpuCore);
        var ui = CoreMetadata.InternalsVisibleTo("CodeBrix.Platform.UI.Core");
        var composition = CoreMetadata.InternalsVisibleTo("CodeBrix.Platform.UI.Composition.Core");

        //Assert
        grants.Should().Contain(AndroidName);
        grants.Should().Contain(AndroidName + ".Tests");
        ui.Should().Contain(AndroidName, "the view overrides UI.Core's internal CreateElementVisual");
        composition.Should().Contain(AndroidName, "the view takes the shared compositor, internal to Composition.Core");
    }

    [Fact]
    public void The_WebGpu_Core_loads_the_Android_assembly_by_name_for_its_frame_timer()
    {
        //Arrange
        //Act
        var names = PlatformContract.PlatformAssemblyNames;

        //Assert
        names.Should().Contain(AndroidName);
    }

    [Fact]
    public void The_element_is_not_in_the_Core_so_the_Android_add_in_provides_it()
    {
        //Arrange
        //Act
        var types = CoreMetadata.TypeNames(WebGpuCore);

        //Assert
        types.Should().NotContain("CodeBrix.Platform.UI.WebGpu.WebGpuRenderView");
        types.Should().NotContain("CodeBrix.Platform.UI.WebGpu.IWebGpuRenderer");
        types.Should().NotContain("CodeBrix.Platform.UI.WebGpu.WebGpuFrame");
        types.Should().NotContain("CodeBrix.Platform.UI.WebGpu.WebGpuRenderViewHandoff");
        types.Should().NotContain("CodeBrix.Platform.UI.WebGpu.WebGpuDeviceInfo");
        types.Should().Contain("CodeBrix.Platform.UI.WebGpu.WebGpuFrameDriver");
        types.Should().Contain("CodeBrix.Platform.UI.WebGpu.WebGpuRenderPath");
        types.Should().Contain("CodeBrix.Platform.UI.WebGpu.WebGpuRenderPathChangedEventArgs");
        types.Should().Contain("CodeBrix.Platform.UI.WebGpu.WebGpuDeviceLostEventArgs");
        types.Should().Contain("CodeBrix.Platform.UI.WebGpu.Internal.WebGpuRenderViewRules");
        types.Should().Contain("CodeBrix.Platform.UI.WebGpu.Internal.WebGpuFrameScheduler");
        types.Should().Contain("CodeBrix.Platform.UI.WebGpu.Contracts.IWebGpuFrameTimer");
    }

    [Fact]
    public void The_WebGpu_Core_names_neither_the_WebGPU_engine_nor_Skia()
    {
        //Arrange
        //Act
        var references = CoreMetadata.References(WebGpuCore);

        //Assert
        references.Where(r => r.StartsWith("CodeBrix.WebGpu", System.StringComparison.Ordinal)).Should().BeEmpty();
        references.Should().NotContain("SkiaSharp");
    }
}
