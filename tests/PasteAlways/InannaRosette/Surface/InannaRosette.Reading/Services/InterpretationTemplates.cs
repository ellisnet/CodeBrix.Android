using InannaRosette.Reading.Models;

namespace InannaRosette.Reading.Services;

/// <summary>
/// The sentence stock the <see cref="ReadingInterpreter"/> draws on. Every slot offers
/// several variants, chosen by a seed derived from the cards themselves, so that two
/// different layouts do not read alike while the same layout always reads the same.
/// </summary>
internal static class InterpretationTemplates
{
    #region Deterministic choice

    /// <summary>Scrambles a seed with a salt so that different slots make independent choices.</summary>
    public static int Mix(int seed, int salt)
    {
        unchecked
        {
            var h = (uint)seed * 2654435761u ^ (uint)(salt * 40503 + 0x9E37);
            h ^= h >> 15;
            h *= 2246822519u;
            h ^= h >> 13;
            h *= 3266489917u;
            h ^= h >> 16;
            return (int)(h & 0x7FFFFFFF);
        }
    }

    /// <summary>Picks one of the variants deterministically.</summary>
    public static T Pick<T>(IReadOnlyList<T> variants, int seed, int salt) =>
        variants[Mix(seed, salt) % variants.Count];

    #endregion

    #region Small text helpers

    /// <summary>Joins up to <paramref name="take"/> items as "a, b and c".</summary>
    public static string Phrase(IReadOnlyList<string> items, int take = 3)
    {
        var chosen = items.Take(Math.Max(1, take)).ToList();
        if (chosen.Count == 0) return "what cannot yet be named";
        if (chosen.Count == 1) return chosen[0];
        return string.Join(", ", chosen.Take(chosen.Count - 1)) + " and " + chosen[^1];
    }

    /// <summary>Ensures the text ends with a full stop, question mark or dash.</summary>
    public static string EnsureStop(string text)
    {
        var t = text.Trim();
        if (t.Length == 0) return t;
        return ".?!—:".Contains(t[^1]) ? t : t + ".";
    }

    /// <summary>Counts whitespace-separated words.</summary>
    public static int WordCount(string text) =>
        text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;

    /// <summary>"upright" or "reversed".</summary>
    public static string Orient(bool reversed) => reversed ? "reversed" : "upright";

    #endregion

    #region Opening — frame

    public static readonly string[] OpeningFrame =
    [
        "The rosette has been laid{0}, {1} {2} set in the pattern of the eight-petalled star, to be read as one flower and not as a row of verdicts.{3}",
        "Here is the rosette{0}: {1} {2} turning about a single centre, in the old order that runs from heaven down through the descent and home again.{3}",
        "This reading opens{0} with {1} {2} on the petals of Inanna's star, laid in the sequence the goddess walked, from the highest reach to the great below.{3}",
        "The rosette is down{0} — {1} {2} arranged as the whorl of petals that gave this spread its name, each station holding one face of a single matter.{3}",
    ];

    #endregion

    #region Opening — the weight of the suits

    public static readonly string[] SuitCount =
    [
        "The suits divide as {0}, {1} and {2}.",
        "The count runs {0}, then {1}, then {2}.",
        "By suit that is {0}, {1} and {2}.",
    ];

    public static readonly string[] GoddessDominant =
    [
        "The goddesses hold the field, making this a reading about persons and powers: who you are asked to become, and whose hand is already on the matter.",
        "With the goddesses in the majority, the rosette speaks of character rather than circumstance; the forces working here have names, faces and very long memories.",
        "A crowd of goddesses gives the flower a personal weather. What moves in this matter moves through people — through temperament, allegiance and appetite rather than through events.",
    ];

    public static readonly string[] GateDominant =
    [
        "The Gates dominate, and a gate is a threshold. This reading is about passage: what is taken from you at each door, and in what order.",
        "With so many Gates on the flower, the matter is one of surrender and sequence. Something is being dismantled deliberately, and the dismantling has a shape you can follow.",
        "The Gates crowd this rosette, which is the signature of a season of stripping — not punishment but toll, paid one ornament at a time on the way down.",
    ];

    public static readonly string[] EmblemDominant =
    [
        "The Emblems lead, so the reading speaks in signs and instruments: the objects, habits and boundaries by which a life is organised rather than the people inside it.",
        "With the Sacred Emblems in the majority this is a practical rosette, concerning what you use, keep, plant and mark rather than who is involved.",
        "A weight of Emblems turns the reading outward, toward the visible furniture of your life — the boundary post, the storehouse, the boat, the star you have been steering by.",
    ];

