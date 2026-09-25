using System;
using System.Threading.Tasks;

namespace InannaRosette.Services;

/// <summary>
/// The two things the view model cannot do for itself: ask a person where to write a file, and
/// ask which file to read back. Only a head knows how to show a file dialog, so the page fills
/// these in when it takes the view model as its data context. A head with no file dialog leaves
/// them null and the view model says so instead of saving.
/// </summary>
public interface IReadingFileBridge
{
    /// <summary>
    /// Shows a "save as…" dialog seeded with a suggested file name and returns the full path
    /// the person chose, or <c>null</c> when they cancelled.
    /// Signature: <c>Func&lt;suggestedFileName, typeName, extension, Task&lt;chosenPathOrNull&gt;&gt;</c>.
    /// </summary>
    Func<string, string, string, Task<string?>>? PickSavePathAsync { get; set; }

    /// <summary>
    /// Shows an "open file" dialog filtered to saved readings and returns the full path the
    /// person chose, or <c>null</c> when they cancelled.
    /// </summary>
    Func<Task<string?>>? PickReadingPathAsync { get; set; }
}
