using InannaRosette.Reading.Models;

namespace InannaRosette.Reading.Data;

/// <summary>
/// The nine stations of the Rosette: the Heart at the centre and eight petals set at
/// forty-five degree intervals, numbered clockwise from the top. The petals follow the
/// path of Inanna as Venus — rising, bearing fruit, descending, and returning — so that
/// opposite petals (1-5, 2-6, 3-7, 4-8) always face each other across the flower.
/// </summary>
public static class RosetteSpread
{
    private static readonly IReadOnlyList<SpreadPosition> _positions =
    [
        new SpreadPosition(
            Index: 0,
            Title: "The Heart",
            Subtitle: "the matter itself",
            Question: "What is this actually about?",
            AngleDegrees: 0,
            Description: """The still point the eight petals turn around. The Heart is not your wish and not your fear; it is the subject, the thing that would remain if every opinion about it were removed. Read it as the essence of the querent where no question has been asked, and as the true shape of the question where one has. Everything else in the rosette is a face this centre turns to the light."""),

        new SpreadPosition(
            Index: 1,
            Title: "Heaven",
            Subtitle: "what calls you upward",
            Question: "What is calling you upward, and do you dare to name it?",
            AngleDegrees: 0,
            Description: """Directly above the Heart stands An's country, the highest reach of the matter. This petal holds the aspiration — not the plan, but the thing the plan is secretly for. It asks what you would attempt if you believed you were permitted. Because it faces the Great Below across the flower, whatever is named here is bought at the price the fifth petal states, and the two should always be read together."""),

        new SpreadPosition(
            Index: 2,
            Title: "The Morning Star",
            Subtitle: "what is rising",
            Question: "What is beginning in you now, while it is still small?",
            AngleDegrees: 45,
            Description: """Inanna as Venus before dawn, the light that arrives ahead of the sun. This petal shows what is emerging — a talent, an attraction, a suspicion, a change of direction still too early to defend in public. It is easy to miss and easy to dismiss as mood. Read it for the first signs rather than the finished thing, and note that it faces the sixth petal, where a return completes what begins here."""),

        new SpreadPosition(
            Index: 3,
            Title: "The Storehouse",
            Subtitle: "what bears fruit now",
            Question: "What is actually feeding you, and what have you already gathered?",
            AngleDegrees: 90,
            Description: """The full ganun at the height of the day: resources, skills, allies, money, health, the work already done and now yielding. This petal is deliberately unromantic. It counts what is on the shelf so the reading cannot be built on wishes. Opposite the Evening Star, it forms the axis of gathering and letting settle, and each one keeps the other honest about what is truly sustaining you."""),

        new SpreadPosition(
            Index: 4,
            Title: "The Descent",
            Subtitle: "what must be surrendered",
            Question: "What are you being asked to take off at the gate?",
            AngleDegrees: 135,
            Description: """The turn downward, where the ornaments come away one at a time. This petal names the specific thing to be relinquished — a title, a story, a claim, a protection that has done its work. It rarely asks for the thing you were already willing to lose. Facing The Gift across the rosette, it sets the price against the counsel, so that what is given can be seen as answering what is given up."""),

        new SpreadPosition(
            Index: 5,
            Title: "The Great Below",
            Subtitle: "the hidden root",
            Question: "What is beneath this that you have not looked at?",
            AngleDegrees: 180,
            Description: """Ereshkigal's country, directly beneath the Heart and directly opposite Heaven. Here lies the root of the matter: the old grief, the buried motive, the fact everybody avoids, the part of you that has been kept in the dark because it is inconvenient. It is not an enemy. It is the holder of what you refuse, and it will keep the aspiration above it hollow until it has been faced and named aloud."""),

        new SpreadPosition(
            Index: 6,
            Title: "The Return",
            Subtitle: "what comes back changed",
            Question: "What is coming back to you, and how has it been altered?",
            AngleDegrees: 225,
            Description: """The ascent from the gates, carrying what the descent made. This petal shows the transformed thing: a capacity earned in a bad year, a person returning on new terms, a conviction that went down whole and comes up simpler. It faces the Morning Star, so read the pair as one cycle — what rises there arrives here with its cost paid and its shape finally decided."""),

        new SpreadPosition(
            Index: 7,
            Title: "The Evening Star",
            Subtitle: "what recedes",
            Question: "What is ending, and what would it cost you to let it settle?",
            AngleDegrees: 270,
            Description: """Venus after sunset, the same light going down. This petal holds what is receding and ought to be allowed to recede: an ambition whose season has passed, an argument that no longer needs winning, a pace that cannot be kept. Opposite the Storehouse, it is the counterweight to gathering, and it asks for rest, sleep, closure and the deliberate act of putting something down for good."""),

        new SpreadPosition(
            Index: 8,
            Title: "The Gift",
            Subtitle: "what the Goddess gives",
            Question: "What is being offered to you, and what will you do with it?",
            AngleDegrees: 315,
            Description: """The last petal before the circle closes: counsel, blessing, the practical grace on offer. Where every other station describes the situation, this one hands you something to use — an instruction, a resource, an unexpected ally, a permission. It faces The Descent, so the gift answers the surrender directly, and the reading is finished by holding the two side by side and seeing the exchange whole."""),
    ];

    /// <summary>All nine stations: the Heart first, then the petals clockwise from the top.</summary>
    public static IReadOnlyList<SpreadPosition> Positions => _positions;

    /// <summary>The centre of the rosette (index 0).</summary>
    public static SpreadPosition Center => _positions[0];

    /// <summary>The eight petals, in clockwise order starting at the top.</summary>
    public static IReadOnlyList<SpreadPosition> Petals => _positions.Skip(1).ToList();

    /// <summary>The station at the given index (0..8), or null when out of range.</summary>
    public static SpreadPosition? At(int index) =>
        index >= 0 && index < _positions.Count ? _positions[index] : null;

    /// <summary>The petal facing the given petal across the rosette, or null for the centre.</summary>
    public static SpreadPosition? Opposite(SpreadPosition position) =>
        position.OppositeIndex is int i ? At(i) : null;

    /// <summary>The four opposite petal pairs of the rosette: (1,5), (2,6), (3,7), (4,8).</summary>
    public static IReadOnlyList<(SpreadPosition A, SpreadPosition B)> Axes { get; } =
    [
        (_positions[1], _positions[5]),
        (_positions[2], _positions[6]),
        (_positions[3], _positions[7]),
        (_positions[4], _positions[8]),
    ];
}
