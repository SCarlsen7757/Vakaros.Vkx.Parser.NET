using Vakaros.Vkx.Parser.NET.Models;

namespace Vakaros.Vkx.Parser.NET;

/// <summary>
/// Represents a fully parsed VKX log file. All records are available via <see cref="Records"/>;
/// per-type convenience properties allow direct access to each record category.
/// </summary>
/// <remarks>
/// Records are grouped by type once, when the session is created, so the per-type properties
/// are cheap to read repeatedly and support <c>Count</c> and indexing.
/// </remarks>
public sealed class VkxSession
{
    private readonly List<VkxRecord> _records;
    private readonly List<PositionRecord> _positions = [];
    private readonly List<DeclinationRecord> _declinations = [];
    private readonly List<WindRecord> _wind = [];
    private readonly List<SpeedThroughWaterRecord> _speedThroughWater = [];
    private readonly List<DepthRecord> _depth = [];
    private readonly List<TemperatureRecord> _temperature = [];
    private readonly List<LoadRecord> _load = [];
    private readonly List<RaceTimerEventRecord> _raceTimerEvents = [];
    private readonly List<LinePositionRecord> _linePositions = [];
    private readonly List<ShiftAngleRecord> _shiftAngles = [];
    private readonly List<DeviceConfigurationRecord> _deviceConfigurations = [];

    internal VkxSession(byte formatVersion, List<VkxRecord> records, bool isPartial = false)
    {
        FormatVersion = formatVersion;
        _records = records;
        IsPartial = isPartial;

        foreach (var record in records)
        {
            switch (record)
            {
                case PositionRecord r: _positions.Add(r); break;
                case DeclinationRecord r: _declinations.Add(r); break;
                case WindRecord r: _wind.Add(r); break;
                case SpeedThroughWaterRecord r: _speedThroughWater.Add(r); break;
                case DepthRecord r: _depth.Add(r); break;
                case TemperatureRecord r: _temperature.Add(r); break;
                case LoadRecord r: _load.Add(r); break;
                case RaceTimerEventRecord r: _raceTimerEvents.Add(r); break;
                case LinePositionRecord r: _linePositions.Add(r); break;
                case ShiftAngleRecord r: _shiftAngles.Add(r); break;
                case DeviceConfigurationRecord r: _deviceConfigurations.Add(r); break;
            }
        }
    }

    /// <summary>VKX format version number read from the first page header in the file.
    /// See the revision history in the VKX format specification.
    /// </summary>
    public byte FormatVersion { get; }

    /// <summary>
    /// The VKX specification revision this file was written against, derived from
    /// <see cref="FormatVersion"/>. Returns <see cref="VkxSpecVersion.Unknown"/> when
    /// the format version byte does not match any known revision.
    /// </summary>
    public VkxSpecVersion SpecVersion => VkxFormatVersion.ToSpecVersion(FormatVersion);

    /// <summary>
    /// <see langword="true"/> when parsing stopped early and the session contains only the records
    /// collected up to the stopping point. This happens in two cases:
    /// <list type="bullet">
    ///   <item>The file uses a format version newer than <see cref="VkxFormatVersion.MaxKnown"/> and
    ///   an unrecognised record key was encountered.</item>
    ///   <item>The stream ended mid-row (e.g. a file that was still being written when copied).</item>
    /// </list>
    /// </summary>
    public bool IsPartial { get; }

    /// <summary>All parsed records in file order, including page headers and terminators.</summary>
    public IReadOnlyList<VkxRecord> Records => _records;

    // ── Telemetry ───────────────────────────────────────────────────────────

    /// <summary>Position, Velocity, and Orientation records (0x02), in file order.</summary>
    public IReadOnlyList<PositionRecord> PositionRecords => _positions;

    /// <summary>Declination records (0x03), in file order.</summary>
    public IReadOnlyList<DeclinationRecord> DeclinationRecords => _declinations;

    /// <summary>
    /// Wind records (0x0A), in file order. Only populated when a Calypso Wind Sensor was attached.
    /// </summary>
    public IReadOnlyList<WindRecord> WindRecords => _wind;

    /// <summary>
    /// Speed Through Water records (0x0B), in file order. Only populated when a transducer was attached.
    /// </summary>
    public IReadOnlyList<SpeedThroughWaterRecord> SpeedThroughWaterRecords => _speedThroughWater;

    /// <summary>
    /// Depth records (0x0C), in file order. Only populated when a transducer was attached.
    /// </summary>
    public IReadOnlyList<DepthRecord> DepthRecords => _depth;

    /// <summary>
    /// Temperature records (0x10), in file order. Only populated when a transducer was attached.
    /// </summary>
    public IReadOnlyList<TemperatureRecord> TemperatureRecords => _temperature;

    /// <summary>
    /// Load records (0x0F), in file order. Only populated when a Cyclops load cell was attached.
    /// </summary>
    public IReadOnlyList<LoadRecord> LoadRecords => _load;

    // ── Race / Navigation ───────────────────────────────────────────────────

    /// <summary>Race Timer Event records (0x04), in file order.</summary>
    public IReadOnlyList<RaceTimerEventRecord> RaceTimerEventRecords => _raceTimerEvents;

    /// <summary>Start line position records (0x05), in file order.</summary>
    public IReadOnlyList<LinePositionRecord> LinePositionRecords => _linePositions;

    /// <summary>Shift Angle records (0x06), in file order.</summary>
    public IReadOnlyList<ShiftAngleRecord> ShiftAngleRecords => _shiftAngles;

    // ── System ──────────────────────────────────────────────────────────────

    /// <summary>Device Configuration records (0x08), in file order.</summary>
    public IReadOnlyList<DeviceConfigurationRecord> DeviceConfigurationRecords => _deviceConfigurations;
}
