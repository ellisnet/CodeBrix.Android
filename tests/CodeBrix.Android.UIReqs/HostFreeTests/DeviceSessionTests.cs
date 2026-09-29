using System;
using System.IO;
using System.Threading.Tasks;
using CodeBrix.Android.UIReqs.Hosting;
using Xunit;

namespace CodeBrix.Android.UIReqs.HostFreeTests;

/// <summary>
/// [AP7-C 2026-09-28] Host-free tests of the host's screencap read (no device, no adb): a fake adb output stream that hands
/// out its bytes in small pieces is read whole into one array of the capture's size, for either header length, and two
/// captures read at the same time each get exactly their own bytes (nothing is shared between captures).
/// </summary>
public class DeviceSessionTests
{
    /// <summary>adb's output as a stream that returns at most <see cref="Piece"/> bytes per read, like a pipe.</summary>
    private sealed class PipeLikeStream : Stream
    {
        private const int Piece = 4093;
        private readonly byte[] _data;
        private int _position;

        public PipeLikeStream(byte[] data) => _data = data;

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

        public override int Read(byte[] buffer, int offset, int count)
        {
            var n = Math.Min(Math.Min(count, Piece), _data.Length - _position);
            Buffer.BlockCopy(_data, _position, buffer, offset, n);
            _position += n;
            return n;
        }

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    private static byte[] RawScreencap(int width, int height, int headerLength, byte seed)
    {
        var bytes = new byte[headerLength + (width * height * 4)];
        BitConverter.GetBytes(width).CopyTo(bytes, 0);
        BitConverter.GetBytes(height).CopyTo(bytes, 4);
        BitConverter.GetBytes(1).CopyTo(bytes, 8);
        for (var i = headerLength; i < bytes.Length; i++)
        {
            bytes[i] = (byte)(seed + (i * 31));
        }

        return bytes;
    }

    [Theory]
    [InlineData(16)]
    [InlineData(12)]
    public void A_screencap_is_read_whole_into_one_array_of_its_exact_size(int headerLength)
    {
        var raw = RawScreencap(300, 200, headerLength, 7);

        var read = DeviceSession.ReadScreencap(new PipeLikeStream(raw));

        Assert.Equal(raw.Length, read.Length);
        Assert.Equal(raw, read);
    }

    [Fact]
    public async Task Two_captures_read_at_the_same_time_each_get_exactly_their_own_bytes()
    {
        var token = TestContext.Current.CancellationToken;
        for (var round = 0; round < 8; round++)
        {
            var first = RawScreencap(640, 400, 16, 11);
            var second = RawScreencap(400, 640, 12, 99);

            var readFirst = Task.Run(() => DeviceSession.ReadScreencap(new PipeLikeStream(first)), token);
            var readSecond = Task.Run(() => DeviceSession.ReadScreencap(new PipeLikeStream(second)), token);
            var both = await Task.WhenAll(readFirst, readSecond);

            Assert.Equal(first, both[0]);
            Assert.Equal(second, both[1]);
            Assert.NotSame(both[0], both[1]);
        }
    }

    [Fact]
    public void Output_that_is_not_a_screencap_is_returned_as_it_is_for_the_caller_to_report()
    {
        var text = System.Text.Encoding.UTF8.GetBytes("error: device offline");

        var read = DeviceSession.ReadScreencap(new PipeLikeStream(text));

        Assert.Equal(text, read);
    }
}
