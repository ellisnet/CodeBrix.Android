namespace InannaRosette.Reading.Models;

/// <summary>The interpretation of one station of the rosette.</summary>
public sealed record PositionInterpretation(
    PlacedCard Placed,
    string Heading,
    IReadOnlyList<string> Keywords,
    IReadOnlyList<string> Paragraphs);

/// <summary>A relationship the interpreter noticed between stations (opposite petals, the heart, suit balance...).</summary>
public sealed record Insight(string Title, string Text);

/// <summary>The whole reading, ready for the screen and for the PDF.</summary>
public sealed record ReadingInterpretation(
    RosetteReading Reading,
    string Title,
    string Opening,
    IReadOnlyList<PositionInterpretation> Positions,
    IReadOnlyList<Insight> Insights,
    string Counsel,
    string ClosingInvocation)
{
    /// <summary>
    /// The line every reading ends with, after the last of the interpretation and before the
    /// appendix. It is an init property rather than another positional parameter so that every
    /// existing construction site still reads as the seven things the interpreter composes;
    /// <see cref="Services.ReadingInterpreter.Interpret"/> sets it explicitly all the same.
    /// </summary>
    public string Closing { get; init; } = Services.ReadingInterpreter.ClosingBlessing;
}
