using InannaRosette.Reading.Services;

namespace InannaRosette.Services;

/// <summary>
/// Where a reading's deck comes from. The application registers none, so every view model
/// shuffles a fresh, unseeded <see cref="Deck"/> of its own; an alternate host (a test head)
/// may register one to hand out decks in a known order.
/// </summary>
public interface IDeckFactory
{
    /// <summary>Creates the deck for one new reading table; called once per view model.</summary>
    /// <returns>A deck of all forty cards, already shuffled.</returns>
    Deck Create();
}
