using System;

namespace GitHubIssueFinder.Theming;

/// <summary>
/// One complete colour scheme: a colour for every <see cref="ColorRole"/>, written as an opaque
/// ARGB value, plus whether the scheme sits on a dark ground. It carries no drawing type, so the
/// table of schemes is plain data that a test can read.
/// </summary>
public sealed record ColorSchemePalette
{
    /// <summary>True when this scheme sits on a dark ground.</summary>
    public bool BaseIsDark { get; init; }

    /// <summary>The page ground and the face of a result row.</summary>
    public uint Canvas { get; init; }

    /// <summary>The header bar, box header strips, repository group rows and row hover.</summary>
    public uint CanvasSubtle { get; init; }

    /// <summary>Text-box wells and count pills.</summary>
    public uint CanvasInset { get; init; }

    /// <summary>Box borders and the line under a header.</summary>
    public uint Hairline { get; init; }

    /// <summary>The line between two result rows.</summary>
    public uint HairlineMuted { get; init; }

    /// <summary>Titles and values.</summary>
    public uint TextPrimary { get; init; }

    /// <summary>Body copy and the meta line under a title.</summary>
    public uint TextSecondary { get; init; }

    /// <summary>Captions and placeholders.</summary>
    public uint TextTertiary { get; init; }

    /// <summary>Links, focus and the application mark.</summary>
    public uint Accent { get; init; }

    /// <summary>The face of an accent-filled control and the progress bar.</summary>
    public uint AccentEmphasis { get; init; }

    /// <summary>A selected or informational tint.</summary>
    public uint AccentSubtle { get; init; }

    /// <summary>The open state, as a glyph or as text.</summary>
    public uint Success { get; init; }

    /// <summary>The face of the primary button.</summary>
    public uint SuccessEmphasis { get; init; }

    /// <summary>The primary button with the pointer over it.</summary>
    public uint SuccessEmphasisHover { get; init; }

    /// <summary>Quota-wait text and its glyph.</summary>
    public uint Attention { get; init; }

    /// <summary>The background of the quota-wait pill.</summary>
    public uint AttentionSubtle { get; init; }

    /// <summary>Errors, and a pull request closed without being merged.</summary>
    public uint Danger { get; init; }

    /// <summary>The background of an error pill.</summary>
    public uint DangerSubtle { get; init; }

    /// <summary>A closed issue and a merged pull request.</summary>
    public uint Done { get; init; }

    /// <summary>A draft pull request and an issue closed as not planned.</summary>
    public uint Neutral { get; init; }

    /// <summary>The face of a secondary button.</summary>
    public uint ButtonFace { get; init; }

    /// <summary>A secondary button with the pointer over it.</summary>
    public uint ButtonFaceHover { get; init; }

    /// <summary>A secondary button while it is pressed.</summary>
    public uint ButtonFacePressed { get; init; }

    /// <summary>Text drawn on an emphasis face.</summary>
    public uint OnEmphasis { get; init; }

    /// <summary>
    /// Reads one role out of this scheme.
    /// </summary>
    /// <param name="role">The role wanted.</param>
    /// <returns>The opaque ARGB colour for that role.</returns>
    /// <exception cref="ArgumentOutOfRangeException">role is not a known role.</exception>
    public uint this[ColorRole role] => role switch
    {
        ColorRole.Canvas => Canvas,
        ColorRole.CanvasSubtle => CanvasSubtle,
        ColorRole.CanvasInset => CanvasInset,
        ColorRole.Hairline => Hairline,
        ColorRole.HairlineMuted => HairlineMuted,
        ColorRole.TextPrimary => TextPrimary,
        ColorRole.TextSecondary => TextSecondary,
        ColorRole.TextTertiary => TextTertiary,
        ColorRole.Accent => Accent,
        ColorRole.AccentEmphasis => AccentEmphasis,
        ColorRole.AccentSubtle => AccentSubtle,
        ColorRole.Success => Success,
        ColorRole.SuccessEmphasis => SuccessEmphasis,
        ColorRole.SuccessEmphasisHover => SuccessEmphasisHover,
        ColorRole.Attention => Attention,
        ColorRole.AttentionSubtle => AttentionSubtle,
        ColorRole.Danger => Danger,
        ColorRole.DangerSubtle => DangerSubtle,
        ColorRole.Done => Done,
        ColorRole.Neutral => Neutral,
        ColorRole.ButtonFace => ButtonFace,
        ColorRole.ButtonFaceHover => ButtonFaceHover,
        ColorRole.ButtonFacePressed => ButtonFacePressed,
        ColorRole.OnEmphasis => OnEmphasis,
        _ => throw new ArgumentOutOfRangeException(nameof(role), role, "Unknown colour role."),
    };
}
