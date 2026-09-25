using System.Globalization;
using InannaRosette.Reading.Data;
using InannaRosette.Reading.Models;
using static InannaRosette.Reading.Services.InterpretationTemplates;

namespace InannaRosette.Reading.Services;

/// <summary>
/// Turns a <see cref="RosetteReading"/> into finished prose. Pure, deterministic and tolerant of
/// partial layouts: the seed comes from the cards, their stations and their orientation,
/// so the same rosette always produces the same text and two different rosettes do not.
/// </summary>
public sealed class ReadingInterpreter : IReadingInterpreter
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    /// <summary>
    /// The blessing every reading ends with, on the screen and in the report alike. One constant,
    /// because the line is set in three places — the interpretation, the panel and the PDF — and
    /// they must say it identically.
    /// </summary>
    public const string ClosingBlessing = "~ Blessed is the Queen of Heaven ~ Inanna Zami ~";

    /// <inheritdoc />
    public ReadingInterpretation Interpret(RosetteReading reading)
    {
        ArgumentNullException.ThrowIfNull(reading);

        var placed = reading.Placements
            .Where(p => p is not null)
            .GroupBy(p => p.Position.Index)
            .Select(g => g.First())
            .OrderBy(p => p.Position.Index)
            .ToList();

        var seed = SeedFor(placed);
        var title = BuildTitle(reading);

        if (placed.Count == 0)
        {
            return new ReadingInterpretation(
                reading, title, EmptyOpening, [], EmptyInsights(), EmptyCounsel, EmptyClosing)
            {
                Closing = ClosingBlessing,
            };
        }

        var stats = Stats.From(placed);
        var opening = BuildOpening(reading, placed, stats, seed);
        var positions = placed.Select(p => BuildPosition(p, reading, placed, seed)).ToList();
        var insights = BuildInsights(placed, stats, seed);
        var counsel = BuildCounsel(placed, stats, seed);
        var closing = BuildClosing(placed, seed);

        return new ReadingInterpretation(reading, title, opening, positions, insights, counsel, closing)
        {
            Closing = ClosingBlessing,
        };
    }

    #region Seed and statistics

    private static int SeedFor(IReadOnlyList<PlacedCard> placed)
    {
        unchecked
        {
            var h = 2166136261u;
            foreach (var p in placed)
            {
                h = (h ^ (uint)p.Position.Index) * 16777619u;
                h = (h ^ (uint)p.Card.Id) * 16777619u;
                h = (h ^ (uint)(p.IsReversed ? 7 : 3)) * 16777619u;
            }
            return (int)(h & 0x7FFFFFFF);
        }
    }

    private sealed record Stats(int Total, int Goddess, int Gate, int Emblem, int Reversed)
    {
        public static Stats From(IReadOnlyList<PlacedCard> placed) => new(
            placed.Count,
            placed.Count(p => p.Card.Suit == CardSuit.Goddess),
            placed.Count(p => p.Card.Suit == CardSuit.Gate),
            placed.Count(p => p.Card.Suit == CardSuit.Emblem),
            placed.Count(p => p.IsReversed));

        /// <summary>Goddess, Gate, Emblem or null when no suit leads clearly.</summary>
        public CardSuit? Dominant
        {
            get
            {
                var byCount = new[]
                {
                    (Suit: CardSuit.Goddess, Count: Goddess),
                    (Suit: CardSuit.Gate, Count: Gate),
                    (Suit: CardSuit.Emblem, Count: Emblem),
                }.OrderByDescending(x => x.Count).ToList();
                return byCount[0].Count >= byCount[1].Count + 2 ? byCount[0].Suit : null;
            }
        }

        public bool ManyReversed => Reversed >= 4 || (Reversed >= 2 && Reversed * 2 > Total);
    }

    #endregion

    #region Title

    private static string BuildTitle(RosetteReading reading)
    {
        var who = reading.Querent?.Trim() ?? "";
        var q = reading.Question?.Trim() ?? "";
        var stem = who.Length > 0 ? $"A Rosette for {who}" : "The Rosette Reading";
        return q.Length > 0 ? $"{stem} — {q.TrimEnd('.', ' ')}" : stem;
    }

    #endregion

    #region Opening

    private static string BuildOpening(RosetteReading reading, IReadOnlyList<PlacedCard> placed, Stats s, int seed)
    {
        var who = reading.Querent?.Trim() ?? "";
        var q = reading.Question?.Trim() ?? "";

        var forWhom = who.Length > 0 ? $" for {who}" : "";
        var questionClause = q.Length > 0 ? $" The question laid before it: {EnsureStop(q)}" : "";
        var cardWord = s.Total == 1 ? "card" : "cards";

        var frame = string.Format(Inv, Pick(OpeningFrame, seed, 11), forWhom, Words(s.Total), cardWord, questionClause);
        var counts = SuitCounts(s, seed, 12);

        var dominant = s.Dominant switch
        {
            CardSuit.Goddess => Pick(GoddessDominant, seed, 13),
            CardSuit.Gate => Pick(GateDominant, seed, 13),
            CardSuit.Emblem => Pick(EmblemDominant, seed, 13),
            _ => Pick(SuitsBalanced, seed, 13),
        };

        var reversedLine = Capitalise(s.Reversed switch
        {
            0 => Pick(ReversedNone, seed, 14),
            1 => Pick(ReversedOne, seed, 14),
            _ => string.Format(Inv, Pick(s.ManyReversed ? ReversedMany : ReversedFew, seed, 14), Words(s.Reversed)),
        });

        var heart = placed.FirstOrDefault(p => p.Position.Index == 0);
        var heartLine = heart is null
            ? Pick(HeartAbsent, seed, 15)
            : string.Format(Inv, Pick(HeartPresent, seed, 15),
                heart.Card.Name,
                heart.Card.Epithet,
                Orient(heart.IsReversed),
                // What the matter is *about* is the card's domain; orientation is stated separately.
                heart.Card.Domains[0],
                heart.Card.Domains.Count > 1 ? heart.Card.Domains[1] : heart.Card.Domains[0]);

        return string.Join(" ", frame, counts, dominant, reversedLine, heartLine, Pick(OpeningClose, seed, 16));
    }

    #endregion

    #region Positions

    private static PositionInterpretation BuildPosition(
        PlacedCard p, RosetteReading reading, IReadOnlyList<PlacedCard> placed, int seed)
    {
        var card = p.Card;
        var pos = p.Position;
        var keywords = KeywordsOf(p);
        var meaning = p.IsReversed ? card.ReversedMeaning : card.UprightMeaning;

        var numeral = pos.IsCenter ? "" : Card.ToRoman(pos.Index) + " · ";
        var heading = $"{numeral}{pos.Title} — {card.Name}, {card.Epithet} ({p.OrientationLabel})";

        var salt = 100 + pos.Index * 7;
        var tie = string.Format(Inv, Pick(PositionTie, seed, salt),
            pos.Title, EnsureStop(pos.Question), card.Name, card.Epithet, Orient(p.IsReversed), card.NumeralLabel);
        var close = string.Format(Inv, Pick(PositionClose, seed, salt + 1), pos.Title);

        var paragraphs = new List<string> { string.Join(" ", tie, meaning, close) };

        var opposite = pos.OppositeIndex is int oi ? placed.FirstOrDefault(x => x.Position.Index == oi) : null;
        if (opposite is not null)
        {
            paragraphs.Add(string.Format(Inv, Pick(AxisPair, seed, salt + 2),
                pos.Title,
                opposite.Position.Title,
                card.Name,
                opposite.Card.Name,
                Phrase(keywords, 2),
                Phrase(KeywordsOf(opposite), 2),
                // One domain per end: several domains contain "and", and two of them
                // either side of a conjunction turns the sentence into a chain.
                Phrase(card.Domains, 1),
                Phrase(opposite.Card.Domains, 1)));
        }

        paragraphs.Add(card.Invocation);

        return new PositionInterpretation(p, heading, keywords, paragraphs);
    }

    #endregion

    #region Insights

    private static IReadOnlyList<Insight> BuildInsights(IReadOnlyList<PlacedCard> placed, Stats s, int seed)
    {
        var insights = new List<Insight>();

        // 1. The weight of the suits.
        var counts = SuitCounts(s, seed, 21);
        var dominant = s.Dominant switch
        {
            CardSuit.Goddess => Pick(GoddessDominant, seed, 22),
            CardSuit.Gate => Pick(GateDominant, seed, 22),
            CardSuit.Emblem => Pick(EmblemDominant, seed, 22),
            _ => Pick(SuitsBalanced, seed, 22),
        };
        insights.Add(new Insight("The Weight of the Suits",
            string.Join(" ",
                $"{Capitalise(Words(s.Total))} {(s.Total == 1 ? "card lies" : "cards lie")} on the rosette.",
                counts, dominant, Pick(SuitInsightConsequence, seed, 23))));

        // 2. The turned cards.
        var reversedLine = Capitalise(s.Reversed switch
        {
            0 => Pick(ReversedNone, seed, 24),
            1 => Pick(ReversedOne, seed, 24),
            _ => string.Format(Inv, Pick(s.ManyReversed ? ReversedMany : ReversedFew, seed, 24), Words(s.Reversed)),
        });
        insights.Add(new Insight("The Turned Cards",
            string.Join(" ",
                Pick(ReversedInsightLead, seed, 25), reversedLine, Pick(ReversedInsightAdvice, seed, 26))));

        // 3. The still centre.
        var heart = placed.FirstOrDefault(p => p.Position.Index == 0);
        if (heart is not null)
        {
            var relation = s.Goddess switch
            {
                >= 5 => "The flower around it is thickly peopled with goddesses, so the matter is being worked on you by persons rather than by circumstances, and the persons have their own purposes.",
                >= 2 => "A handful of goddesses stand around it, enough to give the matter faces and intentions without turning the whole reading into a question of character.",
                1 => "Only one other goddess keeps the centre company, so the matter is less about who is involved than about what must be passed through and what must be used.",
                _ => "No goddess stands on the petals at all, which leaves the centre unattended: this is a matter of thresholds and instruments rather than of anyone's temperament.",
            };
            insights.Add(new Insight("The Still Centre",
                string.Join(" ",
                    $"{heart.Card.Name} holds the Heart {Orient(heart.IsReversed)}, and {Words(s.Goddess)} of the {Words(s.Total)} cards on the flower {(s.Goddess == 1 ? "belongs" : "belong")} to the Great Goddesses.",
                    relation, Pick(HeartInsightClose, seed, 29))));
        }

        // 4. Axis insights, most striking first.
        foreach (var axis in StrikingAxes(placed, seed).Take(2)) insights.Add(axis);

        // 5. The order of surrender.
        var gates = placed.Where(p => p.Card.Suit == CardSuit.Gate).OrderBy(p => p.Card.Number).ToList();
        if (gates.Count >= 2 && insights.Count < 6)
        {
            var names = Phrase(gates.Select(g => g.Card.Name).ToList(), gates.Count);
            insights.Add(new Insight("The Order of Surrender",
                string.Join(" ",
                    $"{Capitalise(Words(gates.Count))} of the Eight Gates stand in this reading: {names}.",
                    Pick(GateOrderText, seed, 27))));
        }

        // 6. An unfinished rosette.
        if (s.Total < 9 && insights.Count < 6)
        {
            insights.Add(new Insight("The Rosette Unfinished",
                string.Format(Inv, Pick(PartialInsightText, seed, 28), Words(s.Total))));
        }

        return insights.Take(6).ToList();
    }

    private static IEnumerable<Insight> StrikingAxes(IReadOnlyList<PlacedCard> placed, int seed)
    {
        var found = new List<(int Rank, SpreadPosition A, SpreadPosition B, PlacedCard Pa, PlacedCard Pb)>();

        foreach (var (a, b) in RosetteSpread.Axes)
        {
            var pa = placed.FirstOrDefault(p => p.Position.Index == a.Index);
            var pb = placed.FirstOrDefault(p => p.Position.Index == b.Index);
            if (pa is null || pb is null) continue;

            int rank;
            if (pa.IsReversed && pb.IsReversed) rank = 0;
            else if (pa.Card.Suit == pb.Card.Suit) rank = 1;
            else if ((pa.Card.Suit == CardSuit.Goddess && pb.Card.Suit == CardSuit.Gate)
                  || (pa.Card.Suit == CardSuit.Gate && pb.Card.Suit == CardSuit.Goddess)) rank = 2;
            else continue;

            found.Add((rank, a, b, pa, pb));
        }

        // Two axes of the same kind must not be described in the same words, so each
        // successive one of a kind steps on to the next variant in its array.
        var usedOfKind = new Dictionary<int, int>();
        foreach (var (rank, a, b, pa, pb) in found.OrderBy(f => f.Rank))
        {
            usedOfKind.TryGetValue(rank, out var nth);
            usedOfKind[rank] = nth + 1;

            // The base index depends on the kind only, so stepping by nth guarantees that
            // two axes of the same kind in one reading are never described identically.
            var salt = 40 + rank;
            var body = rank switch
            {
                0 => Step(AxisBothReversed, seed, salt, nth),
                1 => Step(AxisSameSuit, seed, salt, nth),
                _ => Step(AxisGoddessAgainstGate, seed, salt, nth),
            };

            var text = string.Join(" ",
                $"{a.Title} and {b.Title} face one another across the flower, holding {pa.Card.Name} and {pb.Card.Name}.",
                body, Step(AxisAdvice, seed, 44 + rank, nth));
            yield return new Insight($"Axis — {a.Title} and {b.Title}", text);
        }
    }

    /// <summary>Picks deterministically, stepping <paramref name="nth"/> places on so that
    /// repeated uses of one array within a single reading do not collide.</summary>
    private static string Step(IReadOnlyList<string> variants, int seed, int salt, int nth) =>
        variants[(Mix(seed, salt) + nth) % variants.Count];

    #endregion

    #region Counsel

    private static string BuildCounsel(IReadOnlyList<PlacedCard> placed, Stats s, int seed)
    {
        var gift = placed.FirstOrDefault(p => p.Position.Index == 8);
        var heart = placed.FirstOrDefault(p => p.Position.Index == 0);
        var exalted = placed.Where(p => p.Card.Suit == CardSuit.Goddess)
                            .OrderBy(p => p.Card.Number).FirstOrDefault();
        var source = gift ?? heart ?? exalted ?? placed[0];

        string lead;
        if (gift is not null)
            lead = string.Format(Inv, Pick(CounselFromGift, seed, 31), source.Card.Name, source.Card.Epithet, Orient(source.IsReversed));
        else if (heart is not null)
            lead = string.Format(Inv, Pick(CounselFromHeart, seed, 31), source.Card.Name, source.Card.Epithet, Orient(source.IsReversed));
        else if (exalted is not null)
            lead = string.Format(Inv, Pick(CounselFromExalted, seed, 31), source.Card.Name, source.Card.Epithet, Orient(source.IsReversed));
        else
            lead = string.Format(Inv, Pick(CounselFromAny, seed, 31), source.Card.Name, source.Card.Epithet, Orient(source.IsReversed));

        // The instruction sentences want the card's capacities, not its failure modes,
        // so the keywords here are always the upright set even when the card is reversed.
        var keywords = source.Card.UprightKeywords;
        var domains = source.Card.Domains;
        var d0 = domains.Count > 0 ? domains[0] : "the matter in hand";
        var d1 = domains.Count > 1 ? domains[1] : d0;
        var k0 = keywords.Count > 0 ? keywords[0] : d0;
        var k1 = keywords.Count > 1 ? keywords[1] : k0;

        var bridge = string.Format(Inv, Pick(CounselBridge, seed, 32), d0);
        var action = string.Format(Inv, Pick(CounselAction, seed, 33), d0, k0);
        var second = string.Format(Inv, Pick(CounselSecond, seed, 34), d1, k1);
        var close = Pick(CounselClose, seed, 35);

        return string.Join(" ", lead, bridge, action, second, close);
    }

    #endregion

    #region Closing invocation

    private static string BuildClosing(IReadOnlyList<PlacedCard> placed, int seed)
    {
        var goddesses = placed
            .Where(p => p.Card.Suit == CardSuit.Goddess)
            .Select(p => p.Card)
            .DistinctBy(c => c.Id)
            .OrderBy(c => c.Number)
            .ToList();

        var lines = new List<string>();

        if (goddesses.Count >= 3)
            lines.Add($"{goddesses[0].Epithet}, {goddesses[1].Epithet}, {goddesses[2].Epithet} —");
        else if (goddesses.Count == 2)
            lines.Add($"{goddesses[0].Epithet}, {goddesses[1].Epithet} —");
        else if (goddesses.Count == 1)
            lines.Add($"{goddesses[0].Epithet} —");
        else
            lines.Add($"By the sign of {placed[0].Card.Name} and the star of the Queen of Heaven —");

        lines.Add(Pick(InvocationCall, seed, 51));
        lines.Add(Pick(InvocationGrant, seed, 52));
        if (goddesses.Count >= 2) lines.Add(Pick(InvocationSeal, seed, 53));

        return string.Join("\n", lines);
    }

    #endregion

    #region Empty reading

    private const string EmptyOpening =
        "No cards have been laid, so the rosette is still only a shape: a still centre and eight empty petals waiting in the order the goddess walked, from heaven down through the descent and up again to the gift. There is nothing here to read yet, and nothing to be inferred from the silence. Shuffle, ask your question plainly, and set a card at the Heart before anything else; the centre decides what the petals are about. When the flower is even partly open the reading will speak. Until then, take the empty spread as an invitation rather than an omen, and begin whenever you are ready to hear a plain answer.";

    private const string EmptyCounsel =
        "Begin with the Heart. Draw one card and set it at the centre, and let it tell you what the matter actually is before you decide what you want to ask about it. Then lay the petals in order — Heaven first, then the Morning Star, and on around the flower — and resist reading each one as it lands. A rosette is built to be read whole, with every petal corrected by the one facing it, so the sense of it arrives at the end and not before. If you have only a few minutes, lay three: the Heart, and one opposite pair. That is a complete reading in miniature, and it will tell you more than nine cards read in a hurry.";

    private const string EmptyClosing =
        "Queen of Heaven and Earth, whose star stands at both edges of the day —\nkeep this empty flower until there is a hand to fill it,\nand let the first card fall true.";

    private static IReadOnlyList<Insight> EmptyInsights() =>
    [
        new Insight("The Unlaid Rosette",
            "Nothing has been placed. The spread holds nine stations — the Heart at the centre and eight petals at forty-five degree intervals — and until cards occupy them there is no pattern to weigh, no balance of suits, and no axis to read across. An empty rosette is not an unfavourable one. It is simply a question that has not yet been asked in a form the cards can answer."),
        new Insight("Where to Begin",
            "The Heart is the station to fill first, because everything else in the rosette is read as a face of the centre. Without it the petals still speak, but they circle a subject none of them will name. Draw one card, set it at the middle of the flower, and let it establish what this reading is actually about before the eight petals begin to argue about it."),
        new Insight("The Shape of the Spread",
            "The petals run clockwise from the top: Heaven, the Morning Star, the Storehouse, the Descent, the Great Below, the Return, the Evening Star and the Gift. Each faces its opposite across the flower — aspiration against root, rising against returning, gathering against letting go, surrender against gift — and those four axes carry most of the meaning once the cards are down."),
    ];

    #endregion

    #region Helpers

    private static IReadOnlyList<string> KeywordsOf(PlacedCard p) =>
        p.IsReversed ? p.Card.ReversedKeywords : p.Card.UprightKeywords;

    private static readonly string[] NumberWords =
        ["none", "one", "two", "three", "four", "five", "six", "seven", "eight", "nine"];

    private static string Words(int n) =>
        n >= 0 && n < NumberWords.Length ? NumberWords[n] : n.ToString(Inv);

    /// <summary>"six of the Great Goddesses, one of the Eight Gates and two of the Sacred Emblems",
    /// built as noun phrases so the sentence stays grammatical at every count.</summary>
    private static string SuitCounts(Stats s, int seed, int salt) => string.Format(Inv,
        Pick(SuitCount, seed, salt),
        $"{Words(s.Goddess)} of the Great Goddesses",
        $"{Words(s.Gate)} of the Eight Gates",
        $"{Words(s.Emblem)} of the Sacred Emblems");

    private static string Capitalise(string s) =>
        s.Length == 0 ? s : char.ToUpperInvariant(s[0]) + s[1..];

    #endregion
}