    public static readonly string[] SuitsBalanced =
    [
        "No suit takes the flower. Goddess, Gate and Emblem hold roughly equal ground, which says the matter is genuinely mixed: person, passage and practical thing all at once.",
        "The three suits stand near enough level that none rules. Read this as a season with several true causes, none of which yields to attending to only one.",
        "The rosette is evenly weighted. Character, threshold and instrument each claim a share, and the reading will not let you reduce this to a single kind of problem.",
    ];

    #endregion

    #region Opening — the turned cards

    public static readonly string[] ReversedNone =
    [
        "Not one card lies reversed, which is worth noticing: the powers here speak in their plain voices, and what they say may be taken at face value.",
        "Every card stands upright. There is no hidden inversion here, so the difficulty, if difficulty there is, lies in the doing rather than in anything turned against itself.",
        "All of the cards are upright, so the reading arrives without a counter-current. Nothing in the pattern is blocked or folded inward; the whole flower faces you directly.",
    ];

    public static readonly string[] ReversedOne =
    [
        "A single card lies reversed, and one lone inversion is usually the hinge of a reading: the one place where the current runs backwards while everything around it flows.",
        "Just one reversal appears. Read it as the sore tooth of the rosette — small, specific, and responsible for more of the general ache than its size suggests.",
        "One card alone is turned. That single inversion is where the pattern catches, and attending to it honestly will usually loosen several other things at the same time.",
    ];

    public static readonly string[] ReversedFew =
    [
        "{0} cards lie reversed, a moderate resistance through the flower. Some of what is offered here is blocked, turned inward, or arriving at the wrong angle to use.",
        "With {0} reversals the rosette carries a real counter-current. These are not disasters; they are the places where the natural motion of a thing has been interrupted.",
        "{0} of the cards are turned. That is enough to give the reading friction without giving it a verdict: expect several matters to be working against their own grain.",
    ];

    public static readonly string[] ReversedMany =
    [
        "{0} cards lie reversed, which is a great many. A rosette this inverted describes a season under strain, where what ought to flow has been dammed or turned back.",
        "With {0} reversals the flower is heavily turned. Do not read that as doom; read it as evidence that the present arrangement is costing far more than it returns.",
        "{0} cards are reversed, a strong tide against the reading. Something structural is out of true, and righting one or two of these will do more than effort elsewhere.",
    ];

    #endregion

    #region Opening — the heart

    public static readonly string[] HeartPresent =
    [
        "At the still centre lies {0}, {1}, {2}. That is the true subject: whatever names you have been giving this, the matter is about {3} and about {4}.",
        "The Heart of the rosette is {0}, {1}, {2}, naming the matter more honestly than the question did: {3}, {4}.",
        "{0}, {1}, holds the centre {2}. The matter is therefore {3}, with {4} close behind — the petals are only the ways that one thing shows itself in your days.",
    ];

    public static readonly string[] HeartAbsent =
    [
        "The Heart of the rosette is empty, so the reading does not declare its subject. That is no fault: it leaves the centre to you, and the petals stand as evidence rather than commentary.",
        "No card holds the centre. Without a Heart the rosette describes the weather and not the ground; you must name the matter yourself once the petals have been heard.",
        "The still point is unoccupied. Read this as a set of readings around an absence: the petals circle the true subject without any one of them saying it outright.",
    ];

    #endregion

    #region Opening — close

    public static readonly string[] OpeningClose =
    [
        "Take what follows station by station, and let the opposite petals argue with one another before you decide what any of it means.",
        "Read on slowly. Each petal is corrected by the one facing it, and the sense of the whole arrives only at the end.",
        "What follows is given plainly, petal by petal. Nothing here is fixed: a season can be walked differently once it has been seen.",
    ];

    #endregion

    #region Positions

    public static readonly string[] PositionTie =
    [
        "The station of {0} asks: {1} It is answered by {2}, {3}, lying {4}.",
        "{2} falls at {0}, where the rosette asks: {1} Take the card as the shape of that answer, {4} as it lies.",
        "Here at {0} — {1} — the flower sets {2}, {5}, {4}.",
        "{0} puts its question: {1} The answer is {2}, {3}, and it comes to you {4}.",
    ];

    public static readonly string[] PositionClose =
    [
        "Set that against the plain facts of your week, and let {0} keep its own counsel until the petal facing it has spoken.",
        "That is what {0} holds in this reading; carry it as a description and not as a sentence passed on you.",
        "Read this as the particular work of {0}, and do not let it be diluted into a general mood.",
        "Whatever else the rosette says, this is the answer {0} gives, and it is worth writing down again in your own words.",
    ];

