using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Configuration.Writable;

internal static class AsyncFileSaveLock
{
    private const long LockOffset = 0;
    private const long LockLength = 1;
    private static readonly TimeSpan LockRetryDelay = TimeSpan.FromMilliseconds(50);
    private static readonly ConcurrentDictionary<string, Entry> Entries = new(
        Path.DirectorySeparatorChar == '\\'
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal
    );

    internal static async Task<IDisposable> AcquireAsync(
        string configFilePath,
        CancellationToken cancellationToken
    )
    {
        var key = NormalizePath(configFilePath);
        var entry = AddReference(key);
        try
        {
            await entry.Semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                var sidecarLockStream = await AcquireSidecarLockAsync(key, cancellationToken)
                    .ConfigureAwait(false);
                return new Releaser(key, entry, sidecarLockStream);
            }
            catch
            {
                entry.Semaphore.Release();
                throw;
            }
        }
        catch
        {
            ReleaseReference(key, entry);
            throw;
        }
    }

    private static async Task<FileStream> AcquireSidecarLockAsync(
        string normalizedConfigFilePath,
        CancellationToken cancellationToken
    )
    {
        var sidecarPath = normalizedConfigFilePath + ".lock";
        var directory = Path.GetDirectoryName(sidecarPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            FileStream? stream = null;
            try
            {
                stream = new FileStream(
                    sidecarPath,
                    FileMode.OpenOrCreate,
                    FileAccess.ReadWrite,
                    FileShare.ReadWrite
                );
                Lock(stream);
                return stream;
            }
            catch (IOException)
            {
                if (stream != null)
                {
                    await DisposeStreamAsync(stream).ConfigureAwait(false);
                }
                await Task.Delay(LockRetryDelay, cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                if (stream != null)
                {
                    await DisposeStreamAsync(stream).ConfigureAwait(false);
                }
                throw;
            }
        }
    }

    private static void Lock(FileStream stream)
    {
#if NETSTANDARD2_0
        stream.Lock(LockOffset, LockLength);
#else
        if (!OperatingSystem.IsMacOS())
        {
            stream.Lock(LockOffset, LockLength);
        }
#endif
    }

    private static Task DisposeStreamAsync(FileStream stream)
    {
#if NETSTANDARD2_0
        stream.Dispose();
        return Task.CompletedTask;
#else
        return stream.DisposeAsync().AsTask();
#endif
    }

    private static Entry AddReference(string key)
    {
        while (true)
        {
            var entry = Entries.GetOrAdd(key, _ => new Entry());
            lock (entry.SyncRoot)
            {
                if (!entry.Removed && Entries.TryGetValue(key, out var current) && current == entry)
                {
                    entry.ReferenceCount++;
                    return entry;
                }
            }
        }
    }

    private static void ReleaseReference(string key, Entry entry)
    {
        lock (entry.SyncRoot)
        {
            entry.ReferenceCount--;
            if (entry.ReferenceCount != 0)
            {
                return;
            }

            entry.Removed = true;
            ((ICollection<KeyValuePair<string, Entry>>)Entries).Remove(
                new KeyValuePair<string, Entry>(key, entry)
            );
            entry.Semaphore.Dispose();
        }
    }

    private static string NormalizePath(string path)
    {
        try
        {
            return Path.GetFullPath(path);
        }
        catch (ArgumentException)
        {
            return path;
        }
        catch (NotSupportedException)
        {
            return path;
        }
        catch (IOException)
        {
            return path;
        }
    }

    private sealed class Entry
    {
        internal object SyncRoot { get; } = new();
        internal SemaphoreSlim Semaphore { get; } = new(1, 1);
        internal int ReferenceCount { get; set; }
        internal bool Removed { get; set; }
    }

    private sealed class Releaser(string key, Entry entry, FileStream sidecarLockStream)
        : IDisposable
    {
        private Entry? _entry = entry;
        private FileStream? _sidecarLockStream = sidecarLockStream;

        public void Dispose()
        {
            var entryToRelease = Interlocked.Exchange(ref _entry, null);
            if (entryToRelease == null)
            {
                return;
            }

            var stream = Interlocked.Exchange(ref _sidecarLockStream, null);
            try
            {
                if (stream != null)
                {
                    Unlock(stream);
                }
            }
            finally
            {
                stream?.Dispose();
                // Keeping the sidecar prevents concurrent processes from locking different inodes after deletion.
                entryToRelease.Semaphore.Release();
                ReleaseReference(key, entryToRelease);
            }
        }

        private static void Unlock(FileStream stream)
        {
#if NETSTANDARD2_0
            stream.Unlock(LockOffset, LockLength);
#else
            if (!OperatingSystem.IsMacOS())
            {
                stream.Unlock(LockOffset, LockLength);
            }
#endif
        }
    }
}
