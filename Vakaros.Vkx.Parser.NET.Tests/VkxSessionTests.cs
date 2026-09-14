using System.Reflection;
using System.Text;
using Vakaros.Vkx.Parser.NET.Models;
using Xunit;

namespace Vakaros.Vkx.Parser.NET.Tests;

/// <summary>Tests for the per-type record lists and cancellation support.</summary>
public class VkxSessionTests
{
    private const ulong Ts = 1_000_000_000_000UL;

    private static byte[] BuildWindFile(int windRecords)
    {
        using var ms = new MemoryStream();
        using (var bw = new BinaryWriter(ms, Encoding.ASCII, leaveOpen: true))
        {
            bw.Write((byte)0xFF);
            bw.Write(VkxFormatVersion.V1_4);
            bw.Write(new byte[6]);
            for (var i = 0; i < windRecords; i++)
            {
                bw.Write((byte)0x0A);
                bw.Write(Ts + (ulong)i);
                bw.Write(90f);
                bw.Write(4f);
            }
        }
        return ms.ToArray();
    }

    private static Stream OpenRealFile()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var name = assembly.GetManifestResourceNames().Single(n => n.EndsWith(".vkx", StringComparison.OrdinalIgnoreCase));
        return assembly.GetManifestResourceStream(name)!;
    }

    [Fact]
    public void PerTypeLists_MatchRecordsInFileOrder()
    {
        using var stream = OpenRealFile();
        var session = VkxParser.Parse(stream);

        Assert.Equal(session.Records.OfType<PositionRecord>(), session.PositionRecords);
        Assert.Equal(session.Records.OfType<DeclinationRecord>(), session.DeclinationRecords);
        Assert.Equal(session.Records.OfType<WindRecord>(), session.WindRecords);
        Assert.Equal(session.Records.OfType<SpeedThroughWaterRecord>(), session.SpeedThroughWaterRecords);
        Assert.Equal(session.Records.OfType<DepthRecord>(), session.DepthRecords);
        Assert.Equal(session.Records.OfType<TemperatureRecord>(), session.TemperatureRecords);
        Assert.Equal(session.Records.OfType<LoadRecord>(), session.LoadRecords);
        Assert.Equal(session.Records.OfType<RaceTimerEventRecord>(), session.RaceTimerEventRecords);
        Assert.Equal(session.Records.OfType<LinePositionRecord>(), session.LinePositionRecords);
        Assert.Equal(session.Records.OfType<ShiftAngleRecord>(), session.ShiftAngleRecords);
        Assert.Equal(session.Records.OfType<DeviceConfigurationRecord>(), session.DeviceConfigurationRecords);
    }

    [Fact]
    public void PerTypeLists_SupportCountAndIndexing()
    {
        var session = VkxParser.Parse(BuildWindFile(3));

        Assert.Equal(3, session.WindRecords.Count);
        Assert.Equal(DateTimeOffset.FromUnixTimeMilliseconds((long)Ts + 2), session.WindRecords[2].Timestamp);
        Assert.Empty(session.PositionRecords);
    }

    [Fact]
    public void CancelledToken_ThrowsBeforeReading()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        Assert.Throws<OperationCanceledException>(() => VkxParser.Parse(BuildWindFile(1), cts.Token));
    }

    [Fact]
    public void CancellationDuringParse_Throws()
    {
        using var cts = new CancellationTokenSource();
        using var stream = new CancelAfterBytesStream(BuildWindFile(10_000), cancelAfterBytes: 1_000, cts);

        Assert.Throws<OperationCanceledException>(() => VkxParser.Parse(stream, cts.Token));
    }

    [Fact]
    public void ParseFile_WithToken_Parses()
    {
        var path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".vkx");
        try
        {
            File.WriteAllBytes(path, BuildWindFile(2));
            var session = VkxParser.ParseFile(path, CancellationToken.None);
            Assert.Equal(2, session.WindRecords.Count);
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>Cancels the token once the given number of bytes has been read.</summary>
    private sealed class CancelAfterBytesStream(byte[] data, int cancelAfterBytes, CancellationTokenSource cts) : MemoryStream(data)
    {
        public override int ReadByte()
        {
            if (Position >= cancelAfterBytes) cts.Cancel();
            return base.ReadByte();
        }
    }
}
