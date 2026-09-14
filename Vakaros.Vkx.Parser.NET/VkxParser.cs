using System.Buffers.Binary;
using System.Text;
using Vakaros.Vkx.Parser.NET.Models;

namespace Vakaros.Vkx.Parser.NET;

/// <summary>
/// Parses VKX binary log files produced by Vakaros devices into a <see cref="VkxSession"/>.
/// </summary>
/// <remarks>
/// Error contract for every <c>Parse</c> overload:
/// <list type="bullet">
///   <item><see cref="FormatException"/> — the data is corrupt: missing or inconsistent page header,
///   an unknown record key or undefined enum value in a known format version, or a timestamp out of range.</item>
///   <item><see cref="VkxUnsupportedVersionException"/> — the file's format version is older than
///   <see cref="VkxFormatVersion.MinimumSupported"/>.</item>
///   <item><see cref="VkxSession.IsPartial"/> — parsing stopped early because the stream ended mid-row,
///   or an unknown record key was found in a format version newer than <see cref="VkxFormatVersion.MaxKnown"/>.</item>
/// </list>
/// </remarks>
/// <example>
/// <code>
/// // From a file path
/// VkxSession session = VkxParser.ParseFile("session.vkx");
///
/// // From a stream
/// VkxSession session = VkxParser.Parse(stream);
///
/// // From a byte array
/// VkxSession session = VkxParser.Parse(bytes);
///
/// // Access typed records
/// foreach (PositionRecord pos in session.PositionRecords)
///     Console.WriteLine($"{pos.Timestamp} lat={pos.Latitude} lon={pos.Longitude}");
/// </code>
/// </example>
public static class VkxParser
{
    private const byte PageHeaderKey = 0xFF;

    // Largest fixed payload in the spec (internal 0x21).
    private const int MaxPayloadSize = 52;

    // Cancellation is checked once per this many rows.
    private const int CancellationCheckInterval = 4096;

    // Latest instant DateTimeOffset can represent: 9999-12-31T23:59:59.999Z.
    private const ulong MaxUnixMilliseconds = 253_402_300_799_999UL;

    // Maps every known 1-byte key to the size of its fixed payload in bytes; 0 means unknown.
    // Internal messages are included so the parser can skip them correctly.
    private static readonly byte[] PayloadSizes = CreatePayloadSizes();

    private static byte[] CreatePayloadSizes()
    {
        var sizes = new byte[256];
        sizes[0x01] = 32;   // Internal
        sizes[0x02] = 44;   // Position, Velocity, Orientation
        sizes[0x03] = 20;   // Declination
        sizes[0x04] = 13;   // Race Timer Event
        sizes[0x05] = 17;   // Line Position
        sizes[0x06] = 18;   // Shift Angle
        sizes[0x07] = 12;   // Internal
        sizes[0x08] = 13;   // Device Configuration
        sizes[0x0A] = 16;   // Wind
        sizes[0x0B] = 16;   // Speed Through Water
        sizes[0x0C] = 12;   // Depth
        sizes[0x0E] = 16;   // Internal
        sizes[0x0F] = 16;   // Load
        sizes[0x10] = 12;   // Temperature
        sizes[0x20] = 13;   // Internal
        sizes[0x21] = 52;   // Internal
        sizes[0xFE] = 2;    // Page Terminator
        sizes[0xFF] = 7;    // Page Header
        return sizes;
    }

    /// <summary>Parses a VKX file at the given path.</summary>
    /// <param name="filePath">Absolute or relative path to the .vkx file.</param>
    /// <exception cref="FormatException">The file is corrupt. See the <see cref="VkxParser"/> remarks.</exception>
    /// <exception cref="VkxUnsupportedVersionException">The file's format version is not supported.</exception>
    public static VkxSession ParseFile(string filePath)
        => ParseFile(filePath, CancellationToken.None);

