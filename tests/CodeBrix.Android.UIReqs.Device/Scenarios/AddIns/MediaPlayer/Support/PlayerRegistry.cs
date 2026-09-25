using System.Collections.Generic;
using MediaPlayerEngine = Windows.Media.Playback.MediaPlayer;

namespace CodeBrix.Platform.UI.AddIn.MediaPlayer.UIReqs.Support;

/// <summary>
/// The players a scenario's elements built, so that every one of them is released when the
/// scenario ends.
/// <para>
/// The harness's own reset empties the panel, which is enough for an element that only draws.
/// A player is not one of those: it owns native decode threads, a native player handle and -
/// for a clip that carries an audio track - an output device, none of which the garbage
/// collector is in a hurry about. One left alive would still be holding them while the next
/// scenario ran.
/// </para>
/// </summary>
public static class PlayerRegistry
{
	private static readonly object Gate = new();
	private static readonly List<MediaPlayerEngine> Players = [];

	/// <summary>How many players this scenario is holding.</summary>
	public static int Count
	{
		get
		{
			lock (Gate)
			{
				return Players.Count;
			}
		}
	}

	/// <summary>
	/// Remembers a player, unless it is one that is already remembered. The answer is what tells
	/// a caller whether this is the first time it has seen this player - an element that is
	/// handed a different player later publishes the change through the same property, and
	/// subscribing twice would count every event twice.
	/// </summary>
	/// <param name="player">The player to remember.</param>
	/// <returns><c>true</c> when the player was not already remembered.</returns>
	public static bool Add(MediaPlayerEngine player)
	{
		lock (Gate)
		{
			foreach (var known in Players)
			{
				if (ReferenceEquals(known, player))
				{
					return false;
				}
			}

			Players.Add(player);
			return true;
		}
	}

	/// <summary>Hands back every remembered player and forgets them all.</summary>
	/// <returns>The players.</returns>
	public static IReadOnlyList<MediaPlayerEngine> TakeAll()
	{
		lock (Gate)
		{
			var taken = Players.ToArray();
			Players.Clear();
			return taken;
		}
	}
}
