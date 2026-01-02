---
sidebar_position: 7
---

# Logging

Configuration.Writable supports logging through Microsoft.Extensions.Logging for visibility into operations.

## With DI

When using Dependency Injection, logging is automatically configured:

```csharp
builder.Services.AddWritableOptions<UserSetting>(conf =>
{
    // Logging is automatically enabled using ILogger from DI
    // No additional setup required
});
```

The logger uses the category name `"Configuration.Writable"` by default.

## Without DI

When not using DI, you need to configure logging manually:

```csharp
using Microsoft.Extensions.Logging;
using Configuration.Writable;

WritableOptions.Initialize<UserSetting>(conf =>
{
    conf.Logger = LoggerFactory
        .Create(builder => builder.AddConsole())
        .CreateLogger("Configuration.Writable");
});
```

### With Different Providers

```csharp
// Console logging
conf.Logger = LoggerFactory
    .Create(builder => builder.AddConsole())
    .CreateLogger("Configuration.Writable");

// Debug logging
conf.Logger = LoggerFactory
    .Create(builder => builder.AddDebug())
    .CreateLogger("Configuration.Writable");

// Multiple providers
conf.Logger = LoggerFactory
    .Create(builder =>
    {
        builder.AddConsole();
        builder.AddDebug();
        builder.SetMinimumLevel(LogLevel.Information);
    })
    .CreateLogger("Configuration.Writable");
```

## Log Messages

### Information Level

At `LogLevel.Information`, you'll see:

```
info: Configuration.Writable[0]
      Configuration file change detected: settings.json (Renamed)
      
info: Configuration.Writable[0]
      Configuration saved successfully to settings.json
```

### Debug Level

At `LogLevel.Debug`, you'll see additional details:

```
debug: Configuration.Writable[0]
       Loading configuration from: /path/to/settings.json
       
debug: Configuration.Writable[0]
       File write completed: settings.json
```

### Warning Level

Warnings for non-critical issues:

```
warn: Configuration.Writable[0]
      Retry attempt 2/3 for file access: settings.json
```

### Error Level

Errors for failures:

```
error: Configuration.Writable[0]
       Failed to save configuration to settings.json after 3 attempts
```

## Configure Log Level

### In DI

```csharp
// appsettings.json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Configuration.Writable": "Debug"
    }
  }
}
```

Or programmatically:

```csharp
builder.Logging.AddFilter("Configuration.Writable", LogLevel.Debug);
```

### Without DI

```csharp
conf.Logger = LoggerFactory
    .Create(builder =>
    {
        builder.AddConsole();
        builder.AddFilter("Configuration.Writable", LogLevel.Debug);
    })
    .CreateLogger("Configuration.Writable");
```

## Example: Structured Logging

With structured logging providers like Serilog:

```csharp
// Install: Serilog.Extensions.Logging, Serilog.Sinks.Console

using Serilog;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .WriteTo.Console()
    .CreateLogger();

var loggerFactory = LoggerFactory.Create(builder =>
{
    builder.AddSerilog(Log.Logger);
});

WritableOptions.Initialize<UserSetting>(conf =>
{
    conf.Logger = loggerFactory.CreateLogger("Configuration.Writable");
});
```

## Disable Logging

To disable logging:

```csharp
// Without DI
conf.Logger = null;

// With DI - set minimum level to None
builder.Logging.AddFilter("Configuration.Writable", LogLevel.None);
```

## Custom Logger Category

Use a custom category name:

```csharp
// Without DI
conf.Logger = LoggerFactory
    .Create(builder => builder.AddConsole())
    .CreateLogger("MyApp.Settings");

// With DI - the category is determined by the calling code
```

## Example: Complete Logging Setup

```csharp
using Microsoft.Extensions.Logging;
using Configuration.Writable;

var loggerFactory = LoggerFactory.Create(builder =>
{
    builder
        .AddConsole()
        .AddDebug()
        .SetMinimumLevel(LogLevel.Debug)
        .AddFilter("Configuration.Writable", LogLevel.Information);
});

WritableOptions.Initialize<UserSetting>(conf =>
{
    conf.UseFile("settings.json");
    conf.Logger = loggerFactory.CreateLogger("Configuration.Writable");
    conf.FileProvider = new CommonFileProvider()
    {
        MaxRetryCount = 3,
        BackupMaxCount = 5
    };
});

var options = WritableOptions.GetOptions<UserSetting>();

// You'll see logs when saving
await options.SaveAsync(s => s.Name = "New Name");
// Output: info: Configuration.Writable[0]
//         Configuration saved successfully to settings.json
```

## Next Steps

- [File Provider](./file-provider) - Configure file operations
- [Change Detection](./change-detection) - Monitor configuration changes
- [Validation](./validation) - Validate settings
