using InannaRosette.Reading.Models;

namespace InannaRosette.Reading.Services;

/// <summary>Produces the reading text. Pure and deterministic; no UI, no I/O.</summary>
public interface IReadingInterpreter
{
    /// <summary>Writes the finished prose for a reading, complete or partial.</summary>
    ReadingInterpretation Interpret(RosetteReading reading);
}
