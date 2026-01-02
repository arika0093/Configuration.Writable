---
sidebar_position: 4
---

# File Provider

The File Provider controls how settings files are read and written. Configuration.Writable includes a default `CommonFileProvider` with robust features.

## CommonFileProvider (Default)

The default file provider includes:

- **Atomic file writing** - Write to temp file, then rename
- **Automatic retry** - Retry on file access failures
- **Backup rotation** - Optional backup file creation
- **Thread-safe** - Uses internal semaphore

## Configuration

Customize the file provider behavior:

```csharp
using Configuration.Writable.FileProvider;

conf.FileProvider = new CommonFileProvider()
{
    // Retry up to 5 times when file access fails
    MaxRetryCount = 5,
    
    // Wait progressively longer before each retry
    RetryDelay = (attempt) => 100 * attempt,
    
    // Keep 5 backup files when saving (default: 0, disabled)
    BackupMaxCount = 5,
};
```

### Retry Behavior

When file access fails (e.g., file locked by another process), the provider will retry:

```csharp
conf.FileProvider = new CommonFileProvider()
{
    MaxRetryCount = 3,  // Retry up to 3 times
    RetryDelay = (attempt) => 100 * attempt,  // 100ms, 200ms, 300ms
};
```

### Backup Files

Enable backup rotation to keep previous versions:

```csharp
conf.FileProvider = new CommonFileProvider()
{
    BackupMaxCount = 5,  // Keep last 5 backups
};
```

Backup files are named: `settings.json.20240101_120000.bak`

### Atomic Writes

All writes are atomic by default:
1. Write to temporary file (`settings.json.tmp`)
2. Rename to target file (`settings.json`)

This ensures the file is never partially written.

## Custom File Provider

Create a custom provider by implementing `IFileProvider`:

```csharp
using Configuration.Writable.FileProvider;

public class MyFileProvider : IFileProvider
{
    public async Task<string?> ReadFileAsync(
        string filePath,
        ILogger? logger,
        CancellationToken cancellationToken = default)
    {
        // Implement custom read logic
        if (!File.Exists(filePath))
            return null;
        
        return await File.ReadAllTextAsync(filePath, cancellationToken);
    }
    
    public async Task WriteFileAsync(
        string filePath,
        string contents,
        ILogger? logger,
        CancellationToken cancellationToken = default)
    {
        // Implement custom write logic
        await File.WriteAllTextAsync(filePath, contents, cancellationToken);
    }
}
```

Then use it:

```csharp
conf.FileProvider = new MyFileProvider();
```

## Examples

### High Reliability Configuration

```csharp
conf.FileProvider = new CommonFileProvider()
{
    MaxRetryCount = 10,
    RetryDelay = (attempt) => 200 * attempt,
    BackupMaxCount = 10,
};
```

### Simple Configuration (No Retries)

```csharp
conf.FileProvider = new CommonFileProvider()
{
    MaxRetryCount = 0,  // Don't retry
    BackupMaxCount = 0, // Don't create backups
};
```

## Thread Safety

`CommonFileProvider` is thread-safe using an internal semaphore. Multiple concurrent writes to the same file will be serialized automatically.

## Next Steps

- [Change Detection](./change-detection) - Monitor file changes
- [Validation](./validation) - Validate before saving
- [Logging](./logging) - Configure logging
