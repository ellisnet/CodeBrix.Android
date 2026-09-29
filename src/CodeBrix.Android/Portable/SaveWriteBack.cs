using System;

namespace CodeBrix.Android.Services;

/// <summary>
/// [AP8-S batch 2] The pure part of the save picker's compatibility path (ContentDocumentFile.Path of a created
/// document): which file-system events in the path's own cache folder mean "the app finished writing the file", so
/// the file is copied back to the document. The folder is watched, not the file: desktop code deletes the picker's
/// empty placeholder before writing its own file (a pasted app's own placeholder cleanup), or writes
/// a temporary file and renames it into place; a watch on the placeholder's inode died with the placeholder and the
/// document stayed empty.
/// </summary>
internal static class SaveWriteBack
{
    /// <summary>inotify IN_CLOSE_WRITE (FileObserver.CLOSE_WRITE): a file opened for writing was closed.</summary>
    internal const int CloseWrite = 0x008;

    /// <summary>inotify IN_MOVED_TO (FileObserver.MOVED_TO): a file was renamed into the folder.</summary>
    internal const int MovedTo = 0x080;

    /// <summary>The events the folder is watched for.</summary>
    internal const int WatchedEvents = CloseWrite | MovedTo;

    /// <summary>
    /// True when a folder event is the compatibility file being complete: written and closed, or renamed into
    /// place, under exactly the file's name (ordinal: Android file names are case-sensitive).
    /// </summary>
    /// <param name="eventMask">The event bits FileObserver reported.</param>
    /// <param name="eventName">The name, relative to the watched folder, the event is about (null for the folder).</param>
    /// <param name="fileName">The compatibility file's name.</param>
    internal static bool IsFileComplete(int eventMask, string eventName, string fileName) =>
        (eventMask & WatchedEvents) != 0
        && !string.IsNullOrEmpty(eventName)
        && string.Equals(eventName, fileName, StringComparison.Ordinal);
}
