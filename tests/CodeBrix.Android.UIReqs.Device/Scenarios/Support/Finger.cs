using System;
using System.Globalization;

namespace CodeBrix.Platform.UI.Core.UIReqs.Support;

/// <summary>
/// The one finger a scenario is holding down. The harness's tap is a press and a release in a
/// single step, so anything a control shows only WHILE a finger rests on it - a pressed visual
/// state, a tracker, a scrub handle, a long-press affordance - is invisible to a tap. A
/// scenario that wants to see one puts a finger down, captures its frame, and lifts it again;
/// where that finger landed is kept here so that the lift needs no coordinates of its own, and
/// so that a step which puts the finger somewhere only one add-in can work out - the centre of
/// a plot area, say - is lifted by the same core sentence.
/// <para>
/// One finger at a time: a gesture that needs two drives the session's pointer ids itself.
/// </para>
/// </summary>
public static class Finger
{
	/// <summary>The pointer id a one-finger gesture uses.</summary>
	public const int PointerId = 0;

	private static (int X, int Y)? _down;

	/// <summary>Whether a finger is resting on the panel right now.</summary>
	public static bool IsDown => _down is not null;

	/// <summary>Where the finger that is down landed, in device pixels.</summary>
	/// <exception cref="InvalidOperationException">No finger is down.</exception>
	public static (int X, int Y) Position => _down ?? throw NoFinger();

	/// <summary>Records that a finger was put down at a point of the panel.</summary>
	/// <param name="x">The x coordinate it landed at, in device pixels.</param>
	/// <param name="y">The y coordinate it landed at, in device pixels.</param>
	/// <exception cref="InvalidOperationException">A finger is already down.</exception>
	public static void PutDown(int x, int y)
	{
		if (_down is { } already)
		{
			var where = string.Create(CultureInfo.InvariantCulture, $"({already.X},{already.Y})");
			throw new InvalidOperationException(
				$"A finger is already down at {where}, so a second one cannot be put down. "
				+ "Lift it first, or drive a two-finger gesture through the session's pointer ids.");
		}

		_down = (x, y);
	}

	/// <summary>Takes the finger back up and says where it had been.</summary>
	/// <returns>The point the finger was resting on.</returns>
	/// <exception cref="InvalidOperationException">No finger is down.</exception>
	public static (int X, int Y) Lift()
	{
		var down = _down ?? throw NoFinger();
		_down = null;
		return down;
	}

	/// <summary>
	/// Takes the finger back up if there is one, without minding when there is not. This is what
	/// the between-scenario reset asks: a scenario that failed between the press and the lift
	/// must not leave a finger resting on the panel for the next one.
	/// </summary>
	/// <param name="position">The point the finger was resting on.</param>
	/// <returns><c>true</c> when a finger was down, and has now been forgotten.</returns>
	public static bool TryLift(out (int X, int Y) position)
	{
		if (_down is { } down)
		{
			position = down;
			_down = null;
			return true;
		}

		position = default;
		return false;
	}

	private static InvalidOperationException NoFinger() => new(
		"No finger is down, so none can be lifted. A scenario puts one down with "
		+ "\"When a finger is put down on ...\" first.");
}
