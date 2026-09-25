using System;
using System.Threading.Tasks;

namespace CodeBrixVideoTool.Processing.Operations;

/// <summary>
/// The one thing the conversion view model cannot do for itself: ask a person where to put the
/// result. Only a head knows how to show a file dialog, so the page fills this in.
/// </summary>
public interface IOutputPathBridge
{
    /// <summary>
    /// Shows a "save as" dialog seeded with a suggested file name and returns the full path the
    /// person chose, or null if they cancelled. The head leaves this null when it has no file
    /// dialog, in which case the result is written beside the source instead.
    /// </summary>
    Func<string, string, Task<string>> PickOutputPathAsync { get; set; }
}
