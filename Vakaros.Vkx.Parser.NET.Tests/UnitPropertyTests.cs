using System.Text;
using Vakaros.Vkx.Parser.NET.Models;
using Xunit;

namespace Vakaros.Vkx.Parser.NET.Tests;

/// <summary>
/// Tests for <see cref="UnitConversions"/> and the SI / imperial properties derived from raw values.
/// </summary>
public class UnitPropertyTests
{
    private const ulong Ts = 1_000_000_000_000UL;
    private const float Tolerance = 1e-4f;

    private static VkxSession Parse(Action<BinaryWriter> writeRecords)
    {
        using var ms = new MemoryStream();
        using (var bw = new BinaryWriter(ms, Encoding.ASCII, leaveOpen: true))
        {
            bw.Write((byte)0xFF);
            bw.Write(VkxFormatVersion.V1_4);
            bw.Write(new byte[6]);
            writeRecords(bw);
        }
        return VkxParser.Parse(ms.ToArray());
    }

    // ── UnitConversions ───────────────────────────────────────────────────────

    [Theory]
    [InlineData(0f, 0f)]
    [InlineData(90f, MathF.PI / 2)]
    [InlineData(180f, MathF.PI)]
    [InlineData(-45f, -MathF.PI / 4)]
    public void DegreesToRadians(float degrees, float radians)
    {
        Assert.Equal(radians, UnitConversions.DegreesToRadians(degrees), Tolerance);
        Assert.Equal(degrees, UnitConversions.RadiansToDegrees(radians), Tolerance);
    }

    [Fact]
    public void KnotsAndMetresPerSecond()
    {
        Assert.Equal(5.144444f, UnitConversions.KnotsToMetresPerSecond(10f), Tolerance);
        Assert.Equal(10f, UnitConversions.MetresPerSecondToKnots(5.144444f), Tolerance);
    }

    [Fact]
    public void MetresToFeet()
        => Assert.Equal(10f, UnitConversions.MetresToFeet(3.048f), Tolerance);

    [Theory]
    [InlineData(0f, 32f)]
    [InlineData(100f, 212f)]
    [InlineData(-40f, -40f)]
    public void CelsiusToFahrenheit(float celsius, float fahrenheit)
        => Assert.Equal(fahrenheit, UnitConversions.CelsiusToFahrenheit(celsius), Tolerance);

    // ── Records ───────────────────────────────────────────────────────────────

    [Fact]
    public void PositionRecord_ExposesSiAndImperial()
    {
        var session = Parse(bw =>
        {
            bw.Write((byte)0x02);
            bw.Write(Ts);
            bw.Write(537_000_000);
            bw.Write(100_000_000);
            bw.Write(5.144444f);     // SOG m/s
            bw.Write(MathF.PI);      // COG rad
            bw.Write(3.048f);        // altitude m
            bw.Write(1f); bw.Write(0f); bw.Write(0f); bw.Write(0f);
        });

        var p = Assert.Single(session.PositionRecords);
        Assert.Equal(5.144444f, p.SpeedOverGroundMetresPerSecond);
        Assert.Equal(10f, p.SpeedOverGroundKnots, Tolerance);
        Assert.Equal(MathF.PI, p.CourseOverGroundRadians);
        Assert.Equal(180f, p.CourseOverGroundDegrees, Tolerance);
        Assert.Equal(3.048f, p.AltitudeMetres);
        Assert.Equal(10f, p.AltitudeFeet, Tolerance);
    }

    [Fact]
    public void DeclinationRecord_ExposesSiAndDegrees()
    {
        var session = Parse(bw =>
        {
            bw.Write((byte)0x03);
            bw.Write(Ts);
            bw.Write(MathF.PI / 36); // 5°
            bw.Write(537_000_000);
            bw.Write(100_000_000);
        });

        var d = Assert.Single(session.DeclinationRecords);
        Assert.Equal(MathF.PI / 36, d.DeclinationOffsetRadians);
        Assert.Equal(5f, d.DeclinationOffsetDegrees, Tolerance);
    }

    [Fact]
    public void ShiftAngleRecord_ConvertsDegreesAndKnotsToSi()
    {
        var session = Parse(bw =>
        {
            bw.Write((byte)0x06);
            bw.Write(Ts);
            bw.Write((byte)1);
            bw.Write((byte)0);
            bw.Write(180f);   // true heading °
            bw.Write(10f);    // SOG kn
        });

        var s = Assert.Single(session.ShiftAngleRecords);
        Assert.Equal(180f, s.RawTrueHeading);
        Assert.Equal(10f, s.RawSpeedOverGround);
        Assert.Equal(MathF.PI, s.TrueHeadingRadians, Tolerance);
        Assert.Equal(180f, s.TrueHeadingDegrees);
        Assert.Equal(5.144444f, s.SpeedOverGroundMetresPerSecond, Tolerance);
        Assert.Equal(10f, s.SpeedOverGroundKnots);
    }

    [Fact]
    public void WindRecord_ConvertsDegreesToRadians()
    {
        var session = Parse(bw =>
        {
            bw.Write((byte)0x0A);
            bw.Write(Ts);
            bw.Write(90f);          // direction °
            bw.Write(5.144444f);    // speed m/s
        });

        var w = Assert.Single(session.WindRecords);
        Assert.Equal(90f, w.RawWindDirection);
        Assert.Equal(MathF.PI / 2, w.WindDirectionRadians, Tolerance);
        Assert.Equal(90f, w.WindDirectionDegrees);
        Assert.Equal(5.144444f, w.WindSpeedMetresPerSecond);
        Assert.Equal(10f, w.WindSpeedKnots, Tolerance);
    }

    [Fact]
    public void SpeedThroughWaterRecord_ExposesSiAndKnots()
    {
        var session = Parse(bw =>
        {
            bw.Write((byte)0x0B);
            bw.Write(Ts);
            bw.Write(5.144444f);
            bw.Write(0.514444f);
        });

        var s = Assert.Single(session.SpeedThroughWaterRecords);
        Assert.Equal(5.144444f, s.ForwardSpeedMetresPerSecond);
        Assert.Equal(10f, s.ForwardSpeedKnots, Tolerance);
        Assert.Equal(0.514444f, s.HorizontalSpeedMetresPerSecond);
        Assert.Equal(1f, s.HorizontalSpeedKnots, Tolerance);
    }

    [Fact]
    public void DepthRecord_ExposesMetresAndFeet()
    {
        var session = Parse(bw =>
        {
            bw.Write((byte)0x0C);
            bw.Write(Ts);
            bw.Write(3.048f);
        });

        var d = Assert.Single(session.DepthRecords);
        Assert.Equal(3.048f, d.DepthMetres);
        Assert.Equal(10f, d.DepthFeet, Tolerance);
    }

    [Fact]
    public void TemperatureRecord_ExposesCelsiusAndFahrenheit()
    {
        var session = Parse(bw =>
        {
            bw.Write((byte)0x10);
            bw.Write(Ts);
            bw.Write(20f);
        });

        var t = Assert.Single(session.TemperatureRecords);
        Assert.Equal(20f, t.TemperatureCelsius);
        Assert.Equal(68f, t.TemperatureFahrenheit, Tolerance);
    }
}
