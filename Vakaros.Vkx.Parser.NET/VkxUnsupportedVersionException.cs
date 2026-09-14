namespace Vakaros.Vkx.Parser.NET;

/// <summary>
/// Thrown when a VKX file declares a format version this library does not parse.
/// </summary>
/// <remarks>
/// Distinct from <see cref="FormatException"/>, which signals corrupt data. Consumers that
/// validate files before parsing may throw this exception themselves so both paths report
/// unsupported versions the same way.
/// </remarks>
public sealed class VkxUnsupportedVersionException : NotSupportedException
{
    /// <summary>Creates the exception for the given format-version byte.</summary>
    /// <param name="formatVersion">The format-version byte read from the page header.</param>
    public VkxUnsupportedVersionException(byte formatVersion)
        : this(formatVersion,
            $"VKX format version 0x{formatVersion:X2} is not supported. " +
            $"This library parses VKX 1.4 (0x{VkxFormatVersion.MinimumSupported:X2}) and newer.")
    {
    }

    /// <summary>Creates the exception for the given format-version byte with a custom message.</summary>
    /// <param name="formatVersion">The format-version byte read from the page header.</param>
    /// <param name="message">The error message.</param>
    public VkxUnsupportedVersionException(byte formatVersion, string message)
        : base(message)
    {
        FormatVersion = formatVersion;
    }

    /// <summary>The unsupported format-version byte.</summary>
    public byte FormatVersion { get; }
}
