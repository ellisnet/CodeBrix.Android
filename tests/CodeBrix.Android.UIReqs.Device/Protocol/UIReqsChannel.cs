using System;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

namespace CodeBrix.Android.UIReqs.Protocol;

/// <summary>
/// The UIReqs wire protocol between the host runner and the device app (one TCP connection,
/// forwarded with <c>adb forward</c>): every message is one line of JSON; a message whose
/// JSON carries <c>"bytes": N</c> is followed by exactly N raw bytes (a frame's RGBA pixels).
/// This file is shared by both halves (the host project links it).
/// </summary>
public sealed class UIReqsChannel : IDisposable
{
    /// <summary>The TCP port the device app listens on (localhost of the device).</summary>
    public const int DevicePort = 47300;

    private readonly Stream _stream;
    private readonly SemaphoreSlim _writeGate = new(1, 1);
    private readonly byte[] _one = new byte[1];

    /// <summary>Creates a channel over a connected stream.</summary>
    public UIReqsChannel(Stream stream) => _stream = stream ?? throw new ArgumentNullException(nameof(stream));

    /// <summary>Sends one JSON message (and its payload, when given; its length is written as "bytes").</summary>
    public async Task SendAsync(JsonObject message, byte[]? payload = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        if (payload != null)
        {
            message["bytes"] = payload.Length;
        }

        var line = Encoding.UTF8.GetBytes(message.ToJsonString() + "\n");
        await _writeGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await _stream.WriteAsync(line, cancellationToken).ConfigureAwait(false);
            if (payload != null)
            {
                await _stream.WriteAsync(payload, cancellationToken).ConfigureAwait(false);
            }

            await _stream.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _writeGate.Release();
        }
    }

    /// <summary>Receives one message and its payload (null payload when the message has no "bytes"); null at end of stream.</summary>
    public async Task<(JsonObject Message, byte[]? Payload)?> ReceiveAsync(CancellationToken cancellationToken = default)
    {
        var line = await ReadLineAsync(cancellationToken).ConfigureAwait(false);
        if (line == null)
        {
            return null;
        }

        var message = JsonNode.Parse(line)?.AsObject() ?? throw new InvalidDataException("Empty UIReqs message.");
        byte[]? payload = null;
        if (message.TryGetPropertyValue("bytes", out var bytesNode) && bytesNode != null)
        {
            var length = bytesNode.GetValue<int>();
            payload = new byte[length];
            await _stream.ReadExactlyAsync(payload, cancellationToken).ConfigureAwait(false);
        }

        return (message, payload);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _stream.Dispose();
        _writeGate.Dispose();
    }

    private async Task<string?> ReadLineAsync(CancellationToken cancellationToken)
    {
        using var buffer = new MemoryStream();
        while (true)
        {
            var read = await _stream.ReadAsync(_one, cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                return buffer.Length == 0 ? null : Encoding.UTF8.GetString(buffer.ToArray());
            }

            if (_one[0] == (byte)'\n')
            {
                return Encoding.UTF8.GetString(buffer.ToArray());
            }

            buffer.WriteByte(_one[0]);
        }
    }
}

/// <summary>Helpers for reading message fields.</summary>
public static class UIReqsMessage
{
    /// <summary>A string field, or null.</summary>
    public static string? Str(this JsonObject message, string name) =>
        message.TryGetPropertyValue(name, out var node) && node is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;

    /// <summary>An int field, or a default.</summary>
    public static int Int(this JsonObject message, string name, int fallback = 0) =>
        message.TryGetPropertyValue(name, out var node) && node is JsonValue value && value.TryGetValue<int>(out var number) ? number : fallback;

    /// <summary>A bool field, or false.</summary>
    public static bool Bool(this JsonObject message, string name) =>
        message.TryGetPropertyValue(name, out var node) && node is JsonValue value && value.TryGetValue<bool>(out var flag) && flag;

    /// <summary>A string-array field (empty when missing).</summary>
    public static string[] Strings(this JsonObject message, string name)
    {
        if (!message.TryGetPropertyValue(name, out var node) || node is not JsonArray array)
        {
            return Array.Empty<string>();
        }

        var result = new string[array.Count];
        for (var i = 0; i < array.Count; i++)
        {
            result[i] = array[i]?.GetValue<string>() ?? string.Empty;
        }

        return result;
    }

    /// <summary>A JSON array of strings.</summary>
    public static JsonArray ToJsonArray(this System.Collections.Generic.IEnumerable<string> values)
    {
        var array = new JsonArray();
        foreach (var value in values)
        {
            array.Add(value);
        }

        return array;
    }

    /// <summary>Unused; keeps the JsonSerializer reference explicit for trimming analysis.</summary>
    internal static JsonSerializerOptions Options { get; } = new(JsonSerializerDefaults.General);
}
