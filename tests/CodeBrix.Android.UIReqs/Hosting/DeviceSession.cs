using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using CodeBrix.Android.UIReqs.Protocol;
using CodeBrix.Android.UIReqs.TestTarget;
using CodeBrix.Platform.UI.Core.UIReqs.Canvas;

namespace CodeBrix.Android.UIReqs.Hosting;

/// <summary>
/// The host's connection to the scenario app on the emulator. It sends test-run, scenario and
/// step requests and, while a request is in flight, serves the device's own requests: a
/// screencap (<c>adb -s SERIAL exec-out screencap</c>, cropped to the app window, pixels sent
/// back), "save this frame" (the copied FrameArchive / FrameReview, on the host) and a whitelisted
/// device shell command that resizes the REAL window (<c>wm size</c> / <c>wm density</c>, see
/// <see cref="IsAllowedShell"/>; the reply is its exit code).
/// </summary>
/// <remarks>
/// Environment (set by build/test-scripts/android-uireqs-run.sh):
/// UIREQS_SERIAL (default emulator-5600), UIREQS_HOST_PORT (the forwarded port, default
/// 47300), UIREQS_ORIENTATION (Landscape|Portrait, default Portrait), ANDROID_HOME (adb).
/// </remarks>
internal static class DeviceSession
{
    private static readonly Dictionary<long, TestFrame> _frames = new();
    private static UIReqsChannel? _channel;
    private static TcpClient? _client;

    /// <summary>The emulator serial.</summary>
    internal static string Serial { get; } = Environment.GetEnvironmentVariable("UIREQS_SERIAL") is { Length: > 0 } s ? s : "emulator-5600";

    /// <summary>The orientation declared for this run.</summary>
    internal static TestDisplayOrientation Orientation { get; } =
        Enum.TryParse<TestDisplayOrientation>(Environment.GetEnvironmentVariable("UIREQS_ORIENTATION"), true, out var o) ? o : TestDisplayOrientation.Portrait;

    /// <summary>The panel (app window) size and density the device reported.</summary>
    internal static (int Width, int Height, double Density) Panel { get; private set; }

    /// <summary>The last frame captured in the current scenario.</summary>
    internal static TestFrame? LatestFrame { get; private set; }

    /// <summary>How many frames were captured in this run.</summary>
    internal static int CaptureCount { get; private set; }

    /// <summary>Connects to the device app and starts the test run there.</summary>
    /// <summary>The host port forwarded to the device app (UIREQS_HOST_PORT, else the device port).</summary>
    internal static int HostPort => int.TryParse(Environment.GetEnvironmentVariable("UIREQS_HOST_PORT"), out var p) ? p : UIReqsChannel.DevicePort;

    /// <summary>
    /// True when something accepts a connection on the forwarded port within <paramref name="timeout"/>
    /// (an adb-forwarded scenario app); used to decide whether a plain solution test run can run the scenarios.
    /// </summary>
    internal static async Task<bool> IsReachableAsync(TimeSpan timeout)
    {
        try
        {
            using var probe = new TcpClient();
            using var cancel = new System.Threading.CancellationTokenSource(timeout);
            await probe.ConnectAsync("127.0.0.1", HostPort, cancel.Token).ConfigureAwait(false);
            var stream = probe.GetStream();
            stream.ReadTimeout = (int)timeout.TotalMilliseconds;

            // adb accepts forwarded connections even with no app behind them: require the app's reply.
            using var channel = new UIReqsChannel(stream);
            await channel.SendAsync(new JsonObject { ["op"] = "ping" }, null, cancel.Token).ConfigureAwait(false);
            var reply = await channel.ReceiveAsync(cancel.Token).ConfigureAwait(false);
            return reply != null;
        }
        catch (Exception exception) when (exception is SocketException or IOException or InvalidDataException or OperationCanceledException or ObjectDisposedException or InvalidOperationException)
        {
            return false;
        }
    }

