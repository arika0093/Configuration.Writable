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

    internal static ConfigurationFileFingerprint? Capture(IWritableOptionsConfiguration options)
    {
        if (!(options.FileProvider is CommonFileProvider))
        {
            return null;
        }

        try
        {
            var path = Path.GetFullPath(options.ConfigFilePath);
            if (!File.Exists(path))
            {
                return new ConfigurationFileFingerprint(false, 0, 0, null);
            }

            var fileInfo = new FileInfo(path);
            using var stream = new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete
            );
            using var hashAlgorithm = SHA256.Create();
            var hash = Convert.ToBase64String(hashAlgorithm.ComputeHash(stream));
            return new ConfigurationFileFingerprint(
                true,
                fileInfo.Length,
                fileInfo.LastWriteTimeUtc.Ticks,
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
