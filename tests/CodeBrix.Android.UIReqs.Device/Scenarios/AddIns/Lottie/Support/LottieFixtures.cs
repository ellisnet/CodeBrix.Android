using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace CodeBrix.Platform.UI.AddIn.Lottie.UIReqs.Support;

/// <summary>
/// The animation files the scenarios play, and the one word a feature file names each of them
/// by. Both are hand-written Bodymovin documents kept in this project's <c>Assets</c> folder:
/// small enough to read, and drawn as flat colours on whole-pixel boundaries so that where a
/// colour lands on the panel says exactly what the player did with the animation.
/// <para>
/// Both are addressed with <c>ms-appx:///</c>, which resolves against the folder the test
/// executable is in. An <c>embedded://</c> URI names the assembly it is embedded in, and the
/// Landscape and Portrait executables of a pair have different assembly names, so the same
/// feature file could not name one document for both. The build copies the files beside both
/// executables at the same relative path instead.
/// </para>
/// </summary>
public static class LottieFixtures
{
	/// <summary>
	/// A red 30 by 30 square that travels from x = 20 to x = 80 across a 100 by 100 composition
	/// in exactly one second (30 frames at 30 frames per second). Its layer outlives the
	/// composition, so the frame at progress 1 still shows the square rather than an empty
	/// composition.
	/// </summary>
	public const string Pulse = "pulse";

	/// <summary>
	/// A still square that fills its whole 100 by 100 composition, painted by a shape whose name
	/// carries a Foreground colour binding. It is the document a themable source recolours, and
	/// - because its ink is the whole composition - it is also the document that shows what
	/// Stretch does to the size the animation is drawn at.
	/// </summary>
	public const string Themed = "themed";

	private static readonly IReadOnlyDictionary<string, LottieFixture> Fixtures = Build();

	/// <summary>The names a feature file may use, in alphabetical order.</summary>
	public static IReadOnlyCollection<string> Names => new[] { Pulse, Themed };

	/// <summary>Looks a fixture up by the name a feature file wrote.</summary>
	/// <param name="name">The name, as a feature file spells it.</param>
	/// <returns>The fixture.</returns>
	/// <exception cref="NotSupportedException">No fixture goes by that name.</exception>
	public static LottieFixture Named(string name)
	{
		ArgumentException.ThrowIfNullOrEmpty(name);

		return Fixtures.TryGetValue(name, out var fixture)
			? fixture
			: throw new NotSupportedException(
				$"There is no animation called \"{name}\" in this project. It has: "
				+ string.Join(", ", Names) + ".");
	}

	private static IReadOnlyDictionary<string, LottieFixture> Build()
	{
		var fixtures = new Dictionary<string, LottieFixture>(StringComparer.OrdinalIgnoreCase)
		{
			[Pulse] = new LottieFixture(Pulse, new Uri("ms-appx:///Assets/pulse.json"), IsThemable: false),
			[Themed] = new LottieFixture(Themed, new Uri("ms-appx:///Assets/themed.json"), IsThemable: true),
		};

		return new ReadOnlyDictionary<string, LottieFixture>(fixtures);
	}
}

/// <summary>One animation document a scenario can put into a player.</summary>
/// <param name="Name">The name a feature file writes.</param>
/// <param name="Uri">Where the player loads it from.</param>
/// <param name="IsThemable">
/// Whether the document carries colour bindings, and therefore whether a scenario naming it
/// gets the source that can rewrite them.
/// </param>
public sealed record LottieFixture(string Name, Uri Uri, bool IsThemable);