    public static readonly string[] AxisPair =
    [
        "Across the flower, {0} faces {1}, and {2} answers to {3}. The words at this end are {4}; at the other, {5}. Set the two concerns side by side — {6} at this end, {7} at the other — and the bargain of the axis is plain: you are buying one with the other, and it is worth knowing which you are spending.",
        "{0} and {1} are the two ends of a single axis, holding {2} and {3}. The words at this end are {4}; at the far end, {5}. One end is a matter of {6}; the other is a matter of {7}. That exchange is already going on, and it is far better made deliberately than by default.",
        "Look along the axis from {0} to {1}. {2} and {3} are not two subjects but one seen from both sides — {4} here, {5} there. This end is a question of {6}; the far end, of {7}. Read them together or neither will make much sense.",
    ];

    #endregion

    #region Insights

    public static readonly string[] SuitInsightConsequence =
    [
        "Let that proportion set your expectations: a reading weighted this way rewards the kind of attention it is asking for, and frustrates every other kind.",
        "Take the proportion seriously before the details. It tells you what sort of effort will actually purchase something here, and what sort will only tire you out.",
        "The balance of suits is the reading's temperament. Work with it rather than against it, and the individual cards will be much easier to place.",
    ];

    public static readonly string[] ReversedInsightLead =
    [
        "Orientation is half of what a card says, and this rosette has been read exactly as it fell.",
        "Before the individual cards, look at how they are lying; the pattern of turning is itself a message.",
        "A card's direction matters as much as its name, so count the inversions before you read a word.",
    ];

    public static readonly string[] ReversedInsightAdvice =
    [
        "Where a card is turned, do not try to force it upright by effort. Ask instead what is standing in its way, and remove that one obstruction first.",
        "Treat each reversal as a question about conditions rather than about will. Something is being asked to run against its grain, and the grain will win.",
        "A turned card is rarely a punishment. It marks a current running backwards, and the remedy is almost always to change the channel rather than the water.",
    ];

    public static readonly string[] HeartInsightClose =
    [
        "Keep the centre in view as you read the petals; each of them is only this one matter, seen from one of the eight directions.",
        "Whenever a petal seems to wander, bring it back to the centre and ask how it is a version of that same thing.",
        "The centre is the measure of the rest. A petal that cannot be related to it is probably being over-read.",
    ];

    public static readonly string[] PartialInsightText =
    [
        "Only {0} of the nine stations carry cards, so this rosette is deliberately incomplete. What is absent is not empty of meaning: an unfilled station marks ground the reading declines to speak for, and that silence should be respected rather than filled in by guesswork. Read what is here closely, and let the gaps stand as honest gaps until more of the flower is laid.",
        "This is a partial rosette: {0} stations of nine. A short reading is not a lesser one, but it is a narrower one, and it should not be stretched to cover the whole of a life. Take the cards that are present as precise answers to their own questions, and leave the unlaid petals genuinely open.",
        "With {0} of nine stations filled, the flower is only part-open. The petals that are down speak clearly; the ones that are not have not been asked. Resist the pull to infer the missing cards from the pattern of the present ones, and treat what is here as complete in itself, however small.",
    ];

    #endregion

    #region Counsel

    public static readonly string[] CounselFromGift =
    [
        "The Gift is the petal that hands you something to use, and here it holds {0}, {1}. That is the instrument you have been given for this season.",
        "Counsel comes, as it should, from The Gift: {0}, {1}. Whatever the rest of the rosette describes, this is the thing actually placed in your hands.",
        "Take the counsel from the eighth petal, where {0} lies {2}. {1} is not a description of your situation; it is the tool offered for it.",
    ];

    public static readonly string[] CounselFromHeart =
    [
        "No Gift was laid, so the counsel must come from the centre, where {0}, {1}, holds the matter itself.",
        "With the eighth petal empty, the Heart gives the instruction. {0} sits at the centre {2}, and what it asks of you is plain enough.",
        "The rosette offers no Gift, so read the counsel out of the Heart: {0}, {1}, lying {2} at the still point.",
    ];

    public static readonly string[] CounselFromExalted =
    [
        "Neither Gift nor Heart was laid, so the counsel falls to the most exalted goddess on the flower: {0}, {1}.",
        "With the centre and the eighth petal both empty, take the instruction from {0}, {1}, the highest of the goddesses present.",
        "The counsel here belongs to {0}, {1}, who stands nearest the top of the deck's own order among the cards you have drawn.",
    ];

    public static readonly string[] CounselFromAny =
    [
        "This rosette is a small one, so the counsel is drawn from the single strongest card on it: {0}, {1}.",
        "With so little laid, take the instruction from {0}, {1}, and let it stand for the whole.",
        "Let {0}, {1}, carry the counsel; it is the card with the most to say in a reading this short.",
    ];

    public static readonly string[] CounselAction =
    [
        "Concretely: choose one thing belonging to {0} and do it on a named day, not when you feel ready. Write the day down tonight.",
        "In practice, take {1} out of the realm of intention and give it an hour, a place and a witness. Put it in the calendar tonight.",
        "Make it concrete. Name the smallest action that would move {0} forward, do it before the week is out, and plan nothing further until it is finished.",
    ];

