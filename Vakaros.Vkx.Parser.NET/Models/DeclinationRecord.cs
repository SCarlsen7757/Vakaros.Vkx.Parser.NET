namespace Vakaros.Vkx.Parser.NET.Models;

/// <summary>
/// 0x03 — Declination. Logged infrequently with the current magnetic declination offset
/// and the associated lat/lon at the time of computation.
/// </summary>
public record DeclinationRecord : VkxRecord
{
    /// <inheritdoc/>
    public override RecordType Type => RecordType.Declination;

    /// <summary>UTC timestamp of the measurement.</summary>
    public DateTimeOffset Timestamp { get; init; }

    /// <summary>Magnetic declination offset as recorded, in radians.</summary>
    internal float RawDeclinationOffset { get; init; }

    /// <summary>Magnetic declination offset in radians (SI).</summary>
    public float DeclinationOffsetRadians => RawDeclinationOffset;

    /// <summary>Magnetic declination offset in degrees.</summary>
    public float DeclinationOffsetDegrees => UnitConversions.RadiansToDegrees(RawDeclinationOffset);

    /// <summary>Latitude in decimal degrees (WGS-84).</summary>
    public double Latitude { get; init; }

    /// <summary>Longitude in decimal degrees (WGS-84).</summary>
    public double Longitude { get; init; }
}
