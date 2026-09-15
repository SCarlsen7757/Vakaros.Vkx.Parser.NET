using System.Text;
using Vakaros.Vkx.Parser.NET.Models;
using Xunit;

namespace Vakaros.Vkx.Parser.NET.Tests;

/// <summary>
/// Tests for truncation, version consistency, the exception contract, and stream handling.
/// </summary>
public class VkxParserRobustnessTests
{
    private const ulong Ts = 1_000_000_000_000UL;

    private static byte[] Build(Action<BinaryWriter> write)
    {
        using var ms = new MemoryStream();
        using var bw = new BinaryWriter(ms, Encoding.ASCII, leaveOpen: true);
        write(bw);
        bw.Flush();
        return ms.ToArray();
    }

    private static void WritePageHeader(BinaryWriter bw, byte version = VkxFormatVersion.V1_4)
    {
        bw.Write((byte)0xFF);
        bw.Write(version);
        bw.Write(new byte[6]);
    }

    private static void WriteWind(BinaryWriter bw, ulong ts = Ts)
    {
        bw.Write((byte)0x0A);
        bw.Write(ts);
        bw.Write(90.0f);
        bw.Write(4.0f);
    }

    // ── Truncation ────────────────────────────────────────────────────────────

    [Theory]
    [InlineData((byte)0x01, 32)]
    [InlineData((byte)0x07, 12)]
    [InlineData((byte)0x0E, 16)]
    [InlineData((byte)0x20, 13)]
    [InlineData((byte)0x21, 52)]
    public void TruncatedInternalRecord_ReturnsPartialSession(byte key, int payloadSize)
    {
        var data = Build(bw =>
        {
            WritePageHeader(bw);
            bw.Write(key);
            bw.Write(new byte[payloadSize - 1]);
        });

        var session = VkxParser.Parse(data);

        Assert.True(session.IsPartial);
        Assert.IsType<PageHeaderRecord>(Assert.Single(session.Records));
    }

    [Fact]
    public void TruncatedFirstPageHeader_ReturnsEmptyPartialSession()
    {
        var session = VkxParser.Parse([0xFF, VkxFormatVersion.V1_4, 0x00]);

        Assert.True(session.IsPartial);
        Assert.Empty(session.Records);
    }

    [Fact]
    public void TruncatedLaterPageHeader_ReturnsPartialSession()
    {
        var data = Build(bw =>
        {
            WritePageHeader(bw);
            WriteWind(bw);
            bw.Write((byte)0xFF);
            bw.Write(VkxFormatVersion.V1_4);
        });

        var session = VkxParser.Parse(data);

        Assert.True(session.IsPartial);
        Assert.Single(session.WindRecords);
    }

    [Fact]
    public void TruncatedLoadRecord_ReturnsPartialSession()
    {
        var data = Build(bw =>
        {
            WritePageHeader(bw);
            bw.Write((byte)0x0F);
            bw.Write(Ts);
            bw.Write(Encoding.ASCII.GetBytes("MA")); // stops inside the sensor name
        });

        var session = VkxParser.Parse(data);

        Assert.True(session.IsPartial);
        Assert.Empty(session.LoadRecords);
    }

    // ── Format version ────────────────────────────────────────────────────────

    [Fact]
    public void FormatVersion_ComesFromFirstPageHeader()
    {
        var data = Build(bw =>
        {
            WritePageHeader(bw);
            WriteWind(bw);
            WritePageHeader(bw);
        });

        var session = VkxParser.Parse(data);

        Assert.Equal(VkxFormatVersion.V1_4, session.FormatVersion);
        Assert.Equal(2, session.Records.OfType<PageHeaderRecord>().Count());
    }

    [Fact]
    public void InconsistentPageHeaderVersion_ThrowsFormatException()
    {
        var data = Build(bw =>
        {
            WritePageHeader(bw, VkxFormatVersion.V1_4);
            WriteWind(bw);
            WritePageHeader(bw, VkxFormatVersion.MaxKnown + 1);
        });

        var ex = Assert.Throws<FormatException>(() => VkxParser.Parse(data));
        Assert.Contains("Inconsistent", ex.Message);
    }

