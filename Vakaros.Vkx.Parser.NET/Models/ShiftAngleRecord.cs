namespace Vakaros.Vkx.Parser.NET.Models;

/// <summary>
/// 0x06 — Shift Angle. Records the heading angle for a port or starboard tack,
/// together with the average speed on that tack.
/// </summary>
public record ShiftAngleRecord : VkxRecord
{
    /// <inheritdoc/>
    public override RecordType Type => RecordType.ShiftAngle;

    /// <summary>UTC timestamp when the shift was recorded.</summary>
    public DateTimeOffset Timestamp { get; init; }

    /// <summary>
    /// <see langword="true"/> for port tack; <see langword="false"/> for starboard tack.
    /// </summary>
    public bool IsPort { get; init; }

    /// <summary>
    /// <see langword="true"/> if the angle was set manually;
    /// <see langword="false"/> if set by the auto-shift process.
    /// </summary>
    /// <remarks>
    /// The official spec lists "0 = auto, 0 = manual", which is a typo; a value of 1 is assumed to mean manual.
    /// </remarks>
    public bool IsManual { get; init; }

    /// <summary>True heading (not magnetic) as recorded, in degrees.</summary>
    internal float RawTrueHeading { get; init; }

    /// <summary>Average Speed Over Ground on this tack as recorded, in knots.</summary>
    internal float RawSpeedOverGround { get; init; }

    /// <summary>True heading (not magnetic) in radians (SI).</summary>
    public float TrueHeadingRadians => UnitConversions.DegreesToRadians(RawTrueHeading);

    /// <summary>True heading (not magnetic) in degrees.</summary>
    public float TrueHeadingDegrees => RawTrueHeading;

    /// <summary>Average Speed Over Ground on this tack in metres per second (SI).</summary>
    public float SpeedOverGroundMetresPerSecond => UnitConversions.KnotsToMetresPerSecond(RawSpeedOverGround);

    /// <summary>Average Speed Over Ground on this tack in knots (nautical).</summary>
    public float SpeedOverGroundKnots => RawSpeedOverGround;
}
