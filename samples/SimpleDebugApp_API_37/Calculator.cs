using System;
using System.Collections.Generic;

namespace SimpleDebugApp;

/// <summary>
/// Pure C# with no Android dependencies, so breakpoints here show ordinary locals and
/// the same code could run in a host-side test.
/// </summary>
public static class Calculator
{
	/// <summary>Sieve of Eratosthenes; the locals sieve, candidate and found are worth watching.</summary>
	public static int[] PrimesBelow(int limit)
	{
		if (limit < 2)
		{
			return Array.Empty<int>();
		}

		bool[] composite = new bool[limit];
		List<int> found = new List<int>();
		for (int candidate = 2; candidate < limit; candidate++)
		{
			if (composite[candidate])
			{
				continue;
			}

			found.Add(candidate);
			for (int multiple = candidate * 2; multiple < limit; multiple += candidate)
			{
				composite[multiple] = true;
			}
		}

		return found.ToArray();
	}

	/// <summary>A loop long enough to be visibly running when paused from a worker thread.</summary>
	public static long SumOfSquares(int n)
	{
		long total = 0;
		for (int i = 1; i <= n; i++)
		{
			total += (long)i * i;
		}

		return total;
	}

	/// <summary>Builds a sentence from a count; several locals to inspect on one line.</summary>
	public static string DescribeCount(int count)
	{
		string parity = count % 2 == 0 ? "even" : "odd";
		string size = count < 5 ? "small" : count < 20 ? "medium" : "large";
		bool isSquare = Math.Sqrt(count) % 1 == 0;
		string squareNote = isSquare ? ", a perfect square" : string.Empty;
		return $"Count is {count}: {parity}, {size}{squareNote}";
	}

	/// <summary>Always throws; the message carries the count so the exception is easy to identify.</summary>
	public static void ThrowForDemo(int count)
	{
		throw new InvalidOperationException($"Demonstration exception at count {count}");
	}
}
