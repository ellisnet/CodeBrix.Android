using System;
using CodeBrix.Platform.OpenGL;
using CodeBrix.Platform.UI.Core.UIReqs.Support;
using CodeBrix.Platform.WinUI.Graphics3DGL;

namespace CodeBrix.Platform.UI.AddIn.Graphics3DGL.UIReqs.Support;

/// <summary>
/// What every Graphics3DGL fixture element has in common: two shader sources the subclass
/// supplies, one triangle uploaded once, a clear colour and a triangle colour a scenario can
/// name, and the counters a scenario uses as a completion signal - how often the element has
/// been asked to render, and how often it has been set up and torn down.
/// <para>
/// The rules a fixture that draws through the PROCESS-WIDE OpenGL context has to keep are all
/// here, in one place. It NEVER invalidates itself from inside its own render (that is the
/// documented animation idiom, and an element that does it is dirty forever, so the dispatcher
/// never goes idle and every frame the harness asks for times out). It never blocks and never
/// waits inside a render. And it RESTORES the shared state it touches - the bound vertex array
/// and the program in use - because the very same context is the one the panel's own renderer
/// draws with, and anything left behind would outlive the scenario and corrupt every later one
/// in the process.
/// </para>
/// </summary>
public abstract class GLCanvasFixture : GLCanvasElement
{
	// Clip space runs -1..1 on both axes. The base sits at the BOTTOM in OpenGL's own
	// coordinates and the apex at the TOP, which is what makes "the presented image is not
	// upside down" a claim with a right and a wrong answer: the element presents its
	// framebuffer through a vertically flipped brush, so the apex has to come out at the top of
	// the element. The triangle covers 32% of the element (half of 0.8 x 0.8 of the 2 x 2 clip
	// square), comfortably above the fraction any scenario claims.
	private static readonly float[] TriangleVertices = [-0.8f, -0.8f, 0.8f, -0.8f, 0.0f, 0.8f];

	private uint _program;
	private uint _vertexArray;
	private uint _vertexBuffer;
	private int _colorLocation;

	/// <summary>Builds the fixture. The owning-window function is a WinUI concern; here it is null.</summary>
	protected GLCanvasFixture()
		: base(null)
	{
	}

	/// <summary>
	/// How many times the element has been asked to render since it was built. This is the
	/// completion signal a scenario waits on before it looks at a frame: the picture a person
	/// sees is filled in outside the recording, so "the element has rendered" is the fact that
	/// says the picture is there to be looked at.
	/// </summary>
	public int RenderCount { get; private set; }

	/// <summary>How many times the element has set its OpenGL resources up.</summary>
	public int InitCount { get; private set; }

	/// <summary>How many times the element has given its OpenGL resources back.</summary>
	public int DestroyCount { get; private set; }

	/// <summary>Whether the triangle is drawn on top of the cleared surface.</summary>
	public bool DrawTriangle { get; set; } = true;

	/// <summary>The colour the whole surface is cleared to, as red, green and blue in 0..1.</summary>
	public (float Red, float Green, float Blue) ClearColor { get; private set; } = (0f, 0f, 1f);

	/// <summary>The colour the triangle is filled with, as red, green and blue in 0..1.</summary>
	public (float Red, float Green, float Blue) TriangleColor { get; private set; } = (1f, 0f, 0f);

	/// <summary>The vertex shader this fixture compiles.</summary>
	protected abstract string VertexShaderSource { get; }

	/// <summary>The fragment shader this fixture compiles.</summary>
	protected abstract string FragmentShaderSource { get; }

	/// <summary>Sets the colour the surface is cleared to, as a feature file writes it.</summary>
	/// <param name="value">A colour name or an "#AARRGGBB" value.</param>
	public void SetClearColor(string value) => ClearColor = ToRgb(value);

	/// <summary>Sets the colour the triangle is filled with, as a feature file writes it.</summary>
	/// <param name="value">A colour name or an "#AARRGGBB" value.</param>
	public void SetTriangleColor(string value) => TriangleColor = ToRgb(value);

	/// <summary>Sets whether the triangle is drawn at all, as a feature file writes it.</summary>
	/// <param name="value">"true" or "false".</param>
	public void SetDrawTriangle(string value) =>
		DrawTriangle = bool.Parse(GherkinValue.Unquote(value));

