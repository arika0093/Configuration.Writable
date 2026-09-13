using System;
using System.IO;
using System.Security.Cryptography;
using Configuration.Writable.FileProvider;

namespace Configuration.Writable;

internal sealed class ConfigurationFileFingerprint : IEquatable<ConfigurationFileFingerprint>
{
    private ConfigurationFileFingerprint(
        bool exists,
        long length,
        long lastWriteTimeUtcTicks,
        string? hash
    )
    {
        Exists = exists;
        Length = length;
        LastWriteTimeUtcTicks = lastWriteTimeUtcTicks;
        Hash = hash;
    }

    private bool Exists { get; }
    private long Length { get; }
    private long LastWriteTimeUtcTicks { get; }
    private string? Hash { get; }

    internal static ConfigurationFileFingerprint? Capture(
        string configFilePath,
        IWritableFileProvider fileProvider
    )
    {
        // Revision tracking is only supported for physical file providers.
        // In-memory and other virtual providers return null to skip optimistic
        // concurrency checks (preserves historical behavior; shared-file section
        // writes via profiled options would otherwise self-conflict).
        if (fileProvider is not IPhysicalFileProvider physicalFileProvider)
        {
            return null;
        }

        try
        {
            if (!fileProvider.FileExists(configFilePath))
            {
                return new ConfigurationFileFingerprint(false, 0, 0, null);
            }

            var pipeReader = fileProvider.GetFilePipeReader(configFilePath);
            if (pipeReader == null)
            {
                return new ConfigurationFileFingerprint(false, 0, 0, null);
            }

            using var stream = pipeReader.AsStream(leaveOpen: false);
            using var memoryStream = new MemoryStream();
            stream.CopyTo(memoryStream);
            var content = memoryStream.ToArray();

            using var hashAlgorithm = SHA256.Create();
            var hash = Convert.ToBase64String(hashAlgorithm.ComputeHash(content));

            var length = content.Length;

            long lastWriteTimeUtcTicks = 0;
            try
            {
                var path = physicalFileProvider.GetPhysicalFilePath(configFilePath);
                if (File.Exists(path))
                {
                    var fileInfo = new FileInfo(path);
                    lastWriteTimeUtcTicks = fileInfo.LastWriteTimeUtc.Ticks;
                }
            }
            catch
            {
                // Ignore errors when getting file info
            }

            return new ConfigurationFileFingerprint(true, length, lastWriteTimeUtcTicks, hash);
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    internal string ToRevision() =>
        string.Concat(
            Exists ? "1" : "0",
            ":",
            Length.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ":",
            LastWriteTimeUtcTicks.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ":",
            Hash ?? ""
        );

    public bool Equals(ConfigurationFileFingerprint? other) =>
        other != null
        && Exists == other.Exists
        && Length == other.Length
        && LastWriteTimeUtcTicks == other.LastWriteTimeUtcTicks
        && string.Equals(Hash, other.Hash, StringComparison.Ordinal);

    public override bool Equals(object? obj) => Equals(obj as ConfigurationFileFingerprint);

    public override int GetHashCode()
    {
        unchecked
        {
            var hashCode = Exists ? 1 : 0;
            hashCode = (hashCode * 397) ^ Length.GetHashCode();
            hashCode = (hashCode * 397) ^ LastWriteTimeUtcTicks.GetHashCode();
            hashCode = (hashCode * 397) ^ (Hash?.GetHashCode() ?? 0);
            return hashCode;
        }
    }
}
