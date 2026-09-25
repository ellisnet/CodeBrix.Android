using CodeBrix.Android.WinUI.Graphics3DGL.Portable;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Android.WinUI.Graphics3DGL.Tests.Portable;

public class EglAttributesTests
{
    [Fact]
    public void The_config_asks_for_an_OpenGL_ES_3_pbuffer_with_8_bit_RGBA_and_ends_with_EGL_NONE()
    {
        //Arrange
        //Act
        var config = EglAttributes.Config();

        //Assert
        config.Length.Should().Be(13);
        config[^1].Should().Be(EglAttributes.None);
        ValueOf(config, EglAttributes.RenderableType).Should().Be(EglAttributes.OpenGlEs3Bit);
        ValueOf(config, EglAttributes.SurfaceType).Should().Be(EglAttributes.PbufferBit);
        ValueOf(config, EglAttributes.RedSize).Should().Be(8);
        ValueOf(config, EglAttributes.GreenSize).Should().Be(8);
        ValueOf(config, EglAttributes.BlueSize).Should().Be(8);
        ValueOf(config, EglAttributes.AlphaSize).Should().Be(8);
    }

    [Fact]
    public void The_context_is_OpenGL_ES_3_the_floor_of_the_Core()
    {
        //Arrange
        //Act
        var context = EglAttributes.Context();

        //Assert
        context.Should().Equal(EglAttributes.ContextClientVersion, 3, EglAttributes.None);
    }

    [Fact]
    public void The_pbuffer_is_one_pixel()
    {
        //Arrange
        //Act
        var pbuffer = EglAttributes.Pbuffer();

        //Assert
        pbuffer.Should().Equal(EglAttributes.Width, 1, EglAttributes.Height, 1, EglAttributes.None);
    }

    private static int ValueOf(int[] attributes, int key)
    {
        for (var i = 0; i + 1 < attributes.Length; i += 2)
        {
            if (attributes[i] == key)
            {
                return attributes[i + 1];
            }
        }

        return -1;
    }
}
