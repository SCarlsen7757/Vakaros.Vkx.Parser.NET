namespace Vakaros.Vkx.Parser.NET.Models;

/// <summary>
/// 0x10 — Temperature. Reading from a transducer.
/// Only present when the sensor is attached to the device.
/// </summary>
public record TemperatureRecord : VkxRecord
{
    /// <inheritdoc/>
    public override RecordType Type => RecordType.Temperature;

    /// <summary>UTC timestamp of the measurement.</summary>
    public DateTimeOffset Timestamp { get; init; }

    /// <summary>Temperature as recorded, in degrees Celsius.</summary>
    internal float RawTemperature { get; init; }

    /// <summary>Temperature in degrees Celsius (SI-derived).</summary>
    public float TemperatureCelsius => RawTemperature;

    /// <summary>Temperature in degrees Fahrenheit (imperial).</summary>
    public float TemperatureFahrenheit => UnitConversions.CelsiusToFahrenheit(RawTemperature);
}
