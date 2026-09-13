using System;
using System.IO;
using System.Security.Cryptography;
using Configuration.Writable.State;

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
        IFileBackend backend
    )
    {
        // Revision tracking requires a physical file backend. Virtual backends
        // report no revision, so optimistic concurrency checks are skipped.
        if (!backend.IsPhysical)
        {
            return null;
        }

        try
        {
            if (!backend.FileExists(configFilePath))
            {
                return new ConfigurationFileFingerprint(false, 0, 0, null);
            }

            using var stream = backend.OpenReadStream(configFilePath);
            if (stream == null)
            {
                return new ConfigurationFileFingerprint(false, 0, 0, null);
            }

            using var memoryStream = new MemoryStream();
            stream.CopyTo(memoryStream);
            var content = memoryStream.ToArray();

            using var hashAlgorithm = SHA256.Create();
            var hash = Convert.ToBase64String(hashAlgorithm.ComputeHash(content));

            long lastWriteTimeUtcTicks = 0;
            try
            {
                var path = backend.GetPhysicalPath(configFilePath);
                if (File.Exists(path))
                {
                    lastWriteTimeUtcTicks = new FileInfo(path).LastWriteTimeUtc.Ticks;
                }
            }
            catch
            {
                // Ignore errors when getting file info
            }

            return new ConfigurationFileFingerprint(
                true,
                content.Length,
                lastWriteTimeUtcTicks,
                hash
            );
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
