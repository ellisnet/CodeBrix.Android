using InannaRosette.Reading.Models;

namespace InannaRosette.ViewModels;

/// <summary>
/// One card that has left the deck: either waiting in the tray or laid on a station of the
/// rosette. The view model is the only thing that changes these values; the page holds the
/// same instances alongside its own card visuals and reads them to know what to draw.
/// </summary>
[Microsoft.UI.Xaml.Data.Bindable]
public sealed class ReadingCard
{
    /// <summary>Creates a card that has just been drawn and is waiting in the tray.</summary>
    /// <param name="card">The card that was drawn.</param>
    public ReadingCard(Card card) => Card = card;

    /// <summary>The card itself: its name, art, keywords and lore.</summary>
    public Card Card { get; }

    /// <summary>True when the card is laid upside down and reads as its reversed meaning.</summary>
    public bool IsReversed { get; internal set; }

    /// <summary>Station index 0..8, or -1 while the card waits in the tray.</summary>
    public int Station { get; internal set; } = -1;

    /// <summary>True while the card is waiting in the tray rather than laid on the rosette.</summary>
    public bool InTray => Station < 0;
}
