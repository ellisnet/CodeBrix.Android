namespace CodeBrix.Platform.UI.AddIn.Graphics3DGL.UIReqs.Support;

/// <summary>
/// The ordinary fixture element: it clears its surface to one colour and draws one filled
/// triangle on it. This is the element behind the noun "TriangleCanvas" in a feature file, and
/// it is what almost every Graphics3DGL scenario shows.
/// <para>
/// The shaders are written to the "#version 300 es" dialect, which is the one source that
/// compiles on both the desktop-OpenGL and the OpenGL-ES heads the add-in supports.
/// </para>
/// </summary>
public sealed class TriangleCanvas : GLCanvasFixture
{
	private const string Vertex = """
		#version 300 es
		precision highp float;
		layout (location = 0) in vec2 aPosition;
		void main()
		{
			gl_Position = vec4(aPosition, 0.0, 1.0);
		}
		""";

	private const string Fragment = """
		#version 300 es
		precision highp float;
		uniform vec3 uColor;
		out vec4 fragColor;
		void main()
		{
			fragColor = vec4(uColor, 1.0);
		}
		""";

	/// <inheritdoc/>
	protected override string VertexShaderSource => Vertex;

	/// <inheritdoc/>
	protected override string FragmentShaderSource => Fragment;
}