	/// <summary>
	/// Compiles the shaders and uploads the one triangle, with the OpenGL context current.
	/// Called by the add-in on the UI thread when the element is loaded. A shader that cannot
	/// compile throws from here, which is exactly what the add-in turns into a reported
	/// initialisation failure rather than an exception out of a Loaded handler.
	/// </summary>
	/// <param name="gl">The OpenGL binding of the element's context.</param>
	protected override unsafe void Init(GL gl)
	{
		ArgumentNullException.ThrowIfNull(gl);

		InitCount++;

		_program = BuildProgram(gl, VertexShaderSource, FragmentShaderSource);
		_colorLocation = gl.GetUniformLocation(_program, "uColor");

		_vertexArray = gl.GenVertexArray();
		gl.BindVertexArray(_vertexArray);
		_vertexBuffer = gl.GenBuffer();
		gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vertexBuffer);
		gl.BufferData<float>(BufferTargetARB.ArrayBuffer, TriangleVertices, BufferUsageARB.StaticDraw);
		gl.EnableVertexAttribArray(0);
		gl.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, 2 * sizeof(float), (void*) 0);

		// Nothing stays bound: the context is shared with the panel's own renderer.
		gl.BindVertexArray(0);
		gl.BindBuffer(BufferTargetARB.ArrayBuffer, 0);
	}

	/// <summary>
	/// Clears the surface and draws the triangle. Called by the add-in on the UI thread with the
	/// element's own framebuffer bound and the viewport already set to its arranged size.
	/// </summary>
	/// <param name="gl">The OpenGL binding of the element's context.</param>
	protected override void RenderOverride(GL gl)
	{
		ArgumentNullException.ThrowIfNull(gl);

		RenderCount++;

		gl.ClearColor(ClearColor.Red, ClearColor.Green, ClearColor.Blue, 1f);
		gl.Clear((uint) ClearBufferMask.ColorBufferBit | (uint) ClearBufferMask.DepthBufferBit);

		if (!DrawTriangle || _program == 0)
		{
			return;
		}

		gl.UseProgram(_program);
		gl.Uniform3(_colorLocation, TriangleColor.Red, TriangleColor.Green, TriangleColor.Blue);
		gl.BindVertexArray(_vertexArray);
		gl.DrawArrays(PrimitiveType.Triangles, 0, 3);

		// The two lines the add-in's own documentation asks every subclass for. Without them the
		// panel's renderer would go on drawing with this fixture's program and vertex array.
		gl.BindVertexArray(0);
		gl.UseProgram(0);

		// Deliberately NO Invalidate() here: see the class remarks.
	}

	/// <summary>
	/// Gives the shaders and the vertex buffer back. Called by the add-in on the UI thread with
	/// the context current when the element leaves the tree.
	/// </summary>
	/// <param name="gl">The OpenGL binding of the element's context.</param>
	protected override void OnDestroy(GL gl)
	{
		ArgumentNullException.ThrowIfNull(gl);

		DestroyCount++;

		if (_vertexArray != 0)
		{
			gl.DeleteVertexArray(_vertexArray);
			_vertexArray = 0;
		}

		if (_vertexBuffer != 0)
		{
			gl.DeleteBuffer(_vertexBuffer);
			_vertexBuffer = 0;
		}

		if (_program != 0)
		{
			gl.DeleteProgram(_program);
			_program = 0;
		}
	}

	private static (float Red, float Green, float Blue) ToRgb(string value)
	{
		var color = Colors.Parse(GherkinValue.Unquote(value));
		return (color.R / 255f, color.G / 255f, color.B / 255f);
	}

	private static uint BuildProgram(GL gl, string vertexSource, string fragmentSource)
	{
		var vertex = Compile(gl, ShaderType.VertexShader, vertexSource);
		var fragment = Compile(gl, ShaderType.FragmentShader, fragmentSource);

		var handle = gl.CreateProgram();
		gl.AttachShader(handle, vertex);
		gl.AttachShader(handle, fragment);
		gl.LinkProgram(handle);
		gl.GetProgram(handle, ProgramPropertyARB.LinkStatus, out var linked);
		if (linked == 0)
		{
			var log = gl.GetProgramInfoLog(handle);
			gl.DeleteShader(vertex);
			gl.DeleteShader(fragment);
			gl.DeleteProgram(handle);
			throw new InvalidOperationException($"The shader program could not be linked: {log}");
		}

		gl.DetachShader(handle, vertex);
		gl.DetachShader(handle, fragment);
		gl.DeleteShader(vertex);
		gl.DeleteShader(fragment);
		return handle;
	}

	private static uint Compile(GL gl, ShaderType type, string source)
	{
		var shader = gl.CreateShader(type);
		gl.ShaderSource(shader, source);
		gl.CompileShader(shader);
		gl.GetShader(shader, ShaderParameterName.CompileStatus, out var compiled);
		if (compiled == 0)
		{
			var log = gl.GetShaderInfoLog(shader);
			gl.DeleteShader(shader);

			// The driver's own message is what a person needs, and it is what the add-in puts
			// into the failed reason a scenario then reads.
			throw new InvalidOperationException($"The {type} could not be compiled: {log}");
		}

		return shader;
	}
}
