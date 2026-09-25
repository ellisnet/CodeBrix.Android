using System;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using CodeBrix.Android.UIReqs.Device.TestTarget;
using CodeBrix.Android.UIReqs.Protocol;

namespace CodeBrix.Android.UIReqs.Device.Runtime;

/// <summary>
/// The device's requests TO the host in the middle of a step: take a screencap (the host
/// answers with the frame's pixels), and save a captured frame under a label (the host owns
/// the frame files). Only the step-server thread uses it, one request at a time.
/// </summary>
internal static class HostChannel
{
    /// <summary>The connection to the host runner (null between sessions).</summary>
    internal static UIReqsChannel? Current { get; set; }

    /// <summary>Asks the host for a screencap and returns it as a frame.</summary>
    internal static async Task<TestFrame> CaptureAsync(long sequence, long renderGeneration, CancellationToken cancellationToken)
    {
        var channel = Current ?? throw new InvalidOperationException("No host runner is connected; frames come from the host's screencap.");
        await channel.SendAsync(new JsonObject { ["request"] = "capture", ["seq"] = sequence }, null, cancellationToken).ConfigureAwait(false);
        var reply = await channel.ReceiveAsync(cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("The host runner closed the connection during a capture.");
        var (message, payload) = reply;
        if (message.Str("error") is { } error)
        {
            throw new InvalidOperationException("The host could not capture a frame: " + error);
        }

        return new TestFrame(payload ?? Array.Empty<byte>(), message.Int("w"), message.Int("h"), sequence, renderGeneration, message.Str("path"));
    }

    /// <summary>
    /// Asks the host to run a whitelisted device shell command (<c>wm size ...</c> / <c>wm density ...</c>: a REAL
    /// window resize) and returns its exit code and output.
    /// </summary>
    internal static async Task<(int Exit, string Output)> ShellAsync(string args, CancellationToken cancellationToken)
    {
        var channel = Current ?? throw new InvalidOperationException("No host runner is connected; device shell commands are run by the host.");
        await channel.SendAsync(new JsonObject { ["request"] = "shell", ["args"] = args }, null, cancellationToken).ConfigureAwait(false);
        var reply = await channel.ReceiveAsync(cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("The host runner closed the connection during a shell request.");
        var message = reply.Message;
        if (message.Str("error") is { } error)
        {
            throw new InvalidOperationException($"The host could not run \"{args}\": {error}");
        }

        return ((int)message.Int("exit"), message.Str("output") ?? string.Empty);
    }

    /// <summary>Asks the host to save the captured frame <paramref name="sequence"/> ("archive" or "review"); returns its path or null.</summary>
    internal static string? SaveFrame(string kind, long sequence, string? label)
    {
        var channel = Current;
        if (channel == null)
        {
            return null;
        }

        channel.SendAsync(new JsonObject { ["request"] = "save", ["kind"] = kind, ["seq"] = sequence, ["label"] = label }).GetAwaiter().GetResult();
        var reply = channel.ReceiveAsync().GetAwaiter().GetResult();
        return reply?.Message.Str("path");
    }
}
