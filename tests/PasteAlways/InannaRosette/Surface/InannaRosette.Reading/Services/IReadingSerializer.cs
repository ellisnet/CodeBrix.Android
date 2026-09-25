using InannaRosette.Reading.Models;

namespace InannaRosette.Reading.Services;

/// <summary>Saves a reading to JSON and loads one back. Pure; no UI, no file system.</summary>
public interface IReadingSerializer
{
    /// <summary>Writes a reading as indented JSON.</summary>
    string ToJson(RosetteReading reading);

    /// <summary>Reads a reading back from JSON. Unknown card ids or station indexes are skipped.</summary>
    /// <exception cref="FormatException">The text is not a reading document.</exception>
    RosetteReading FromJson(string json);
}
