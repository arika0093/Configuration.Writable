---
sidebar_position: 3
---

# InstanceName (Multiple Instances)

Configuration.Writable allows you to manage multiple configurations of the same settings type by using different instance names.

## Overview

When you need multiple configurations of the same type (e.g., multiple database connections, multiple API endpoints), use `InstanceName` to distinguish between them.

## Basic Usage

### Registration

Register multiple instances with different names:

```csharp
// First instance
builder.Services.AddWritableOptions<DatabaseSettings>("Primary", conf =>
{
    conf.UseFile("primary-db.json");
});

// Second instance
builder.Services.AddWritableOptions<DatabaseSettings>("Secondary", conf =>
{
    conf.UseFile("secondary-db.json");
});
```

### Access with Named Interfaces

Use `IReadOnlyNamedOptions<T>` or `IWritableNamedOptions<T>`:

```csharp
public class DatabaseService(IWritableNamedOptions<DatabaseSettings> options)
{
    public async Task UpdatePrimaryAsync()
    {
        // Get specific instance
        var primarySettings = options.Get("Primary");
        Console.WriteLine($"Primary DB: {primarySettings.ConnectionString}");
        
        // Save to specific instance
        await options.SaveAsync("Primary", setting =>
        {
            setting.ConnectionString = "new-connection-string";
        });
    }
    
    public async Task UpdateSecondaryAsync()
    {
        var secondarySettings = options.Get("Secondary");
        await options.SaveAsync("Secondary", setting =>
        {
            setting.MaxConnections = 50;
        });
    }
}
```

### Access with Keyed Services

Alternatively, use `[FromKeyedServices]` attribute:

```csharp
public class DatabaseService(
    [FromKeyedServices("Primary")] IWritableOptions<DatabaseSettings> primaryOptions,
    [FromKeyedServices("Secondary")] IWritableOptions<DatabaseSettings> secondaryOptions
)
{
    public async Task UpdateAsync()
    {
        var primarySettings = primaryOptions.CurrentValue;
        var secondarySettings = secondaryOptions.CurrentValue;
        
        await primaryOptions.SaveAsync(s => s.ConnectionString = "new-primary");
        await secondaryOptions.SaveAsync(s => s.ConnectionString = "new-secondary");
    }
}
```

### GetSpecifiedInstance Helper

Get a dedicated interface for a specific instance:

```csharp
public class DatabaseService(IWritableNamedOptions<DatabaseSettings> options)
{
    private readonly IWritableOptions<DatabaseSettings> _primaryOptions;
    
    public DatabaseService(IWritableNamedOptions<DatabaseSettings> options)
    {
        // Get dedicated interface for "Primary" instance
        _primaryOptions = options.GetSpecifiedInstance("Primary");
    }
    
    public async Task UpdateAsync()
    {
        // Use like a regular IWritableOptions
        var settings = _primaryOptions.CurrentValue;
        await _primaryOptions.SaveAsync(s => s.Timeout = 30);
    }
}
```

## Complete Example

```csharp
using Configuration.Writable;

var builder = Host.CreateApplicationBuilder(args);

// Register multiple database configurations
builder.Services.AddWritableOptions<DatabaseSettings>("Primary", conf =>
{
    conf.UseFile("config/primary-db.json");
});

builder.Services.AddWritableOptions<DatabaseSettings>("Secondary", conf =>
{
    conf.UseFile("config/secondary-db.json");
});

builder.Services.AddWritableOptions<DatabaseSettings>("Cache", conf =>
{
    conf.UseFile("config/cache-db.json");
});

builder.Services.AddSingleton<DatabaseManager>();

var app = builder.Build();
app.Run();

// Settings class
public partial class DatabaseSettings : IOptionsModel<DatabaseSettings>
{
    public string ConnectionString { get; set; } = "";
    public int MaxConnections { get; set; } = 100;
    public int Timeout { get; set; } = 30;
}

// Service using multiple instances
public class DatabaseManager(IWritableNamedOptions<DatabaseSettings> dbOptions)
{
    public async Task InitializeAsync()
    {
        // Access all instances
        var primary = dbOptions.Get("Primary");
        var secondary = dbOptions.Get("Secondary");
        var cache = dbOptions.Get("Cache");
        
        Console.WriteLine($"Primary: {primary.ConnectionString}");
        Console.WriteLine($"Secondary: {secondary.ConnectionString}");
        Console.WriteLine($"Cache: {cache.ConnectionString}");
    }
    
    public async Task FailoverAsync()
    {
        // Update primary to point to secondary
        var secondarySettings = dbOptions.Get("Secondary");
        await dbOptions.SaveAsync("Primary", primary =>
        {
            primary.ConnectionString = secondarySettings.ConnectionString;
        });
    }
}
```

