namespace GitHubIssueFinder.GitHub;

//One entry of an item's labels array. The colour arrives as six hexadecimal digits with no
//leading hash, and is carried through unchanged so the caller can decide what to do with a
//value it cannot read.
internal sealed class LabelDto
{
    public string Name { get; set; }

    public string Color { get; set; }

    public string Description { get; set; }
}
