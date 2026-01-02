---
sidebar_position: 5
---

# Change Detection

Configuration.Writable automatically detects when configuration files are changed externally and provides notifications.

## Watching for Changes

Register a callback to be notified when settings change:

```csharp
public class MyService(IWritableOptions<UserSetting> options) : IDisposable
{
    private readonly IDisposable _changeToken;
    
    public MyService()
    {
        // Register change callback
        _changeToken = options.OnChange(newSetting =>
        {
            Console.WriteLine($"Settings changed!");
            Console.WriteLine($"  Name: {newSetting.Name}");
            Console.WriteLine($"  Age: {newSetting.Age}");
        });
    }
    
    public void Dispose() => _changeToken?.Dispose();
}
```

## Change Sources

Changes are detected from multiple sources:

1. **SaveAsync calls** - When you save settings programmatically
2. **External file edits** - When the file is edited by another process
3. **File watcher events** - File system notifications

## Throttling

By default, change notifications are throttled to prevent high-frequency updates:

```csharp
// Default: 1000ms throttle
builder.Services.AddWritableOptions<UserSetting>(conf =>
{
    conf.OnChangeThrottleMs = 1000; // Wait 1 second after change
});
```

### Custom Throttle

Adjust throttling to your needs:

```csharp
// More responsive (500ms)
conf.OnChangeThrottleMs = 500;

// No throttling (immediate notifications)
conf.OnChangeThrottleMs = 0;

// Longer throttle (5 seconds)
conf.OnChangeThrottleMs = 5000;
```

## Named Instance Changes

When using named instances, you can watch specific instances:

```csharp
public class MyService(IWritableNamedOptions<UserSetting> options)
{
    public void WatchFirst()
    {
        // Watch only the "First" instance
        options.OnChange("First", setting =>
        {
            Console.WriteLine($"First instance changed: {setting.Name}");
        });
    }
}
```

Or use `IOptionsMonitor<T>` for unified change detection:

```csharp
public class MyService(IOptionsMonitor<UserSetting> options)
{
    public void WatchAll()
    {
        // Watch all instances (named and unnamed)
        options.OnChange((setting, name) =>
        {
            var instanceName = name ?? "unnamed";
            Console.WriteLine($"{instanceName} changed: {setting.Name}");
        });
    }
}
```

## Example: Real-Time Updates

```csharp
public class ConfigMonitorService : BackgroundService
{
    private readonly IWritableOptions<AppSettings> _options;
    private readonly ILogger<ConfigMonitorService> _logger;
    private IDisposable? _changeToken;
    
    public ConfigMonitorService(
        IWritableOptions<AppSettings> options,
        ILogger<ConfigMonitorService> logger)
    {
        _options = options;
        _logger = logger;
    }
    
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Watch for changes
        _changeToken = _options.OnChange(newSettings =>
        {
            _logger.LogInformation(
                "Configuration changed: {FeatureEnabled}", 
                newSettings.FeatureEnabled);
            
            // React to changes
            ApplyNewConfiguration(newSettings);
        });
        
        return Task.CompletedTask;
    }
    
    private void ApplyNewConfiguration(AppSettings settings)
    {
        // Update application behavior based on new settings
    }
    
    public override void Dispose()
    {
        _changeToken?.Dispose();
        base.Dispose();
    }
}
```

## File Watching

Configuration.Writable uses `PhysicalFileProvider` when available to watch for file system changes. This works automatically in most scenarios.

### When File Watching Works

- ✅ Local file system
- ✅ Windows, macOS, Linux
- ✅ Normal file edits
- ✅ ASP.NET Core applications
- ✅ Worker Services

### Limitations

- ❌ Network drives (may not support file watching)
- ❌ Some container environments
- ❌ Read-only file systems

In scenarios where file watching isn't available, changes made via `SaveAsync` are still detected and reflected.

## Manual Refresh

While not typically needed, you can manually trigger a refresh:

```csharp
// Access CurrentValue to get the latest settings
var current = options.CurrentValue;
```

The library handles caching and refresh automatically.

## Next Steps

- [Validation](./validation) - Validate settings before saving
- [Logging](./logging) - Configure logging for change events
- [Usage Examples](../usage/host-app) - See change detection in action
