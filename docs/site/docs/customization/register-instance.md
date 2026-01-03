---
sidebar_position: 9
---

# RegisterInstanceToContainer

By default, Configuration.Writable registers interfaces (`IReadOnlyOptions<T>`, `IWritableOptions<T>`) in the DI container. However, you can also register the settings class itself directly for injection.

## Basic Usage

Enable direct instance registration:

```csharp
builder.Services.AddWritableOptions<UserSetting>(conf =>
{
    conf.RegisterInstanceToContainer = true;
});
```

Now you can inject the settings class directly:

```csharp
public class MyService(UserSetting setting)
{
    public void Print()
    {
        Console.WriteLine($"Name: {setting.Name}");
        Console.WriteLine($"Age: {setting.Age}");
    }
}
```

## Implications

:::warning Important Considerations
When `RegisterInstanceToContainer = true`:

- The settings class is registered as a **Singleton** in the DI container
- The instance is created **once** at application startup
- **Dynamic updates are NOT reflected** in the injected instance
- You lose the ability to use `IOptionsMonitor` for change detection
- The instance reflects the settings at the time of DI container build
:::

## Use Cases

Direct instance registration is useful when:

- You have simple, static configuration that doesn't change
- You want to simplify dependency injection
- You don't need change detection
- Performance is critical (avoid interface overhead)

## Compatibility

You can still use the standard interfaces even when direct registration is enabled:

```csharp
builder.Services.AddWritableOptions<UserSetting>(conf =>
{
    conf.RegisterInstanceToContainer = true;
});

// Both work:
public class ServiceA(UserSetting setting) { }  // Direct instance
public class ServiceB(IReadOnlyOptions<UserSetting> options) { }  // Interface
```

## Example

```csharp
using Configuration.Writable;

var builder = Host.CreateApplicationBuilder(args);

// Enable direct instance registration
builder.Services.AddWritableOptions<AppSettings>(conf =>
{
    conf.UseFile("appsettings.json");
    conf.SectionName = "AppSettings";
    conf.RegisterInstanceToContainer = true;
});

builder.Services.AddSingleton<MyService>();

var app = builder.Build();
app.Run();

// Settings class
public partial class AppSettings : IOptionsModel<AppSettings>
{
    public string ApplicationName { get; set; } = "MyApp";
    public int MaxConnections { get; set; } = 100;
}

// Service using direct instance
public class MyService(AppSettings settings)
{
    public void Start()
    {
        Console.WriteLine($"Starting {settings.ApplicationName}");
        Console.WriteLine($"Max connections: {settings.MaxConnections}");
    }
}
```

## Comparison

### With Interface (Default)

```csharp
// Registration
builder.Services.AddWritableOptions<UserSetting>();

// Usage
public class MyService(IReadOnlyOptions<UserSetting> options)
{
    public void Use()
    {
        var setting = options.CurrentValue;  // Always current
        // Can detect changes with options.OnChange()
    }
}
```

### With Direct Instance

```csharp
// Registration
builder.Services.AddWritableOptions<UserSetting>(conf =>
{
    conf.RegisterInstanceToContainer = true;
});

// Usage
public class MyService(UserSetting setting)
{
    public void Use()
    {
        // Direct access - snapshot from startup
        var name = setting.Name;
        // NO change detection available
    }
}
```

## Lifecycle Management

The registered instance lifecycle:

1. **Startup**: Settings loaded from file
2. **DI Build**: Instance created and registered as Singleton
3. **Runtime**: Same instance used throughout application lifetime
4. **Writes**: `SaveAsync` updates the file, but NOT the registered instance

## With Named Instances

Direct registration also works with named instances:

```csharp
builder.Services.AddWritableOptions<UserSetting>("First", conf =>
{
    conf.UseFile("first.json");
    conf.RegisterInstanceToContainer = true;
});

builder.Services.AddWritableOptions<UserSetting>("Second", conf =>
{
    conf.UseFile("second.json");
    conf.RegisterInstanceToContainer = true;
});

// Inject with keyed services
public class MyService(
    [FromKeyedServices("First")] UserSetting firstSettings,
    [FromKeyedServices("Second")] UserSetting secondSettings
)
{
    public void Use()
    {
        Console.WriteLine($"First: {firstSettings.Name}");
        Console.WriteLine($"Second: {secondSettings.Name}");
    }
}
```

## Best Practices

✅ **Use when:**
- Configuration is static and doesn't change at runtime
- Simplicity is preferred over flexibility
- You don't need change detection

❌ **Avoid when:**
- Configuration needs to be updated at runtime
- You need change detection or monitoring
- Settings can change externally

## Next Steps

- [InstanceName](../advanced/instance-name) - Multiple instances of same type
- [Change Detection](./change-detection) - Monitor configuration changes
- [Dynamic Options](../advanced/dynamic-options) - Add/remove at runtime
