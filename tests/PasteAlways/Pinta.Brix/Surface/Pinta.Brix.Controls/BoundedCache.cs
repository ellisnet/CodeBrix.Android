// BoundedCache.cs
//
// The icon caches' storage. Icons are asked for from the toolbox rebuild, the
// menu build, the toolbar builds and the pad toolbars, and a rendered icon is
// worth keeping; an unbounded dictionary that several of those could be
// writing at once is not. This is the smallest thing that fixes both: one lock
// around a dictionary, and a hard capacity that evicts the least recently used
// entry rather than growing without limit.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace Pinta.Brix.Controls;

/// <summary>
/// A thread-safe cache holding at most <see cref="Capacity"/> entries. When a
/// new entry does not fit, the least recently used entry is dropped.
/// </summary>
/// <typeparam name="TKey">The key entries are looked up by.</typeparam>
/// <typeparam name="TValue">The value stored against a key.</typeparam>
public sealed class BoundedCache<TKey, TValue> where TKey : notnull
{
	private readonly object sync = new ();
	private readonly Dictionary<TKey, LinkedListNode<KeyValuePair<TKey, TValue>>> entries;
	private readonly LinkedList<KeyValuePair<TKey, TValue>> order = new ();

	/// <summary>The most entries the cache will hold.</summary>
	public int Capacity { get; }

	/// <summary>How many entries the cache holds right now.</summary>
	public int Count {
		get {
			lock (sync)
				return entries.Count;
		}
	}

	/// <param name="capacity">The most entries to hold; must be greater than zero.</param>
	/// <exception cref="ArgumentOutOfRangeException">
	/// <paramref name="capacity"/> is zero or negative.
	/// </exception>
	public BoundedCache (int capacity)
	{
		if (capacity <= 0)
			throw new ArgumentOutOfRangeException (nameof (capacity), capacity, "A cache has to be able to hold at least one entry.");

		Capacity = capacity;
		entries = new Dictionary<TKey, LinkedListNode<KeyValuePair<TKey, TValue>>> (capacity);
	}

	/// <summary>
	/// Looks a key up, and marks it most recently used when it is found.
	/// </summary>
	/// <param name="key">The key to look up.</param>
	/// <param name="value">The value found, or the default when there is none.</param>
	/// <returns>True when the key was in the cache.</returns>
	public bool TryGetValue (TKey key, [MaybeNullWhen (false)] out TValue value)
	{
		lock (sync) {
			if (!entries.TryGetValue (key, out LinkedListNode<KeyValuePair<TKey, TValue>>? node)) {
				value = default;
				return false;
			}

			order.Remove (node);
			order.AddFirst (node);
			value = node.Value.Value;
			return true;
		}
	}

	/// <summary>
	/// Stores a value against a key, replacing any value already there and
	/// evicting the least recently used entry if the cache is full.
	/// </summary>
	/// <param name="key">The key to store under.</param>
	/// <param name="value">The value to store.</param>
	public void Set (TKey key, TValue value)
	{
		lock (sync) {
			if (entries.TryGetValue (key, out LinkedListNode<KeyValuePair<TKey, TValue>>? existing))
				order.Remove (existing);
			else if (entries.Count >= Capacity && order.Last is { } leastRecent) {
				order.RemoveLast ();
				entries.Remove (leastRecent.Value.Key);
			}

			entries[key] = order.AddFirst (new KeyValuePair<TKey, TValue> (key, value));
		}
	}

	/// <summary>Empties the cache.</summary>
	public void Clear ()
	{
		lock (sync) {
			entries.Clear ();
			order.Clear ();
		}
	}
}
