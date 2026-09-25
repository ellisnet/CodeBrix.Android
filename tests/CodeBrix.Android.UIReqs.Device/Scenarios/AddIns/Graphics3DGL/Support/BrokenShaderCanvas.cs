namespace CodeBrix.Platform.UI.AddIn.Graphics3DGL.UIReqs.Support;

/// <summary>
/// The fixture element whose shaders cannot be compiled, behind the noun "BrokenShaderCanvas".
/// It exists for one requirement: a canvas the driver refuses has to REPORT that, in the
/// driver's own words, instead of throwing out of the framework's Loaded handler and instead of
/// silently showing an empty rectangle nobody can explain.
/// <para>
/// The fragment shader is ordinary "#version 300 es" apart from one line that is not GLSL at
/// all, so the failure comes from the driver's own compiler rather than from anything this
/// project decided.
/// </para>
/// </summary>
public sealed class BrokenShaderCanvas : GLCanvasFixture
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

	// The line below is deliberately not GLSL: no such type, no such function.
	private const string Fragment = """
		#version 300 es
		precision highp float;
		uniform vec3 uColor;
		out vec4 fragColor;
		void main()
		{
			this line is not GLSL and cannot be compiled;
			fragColor = vec4(uColor, 1.0);
		}
		""";

	/// <inheritdoc/>
	protected override string VertexShaderSource => Vertex;

	/// <inheritdoc/>
	protected override string FragmentShaderSource => Fragment;
}
