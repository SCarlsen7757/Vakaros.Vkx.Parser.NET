namespace Vakaros.Vkx.Parser.NET.Models;

/// <summary>
/// 0x0B — Speed Through Water. Reading from a transducer.
/// Only present when the sensor is attached to the device.
/// </summary>
public record SpeedThroughWaterRecord : VkxRecord
{
    /// <inheritdoc/>
    public override RecordType Type => RecordType.SpeedThroughWater;

    /// <summary>UTC timestamp of the measurement.</summary>
    public DateTimeOffset Timestamp { get; init; }

    /// <summary>Speed of water in the forward direction as recorded, in metres per second.</summary>
    internal float RawForwardSpeed { get; init; }

    /// <summary>Speed of water in the horizontal (lateral) direction as recorded, in metres per second.</summary>
    internal float RawHorizontalSpeed { get; init; }

    /// <summary>Speed of water in the forward direction in metres per second (SI).</summary>
    public float ForwardSpeedMetresPerSecond => RawForwardSpeed;

    /// <summary>Speed of water in the forward direction in knots (nautical).</summary>
    public float ForwardSpeedKnots => UnitConversions.MetresPerSecondToKnots(RawForwardSpeed);

    /// <summary>Speed of water in the horizontal (lateral) direction in metres per second (SI).</summary>
    public float HorizontalSpeedMetresPerSecond => RawHorizontalSpeed;

    /// <summary>Speed of water in the horizontal (lateral) direction in knots (nautical).</summary>
    public float HorizontalSpeedKnots => UnitConversions.MetresPerSecondToKnots(RawHorizontalSpeed);
}
