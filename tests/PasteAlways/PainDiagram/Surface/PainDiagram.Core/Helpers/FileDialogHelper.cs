using System.IO;

namespace PainDiagram.Helpers;

/// <summary>
/// Small helpers shared by the heads' native "Save PNG as…" dialogs.
/// </summary>
public static class FileDialogHelper
{
    /// <summary>
    /// The WinRT <c>FileSavePicker</c> (Skia heads and native WinUI) creates an empty
    /// placeholder file at the chosen path for a brand-new name. Remove it — but only when it
    /// is genuinely empty — so a chosen path behaves like a pure destination and the app's own
    /// "replace existing file?" prompt fires only for a real, non-empty file. A file that has
    /// content is never deleted, so no user data is lost before the save-time confirmation.
    /// </summary>
    public static void RemoveEmptyPlaceholder(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) { return; }

        try
        {
            var info = new FileInfo(path);
            if (info.Exists && info.Length == 0)
            {
                info.Delete();
            }
        }
        catch
        {
            //Leave the file in place if it cannot be removed; the save-time overwrite
            //  prompt will simply ask about it.
        }
    }
}
