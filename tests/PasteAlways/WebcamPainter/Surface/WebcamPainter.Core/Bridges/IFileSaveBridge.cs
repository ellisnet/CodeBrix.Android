using System;
using System.Threading.Tasks;

namespace WebcamPainter.Bridges;

/// <summary>
/// Lets the hosting page give the view model a native "Save JPEG as…" file dialog. The
/// Skia heads wire this up with the CodeBrix.Platform <c>FileSavePicker</c>; heads with no
/// dialog (the Linux framebuffer head) leave it null and the image saves to a default
/// Pictures-folder path instead.
/// </summary>
public interface IFileSaveBridge
{
    /// <summary>
    /// Shows a "save JPEG" dialog seeded with the suggested file name and returns the full
    /// path the user chose, or <c>null</c> if they cancelled.
    /// Signature: <c>Func&lt;suggestedFileName, Task&lt;chosenPathOrNull&gt;&gt;</c>.
    /// </summary>
    Func<string, Task<string>> PickSaveJpegPathAsync { get; set; }
}
