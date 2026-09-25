using CodeBrix.Android.UIReqs.Device.TestTarget;
using Microsoft.UI.Xaml;
using Reqnroll;
using Windows.System;
using Windows.UI;

namespace CodeBrix.Platform.UI.Core.UIReqs.Support;

/// <summary>
/// How a quoted value in a feature file becomes the type a step definition asks for. Reqnroll
/// picks the transformation by the step's parameter type, so a step can simply take a
/// <see cref="Color"/> and the scenario can simply say "Red".
/// <para>
/// A coverage group that needs another conversion adds it as a
/// <c>[StepArgumentTransformation]</c> method - either in a partial file of this class named
/// <c>StepArgumentTransformations.&lt;Group&gt;.cs</c>, or in a binding class of its own, since
/// Reqnroll collects them from every binding class in the assembly. A partial CLASS admits as
/// many files as there are groups; only a partial METHOD would be single-occupancy, which is
/// why no registration hook of this harness is one. One conversion per parameter type: two
/// transformations returning the same type make every step that takes it ambiguous.
/// </para>
/// </summary>
[Binding]
public sealed partial class StepArgumentTransformations
{
	/// <summary>Converts a colour name or "#AARRGGBB" into a colour.</summary>
	/// <param name="text">The text a feature file wrote.</param>
	/// <returns>The colour.</returns>
	[StepArgumentTransformation]
	public Color ToColor(string text) => Colors.Parse(text);

	/// <summary>Converts "8", "8,4" or "1,2,3,4" into a thickness.</summary>
	/// <param name="text">The text a feature file wrote.</param>
	/// <returns>The thickness.</returns>
	[StepArgumentTransformation]
	public Thickness ToThickness(string text) => GherkinValue.ToThickness(text);

	/// <summary>Converts a key name into a key.</summary>
	/// <param name="text">The text a feature file wrote.</param>
	/// <returns>The key.</returns>
	[StepArgumentTransformation]
	public VirtualKey ToVirtualKey(string text) => GherkinValue.ToVirtualKey(text);

	/// <summary>Converts "Landscape" or "Portrait" into a panel orientation.</summary>
	/// <param name="text">The text a feature file wrote.</param>
	/// <returns>The orientation.</returns>
	[StepArgumentTransformation]
	public TestDisplayOrientation ToOrientation(string text) => GherkinValue.ToOrientation(text);
}
