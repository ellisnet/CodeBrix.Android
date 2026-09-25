using InannaRosette.ViewModels;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace InannaRosette.Services;

/// <summary>
/// The visual side of a change to the table. The view model decides what is allowed and what
/// happens — which card is drawn, where it may be laid, which way up it reads — and then tells
/// the page, through these delegates, what the person should see. Only a page can build a card
/// visual, move it across the altar or flip it over, so the page fills these in when it takes
/// the view model as its data context.
/// </summary>
public interface IReadingTableBridge
{
    /// <summary>
    /// A card has left the deck. Its <see cref="ReadingCard.Station"/> already says where it
    /// belongs: the tray for an ordinary draw, a station for a reading that was just opened
    /// from a file.
    /// </summary>
    Action<ReadingCard>? CardAdded { get; set; }

    /// <summary>
    /// A card is now somewhere else — laid on <see cref="ReadingCard.Station"/>, or back in the
    /// tray. The boolean asks for the "settling onto the altar" animation, which a deliberate
    /// lay uses and a drag (already at the pointer) does not.
    /// </summary>
    Action<ReadingCard, bool>? CardMoved { get; set; }

    /// <summary>A card's orientation changed and its face has to be turned around.</summary>
    Action<ReadingCard>? CardFlipped { get; set; }

    /// <summary>
    /// A card worth marking has just been drawn from the deck — the goddess of the deck herself —
    /// and the page should celebrate it: a radiance out of the card that was drawn and the words
    /// handed over, said over the altar. The second argument is the blessing to show, so the page
    /// never has to keep a copy of it. It is raised only on a draw, never when a card is moved
    /// between stations or when a saved reading is laid out again, and the view model does not
    /// wait for it: the celebration is a picture over the table, not a step in the command.
    /// </summary>
    Action<ReadingCard, string>? CardCelebrated { get; set; }

    /// <summary>Every card has gone back to the deck; remove all of them from the scene.</summary>
    Action? TableCleared { get; set; }

    /// <summary>
    /// The deck has just been shuffled. The cards handed over were waiting in the tray and have
    /// already gone back into the stack, so the page flies their visuals home and then riffles
    /// the stack; whatever is laid on the rosette was not touched and must not move. The task
    /// completes when the last frame has been drawn, which is what holds
    /// <see cref="ViewModels.MainViewModel.IsBusy"/> true for the length of the animation.
    /// </summary>
    Func<IReadOnlyList<ReadingCard>, Task>? DeckShuffled { get; set; }
}