    [Fact]
    public void UnsupportedVersionException_IsNotSupportedException()
    {
        var ex = new VkxUnsupportedVersionException(VkxFormatVersion.V1_0);

        Assert.IsAssignableFrom<NotSupportedException>(ex);
        Assert.Equal(VkxFormatVersion.V1_0, ex.FormatVersion);
    }

    // ── Value validation ──────────────────────────────────────────────────────

    [Theory]
    [InlineData(253_402_300_800_000UL)]    // one millisecond past 9999-12-31T23:59:59.999Z
    [InlineData(ulong.MaxValue)]           // would wrap negative when cast to long
    public void TimestampOutOfRange_ThrowsFormatException(ulong timestamp)
    {
        var data = Build(bw =>
        {
            WritePageHeader(bw);
            WriteWind(bw, timestamp);
        });

        Assert.Throws<FormatException>(() => VkxParser.Parse(data));
    }

    [Fact]
    public void MaximumTimestamp_Parses()
    {
        var data = Build(bw =>
        {
            WritePageHeader(bw);
            WriteWind(bw, 253_402_300_799_999UL);
        });

        var wind = Assert.Single(VkxParser.Parse(data).WindRecords);
        Assert.Equal(DateTimeOffset.MaxValue.UtcDateTime.Year, wind.Timestamp.Year);
    }

    [Fact]
    public void UndefinedTimerEventType_ThrowsFormatException()
    {
        var data = Build(bw =>
        {
            WritePageHeader(bw);
            bw.Write((byte)0x04);
            bw.Write(Ts);
            bw.Write((byte)5);
            bw.Write(0);
        });

        Assert.Throws<FormatException>(() => VkxParser.Parse(data));
    }

    [Fact]
    public void UndefinedLineEndType_ThrowsFormatException()
    {
        var data = Build(bw =>
        {
            WritePageHeader(bw);
            bw.Write((byte)0x05);
            bw.Write(Ts);
            bw.Write((byte)2);
            bw.Write(53.7f);
            bw.Write(10.0f);
        });

        Assert.Throws<FormatException>(() => VkxParser.Parse(data));
    }

    [Fact]
    public void UndefinedEnumValue_InFutureVersion_IsKept()
    {
        var data = Build(bw =>
        {
            WritePageHeader(bw, VkxFormatVersion.MaxKnown + 1);
            bw.Write((byte)0x04);
            bw.Write(Ts);
            bw.Write((byte)9);
            bw.Write(0);
        });

        var timer = Assert.Single(VkxParser.Parse(data).RaceTimerEventRecords);
        Assert.Equal((TimerEventType)9, timer.EventType);
    }

    // ── Streams ───────────────────────────────────────────────────────────────

    [Fact]
    public void NonSeekableStream_Parses()
    {
        var data = Build(bw =>
        {
            WritePageHeader(bw);
            WriteWind(bw);
        });

        using var stream = new NonSeekableStream(data);
        var session = VkxParser.Parse(stream);

        Assert.False(session.IsPartial);
        Assert.Single(session.WindRecords);
    }

    [Fact]
    public void Parse_ReadsFromCurrentStreamPosition()
    {
        var data = Build(bw =>
        {
            bw.Write(new byte[3]); // prefix that is not part of the VKX data
            WritePageHeader(bw);
            WriteWind(bw);
        });

        using var stream = new MemoryStream(data) { Position = 3 };
        var session = VkxParser.Parse(stream);

        Assert.Single(session.WindRecords);
    }

    /// <summary>A read-only stream that cannot seek or report its length, like a network stream.</summary>
    private sealed class NonSeekableStream(byte[] data) : Stream
    {
        private readonly MemoryStream _inner = new(data);

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        // Return at most 5 bytes per call to exercise short reads.
        public override int Read(byte[] buffer, int offset, int count) => _inner.Read(buffer, offset, Math.Min(count, 5));
        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing) _inner.Dispose();
            base.Dispose(disposing);
        }
    }
}
