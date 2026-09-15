using System.Reflection;
using System.Text;
using Vakaros.Vkx.Parser.NET;
using Vakaros.Vkx.Parser.NET.Models;
using Xunit;

namespace Vakaros.Vkx.Parser.NET.Tests;

/// <summary>
/// Unit and integration tests for <see cref="VkxParser"/>.
/// All tests that don't use the real .vkx fixture build their payloads programmatically
/// using little-endian <see cref="BinaryWriter"/> — matching the format spec exactly.
/// </summary>
public class VkxParserTests
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>Wraps an action over a BinaryWriter and returns the resulting byte array.</summary>
    private static byte[] Build(Action<BinaryWriter> write)
    {
        using var ms = new MemoryStream();
        using var bw = new BinaryWriter(ms, Encoding.ASCII, leaveOpen: true);
        write(bw);
        bw.Flush();
        return ms.ToArray();
    }

    /// <summary>Writes a 0xFF page header with the given format version (+ 6 zero internal bytes).</summary>
    private static void WritePageHeader(BinaryWriter bw, byte version = VkxFormatVersion.V1_4)
    {
        bw.Write((byte)0xFF);
        bw.Write(version);
        bw.Write(new byte[6]);
    }

    /// <summary>Writes a 0xFE page terminator.</summary>
    private static void WritePageTerminator(BinaryWriter bw, ushort previousPageLength = 0)
    {
        bw.Write((byte)0xFE);
        bw.Write(previousPageLength);
    }

    private static VkxSession ParseBytes(byte[] data) => VkxParser.Parse(data);

    // ── Session / empty stream ────────────────────────────────────────────────

    [Fact]
    public void EmptyStream_ReturnsEmptySession()
    {
        var session = ParseBytes([]);
        Assert.Empty(session.Records);
        Assert.Equal(0, session.FormatVersion);
        Assert.False(session.IsPartial);
    }

    [Fact]
    public void PageHeaderOnly_ReturnsSingleRecord_WithFormatVersion()
    {
        var data = Build(bw => WritePageHeader(bw, VkxFormatVersion.V1_4));
        var session = ParseBytes(data);

        Assert.Equal(VkxFormatVersion.V1_4, session.FormatVersion);
        Assert.Single(session.Records);
        Assert.IsType<PageHeaderRecord>(session.Records[0]);
        Assert.False(session.IsPartial);
    }

    [Fact]
    public void IsPartial_IsFalse_ForCompleteKnownVersionFile()
    {
        var data = Build(bw =>
        {
            WritePageHeader(bw);
            WritePageTerminator(bw);
        });

        var session = ParseBytes(data);
        Assert.False(session.IsPartial);
    }

    // ── Version enforcement ───────────────────────────────────────────────────

    [Fact]
    public void Version_BelowMinimum_ThrowsVkxUnsupportedVersionException()
    {
        var data = Build(bw => WritePageHeader(bw, VkxFormatVersion.V1_3));

        var ex = Assert.Throws<VkxUnsupportedVersionException>(() => ParseBytes(data));
        Assert.Equal(VkxFormatVersion.V1_3, ex.FormatVersion);
        Assert.Contains("0x04", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("1.4", ex.Message);
    }

    [Fact]
    public void MissingPageHeader_ThrowsFormatException()
    {
        // A stream that starts directly with a record key (no 0xFF page header).
        var data = Build(bw =>
        {
            bw.Write((byte)0x02); // position key
            bw.Write(1_000_000_000_000UL);
            bw.Write(537_000_000);
            bw.Write(100_000_000);
            bw.Write(2.5f);
            bw.Write(1.2f);
            bw.Write(3.0f);
            bw.Write(1.0f);
            bw.Write(0.1f);
            bw.Write(0.2f);
            bw.Write(0.3f);
        });

        Assert.Throws<FormatException>(() => ParseBytes(data));
    }

    [Fact]
    public void Version_1_4_ParsesSuccessfully()
    {
        var data = Build(bw => WritePageHeader(bw, VkxFormatVersion.V1_4));
        var session = ParseBytes(data);
        Assert.Equal(VkxFormatVersion.V1_4, session.FormatVersion);
    }

    [Fact]
    public void FutureVersion_WithUnknownKey_ReturnsPartialSession()
    {
        const byte futureVersion = VkxFormatVersion.MaxKnown + 1;
        var data = Build(bw =>
        {
            WritePageHeader(bw, futureVersion);
            // Unknown key that the parser has never seen.
            bw.Write((byte)0x99);
        });

        var session = ParseBytes(data);
        Assert.True(session.IsPartial);
        // The page header was parsed; the unknown record stopped parsing.
        Assert.Single(session.Records);
        Assert.IsType<PageHeaderRecord>(session.Records[0]);
    }

    [Fact]
    public void FutureVersion_WithOnlyKnownKeys_IsNotPartial()
    {
        const byte futureVersion = VkxFormatVersion.MaxKnown + 1;
        var data = Build(bw =>
        {
            WritePageHeader(bw, futureVersion);
            // Write a known record (Wind = 0x0A).
            bw.Write((byte)0x0A);
            bw.Write(1_000_000_000_000UL); // timestamp
            bw.Write(180.0f);              // direction
            bw.Write(5.0f);               // speed
        });

        var session = ParseBytes(data);
        Assert.False(session.IsPartial);
        Assert.Equal(2, session.Records.Count); // header + wind
    }

    [Fact]
    public void TruncatedPayload_ReturnsPartialSession()
    {
        // Write a valid header then a position key (0x02) with only 4 bytes instead of 44.
        var data = Build(bw =>
        {
            WritePageHeader(bw);
            bw.Write((byte)0x02);
            bw.Write((int)42); // 4 bytes — payload is incomplete (needs 44)
        });

        var session = ParseBytes(data);
        Assert.True(session.IsPartial);
        // Page header was collected before truncation.
        Assert.Single(session.Records);
        Assert.IsType<PageHeaderRecord>(session.Records[0]);
    }

    // ── Page records ──────────────────────────────────────────────────────────

    [Fact]
    public void ParsesPageTerminatorRecord()
    {
        var data = Build(bw =>
        {
            WritePageHeader(bw);
            WritePageTerminator(bw, previousPageLength: 1024);
        });

        var session = ParseBytes(data);
        var terminator = Assert.Single(session.Records.OfType<PageTerminatorRecord>());
        Assert.Equal(1024, terminator.PreviousPageLength);
    }

    // ── Telemetry record round-trips ──────────────────────────────────────────

    [Fact]
    public void ParsesPositionRecord()
    {
        const ulong ts = 1_000_000_000_000UL;
        const int rawLat = 537_000_000;   // 53.7°
        const int rawLon = 100_000_000;   // 10.0°
        const float sog = 2.5f;
        const float cog = 1.2f;
        const float alt = 3.0f;
        const float qw = 1.0f, qx = 0.1f, qy = 0.2f, qz = 0.3f;

        var data = Build(bw =>
        {
            WritePageHeader(bw);
            bw.Write((byte)0x02);
            bw.Write(ts);
            bw.Write(rawLat);
            bw.Write(rawLon);
            bw.Write(sog);
            bw.Write(cog);
            bw.Write(alt);
            bw.Write(qw);
            bw.Write(qx);
            bw.Write(qy);
            bw.Write(qz);
        });

        var session = ParseBytes(data);
        var record = Assert.Single(session.Records.OfType<PositionRecord>());

        Assert.Equal(DateTimeOffset.FromUnixTimeMilliseconds((long)ts), record.Timestamp);
        Assert.Equal(rawLat * 1e-7, record.Latitude, precision: 10);
        Assert.Equal(rawLon * 1e-7, record.Longitude, precision: 10);
        Assert.Equal(sog, record.RawSpeedOverGround);
        Assert.Equal(cog, record.RawCourseOverGround);
        Assert.Equal(alt, record.RawAltitude);
        Assert.Equal(qw, record.QuaternionW);
        Assert.Equal(qx, record.QuaternionX);
        Assert.Equal(qy, record.QuaternionY);
        Assert.Equal(qz, record.QuaternionZ);
    }

    [Fact]
    public void ParsesDeclinationRecord()
    {
        const ulong ts = 1_000_000_000_000UL;
        const float decl = 0.05f;
        const int rawLat = 537_000_000;
        const int rawLon = 100_000_000;

        var data = Build(bw =>
        {
            WritePageHeader(bw);
            bw.Write((byte)0x03);
            bw.Write(ts);
            bw.Write(decl);
            bw.Write(rawLat);
            bw.Write(rawLon);
        });

        var session = ParseBytes(data);
        var record = Assert.Single(session.Records.OfType<DeclinationRecord>());

        Assert.Equal(DateTimeOffset.FromUnixTimeMilliseconds((long)ts), record.Timestamp);
        Assert.Equal(decl, record.RawDeclinationOffset);
        Assert.Equal(rawLat * 1e-7, record.Latitude, precision: 10);
        Assert.Equal(rawLon * 1e-7, record.Longitude, precision: 10);
    }

    [Fact]
    public void ParsesRaceTimerEventRecord()
    {
        const ulong ts = 1_000_000_000_000UL;
        const byte eventType = (byte)TimerEventType.RaceStart;
        const int timerValue = -300;

        var data = Build(bw =>
        {
            WritePageHeader(bw);
            bw.Write((byte)0x04);
            bw.Write(ts);
            bw.Write(eventType);
            bw.Write(timerValue);
        });

        var session = ParseBytes(data);
        var record = Assert.Single(session.Records.OfType<RaceTimerEventRecord>());

        Assert.Equal(DateTimeOffset.FromUnixTimeMilliseconds((long)ts), record.Timestamp);
        Assert.Equal(TimerEventType.RaceStart, record.EventType);
        Assert.Equal(timerValue, record.TimerValue);
    }

    [Fact]
    public void ParsesLinePositionRecord()
    {
        const ulong ts = 1_000_000_000_000UL;
        const byte lineEnd = (byte)LineEndType.Pin;
        const float lat = 53.7f;
        const float lon = 10.0f;

        var data = Build(bw =>
        {
            WritePageHeader(bw);
            bw.Write((byte)0x05);
            bw.Write(ts);
            bw.Write(lineEnd);
            bw.Write(lat);
            bw.Write(lon);
        });

        var session = ParseBytes(data);
        var record = Assert.Single(session.Records.OfType<LinePositionRecord>());

        Assert.Equal(DateTimeOffset.FromUnixTimeMilliseconds((long)ts), record.Timestamp);
        Assert.Equal(LineEndType.Pin, record.LineEnd);
        Assert.Equal(lat, record.Latitude);
        Assert.Equal(lon, record.Longitude);
    }

    [Theory]
    [InlineData(1, 0, true,  false)]   // port / auto
    [InlineData(0, 0, false, false)]   // starboard / auto
    [InlineData(1, 1, true,  true)]    // port / manual
    [InlineData(0, 1, false, true)]    // starboard / manual
    public void ParsesShiftAngleRecord(byte tackId, byte setBy, bool expectedIsPort, bool expectedIsManual)
    {
        const ulong ts = 1_000_000_000_000UL;
        const float heading = 45.0f;
        const float sog = 3.5f;

        var data = Build(bw =>
        {
            WritePageHeader(bw);
            bw.Write((byte)0x06);
            bw.Write(ts);
            bw.Write(tackId);
            bw.Write(setBy);
            bw.Write(heading);
            bw.Write(sog);
        });

        var session = ParseBytes(data);
        var record = Assert.Single(session.Records.OfType<ShiftAngleRecord>());

        Assert.Equal(DateTimeOffset.FromUnixTimeMilliseconds((long)ts), record.Timestamp);
        Assert.Equal(expectedIsPort, record.IsPort);
        Assert.Equal(expectedIsManual, record.IsManual);
        Assert.Equal(heading, record.RawTrueHeading);
        Assert.Equal(sog, record.RawSpeedOverGround);
    }

    [Fact]
    public void ParsesDeviceConfigurationRecord()
    {
        const uint bitfield = 0x01; // IsFixedToBodyFrame = true
        const byte loggingRate = 10;

        var data = Build(bw =>
        {
            WritePageHeader(bw);
            bw.Write((byte)0x08);
            bw.Write(0UL);      // unused field
            bw.Write(bitfield);
            bw.Write(loggingRate);
        });

        var session = ParseBytes(data);
        var record = Assert.Single(session.Records.OfType<DeviceConfigurationRecord>());

        Assert.True(record.IsFixedToBodyFrame);
        Assert.Equal(loggingRate, record.TelemetryLoggingRate);
    }

    [Fact]
    public void ParsesDeviceConfigurationRecord_NotFixed()
    {
        const uint bitfield = 0x00; // IsFixedToBodyFrame = false
        const byte loggingRate = 5;

        var data = Build(bw =>
        {
            WritePageHeader(bw);
            bw.Write((byte)0x08);
            bw.Write(0UL);
            bw.Write(bitfield);
            bw.Write(loggingRate);
        });

        var session = ParseBytes(data);
        var record = Assert.Single(session.Records.OfType<DeviceConfigurationRecord>());

        Assert.False(record.IsFixedToBodyFrame);
        Assert.Equal(loggingRate, record.TelemetryLoggingRate);
    }

    [Fact]
    public void ParsesWindRecord()
    {
        const ulong ts = 1_000_000_000_000UL;
        const float direction = 135.0f;
        const float speed = 7.2f;

        var data = Build(bw =>
        {
            WritePageHeader(bw);
            bw.Write((byte)0x0A);
            bw.Write(ts);
            bw.Write(direction);
            bw.Write(speed);
        });

        var session = ParseBytes(data);
        var record = Assert.Single(session.Records.OfType<WindRecord>());

        Assert.Equal(DateTimeOffset.FromUnixTimeMilliseconds((long)ts), record.Timestamp);
        Assert.Equal(direction, record.RawWindDirection);
        Assert.Equal(speed, record.RawWindSpeed);
    }

    [Fact]
    public void ParsesSpeedThroughWaterRecord()
    {
        const ulong ts = 1_000_000_000_000UL;
        const float forward = 3.1f;
        const float horizontal = 0.4f;

        var data = Build(bw =>
        {
            WritePageHeader(bw);
            bw.Write((byte)0x0B);
            bw.Write(ts);
            bw.Write(forward);
            bw.Write(horizontal);
        });

        var session = ParseBytes(data);
        var record = Assert.Single(session.Records.OfType<SpeedThroughWaterRecord>());

        Assert.Equal(DateTimeOffset.FromUnixTimeMilliseconds((long)ts), record.Timestamp);
        Assert.Equal(forward, record.RawForwardSpeed);
        Assert.Equal(horizontal, record.RawHorizontalSpeed);
    }

    [Fact]
    public void ParsesDepthRecord()
    {
        const ulong ts = 1_000_000_000_000UL;
        const float depth = 12.5f;

        var data = Build(bw =>
        {
            WritePageHeader(bw);
            bw.Write((byte)0x0C);
            bw.Write(ts);
            bw.Write(depth);
        });

        var session = ParseBytes(data);
        var record = Assert.Single(session.Records.OfType<DepthRecord>());

        Assert.Equal(DateTimeOffset.FromUnixTimeMilliseconds((long)ts), record.Timestamp);
        Assert.Equal(depth, record.RawDepth);
    }

    [Fact]
    public void ParsesLoadRecord()
    {
        const ulong ts = 1_000_000_000_000UL;
        const string name = "MAIN";
        const float load = 250.0f;

        var data = Build(bw =>
        {
            WritePageHeader(bw);
            bw.Write((byte)0x0F);
            bw.Write(ts);
            bw.Write(Encoding.ASCII.GetBytes(name));
            bw.Write(load);
        });

        var session = ParseBytes(data);
        var record = Assert.Single(session.Records.OfType<LoadRecord>());

        Assert.Equal(DateTimeOffset.FromUnixTimeMilliseconds((long)ts), record.Timestamp);
        Assert.Equal(name, record.SensorName);
        Assert.Equal(load, record.Load);
    }

    [Fact]
    public void ParsesLoadRecord_WithNullPaddedShortName()
    {
        const ulong ts = 1_000_000_000_000UL;
        const float load = 100.0f;

        var data = Build(bw =>
        {
            WritePageHeader(bw);
            bw.Write((byte)0x0F);
            bw.Write(ts);
            bw.Write(Encoding.ASCII.GetBytes("JIB\0")); // 3 chars + null padding
            bw.Write(load);
        });

        var session = ParseBytes(data);
        var record = Assert.Single(session.Records.OfType<LoadRecord>());

        Assert.Equal("JIB", record.SensorName);
        Assert.Equal(load, record.Load);
    }

    [Fact]
    public void ParsesTemperatureRecord()
    {
        const ulong ts = 1_000_000_000_000UL;
        const float temp = 18.5f;

        var data = Build(bw =>
        {
            WritePageHeader(bw);
            bw.Write((byte)0x10);
            bw.Write(ts);
            bw.Write(temp);
        });

        var session = ParseBytes(data);
        var record = Assert.Single(session.Records.OfType<TemperatureRecord>());

        Assert.Equal(DateTimeOffset.FromUnixTimeMilliseconds((long)ts), record.Timestamp);
        Assert.Equal(temp, record.RawTemperature);
    }

    // ── Internal / skipped records ────────────────────────────────────────────

    [Theory]
    [InlineData((byte)0x01, 32)]
    [InlineData((byte)0x07, 12)]
    [InlineData((byte)0x0E, 16)]
    [InlineData((byte)0x20, 13)]
    [InlineData((byte)0x21, 52)]
    public void InternalRecords_AreNotAddedToRecordsList(byte key, int payloadSize)
    {
        var data = Build(bw =>
        {
            WritePageHeader(bw);
            bw.Write(key);
            bw.Write(new byte[payloadSize]);
        });

        var session = ParseBytes(data);
        // Only the page header should be in the list; the internal record is discarded.
        Assert.Single(session.Records);
        Assert.IsType<PageHeaderRecord>(session.Records[0]);
    }

    // ── Full VKX 1.4 synthetic integration test ───────────────────────────────

    [Fact]
    public void FullV14Synthetic_ParsesAllRecords()
    {
        const ulong ts = 1_000_000_000_000UL;

        var data = Build(bw =>
        {
            // Page header (VKX 1.4)
            WritePageHeader(bw, VkxFormatVersion.V1_4);

            // Position record (0x02)
            bw.Write((byte)0x02);
            bw.Write(ts);
            bw.Write(537_000_000);  // lat
            bw.Write(100_000_000);  // lon
            bw.Write(2.5f);         // sog
            bw.Write(1.2f);         // cog
            bw.Write(0.0f);         // altitude
            bw.Write(1.0f);         // qw
            bw.Write(0.0f);         // qx
            bw.Write(0.0f);         // qy
            bw.Write(0.0f);         // qz

            // Wind record (0x0A)
            bw.Write((byte)0x0A);
            bw.Write(ts + 1000UL);
            bw.Write(180.0f);
            bw.Write(5.0f);

            // Race timer event (0x04)
            bw.Write((byte)0x04);
            bw.Write(ts + 2000UL);
            bw.Write((byte)TimerEventType.Start);
            bw.Write(-300);

            // Internal record — should be silently skipped (0x01, 32 bytes)
            bw.Write((byte)0x01);
            bw.Write(new byte[32]);

            // Page terminator (0xFE)
            WritePageTerminator(bw, 0);
        });

        var session = ParseBytes(data);

        Assert.Equal(VkxFormatVersion.V1_4, session.FormatVersion);
        Assert.False(session.IsPartial);

        // Header + position + wind + race timer + terminator (internal 0x01 is skipped)
        Assert.Equal(5, session.Records.Count);

        Assert.Single(session.PositionRecords);
        Assert.Single(session.WindRecords);
        Assert.Single(session.RaceTimerEventRecords);

        var pos = session.PositionRecords.First();
        Assert.Equal(53.7, pos.Latitude, precision: 5);
        Assert.Equal(10.0, pos.Longitude, precision: 5);

        var wind = session.WindRecords.First();
        Assert.Equal(180.0f, wind.RawWindDirection);
        Assert.Equal(5.0f, wind.RawWindSpeed);

        var timer = session.RaceTimerEventRecords.First();
        Assert.Equal(TimerEventType.Start, timer.EventType);
        Assert.Equal(-300, timer.TimerValue);
    }

    // ── Real file integration test ────────────────────────────────────────────

    [Fact]
    public void RealV14File_ParsesSuccessfully()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resourceName = assembly.GetManifestResourceNames()
            .Single(n => n.EndsWith(".vkx", StringComparison.OrdinalIgnoreCase));

        using var stream = assembly.GetManifestResourceStream(resourceName)!;
        Assert.NotNull(stream);

        var session = VkxParser.Parse(stream);

        Assert.Equal(VkxFormatVersion.V1_4, session.FormatVersion);
        Assert.NotEmpty(session.Records);
        Assert.NotEmpty(session.PositionRecords);
        Assert.False(session.IsPartial);
    }

    // ── Error handling ────────────────────────────────────────────────────────

    [Fact]
    public void KnownVersion_WithUnknownKey_ThrowsFormatException()
    {
        var data = Build(bw =>
        {
            WritePageHeader(bw, VkxFormatVersion.V1_4);
            bw.Write((byte)0x99); // Unknown key not in the V1.4 spec.
        });

        Assert.Throws<FormatException>(() => ParseBytes(data));
    }

    // ── ParseFile overload ────────────────────────────────────────────────────

    [Fact]
    public void ParseFile_ReturnsValidSession()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resourceName = assembly.GetManifestResourceNames()
            .Single(n => n.EndsWith(".vkx", StringComparison.OrdinalIgnoreCase));

        using var resourceStream = assembly.GetManifestResourceStream(resourceName)!;
        var tempPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".vkx");
        try
        {
            using (var file = File.Create(tempPath))
                resourceStream.CopyTo(file);

            var session = VkxParser.ParseFile(tempPath);

            Assert.Equal(VkxFormatVersion.V1_4, session.FormatVersion);
            Assert.Equal(VkxSpecVersion.V1_4, session.SpecVersion);
            Assert.NotEmpty(session.Records);
            Assert.False(session.IsPartial);
        }
        finally
        {
            File.Delete(tempPath);
        }
    }

    // ── Multiple records of same type ─────────────────────────────────────────

    [Fact]
    public void MultipleRecordsOfSameType_AreAllParsed()
    {
        const ulong ts = 1_000_000_000_000UL;

        var data = Build(bw =>
        {
            WritePageHeader(bw);
            // First Wind record
            bw.Write((byte)0x0A);
            bw.Write(ts);
            bw.Write(90.0f);
            bw.Write(3.0f);
            // Second Wind record
            bw.Write((byte)0x0A);
            bw.Write(ts + 1000UL);
            bw.Write(180.0f);
            bw.Write(5.0f);
        });

        var session = ParseBytes(data);

        Assert.Equal(2, session.WindRecords.Count());
    }
}
