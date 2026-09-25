using System;
using System.Threading.Tasks;

namespace Pinta.Brix.Bridges;

/// <summary>
/// Lets the hosting page hand the view model the save-prompt loop that only a
/// page can run - it needs a <c>XamlRoot</c> to attach its dialogs to, and it
/// has to survive the document being saved through the page's own file
/// dialogs. The page fills this in from its <c>DataContextChanged</c> handler.
/// </summary>
public interface IShellCloseBridge
{
    /// <summary>
    /// Walks every dirty document, offering to save, discard or cancel, and
    /// answers false when the user cancelled so the close is abandoned. The
    /// view model treats a null delegate as "nothing to prompt about".
    /// Signature: <c>Func&lt;Task&lt;closeMayProceed&gt;&gt;</c>.
    /// </summary>
    Func<Task<bool>> ConfirmCloseApplicationAsync { get; set; }
}
