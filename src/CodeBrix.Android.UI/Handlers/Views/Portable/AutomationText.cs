namespace CodeBrix.Android.UI.Handlers;

/// <summary>
/// AP9-3: the platform-free rules of the AutomationProperties mapping (the element's automation name and id
/// become the native view's content description and tag; see ViewHandler and ViewMappers): kept here so they
/// are unit-tested on the host.
/// </summary>
internal static class AutomationText
{
    /// <summary>
    /// The content description an automation name gives the native view: the name without leading and trailing
    /// white space, or null for no name (null, empty or white space) - then the view keeps no description of
    /// ours and TalkBack falls back to the view's own text.
    /// </summary>
    /// <param name="automationName">The value of AutomationProperties.Name.</param>
    /// <returns>The content description, or null.</returns>
    internal static string ContentDescriptionOf(string automationName) =>
        string.IsNullOrWhiteSpace(automationName) ? null : automationName.Trim();

    /// <summary>
    /// The content description of a handler that labels its widget itself (a rating bar, a person picture):
    /// the automation name when the element has one (the app's name wins), else the handler's own label.
    /// </summary>
    /// <param name="automationName">The value of AutomationProperties.Name.</param>
    /// <param name="ownLabel">The handler's own label (may be null).</param>
    /// <returns>The content description to show.</returns>
    internal static string ContentDescriptionOr(string automationName, string ownLabel) =>
        ContentDescriptionOf(automationName) ?? ownLabel;

    /// <summary>
    /// The tag an automation id gives the native view: the id as it is, or null for none (null or empty).
    /// </summary>
    /// <param name="automationId">The value of AutomationProperties.AutomationId.</param>
    /// <returns>The tag text, or null.</returns>
    internal static string TagOf(string automationId) => string.IsNullOrEmpty(automationId) ? null : automationId;
}