    public static readonly string[] CounselSecond =
    [
        "Then attend to the practical side of {0}: one conversation, one payment, one appointment, one repair — the unglamorous item you have carried from list to list since spring.",
        "Alongside that, guard {1}. Decide what you will decline in order to protect it, and decline it plainly rather than by going quiet and hoping to be understood.",
        "Second, put a limit somewhere. Decide the hour you stop, the sum you will not exceed, or the request you will not take on, and say it aloud to one person.",
    ];

    public static readonly string[] CounselClose =
    [
        "None of this requires you to feel differently first. Do the acts in order, and let the feeling catch up in its own time.",
        "Do not attempt the whole rosette at once. One petal, worked properly and to the end, will move the rest of the flower without being asked.",
        "Keep it small enough to actually finish. A completed small thing changes a season, while an unstarted large one changes nothing at all.",
    ];


    public static readonly string[] CounselBridge =
    [
        "Everything in this rosette points back to the same practical ground: {0}. Work there, and the rest of the flower will reorganise itself around the change.",
        "The whole reading keeps returning to {0}, and that is where your hours should go this month, rather than into the parts of the matter you cannot actually reach.",
        "Read across the petals and one plain requirement stands out: {0}. Give it the best hour of your day rather than the hour that is left over.",
    ];

    public static readonly string[] AxisBothReversed =
    [
        "Both ends of this axis lie reversed, which is the rosette insisting. When a pair is turned together the whole line is jammed, and neither end can be freed by working on it alone.",
        "Both cards are turned, so the axis has stalled at both ends. Nothing will move here until the exchange between the two stations is named out loud and renegotiated.",
        "The pair is doubly reversed. That is rare, and it means the difficulty is not in either station but in the traffic between them, which has stopped running in both directions.",
    ];

    public static readonly string[] AxisSameSuit =
    [
        "Both cards belong to the same suit, so the axis speaks in one register rather than two. That narrows the reading and sharpens it: this line of the flower has a single kind of answer.",
        "One suit holds both ends, which is unusual on an axis built to oppose. Expect the two stations to agree more than they argue, and expect the agreement itself to be the message.",
        "The two ends share a suit. The opposition here is therefore internal — a matter arguing with itself in its own language rather than being met by a different kind of force.",
    ];

    public static readonly string[] AxisGoddessAgainstGate =
    [
        "A Great Goddess faces one of the Eight Gates along this line, setting a person against a threshold. Someone or something with a name is standing at a door that will take payment.",
        "Goddess and Gate hold the two ends, which is the classic tension of this deck: character meeting toll. What you are at one end determines what is asked of you at the other.",
        "Here a named power faces a bare threshold. The axis is asking whether the self at one end can survive the surrender at the other, and the honest answer is usually yes, altered.",
    ];

    public static readonly string[] AxisAdvice =
    [
        "Read the two together before either alone, and act on the pair rather than on whichever end is louder today.",
        "Whatever you decide about one of these stations, check it against the other before you move; this axis punishes decisions made at one end only.",
        "Write the two cards side by side and say in one sentence what is being exchanged. That sentence is the useful part of this axis.",
    ];

    public static readonly string[] GateOrderText =
    [
        "The order of surrender is set by the Gates themselves, and it runs from the outermost sign inward. Each door takes something less visible and more intimate than the one before it, so read them in their numbered sequence rather than in the order they happen to sit on the flower.",
        "Gates arriving together always describe a sequence. The deck numbers them for a reason: the crown goes before the voice, the voice before the heart, the heart before the last covering. Take them in that order and the season stops feeling arbitrary and starts feeling paid.",
        "When more than one Gate appears the reading becomes a stair rather than a room. Follow them by number, lowest first, and you will see the shape of what is being asked — the tolls in order, each one closer to the skin than the last.",
    ];
    #endregion

    #region Closing invocation

    public static readonly string[] InvocationCall =
    [
        "you who stand at the corners of this flower, keep the one who reads it;",
        "you who are named in this rosette, stand at the four doors of this house;",
        "you whose faces open in these petals, look kindly on the hands that laid them;",
    ];

    public static readonly string[] InvocationGrant =
    [
        "let what must go down go down gently, and let what is coming up be met at the door.",
        "let nothing be measured here that cannot be borne, and let nothing be borne alone.",
        "let the ornaments fall in their own order, and let none of them be torn away.",
    ];

    public static readonly string[] InvocationSeal =
    [
        "Let the star that sets rise again over this house.",
        "Let the morning find this name still written.",
        "Let the boat come in with its cargo whole.",
    ];

    #endregion
}
