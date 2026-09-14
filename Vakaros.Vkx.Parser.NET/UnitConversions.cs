namespace Vakaros.Vkx.Parser.NET;

/// <summary>
/// Single source of truth for the unit conversions behind the SI and imperial/nautical
/// properties on the record types.
/// </summary>
internal static class UnitConversions
{
    /// <summary>One international knot is exactly 1852 metres per hour.</summary>
    internal const double MetresPerSecondPerKnot = 1852.0 / 3600.0;

    /// <summary>One international foot is exactly 0.3048 metres.</summary>
    internal const double MetresPerFoot = 0.3048;

    internal static float DegreesToRadians(float degrees) => (float)(degrees * Math.PI / 180.0);

    internal static float RadiansToDegrees(float radians) => (float)(radians * 180.0 / Math.PI);

    internal static float KnotsToMetresPerSecond(float knots) => (float)(knots * MetresPerSecondPerKnot);

    internal static float MetresPerSecondToKnots(float metresPerSecond) => (float)(metresPerSecond / MetresPerSecondPerKnot);

    internal static float MetresToFeet(float metres) => (float)(metres / MetresPerFoot);

    internal static float CelsiusToFahrenheit(float celsius) => (float)(celsius * 9.0 / 5.0 + 32.0);
}
