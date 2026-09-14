namespace Vakaros.Vkx.Parser.NET.Models;

/// <summary>
/// 0x0A — Wind. Apparent wind reading from a Calypso Wind Sensor.
/// Only present when the sensor is attached to the device.
/// </summary>
public record WindRecord : VkxRecord
{
    /// <inheritdoc/>
    public override RecordType Type => RecordType.Wind;

    /// <summary>UTC timestamp of the measurement.</summary>
    public DateTimeOffset Timestamp { get; init; }

    /// <summary>Apparent wind direction as recorded, in degrees.</summary>
    internal float RawWindDirection { get; init; }

    /// <summary>Apparent wind speed as recorded, in metres per second.</summary>
    internal float RawWindSpeed { get; init; }

    /// <summary>Apparent wind direction in radians (SI).</summary>
    public float WindDirectionRadians => UnitConversions.DegreesToRadians(RawWindDirection);

    /// <summary>Apparent wind direction in degrees.</summary>
    public float WindDirectionDegrees => RawWindDirection;

    /// <summary>Apparent wind speed in metres per second (SI).</summary>
    public float WindSpeedMetresPerSecond => RawWindSpeed;

    /// <summary>Apparent wind speed in knots (nautical).</summary>
    public float WindSpeedKnots => UnitConversions.MetresPerSecondToKnots(RawWindSpeed);
}
