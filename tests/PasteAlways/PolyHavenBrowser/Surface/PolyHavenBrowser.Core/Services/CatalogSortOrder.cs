namespace PolyHavenBrowser.Services;

/// <summary>The orderings the Browsing View's sort selector offers for the model catalog.</summary>
public enum CatalogSortOrder
{
    /// <summary>Most-downloaded models first (the default).</summary>
    MostPopular,

    /// <summary>Most recently published models first.</summary>
    Newest,

    /// <summary>Alphabetical by model name.</summary>
    NameAscending,
}