    /// <summary>Parses a VKX file at the given path.</summary>
    /// <param name="filePath">Absolute or relative path to the .vkx file.</param>
    /// <param name="cancellationToken">Token checked periodically while parsing.</param>
    /// <exception cref="FormatException">The file is corrupt. See the <see cref="VkxParser"/> remarks.</exception>
    /// <exception cref="VkxUnsupportedVersionException">The file's format version is not supported.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was cancelled.</exception>
    public static VkxSession ParseFile(string filePath, CancellationToken cancellationToken)
    {
        using var stream = File.OpenRead(filePath);
        return Parse(stream, cancellationToken);
    }

    /// <summary>Parses a VKX file from a byte array.</summary>
    /// <exception cref="FormatException">The data is corrupt. See the <see cref="VkxParser"/> remarks.</exception>
    /// <exception cref="VkxUnsupportedVersionException">The file's format version is not supported.</exception>
    public static VkxSession Parse(byte[] data)
        => Parse(data, CancellationToken.None);

    /// <summary>Parses a VKX file from a byte array.</summary>
    /// <param name="data">The complete file contents.</param>
    /// <param name="cancellationToken">Token checked periodically while parsing.</param>
    /// <exception cref="FormatException">The data is corrupt. See the <see cref="VkxParser"/> remarks.</exception>
    /// <exception cref="VkxUnsupportedVersionException">The file's format version is not supported.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was cancelled.</exception>
    public static VkxSession Parse(byte[] data, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(data);
        using var stream = new MemoryStream(data, writable: false);
        return Parse(stream, cancellationToken);
    }

    /// <summary>
    /// Parses a VKX file from a <see cref="Stream"/>, reading from its current position to the end.
    /// The stream does not need to be seekable and is left open.
    /// </summary>
    /// <exception cref="FormatException">The data is corrupt. See the <see cref="VkxParser"/> remarks.</exception>
    /// <exception cref="VkxUnsupportedVersionException">The file's format version is not supported.</exception>
    public static VkxSession Parse(Stream stream)
        => Parse(stream, CancellationToken.None);

    /// <summary>
    /// Parses a VKX file from a <see cref="Stream"/>, reading from its current position to the end.
    /// The stream does not need to be seekable and is left open.
    /// </summary>
    /// <param name="stream">The stream to read.</param>
    /// <param name="cancellationToken">Token checked periodically while parsing.</param>
    /// <exception cref="FormatException">The data is corrupt. See the <see cref="VkxParser"/> remarks.</exception>
    /// <exception cref="VkxUnsupportedVersionException">The file's format version is not supported.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was cancelled.</exception>
    public static VkxSession Parse(Stream stream, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(stream);
        cancellationToken.ThrowIfCancellationRequested();

        var records = new List<VkxRecord>();
        byte? formatVersion = null;
        var isPartial = false;
        long offset = 0;
        long rowCount = 0;
        Span<byte> buffer = stackalloc byte[MaxPayloadSize];

        while (true)
        {
            if (++rowCount % CancellationCheckInterval == 0)
                cancellationToken.ThrowIfCancellationRequested();

            var keyValue = stream.ReadByte();
            if (keyValue < 0)
                break;

            var key = (byte)keyValue;
            var rowOffset = offset;

            if (formatVersion is null && key != PageHeaderKey)
                throw new FormatException(
                    $"The VKX data does not begin with a page header (0xFF); found key 0x{key:X2}. " +
                    "The file may be corrupt or is not a VKX file.");

            int size = PayloadSizes[key];
            if (size == 0)
            {
                if (formatVersion > VkxFormatVersion.MaxKnown)
                {
                    // Future format version — stop parsing gracefully instead of throwing.
                    isPartial = true;
                    break;
                }

                throw new FormatException($"Unknown VKX record key 0x{key:X2} at offset {rowOffset}.");
            }

            var payload = buffer[..size];
            if (stream.ReadAtLeast(payload, size, throwOnEndOfStream: false) < size)
            {
                // Stream ended mid-row (e.g. a file copied while still being written).
                isPartial = true;
                break;
            }

            offset += 1 + size;

            if (key == PageHeaderKey)
            {
                var version = payload[0];
                if (formatVersion is null)
                {
                    if (version < VkxFormatVersion.MinimumSupported)
                        throw new VkxUnsupportedVersionException(version);
                    formatVersion = version;
                }
                else if (version != formatVersion)
                {
                    throw new FormatException(
                        $"Inconsistent VKX format version at offset {rowOffset}: " +
                        $"page header declares 0x{version:X2} but the file started with 0x{formatVersion:X2}.");
                }

                // Internal log state (6 bytes) is not exposed.
                records.Add(new PageHeaderRecord { FormatVersion = version });
                continue;
            }

            var isKnownVersion = formatVersion <= VkxFormatVersion.MaxKnown;
            var record = ParseRecord(key, payload, isKnownVersion, rowOffset);
            if (record is not null)
                records.Add(record);
        }

        return new VkxSession(formatVersion ?? 0, records, isPartial);
    }

