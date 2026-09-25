using System;
using System.Threading.Tasks;

namespace CodeBrixVideoTool.Services;

/// <summary>
/// The one thing the main view model cannot do for itself: ask a person which file to open. Only a
/// head knows how to show a file dialog, so the page fills this in.
/// </summary>
public interface IMediaFileBridge
{
    /// <summary>
    /// Shows an "open file" dialog filtered to the containers this application reads, and returns
    /// the full path the person chose, or null if they cancelled. The head leaves this null when it
    /// has no file dialog, in which case nothing can be opened by hand.
    /// </summary>
    Func<Task<string>> PickMediaFileAsync { get; set; }
}
