---
sidebar_position: 1
---

# API Reference - Interfaces

Configuration.Writable provides a comprehensive set of interfaces for reading and writing configuration options.

## Interface Hierarchy

```
IOptions<T>
IOptionsSnapshot<T>
IOptionsMonitor<T>
    ↓
IReadOnlyOptionsMonitor<T>
    ↓
IReadOnlyOptions<T> / IReadOnlyNamedOptions<T>
    ↓
IWritableOptionsMonitor<T>
    ↓
IWritableOptions<T> / IWritableNamedOptions<T>
```

## Standard Microsoft Interfaces

These interfaces are from `Microsoft.Extensions.Options` and are fully supported.

### `IOptions<T>`

Provides the value at application startup. Does not reflect configuration changes.

```csharp
public interface IOptions<T> where T : class
{
    T Value { get; }
}
```

**Usage:**
```csharp
public class MyService(IOptions<UserSetting> options)
{
    public void UseSettings()
    {
        var setting = options.Value;
        // Value is cached at startup
    }
}
```

### `IOptionsSnapshot<T>`

Provides the latest value per scope (request in ASP.NET Core).

```csharp
public interface IOptionsSnapshot<T> : IOptions<T> where T : class
{
    T Get(string? name);
}
```

**Usage:**
```csharp
public class MyScopedService(IOptionsSnapshot<UserSetting> options)
{
    public void UseSettings()
    {
        var setting = options.Value;
        // Refreshed per scope
        
        var named = options.Get("MyInstance");
        // Named instance access
    }
}
```

### `IOptionsMonitor<T>`

Provides the latest value at any time with change notifications.

```csharp
public interface IOptionsMonitor<T>
{
    T CurrentValue { get; }
    T Get(string? name);
    IDisposable OnChange(Action<T, string> listener);
}
```

**Usage:**
```csharp
public class MyService(IOptionsMonitor<UserSetting> options) : IDisposable
{
    private readonly IDisposable _changeToken;
    
    public MyService()
    {
        _changeToken = options.OnChange((setting, name) =>
        {
            Console.WriteLine($"Settings changed: {name}");
        });
    }
    
    public void UseSettings()
    {
        var current = options.CurrentValue;
        var named = options.Get("MyInstance");
    }
    
    public void Dispose() => _changeToken?.Dispose();
}
```

## Read-Only Interfaces

### `IReadOnlyOptions<T>`

Simplified read-only options interface without named access.

```csharp
public interface IReadOnlyOptions<T> where T : class
{
    T CurrentValue { get; }
    IDisposable OnChange(Action<T> listener);
    WritableOptionsConfiguration<T> GetOptionsConfiguration();
}
```

**Benefits over `IOptionsMonitor<T>`:**
- Simpler API for unnamed instances
- OnChange callback doesn't require name parameter
- Access to configuration details

**Usage:**
```csharp
public class MyService(IReadOnlyOptions<UserSetting> options)
{
    public void UseSettings()
    {
        var setting = options.CurrentValue;
        
        var config = options.GetOptionsConfiguration();
        Console.WriteLine($"File: {config.ConfigFilePath}");
    }
    
    public void WatchChanges()
    {
        options.OnChange(setting =>
        {
            Console.WriteLine($"Changed: {setting.Name}");
        });
    }
}
```

### `IReadOnlyNamedOptions<T>`

Read-only interface with named instance support.

```csharp
public interface IReadOnlyNamedOptions<T> where T : class
{
    T Get(string name);
    IDisposable OnChange(string name, Action<T> listener);
    IReadOnlyOptions<T> GetSpecifiedInstance(string name);
    WritableOptionsConfiguration<T> GetOptionsConfiguration(string name);
}
```

