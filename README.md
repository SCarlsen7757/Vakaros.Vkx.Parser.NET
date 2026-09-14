# Vakaros.Vkx.Parser.NET

A .NET library for parsing Vakaros VKX binary log files into strongly-typed objects.

[![NuGet](https://img.shields.io/nuget/v/Vakaros.Vkx.Parser.NET.svg)](https://www.nuget.org/packages/Vakaros.Vkx.Parser.NET)

## Installation

```bash
dotnet add package Vakaros.Vkx.Parser.NET
```

## Quick Start

```csharp
using Vakaros.Vkx.Parser.NET;

// From a file path
VkxSession session = VkxParser.ParseFile("session.vkx");

// From a stream
VkxSession session = VkxParser.Parse(stream);

// From a byte array
VkxSession session = VkxParser.Parse(bytes);

// Every overload accepts an optional CancellationToken
VkxSession session = VkxParser.Parse(stream, cancellationToken);
```

## Accessing Records

```csharp
// GPS track
foreach (PositionRecord pos in session.PositionRecords)
    Console.WriteLine($"{pos.Timestamp} lat={pos.Latitude} lon={pos.Longitude} sog={pos.SpeedOverGroundKnots} kn");

// Wind data (only present when a Calypso sensor was attached)
foreach (WindRecord wind in session.WindRecords)
    Console.WriteLine($"{wind.Timestamp} dir={wind.WindDirectionDegrees}° speed={wind.WindSpeedMetresPerSecond} m/s");

// Race timer events
foreach (RaceTimerEventRecord evt in session.RaceTimerEventRecords)
    Console.WriteLine($"{evt.Timestamp} {evt.EventType} timer={evt.TimerValue}s");

// All records in file order
foreach (VkxRecord record in session.Records)
    Console.WriteLine(record.Type);
```

The per-type properties (`PositionRecords`, `WindRecords`, …) are `IReadOnlyList<T>` built once while parsing, so reading them repeatedly, taking `Count` or indexing is cheap.

## Supported Record Types

| Key    | Type                       | Description                                  |
|--------|----------------------------|----------------------------------------------|
| `0x02` | `PositionRecord`           | GPS position, speed, course, orientation     |
| `0x03` | `DeclinationRecord`        | Magnetic declination                         |
| `0x04` | `RaceTimerEventRecord`     | Race timer events (start, reset, sync, etc.) |
| `0x05` | `LinePositionRecord`       | Start line pin/boat end positions            |
| `0x06` | `ShiftAngleRecord`         | Port/starboard tack shift angles             |
| `0x08` | `DeviceConfigurationRecord`| Device configuration                         |
| `0x0A` | `WindRecord`               | Apparent wind (Calypso sensor)               |
| `0x0B` | `SpeedThroughWaterRecord`  | Speed through water (transducer)             |
| `0x0C` | `DepthRecord`              | Water depth (transducer)                     |
| `0x0F` | `LoadRecord`               | Load cell reading (Cyclops sensor)           |
| `0x10` | `TemperatureRecord`        | Water temperature (transducer)               |

## Units

The VKX format does not use one unit system: wind direction and shift-angle heading are recorded in degrees, shift-angle speed in knots, and everything else in SI. To avoid mix-ups, every measured value is exposed with an explicit unit in its name, in two unit systems:

- **SI** — metres, metres per second, radians, degrees Celsius.
- **Imperial / nautical** — feet, knots, degrees, degrees Fahrenheit.

The value as recorded in the file is kept internally, and both public properties are computed from it, so both always agree.

| Record | SI | Imperial / nautical | Recorded in |
|---|---|---|---|
| `PositionRecord` | `SpeedOverGroundMetresPerSecond`, `CourseOverGroundRadians`, `AltitudeMetres` | `SpeedOverGroundKnots`, `CourseOverGroundDegrees`, `AltitudeFeet` | m/s, rad, m |
| `DeclinationRecord` | `DeclinationOffsetRadians` | `DeclinationOffsetDegrees` | rad |
| `ShiftAngleRecord` | `TrueHeadingRadians`, `SpeedOverGroundMetresPerSecond` | `TrueHeadingDegrees`, `SpeedOverGroundKnots` | °, kn |
| `WindRecord` | `WindDirectionRadians`, `WindSpeedMetresPerSecond` | `WindDirectionDegrees`, `WindSpeedKnots` | °, m/s |
| `SpeedThroughWaterRecord` | `ForwardSpeedMetresPerSecond`, `HorizontalSpeedMetresPerSecond` | `ForwardSpeedKnots`, `HorizontalSpeedKnots` | m/s |
| `DepthRecord` | `DepthMetres` | `DepthFeet` | m |
| `TemperatureRecord` | `TemperatureCelsius` | `TemperatureFahrenheit` | °C |

Not converted: latitude and longitude (decimal degrees, WGS-84), `LoadRecord.Load` (the spec defines no unit), quaternion components, timestamps (UTC `DateTimeOffset`), enums and flags.

## Error Handling

| Outcome | Meaning |
|---|---|
| `FormatException` | The data is corrupt: missing or inconsistent page header, an unknown record key or undefined enum value in a known format version, or a timestamp out of range. |
| `VkxUnsupportedVersionException` | The file's format version is older than VKX 1.4. `FormatVersion` holds the version byte. |
| `session.IsPartial == true` | Parsing stopped early: the stream ended mid-row (e.g. a file copied while still being written), or the file uses a newer format version with a record type this library does not know. The records read so far are returned. |

Streams do not need to be seekable; parsing reads from the current position to the end.

## Notes

- Records marked "only present when sensor attached" will return empty collections if the sensor was not connected.
- The VKX format spec is in [`vkx_format.md`](vkx_format.md) (a copy of the [official Vakaros spec](https://github.com/vakaros/vkx)).

## Requirements

- .NET 10.0 or later

## License

MIT — see [LICENSE](LICENSE).
