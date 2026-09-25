namespace GitHubIssueFinder.Theming;

/// <summary>
/// The jobs a colour does in this application. A scheme is nothing more than one colour for each
/// of these roles, which is what lets the whole application be repainted by walking a table.
/// </summary>
public enum ColorRole
{
    /// <summary>The page ground and the face of a result row.</summary>
    Canvas,

    /// <summary>The header bar, box header strips, repository group rows and row hover.</summary>
    CanvasSubtle,

    /// <summary>Text-box wells and count pills.</summary>
    CanvasInset,

    /// <summary>Box borders and the line under a header.</summary>
    Hairline,

    /// <summary>The line between two result rows.</summary>
    HairlineMuted,

    /// <summary>Titles and values.</summary>
    TextPrimary,

    /// <summary>Body copy and the meta line under a title.</summary>
    TextSecondary,

    /// <summary>Captions and placeholders.</summary>
    TextTertiary,

    /// <summary>Links, focus and the application mark.</summary>
    Accent,

    /// <summary>The face of an accent-filled control and the progress bar.</summary>
    AccentEmphasis,

    /// <summary>A selected or informational tint.</summary>
    AccentSubtle,

    /// <summary>The open state, as a glyph or as text.</summary>
    Success,

    /// <summary>The face of the primary button.</summary>
    SuccessEmphasis,

    /// <summary>The primary button with the pointer over it.</summary>
    SuccessEmphasisHover,

    /// <summary>Quota-wait text and its glyph.</summary>
    Attention,

    /// <summary>The background of the quota-wait pill.</summary>
    AttentionSubtle,

    /// <summary>Errors, and a pull request closed without being merged.</summary>
    Danger,

    /// <summary>The background of an error pill.</summary>
    DangerSubtle,

    /// <summary>A closed issue and a merged pull request.</summary>
    Done,

    /// <summary>A draft pull request and an issue closed as not planned.</summary>
    Neutral,

    /// <summary>The face of a secondary button.</summary>
    ButtonFace,

    /// <summary>A secondary button with the pointer over it.</summary>
    ButtonFaceHover,

    /// <summary>A secondary button while it is pressed.</summary>
    ButtonFacePressed,

    /// <summary>Text drawn on an emphasis face.</summary>
    OnEmphasis,
}