**Usage:**
```csharp
public class MyService(IReadOnlyNamedOptions<UserSetting> options)
{
    public void UseNamedSettings()
    {
        var first = options.Get("First");
        var second = options.Get("Second");
        
        // Watch specific instance
        options.OnChange("First", setting =>
        {
            Console.WriteLine($"First changed: {setting.Name}");
        });
        
        // Get dedicated interface for an instance
        var firstOptions = options.GetSpecifiedInstance("First");
        var value = firstOptions.CurrentValue;
    }
}
```

### `IReadOnlyOptionsMonitor<T>`

Combined interface providing both unnamed, named, and `IOptionsMonitor<T>` compatibility.

```csharp
public interface IReadOnlyOptionsMonitor<T> : 
    IReadOnlyOptions<T>,
    IReadOnlyNamedOptions<T>,
    IOptionsMonitor<T>
    where T : class
{
}
```

## Writable Interfaces

### `IWritableOptions<T>`

Adds write capability to `IReadOnlyOptions<T>`.

```csharp
public interface IWritableOptions<T> : IReadOnlyOptions<T> where T : class
{
    Task SaveAsync(Action<T> updateAction);
}
```

**Usage:**
```csharp
public class MyService(IWritableOptions<UserSetting> options)
{
    public async Task UpdateSettingsAsync()
    {
        await options.SaveAsync(setting =>
        {
            setting.Name = "New Name";
            setting.UpdatedAt = DateTime.Now;
        });
    }
}
```

### `IWritableNamedOptions<T>`

Adds write capability to `IReadOnlyNamedOptions<T>`.

```csharp
public interface IWritableNamedOptions<T> : IReadOnlyNamedOptions<T> where T : class
{
    Task SaveAsync(string name, Action<T> updateAction);
    IWritableOptions<T> GetSpecifiedInstance(string name);
}
```

**Usage:**
```csharp
public class MyService(IWritableNamedOptions<UserSetting> options)
{
    public async Task UpdateNamedSettingsAsync()
    {
        await options.SaveAsync("First", setting =>
        {
            setting.Name = "First Updated";
        });
        
        await options.SaveAsync("Second", setting =>
        {
            setting.Name = "Second Updated";
        });
        
        // Or get dedicated interface
        var firstOptions = options.GetSpecifiedInstance("First");
        await firstOptions.SaveAsync(s => s.Value++);
    }
}
```

### `IWritableOptionsMonitor<T>`

Combined writable interface with full compatibility.

```csharp
public interface IWritableOptionsMonitor<T> : 
    IWritableOptions<T>,
    IWritableNamedOptions<T>,
    IReadOnlyOptionsMonitor<T>,
    IOptionsMonitor<T>
    where T : class
{
}
```

## Keyed Services Support

All interfaces support .NET's keyed services:

```csharp
// Register with name
builder.Services.AddWritableOptions<UserSetting>("MyInstance", conf =>
{
    conf.UseFile("myinstance.json");
});

// Inject with FromKeyedServices
public class MyService(
    [FromKeyedServices("MyInstance")] IWritableOptions<UserSetting> options
)
{
    public async Task UpdateAsync()
    {
        await options.SaveAsync(s => s.Name = "Updated");
    }
}
```

## Configuration Types

### `WritableOptionsConfiguration<T>`

Contains all configuration details for an options instance:

```csharp
public record WritableOptionsConfiguration<T>
{
    public IFormatProvider FormatProvider { get; init; }
    public string ConfigFilePath { get; init; }
    public string? InstanceName { get; init; }
    public string? SectionName { get; init; }
    public ILogger? Logger { get; init; }
    public IValidateOptions<T>? Validator { get; init; }
}
```

**Usage:**
```csharp
var config = options.GetOptionsConfiguration();
Console.WriteLine($"File: {config.ConfigFilePath}");
Console.WriteLine($"Instance: {config.InstanceName ?? "unnamed"}");
Console.WriteLine($"Section: {config.SectionName ?? "root"}");
```

## Next Steps

- [Usage Examples](../usage/simple-app) - See interfaces in action
- [Testing](../advanced/testing) - Test with interface stubs
- [Format Providers](../customization/format-provider) - Learn about format and file providers