    internal static async Task<JsonObject> ConnectAsync()
    {
        var port = HostPort;
        var deadline = Stopwatch.StartNew();
        Exception? last = null;
        while (deadline.Elapsed < TimeSpan.FromSeconds(60))
        {
            try
            {
                _client = new TcpClient { NoDelay = true };
                await _client.ConnectAsync("127.0.0.1", port).ConfigureAwait(false);
                _channel = new UIReqsChannel(_client.GetStream());
                var hello = await RequestAsync(new JsonObject { ["op"] = "hello", ["orientation"] = Orientation.ToString() }).ConfigureAwait(false);
                Panel = (hello.Int("width"), hello.Int("height"), (double?)hello["density"] ?? 1.0);
                return hello;
            }
            catch (Exception exception) when (exception is SocketException or IOException or InvalidDataException)
            {
                last = exception;
                _client?.Dispose();
                await Task.Delay(500).ConfigureAwait(false);
            }
        }

        throw new InvalidOperationException($"Could not reach the UIReqs device app on 127.0.0.1:{port} (is it running on {Serial} and forwarded with adb forward?).", last);
    }

    /// <summary>Ends the run on the device and disconnects.</summary>
    internal static async Task DisconnectAsync()
    {
        if (_channel == null)
        {
            return;
        }

        try
        {
            await RequestAsync(new JsonObject { ["op"] = "bye" }).ConfigureAwait(false);
        }
        finally
        {
            _channel.Dispose();
            _client?.Dispose();
            _channel = null;
        }
    }

    /// <summary>Forgets the frames of the previous scenario.</summary>
    internal static void BeginScenario()
    {
        _frames.Clear();
        LatestFrame = null;
    }

    /// <summary>
    /// Sends a request and waits for its reply, serving the device's capture/save requests
    /// in between.
    /// </summary>
    internal static async Task<JsonObject> RequestAsync(JsonObject request)
    {
        var channel = _channel ?? throw new InvalidOperationException("The UIReqs device app is not connected.");
        await channel.SendAsync(request).ConfigureAwait(false);
        while (true)
        {
            var received = await channel.ReceiveAsync().ConfigureAwait(false)
                ?? throw new InvalidOperationException("The UIReqs device app closed the connection.");
            var message = received.Message;
            switch (message.Str("request"))
            {
                case "capture":
                    await ServeCaptureAsync(channel, message.Int("seq")).ConfigureAwait(false);
                    continue;
                case "save":
                    await ServeSaveAsync(channel, message).ConfigureAwait(false);
                    continue;
                case "shell":
                    await ServeShellAsync(channel, message.Str("args")).ConfigureAwait(false);
                    continue;
                default:
                    return message;
            }
        }
    }

    private static async Task ServeCaptureAsync(UIReqsChannel channel, long sequence)
    {
        try
        {
            var frame = Screencap(sequence);
            _frames[sequence] = frame;
            LatestFrame = frame;
            CaptureCount++;
            await channel.SendAsync(new JsonObject { ["w"] = frame.Width, ["h"] = frame.Height }, frame.Rgba).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            // AP1.10: the whole exception (type and stack), not only its message - a one-off
            // "Object reference not set to an instance of an object." in a stability run could not be traced
            // (FIXLIST [AP1.10]). The host log keeps it too.
            Console.Error.WriteLine($"UIReqs host: capture {sequence} failed: {exception}");
            await channel.SendAsync(new JsonObject { ["error"] = exception.ToString() }).ConfigureAwait(false);
        }
    }

    private static async Task ServeSaveAsync(UIReqsChannel channel, JsonObject message)
    {
        string? path = null;
        if (_frames.TryGetValue(message.Int("seq"), out var frame))
        {
            path = message.Str("kind") == "archive" ? FrameArchive.TrySave(frame) : FrameReview.Save(frame, message.Str("label"));
        }

        await channel.SendAsync(new JsonObject { ["path"] = path }).ConfigureAwait(false);
    }

