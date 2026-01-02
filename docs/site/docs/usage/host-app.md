---
sidebar_position: 2
---

# Host Application (With DI)

If you are using Dependency Injection (for example, in ASP.NET Core, Blazor, Worker Service, etc.), register `IReadOnlyOptions<T>` and `IWritableOptions<T>` in the DI container.

## Basic Setup

### Register Settings

Call `AddWritableOptions<T>` to register the settings class in your `Program.cs`:

```csharp
using Configuration.Writable;

var builder = Host.CreateApplicationBuilder(args);

// Register writable options
builder.Services.AddWritableOptions<UserSetting>();

var app = builder.Build();
app.Run();
```

### Read-Only Access

Inject `IReadOnlyOptions<T>` when you only need to read settings:

```csharp
public class ConfigReadService(IReadOnlyOptions<UserSetting> options)
{
    public void Print()
    {
        // Get the UserSetting instance
        var setting = options.CurrentValue;
        Console.WriteLine($"Name: {setting.Name}");
        Console.WriteLine($"Age: {setting.Age}");
    }
}
```

### Read-Write Access

Inject `IWritableOptions<T>` when you need to both read and write settings:

```csharp
public class ConfigService(IWritableOptions<UserSetting> options)
{
    public async Task UpdateNameAsync(string newName)
    {
        await options.SaveAsync(setting =>
        {
            setting.Name = newName;
        });
    }
    
    public string GetCurrentName()
    {
        return options.CurrentValue.Name;
    }
}
```

## Standard Options Interfaces

You can also use the standard Microsoft.Extensions.Options interfaces:

```csharp
// IOptions<T> - snapshot at startup
public class MyService(IOptions<UserSetting> options)
{
    public void UseOptions()
    {
        var setting = options.Value;
        Console.WriteLine($"Name: {setting.Name}");
    }
}

// IOptionsSnapshot<T> - scoped, refreshed per request
public class MyScopedService(IOptionsSnapshot<UserSetting> options)
{
    public void UseOptions()
    {
        var setting = options.Value;
        Console.WriteLine($"Name: {setting.Name}");
    }
}

// IOptionsMonitor<T> - singleton, always current
public class MyMonitorService(IOptionsMonitor<UserSetting> options)
{
    public void UseOptions()
    {
        var setting = options.CurrentValue;
        Console.WriteLine($"Name: {setting.Name}");
    }
}
```

## Worker Service Example

Here's a complete Worker Service example:

```csharp
// Program.cs
using Configuration.Writable;

var builder = Host.CreateApplicationBuilder(args);

// Register writable options
builder.Services.AddWritableOptions<SampleSetting>();

// Register worker
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();

// Worker.cs
public class Worker(
    ILogger<Worker> logger,
    IWritableOptions<SampleSetting> options
) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Watch for changes
        var disposable = options.OnChange(setting =>
        {
            logger.LogInformation("Settings changed: Name={Name}", setting.Name);
        });
        
        while (!stoppingToken.IsCancellationRequested)
        {
            var setting = options.CurrentValue;
            logger.LogInformation("Current Name: {Name}", setting.Name);
            
            // Update settings periodically
            await options.SaveAsync(s =>
            {
                s.LastChecked = DateTime.Now;
            });
            
            await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
        }
        
        disposable.Dispose();
    }
}

// SampleSetting.cs
public partial class SampleSetting : IOptionsModel<SampleSetting>
{
    public string Name { get; set; } = "Worker Service";
    public DateTime LastChecked { get; set; } = DateTime.Now;
}
```

## Custom Configuration

You can customize settings during registration:

```csharp
builder.Services.AddWritableOptions<UserSetting>(conf =>
{
    // Save to standard config directory
    // Windows: %APPDATA%/MyApp
    // macOS: ~/Library/Application Support/MyApp
    // Linux: ~/.config/MyApp
    conf.UseStandardSaveDirectory("MyApp")
        .AddFilePath("settings.json");
    
    // Enable logging (automatically uses ILogger from DI)
    // No manual setup needed!
});
```

## Next Steps

- [ASP.NET Core](./aspnet-core) - Use with ASP.NET Core apps
- [Change Detection](../customization/change-detection) - Monitor configuration changes
- [Validation](../customization/validation) - Validate settings before saving