    // Returns null for internal record types that carry no public data.
    private static VkxRecord? ParseRecord(byte key, ReadOnlySpan<byte> payload, bool isKnownVersion, long offset) => key switch
    {
        0xFE => ParsePageTerminator(payload),
        0x02 => ParsePosition(payload, offset),
        0x03 => ParseDeclination(payload, offset),
        0x04 => ParseRaceTimerEvent(payload, isKnownVersion, offset),
        0x05 => ParseLinePosition(payload, isKnownVersion, offset),
        0x06 => ParseShiftAngle(payload, offset),
        0x08 => ParseDeviceConfiguration(payload),
        0x0A => ParseWind(payload, offset),
        0x0B => ParseSpeedThroughWater(payload, offset),
        0x0C => ParseDepth(payload, offset),
        0x0F => ParseLoad(payload, offset),
        0x10 => ParseTemperature(payload, offset),
        // Internal messages (0x01, 0x07, 0x0E, 0x20, 0x21) — payload already consumed and discarded.
        _ => null,
    };

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static ulong U8(ReadOnlySpan<byte> payload, int index) => BinaryPrimitives.ReadUInt64LittleEndian(payload[index..]);
    private static uint U4(ReadOnlySpan<byte> payload, int index) => BinaryPrimitives.ReadUInt32LittleEndian(payload[index..]);
    private static ushort U2(ReadOnlySpan<byte> payload, int index) => BinaryPrimitives.ReadUInt16LittleEndian(payload[index..]);
    private static int I4(ReadOnlySpan<byte> payload, int index) => BinaryPrimitives.ReadInt32LittleEndian(payload[index..]);
    private static float F4(ReadOnlySpan<byte> payload, int index) => BinaryPrimitives.ReadSingleLittleEndian(payload[index..]);

    /// <summary>Reads a Unix timestamp in milliseconds (U8 at payload index 0).</summary>
    private static DateTimeOffset Timestamp(ReadOnlySpan<byte> payload, long offset)
    {
        var milliseconds = U8(payload, 0);
        if (milliseconds > MaxUnixMilliseconds)
            throw new FormatException($"VKX timestamp {milliseconds} ms at offset {offset} is out of range.");
        return DateTimeOffset.FromUnixTimeMilliseconds((long)milliseconds);
    }

    /// <summary>Converts an I4 lat/lon value (10^-7 degrees) to decimal degrees.</summary>
    private static double LatLon(ReadOnlySpan<byte> payload, int index)
        => I4(payload, index) * 1e-7;

    // ── Record parsers ────────────────────────────────────────────────────────

    private static PageTerminatorRecord ParsePageTerminator(ReadOnlySpan<byte> payload)
        => new() { PreviousPageLength = U2(payload, 0) };

