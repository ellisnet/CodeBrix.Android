namespace InannaRosette.Reading.Models;

/// <summary>The three suits of the Rosette of Inanna deck.</summary>
public enum CardSuit
{
    /// <summary>The Great Goddesses of Sumer, Akkad, Babylon and Assyria (24 cards).</summary>
    Goddess,
    /// <summary>The Eight Gates: the seven thresholds of Inanna's Descent and the Return (8 cards).</summary>
    Gate,
    /// <summary>The Sacred Emblems: symbols and attributes of the Divine Feminine in Mesopotamia (8 cards).</summary>
    Emblem,
}

/// <summary>
/// The pictorial motif the UI and the PDF draw for a card. Every card names exactly one.
/// The renderer draws these procedurally (vector art); there are no image assets.
/// </summary>
public enum Emblem
{
    /// <summary>The eight-pointed star of Inanna, her own sign in the sky.</summary>
    EightPointedStar,
    /// <summary>The eight-petalled rosette, the shape of the spread itself.</summary>
    Rosette,
    /// <summary>The looped reed bundle that stands at the gate of the storehouse.</summary>
    ReedBundle,
    /// <summary>The lion, the goddess's beast of war.</summary>
    Lion,
    /// <summary>The date palm, the orchard's provision.</summary>
    DatePalm,
    /// <summary>The dove, messenger and offering.</summary>
    Dove,
    /// <summary>The storehouse, where the year's increase is kept.</summary>
    Storehouse,
    /// <summary>The river boat, carrying goods and gods between cities.</summary>
    Boat,
    /// <summary>The crescent moon, Nanna's sign and Inanna's kin.</summary>
    Crescent,
    /// <summary>The sun disc, Utu's justice at noon.</summary>
    Sun,
    /// <summary>The serpent, renewal by shedding.</summary>
    Serpent,
    /// <summary>The scorpion, guardian of the mountain pass.</summary>
    Scorpion,
    /// <summary>The owl of the night, watching what is hidden.</summary>
    Owl,
    /// <summary>The sheaf of barley, the harvest counted.</summary>
    GrainSheaf,
    /// <summary>The beer jar, hospitality and the drinking of oaths.</summary>
    BeerJar,
    /// <summary>The clay tablet, the written decree.</summary>
    Tablet,
    /// <summary>The flame on the altar, offering and ordeal.</summary>
    Flame,
    /// <summary>The bull of heaven, strength that must be answered.</summary>
    Bull,
    /// <summary>The mountain, the far country and the hard threshold.</summary>
    Mountain,
    /// <summary>The water of the two rivers, flowing and returning.</summary>
    Wave,
    /// <summary>The crown of the steppe, first of the seven ornaments.</summary>
    Crown,
    /// <summary>The gate with its bar, a threshold to be passed.</summary>
    Gate,
    /// <summary>The measuring ring, the standard against which things are judged.</summary>
    Ring,
    /// <summary>The measuring rod, the instrument of the same judgement.</summary>
    MeasuringRod,
    /// <summary>The robe of office, the outermost thing surrendered.</summary>
    Robe,
    /// <summary>The lapis necklace worn at the throat.</summary>
    Necklace,
    /// <summary>The breastplate called 'Come, man, come'.</summary>
    Breastplate,
    /// <summary>The double strand of lapis beads.</summary>
    Beads,
    /// <summary>The huluppu tree, planted and contested.</summary>
    Tree,
    /// <summary>The fish of the marsh, abundance out of sight.</summary>
    Fish,
    /// <summary>The anzu bird, nesting in the branches.</summary>
    Bird,
    /// <summary>The spindle, the work of the hands and of the household.</summary>
    Spindle,
    /// <summary>The libation vessel, poured out for the dead.</summary>
    Vessel,
    /// <summary>The throne of the temple, authority seated.</summary>
    Throne,
    /// <summary>The lyre, lament and praise together.</summary>
    Lyre,
    /// <summary>The votive eye, the worshipper's fixed attention.</summary>
    Eye,
    /// <summary>The cow of the byre, patient increase.</summary>
    Cow,
    /// <summary>The gazelle of the steppe, swift and easily startled.</summary>
    Gazelle,
    /// <summary>The clay lamp, small light carried into the dark.</summary>
    Lamp,
    /// <summary>The knot of the reed goddess, a binding that holds.</summary>
    Knot,
    /// <summary>The eight-petalled rosette of the altar, Inanna's own table.</summary>
    AltarRosette,
    /// <summary>The bronze hand-mirror of the toilette, charm looked at directly.</summary>
    Mirror,
    /// <summary>The loaves heaped on the offering table, grain made into the day's bread.</summary>
    Loaves,
    /// <summary>The turtle of the Abzu, shaped from the clay of the deep; a shelter carried.</summary>
    Turtle,
}

/// <summary>One card of the deck. Immutable content; orientation lives on <see cref="PlacedCard"/>.</summary>
public sealed record Card(
    int Id,
    CardSuit Suit,
    int Number,
    string Name,
    string Epithet,
    string Transliteration,
    Emblem Emblem,
    string AccentColor,
    string SecondaryColor,
    IReadOnlyList<string> Domains,
    string Lore,
    IReadOnlyList<string> UprightKeywords,
    string UprightMeaning,
    IReadOnlyList<string> ReversedKeywords,
    string ReversedMeaning,
    string Invocation)
{
    /// <summary>The full name of the card's suit, as it is printed on the card.</summary>
    public string SuitName => Suit switch
    {
        CardSuit.Goddess => "The Great Goddesses",
        CardSuit.Gate => "The Eight Gates",
        CardSuit.Emblem => "The Sacred Emblems",
        _ => Suit.ToString(),
    };

    /// <summary>The card's number as it is printed: a Roman numeral, prefixed by suit where the suit needs saying.</summary>
    public string NumeralLabel => Suit switch
    {
        CardSuit.Goddess => ToRoman(Number),
        CardSuit.Gate => $"Gate {ToRoman(Number)}",
        CardSuit.Emblem => $"Emblem {ToRoman(Number)}",
        _ => Number.ToString(),
    };

    /// <summary>Writes a number 1-3999 as a Roman numeral; anything else is returned as digits.</summary>
    public static string ToRoman(int n)
    {
        if (n <= 0 || n > 3999) return n.ToString();
        var map = new (int v, string s)[] { (1000,"M"),(900,"CM"),(500,"D"),(400,"CD"),(100,"C"),(90,"XC"),(50,"L"),(40,"XL"),(10,"X"),(9,"IX"),(5,"V"),(4,"IV"),(1,"I") };
        var sb = new System.Text.StringBuilder();
        foreach (var (v, s) in map) while (n >= v) { sb.Append(s); n -= v; }
        return sb.ToString();
    }
}
