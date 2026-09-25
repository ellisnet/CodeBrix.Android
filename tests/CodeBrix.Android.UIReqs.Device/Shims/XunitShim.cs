using System;
using System.Collections.Generic;
using System.Threading;

namespace Xunit;

/// <summary>
/// The xunit TestContext surface the copied scenario code uses (the cancellation token of the run
/// and attachments). On the device the token is the host session's, and attachments are handed
/// back to the host with the step result.
/// </summary>
public sealed class TestContext
{
    private static readonly TestContext _current = new();
    private readonly List<(string Name, byte[] Bytes, string MediaType)> _attachments = new();

    /// <summary>The context of the running scenario.</summary>
    public static TestContext Current => _current;

    /// <summary>Cancelled when the host session ends.</summary>
    public CancellationToken CancellationToken => CodeBrix.Android.UIReqs.Device.Runtime.StepServer.SessionToken;

    /// <summary>Adds a binary attachment.</summary>
    public void AddAttachment(string name, byte[] value, string mediaType)
    {
        lock (_attachments)
        {
            _attachments.Add((name, value, mediaType));
        }
    }

    /// <summary>Adds a text attachment.</summary>
    public void AddAttachment(string name, string value) => AddAttachment(name, System.Text.Encoding.UTF8.GetBytes(value ?? string.Empty), "text/plain");

    /// <summary>Takes (and clears) the attachments added so far.</summary>
    internal IReadOnlyList<(string Name, byte[] Bytes, string MediaType)> TakeAttachments()
    {
        lock (_attachments)
        {
            var taken = _attachments.ToArray();
            _attachments.Clear();
            return taken;
        }
    }

    /// <summary>Kept for API shape; unused.</summary>
    internal static Exception NotAvailable(string what) => new NotSupportedException(what + " is not available on the device.");
}
