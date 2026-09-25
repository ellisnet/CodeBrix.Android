namespace RedisSetupTool.TerminalView;

/// <summary>
/// The few lines the application writes into a terminal itself, rather than forwarding from a
/// shell. The escape sequences live here, beside the pump that feeds them, so that no page has
/// to know how a terminal is told to color a line.
/// </summary>
public static class TerminalText
{
    private const string Red = "\x1b[31m";
    private const string Reset = "\x1b[0m";
    private const string NewLine = "\r\n";

    /// <summary>Wraps a message as a red line of its own, ready to feed into a terminal.</summary>
    /// <param name="message">The message to show.</param>
    /// <returns>
    /// The message with the line breaks and escape sequences a terminal needs, or an empty
    /// string when there is no message to show.
    /// </returns>
    public static string Error(string message) =>
        string.IsNullOrEmpty(message)
            ? string.Empty
            : NewLine + Red + message + Reset + NewLine;
}