    private static PositionRecord ParsePosition(ReadOnlySpan<byte> payload, long offset) => new()
    {
        Timestamp = Timestamp(payload, offset),
        Latitude = LatLon(payload, 8),
        Longitude = LatLon(payload, 12),
        RawSpeedOverGround = F4(payload, 16),
        RawCourseOverGround = F4(payload, 20),
        RawAltitude = F4(payload, 24),
        QuaternionW = F4(payload, 28),
        QuaternionX = F4(payload, 32),
        QuaternionY = F4(payload, 36),
        QuaternionZ = F4(payload, 40),
    };

    private static DeclinationRecord ParseDeclination(ReadOnlySpan<byte> payload, long offset) => new()
    {
        Timestamp = Timestamp(payload, offset),
        RawDeclinationOffset = F4(payload, 8),
        Latitude = LatLon(payload, 12),
        Longitude = LatLon(payload, 16),
    };

    private static RaceTimerEventRecord ParseRaceTimerEvent(ReadOnlySpan<byte> payload, bool isKnownVersion, long offset)
    {
        var eventType = payload[8];
        if (isKnownVersion && eventType > (byte)TimerEventType.RaceEnd)
            throw new FormatException($"Undefined race timer event type {eventType} at offset {offset}.");

        return new RaceTimerEventRecord
        {
            Timestamp = Timestamp(payload, offset),
            EventType = (TimerEventType)eventType,
            TimerValue = I4(payload, 9),
        };
    }

    private static LinePositionRecord ParseLinePosition(ReadOnlySpan<byte> payload, bool isKnownVersion, long offset)
    {
        var lineEnd = payload[8];
        if (isKnownVersion && lineEnd > (byte)LineEndType.Boat)
            throw new FormatException($"Undefined line end type {lineEnd} at offset {offset}.");

        return new LinePositionRecord
        {
            Timestamp = Timestamp(payload, offset),
            LineEnd = (LineEndType)lineEnd,
            Latitude = F4(payload, 9),
            Longitude = F4(payload, 13),
        };
    }

    private static ShiftAngleRecord ParseShiftAngle(ReadOnlySpan<byte> payload, long offset) => new()
    {
        Timestamp = Timestamp(payload, offset),
        IsPort = payload[8] == 1,
        // The official spec lists "0 = auto, 0 = manual" (a typo); 1 is assumed to mean manual.
        IsManual = payload[9] == 1,
        RawTrueHeading = F4(payload, 10),
        RawSpeedOverGround = F4(payload, 14),
    };

    private static DeviceConfigurationRecord ParseDeviceConfiguration(ReadOnlySpan<byte> payload) => new()
    {
        // Bytes 0-7 are unused per spec.
        IsFixedToBodyFrame = (U4(payload, 8) & 0x01) != 0,
        TelemetryLoggingRate = payload[12],
    };

    private static WindRecord ParseWind(ReadOnlySpan<byte> payload, long offset) => new()
    {
        Timestamp = Timestamp(payload, offset),
        RawWindDirection = F4(payload, 8),
        RawWindSpeed = F4(payload, 12),
    };

    private static SpeedThroughWaterRecord ParseSpeedThroughWater(ReadOnlySpan<byte> payload, long offset) => new()
    {
        Timestamp = Timestamp(payload, offset),
        RawForwardSpeed = F4(payload, 8),
        RawHorizontalSpeed = F4(payload, 12),
    };

    private static DepthRecord ParseDepth(ReadOnlySpan<byte> payload, long offset) => new()
    {
        Timestamp = Timestamp(payload, offset),
        RawDepth = F4(payload, 8),
    };

    private static TemperatureRecord ParseTemperature(ReadOnlySpan<byte> payload, long offset) => new()
    {
        Timestamp = Timestamp(payload, offset),
        RawTemperature = F4(payload, 8),
    };

    private static LoadRecord ParseLoad(ReadOnlySpan<byte> payload, long offset) => new()
    {
        Timestamp = Timestamp(payload, offset),
        SensorName = Encoding.ASCII.GetString(payload.Slice(8, 4)).TrimEnd('\0'),
        Load = F4(payload, 12),
    };
}
