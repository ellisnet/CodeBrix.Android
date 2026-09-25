namespace InannaRosette.Reading.Models;

/// <summary>
/// One of the nine stations of the Rosette spread: the heart (index 0) and eight petals
/// (index 1..8, starting at the top and proceeding clockwise, 45 degrees apart).
/// </summary>
public sealed record SpreadPosition(
    int Index,
    string Title,
    string Subtitle,
    string Question,
    double AngleDegrees,
    string Description)
{
    /// <summary>True for the Heart at the middle of the flower, false for the eight petals.</summary>
    public bool IsCenter => Index == 0;

    /// <summary>The petal directly opposite this one on the rosette (petals only), or null for the center.</summary>
    public int? OppositeIndex => Index == 0 ? null : ((Index - 1 + 4) % 8) + 1;
}

/// <summary>A card laid at a station of the spread.</summary>
public sealed record PlacedCard(SpreadPosition Position, Card Card, bool IsReversed)
{
    /// <summary>"Upright" or "Reversed", for headings and legends.</summary>
    public string OrientationLabel => IsReversed ? "Reversed" : "Upright";
}

/// <summary>A completed (or partial) reading: the cards on the rosette and who asked.</summary>
public sealed class RosetteReading
{
    /// <summary>When the reading was laid.</summary>
    public DateTime Created { get; init; } = DateTime.Now;

    /// <summary>Who the reading is for; may be empty.</summary>
    public string Querent { get; set; } = "";

    /// <summary>The question put to the rosette; may be empty.</summary>
    public string Question { get; set; } = "";

    /// <summary>The cards on the rosette, in the order they were laid.</summary>
    public List<PlacedCard> Placements { get; } = new();

    /// <summary>The card at a station, or null when the station is empty.</summary>
    public PlacedCard? At(int positionIndex) => Placements.FirstOrDefault(p => p.Position.Index == positionIndex);

    /// <summary>True once all nine stations hold a card.</summary>
    public bool IsComplete => Placements.Count == 9;
}
