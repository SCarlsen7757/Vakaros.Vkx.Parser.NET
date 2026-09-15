namespace Vakaros.Vkx.Parser.NET.Models;

/// <summary>
/// 0x02 — Position, Velocity, and Orientation. Primary telemetry message logged at the
/// device's configured rate.
/// </summary>
public record PositionRecord : VkxRecord
{
    /// <inheritdoc/>
    public override RecordType Type => RecordType.PositionVelocityOrientation;

    /// <summary>UTC timestamp of the measurement.</summary>
    public DateTimeOffset Timestamp { get; init; }

    /// <summary>Latitude in decimal degrees (WGS-84).</summary>
    public double Latitude { get; init; }

    /// <summary>Longitude in decimal degrees (WGS-84).</summary>
    public double Longitude { get; init; }

    /// <summary>Speed Over Ground as recorded, in metres per second.</summary>
    internal float RawSpeedOverGround { get; init; }

    /// <summary>Course Over Ground as recorded, in radians.</summary>
    internal float RawCourseOverGround { get; init; }

    /// <summary>Altitude as recorded, in metres.</summary>
    internal float RawAltitude { get; init; }

    /// <summary>Speed Over Ground in metres per second (SI).</summary>
    public float SpeedOverGroundMetresPerSecond => RawSpeedOverGround;

    /// <summary>Speed Over Ground in knots (nautical).</summary>
    public float SpeedOverGroundKnots => UnitConversions.MetresPerSecondToKnots(RawSpeedOverGround);

    /// <summary>Course Over Ground in radians (SI).</summary>
    public float CourseOverGroundRadians => RawCourseOverGround;

    /// <summary>Course Over Ground in degrees.</summary>
    public float CourseOverGroundDegrees => UnitConversions.RadiansToDegrees(RawCourseOverGround);

    /// <summary>Altitude in metres (SI).</summary>
    public float AltitudeMetres => RawAltitude;

    /// <summary>Altitude in feet (imperial).</summary>
    public float AltitudeFeet => UnitConversions.MetresToFeet(RawAltitude);

    /// <summary>Orientation quaternion W component (true NED frame).</summary>
    public float QuaternionW { get; init; }

    /// <summary>Orientation quaternion X component.</summary>
    public float QuaternionX { get; init; }

    /// <summary>Orientation quaternion Y component.</summary>
    public float QuaternionY { get; init; }

    /// <summary>Orientation quaternion Z component.</summary>
    public float QuaternionZ { get; init; }
}