## With Direct Instance Registration

You can also use `RegisterInstanceToContainer` with named instances:

```csharp
builder.Services.AddWritableOptions<DatabaseSettings>("Primary", conf =>
{
    conf.UseFile("primary-db.json");
    conf.RegisterInstanceToContainer = true;
});

builder.Services.AddWritableOptions<DatabaseSettings>("Secondary", conf =>
{
    conf.UseFile("secondary-db.json");
    conf.RegisterInstanceToContainer = true;
});

// Inject directly
public class DatabaseService(
    [FromKeyedServices("Primary")] DatabaseSettings primaryDb,
    [FromKeyedServices("Secondary")] DatabaseSettings secondaryDb
)
{
    public void Use()
    {
        Console.WriteLine($"Primary: {primaryDb.ConnectionString}");
        Console.WriteLine($"Secondary: {secondaryDb.ConnectionString}");
    }
}
```

## Change Detection

Monitor changes to specific instances:

```csharp
public class DatabaseMonitor(IWritableNamedOptions<DatabaseSettings> options) : IDisposable
{
    private readonly List<IDisposable> _disposables = new();
    
    public void StartMonitoring()
    {
        // Monitor specific instance
        var primaryWatcher = options.OnChange("Primary", settings =>
        {
            Console.WriteLine($"Primary DB changed: {settings.ConnectionString}");
        });
        _disposables.Add(primaryWatcher);
        
        var secondaryWatcher = options.OnChange("Secondary", settings =>
        {
            Console.WriteLine($"Secondary DB changed: {settings.ConnectionString}");
        });
        _disposables.Add(secondaryWatcher);
    }
    
    public void Dispose()
    {
        foreach (var disposable in _disposables)
        {
            disposable.Dispose();
        }
    }
}
```

## Without DI

:::note
When using `WritableOptions` without DI, managing multiple configurations is intentionally not supported to keep the API simple.

For multiple configurations, use the DI-based approach with `AddWritableOptions`.
:::

## Use Cases

Named instances are useful for:

- **Multiple Databases**: Primary, secondary, cache databases
- **Multiple APIs**: Different API endpoints with different configurations
- **Multi-Tenant**: Per-tenant settings
- **Environment Tiers**: Development, staging, production configs
- **Per-User Settings**: Individual user preferences

## Best Practices

1. **Use Descriptive Names**: "Primary", "Secondary", not "Config1", "Config2"
2. **Consistent Naming**: Use the same names throughout the application
3. **Document Instance Names**: Make it clear which instance is which
4. **Consider Defaults**: Use unnamed instance for the primary configuration

## Comparison

### Single Instance (Default)

```csharp
builder.Services.AddWritableOptions<AppSettings>();

public class MyService(IWritableOptions<AppSettings> options) { }
```

### Multiple Named Instances

```csharp
builder.Services.AddWritableOptions<AppSettings>("First", conf => { });
builder.Services.AddWritableOptions<AppSettings>("Second", conf => { });

public class MyService(IWritableNamedOptions<AppSettings> options)
{
    var first = options.Get("First");
    var second = options.Get("Second");
}
```

## Next Steps

- [Dynamic Options](./dynamic-options) - Add/remove instances at runtime
- [RegisterInstanceToContainer](../customization/register-instance) - Direct instance injection
- [Testing](./testing) - Test with named instances
