using System.Collections.Generic;
using System.Linq;
using CodeBrix.Android.UI.Handlers;
using CodeBrix.Android.UI.Tests.HostFree;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Android.UI.Tests.Handlers;

[Collection(HostFreeCoreCollection.Name)]
public class PropertyMapperTests
{
    public PropertyMapperTests() => HostFreeCore.EnsureInitialized();

    [Fact]
    public void GetProperty_falls_through_to_the_chained_mapper()
    {
        //Arrange
        var calls = new List<string>();
        var baseMapper = new PropertyMapper<UIElement, IAndroidElementHandler> { [UIElement.OpacityProperty] = (_, _) => calls.Add("base-opacity") };
        var mapper = new PropertyMapper<Border, IAndroidElementHandler>(baseMapper) { [Border.CornerRadiusProperty] = (_, _) => calls.Add("corner") };
        var handler = new RecordingHandler(mapper);

        //Act
        mapper.UpdateProperty(handler, new Border(), UIElement.OpacityProperty);
        mapper.UpdateProperty(handler, new Border(), Border.CornerRadiusProperty);

        //Assert
        calls.Should().Equal("base-opacity", "corner");
    }

    [Fact]
    public void UpdateProperties_runs_base_keys_first_in_registration_order()
    {
        //Arrange
        var calls = new List<string>();
        var baseMapper = new PropertyMapper<UIElement, IAndroidElementHandler>
        {
            [UIElement.VisibilityProperty] = (_, _) => calls.Add("visibility"),
            [UIElement.OpacityProperty] = (_, _) => calls.Add("opacity"),
        };
        var mapper = new PropertyMapper<UIElement, IAndroidElementHandler>(baseMapper)
        {
            [FrameworkElement.BackgroundProperty] = (_, _) => calls.Add("background"),
        };

        //Act
        mapper.UpdateProperties(new RecordingHandler(mapper), new Border());

        //Assert
        calls.Should().Equal("visibility", "opacity", "background");
    }

    [Fact]
    public void A_derived_mapping_replaces_the_base_mapping_at_the_base_position()
    {
        //Arrange
        var calls = new List<string>();
        var baseMapper = new PropertyMapper<UIElement, IAndroidElementHandler>
        {
            [UIElement.VisibilityProperty] = (_, _) => calls.Add("base-visibility"),
            [UIElement.OpacityProperty] = (_, _) => calls.Add("opacity"),
        };
        var mapper = new PropertyMapper<UIElement, IAndroidElementHandler>(baseMapper)
        {
            [UIElement.VisibilityProperty] = (_, _) => calls.Add("derived-visibility"),
        };

        //Act
        mapper.UpdateProperties(new RecordingHandler(mapper), new Border());

        //Assert
        calls.Should().Equal("derived-visibility", "opacity");
    }

    [Fact]
    public void A_typed_mapping_for_another_element_type_falls_back_to_the_chain()
    {
        //Arrange
        var calls = new List<string>();
        var baseMapper = new PropertyMapper<UIElement, IAndroidElementHandler> { [UIElement.OpacityProperty] = (_, _) => calls.Add("base") };
        var mapper = new PropertyMapper<Border, IAndroidElementHandler>(baseMapper) { [UIElement.OpacityProperty] = (_, _) => calls.Add("border-only") };

        //Act
        mapper.UpdateProperty(new RecordingHandler(mapper), new Grid(), UIElement.OpacityProperty);

        //Assert
        calls.Should().Equal("base");
    }

    [Fact]
    public void UpdateProperty_of_an_unmapped_key_does_nothing()
    {
        //Arrange
        var mapper = new PropertyMapper<UIElement, IAndroidElementHandler>();

        //Act
        var handler = new RecordingHandler(mapper);
        mapper.UpdateProperty(handler, new Border(), UIElement.OpacityProperty);

        //Assert
        mapper.GetProperty(UIElement.OpacityProperty).Should().BeNull();
        mapper.GetKeys().Should().BeEmpty();
    }

    [Fact]
    public void AppendToMapping_runs_after_and_PrependToMapping_before_the_existing_mapping()
    {
        //Arrange
        var calls = new List<string>();
        var mapper = new PropertyMapper<UIElement, IAndroidElementHandler> { [UIElement.OpacityProperty] = (_, _) => calls.Add("original") };
        mapper.AppendToMapping(UIElement.OpacityProperty, (_, _) => calls.Add("appended"));
        mapper.PrependToMapping(UIElement.OpacityProperty, (_, _) => calls.Add("prepended"));

        //Act
        mapper.UpdateProperty(new RecordingHandler(mapper), new Border(), UIElement.OpacityProperty);

        //Assert
        calls.Should().Equal("prepended", "original", "appended");
    }

    [Fact]
    public void ReplaceMapping_drops_the_existing_mapping()
    {
        //Arrange
        var calls = new List<string>();
        var mapper = new PropertyMapper<UIElement, IAndroidElementHandler> { [UIElement.OpacityProperty] = (_, _) => calls.Add("original") };
        mapper.ReplaceMapping(UIElement.OpacityProperty, (_, _) => calls.Add("replacement"));

        //Act
        mapper.UpdateProperty(new RecordingHandler(mapper), new Border(), UIElement.OpacityProperty);

        //Assert
        calls.Should().Equal("replacement");
    }

    [Fact]
    public void GetKeys_lists_the_chained_keys_before_the_own_keys()
    {
        //Arrange
        var baseMapper = new PropertyMapper<UIElement, IAndroidElementHandler> { [UIElement.OpacityProperty] = (_, _) => { } };
        var mapper = new PropertyMapper<UIElement, IAndroidElementHandler>(baseMapper) { [FrameworkElement.BackgroundProperty] = (_, _) => { } };

        //Act
        var keys = mapper.GetKeys().ToList();

        //Assert
        keys.Should().Equal(UIElement.OpacityProperty, FrameworkElement.BackgroundProperty);
    }
}
