using InannaRosette.Reading.Data;
using InannaRosette.Reading.Models;

namespace InannaRosette.Reading.Services;

/// <summary>
/// A shuffled stack of the forty cards. The top of the stack is index 0 of <see cref="Cards"/>.
/// Deterministic when constructed or shuffled with a seed. No UI, no I/O.
/// </summary>
public sealed class Deck
{
    /// <summary>Chance that a drawn card is laid reversed.</summary>
    public const double ReversedChance = 0.25;

    private readonly List<Card> _stack;
    private Random _rng;

    /// <summary>Creates a deck of all forty cards, already shuffled.</summary>
    /// <param name="seed">Optional seed; the same seed always produces the same order.</param>
    public Deck(int? seed = null)
    {
        _rng = seed is int s ? new Random(s) : new Random();
        _stack = [.. DeckData.Cards];
        ShuffleStack();
    }

    /// <summary>The cards still in the stack, top first.</summary>
    public IReadOnlyList<Card> Cards => _stack;

    /// <summary>How many cards remain in the stack.</summary>
    public int Remaining => _stack.Count;

    /// <summary>The random source used for shuffling and for <see cref="RollReversed"/>.</summary>
    public Random Rng => _rng;

    /// <summary>Fisher-Yates shuffle of whatever is currently in the stack.</summary>
    /// <param name="seed">Optional seed; when given, the deck's random source is reset to it.</param>
    public void Shuffle(int? seed = null)
    {
        if (seed is int s) _rng = new Random(s);
        ShuffleStack();
    }

    /// <summary>Takes the top card, or null when the deck is empty.</summary>
    public Card? Draw()
    {
        if (_stack.Count == 0) return null;
        var card = _stack[0];
        _stack.RemoveAt(0);
        return card;
    }

    /// <summary>Looks at the top card without removing it, or null when the deck is empty.</summary>
    public Card? Peek() => _stack.Count == 0 ? null : _stack[0];

    /// <summary>Places a card under the stack.</summary>
    public void ReturnToBottom(Card card)
    {
        ArgumentNullException.ThrowIfNull(card);
        _stack.Add(card);
    }

    /// <summary>
    /// Takes one named card out of the stack, wherever in it the card is sitting. A caller that
    /// is laying a card on the table uses this to keep the two halves of the pack apart: what is
    /// on the table is never also in the stack, so it can never be drawn a second time.
    /// </summary>
    /// <param name="card">The card to take out; matched on <see cref="Models.Card.Id"/>.</param>
    /// <returns>True when the card was in the stack and has been taken out of it;
    /// false when the stack did not hold it, which leaves the stack unchanged.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="card"/> is null.</exception>
    public bool Remove(Card card)
    {
        ArgumentNullException.ThrowIfNull(card);

        var index = _stack.FindIndex(c => c.Id == card.Id);
        if (index < 0) { return false; }

        _stack.RemoveAt(index);
        return true;
    }

    /// <summary>Restores all forty cards and reshuffles.</summary>
    public void Reset()
    {
        _stack.Clear();
        _stack.AddRange(DeckData.Cards);
        ShuffleStack();
    }

    /// <summary>Draws a card together with an orientation, or null when the deck is empty.</summary>
    public (Card Card, bool IsReversed)? DrawOriented()
    {
        var card = Draw();
        return card is null ? null : (card, RollReversed(_rng));
    }

    /// <summary>True roughly one time in four: whether a card is laid reversed.</summary>
    public static bool RollReversed(Random rng)
    {
        ArgumentNullException.ThrowIfNull(rng);
        return rng.NextDouble() < ReversedChance;
    }

    private void ShuffleStack()
    {
        for (var i = _stack.Count - 1; i > 0; i--)
        {
            var j = _rng.Next(i + 1);
            (_stack[i], _stack[j]) = (_stack[j], _stack[i]);
        }
    }
}
