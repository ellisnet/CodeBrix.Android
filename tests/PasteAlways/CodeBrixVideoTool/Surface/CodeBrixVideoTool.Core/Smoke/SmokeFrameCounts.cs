namespace CodeBrixVideoTool.Smoke;

/// <summary>What the player element's frame counters said at one moment.</summary>
/// <param name="Posted">How many frames the player element was handed to show.</param>
/// <param name="Presented">How many of them it drew.</param>
/// <param name="Dropped">How many of them it gave up on.</param>
public sealed record SmokeFrameCounts(long Posted, long Presented, long Dropped);
