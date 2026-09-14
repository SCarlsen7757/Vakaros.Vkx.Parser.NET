namespace Vakaros.Vkx.Parser.NET.Models;

/// <summary>
/// 0x0C — Depth. Reading from a transducer.
/// Only present when the sensor is attached to the device.
/// </summary>
public record DepthRecord : VkxRecord
{
    /// <inheritdoc/>
    public override RecordType Type => RecordType.Depth;

    /// <summary>UTC timestamp of the measurement.</summary>
    public DateTimeOffset Timestamp { get; init; }

    /// <summary>Water depth as recorded, in metres.</summary>
    internal float RawDepth { get; init; }

    /// <summary>Water depth in metres (SI).</summary>
    public float DepthMetres => RawDepth;

    /// <summary>Water depth in feet (imperial).</summary>
    public float DepthFeet => UnitConversions.MetresToFeet(RawDepth);
}