    /// <summary>
    /// True for the device shell commands a scenario may ask the host to run: <c>wm size</c> and
    /// <c>wm density</c>, with no argument, <c>reset</c>, a size <c>WxH</c> or a density, and <c>input tap X Y</c>
    /// (AP7-B TerminalView: a real system touch, the only thing that puts the display back into touch mode after a
    /// scenario sent a navigation key through the system) and <c>ime reset</c> (a fresh input method after a scenario
    /// that typed through a custom control's session, so its state cannot reach the next scenarios) - nothing else.
    /// </summary>
    /// <param name="args">The shell command line.</param>
    /// <returns>True when allowed.</returns>
    internal static bool IsAllowedShell(string? args) =>
        args != null && System.Text.RegularExpressions.Regex.IsMatch(args, @"^(wm (size( (reset|[1-9][0-9]{1,4}x[1-9][0-9]{1,4}))?|density( (reset|[1-9][0-9]{1,3}))?)|input tap [0-9]{1,4} [0-9]{1,4}|ime reset)$");

    private static async Task ServeShellAsync(UIReqsChannel channel, string? args)
    {
        if (!IsAllowedShell(args))
        {
            await channel.SendAsync(new JsonObject { ["exit"] = -1, ["error"] = $"shell command not allowed: \"{args}\"" }).ConfigureAwait(false);
            return;
        }

        try
        {
            var start = new ProcessStartInfo(Adb(), $"-s {Serial} shell {args}")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };
            using var process = Process.Start(start) ?? throw new InvalidOperationException("adb did not start.");
            var output = await process.StandardOutput.ReadToEndAsync().ConfigureAwait(false);
            var error = await process.StandardError.ReadToEndAsync().ConfigureAwait(false);
            await process.WaitForExitAsync().ConfigureAwait(false);
            await channel.SendAsync(new JsonObject { ["exit"] = process.ExitCode, ["output"] = (output + error).Trim() }).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            await channel.SendAsync(new JsonObject { ["exit"] = -1, ["error"] = exception.Message }).ConfigureAwait(false);
        }
    }

    private static string Adb() =>
        Path.Combine(Environment.GetEnvironmentVariable("ANDROID_HOME") is { Length: > 0 } home ? home : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Android", "Sdk"), "platform-tools", "adb");

    /// <summary>
    /// Takes a screencap in the raw format (a 12- or 16-byte header of width, height, format
    /// [, colour space] followed by RGBA_8888 rows) and crops it to the app window.
    /// </summary>
    private static TestFrame Screencap(long sequence)
    {
        var start = new ProcessStartInfo(Adb(), $"-s {Serial} exec-out screencap")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        using var process = Process.Start(start) ?? throw new InvalidOperationException("adb did not start.");
        using var buffer = new MemoryStream();
        process.StandardOutput.BaseStream.CopyTo(buffer);
        process.WaitForExit();
        var bytes = buffer.ToArray();
        if (process.ExitCode != 0 || bytes.Length < 16)
        {
            throw new InvalidOperationException($"adb screencap failed (exit {process.ExitCode}): {process.StandardError.ReadToEnd()}");
        }

        var width = BitConverter.ToInt32(bytes, 0);
        var height = BitConverter.ToInt32(bytes, 4);
        var format = BitConverter.ToInt32(bytes, 8);
        var header = bytes.Length - (width * height * 4);
        if (format != 1 || header is not (12 or 16))
        {
            throw new InvalidOperationException($"Unexpected screencap format {format} ({width} x {height}, {bytes.Length} bytes).");
        }

        var (panelWidth, panelHeight, _) = Panel;
        var cropWidth = panelWidth > 0 ? Math.Min(panelWidth, width) : width;
        var cropHeight = panelHeight > 0 ? Math.Min(panelHeight, height) : height;
        var rgba = new byte[cropWidth * cropHeight * 4];
        for (var y = 0; y < cropHeight; y++)
        {
            Buffer.BlockCopy(bytes, header + (y * width * 4), rgba, y * cropWidth * 4, cropWidth * 4);
        }

        return new TestFrame(rgba, cropWidth, cropHeight, sequence);
    }
}
