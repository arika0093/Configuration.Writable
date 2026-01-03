---
sidebar_position: 5
---

# CloneStrategy

Configuration.Writable uses deep cloning to prevent direct modification of the internal cache. By default, it uses `IDeepCloneable` for cloning, but you can customize this behavior.

## Overview

To improve performance, configuration files are loaded once and cached internally. When you access settings or save changes, a deep copy is created to prevent modifications to the cache.

### Why Cloning?

```csharp
// Without cloning - DANGEROUS!
var settings = options.CurrentValue;
settings.Name = "Modified";  // This would modify the cache directly!

// With cloning - SAFE
var settings = options.CurrentValue;  // Gets a deep copy
settings.Name = "Modified";  // Only modifies the copy
```

## Default Clone Strategy

By default, Configuration.Writable uses [IDeepCloneable](https://github.com/arika0093/IDeepCloneable) for deep cloning:

```csharp
using Configuration.Writable;

// Settings class automatically implements deep cloning
public partial class UserSetting : IOptionsModel<UserSetting>
{
    public string Name { get; set; } = "default";
    public int Age { get; set; } = 20;
    public List<string> Tags { get; set; } = new();
    public Dictionary<string, string> Metadata { get; set; } = new();
}

// IDeepCloneable is automatically implemented via source generation
// This handles all properties, including collections and nested objects
```

## Custom Clone Strategy

If you need custom cloning logic, use `UseCustomCloneStrategy`:

```csharp
builder.Services.AddWritableOptions<UserSetting>(conf =>
{
    conf.UseCustomCloneStrategy(original =>
    {
        // Custom cloning logic
        return new UserSetting
        {
            Name = original.Name,
            Age = original.Age,
            Tags = new List<string>(original.Tags),
            Metadata = new Dictionary<string, string>(original.Metadata)
        };
    });
});
```

## Using JSON Serialization

A simple custom strategy using JSON serialization:

```csharp
using System.Text.Json;

conf.UseCustomCloneStrategy(original =>
{
    // Serialize and deserialize for deep clone
    var json = JsonSerializer.Serialize(original);
    return JsonSerializer.Deserialize<UserSetting>(json)!;
});
```

### With Source Generation

For NativeAOT compatibility:

```csharp
[JsonSourceGenerationOptions(WriteIndented = false)]
[JsonSerializable(typeof(UserSetting))]
partial class UserSettingContext : JsonSerializerContext;

conf.UseCustomCloneStrategy(original =>
{
    var json = JsonSerializer.Serialize(original, UserSettingContext.Default.UserSetting);
    return JsonSerializer.Deserialize(json, UserSettingContext.Default.UserSetting)!;
});
```

## Using Third-Party Libraries

### DeepCloner

```bash
dotnet add package DeepCloner
```

```csharp
using DeepCloner;

conf.UseCustomCloneStrategy(original => original.DeepClone());
```

### AutoMapper

```bash
dotnet add package AutoMapper
```

```csharp
using AutoMapper;

// Configure AutoMapper
var config = new MapperConfiguration(cfg =>
{
    cfg.CreateMap<UserSetting, UserSetting>();
});
var mapper = config.CreateMapper();

// Use in clone strategy
conf.UseCustomCloneStrategy(original => mapper.Map<UserSetting>(original));
```

## Performance Considerations

Different cloning strategies have different performance characteristics:

| Strategy | Performance | Memory | NativeAOT |
|----------|-------------|---------|-----------|
| IDeepCloneable (default) | ⚡ Fast | ✅ Low | ✅ Yes |
| JSON Serialization | 🐌 Slow | ❌ High | ✅ Yes (with source gen) |
| DeepCloner | ⚡ Fast | ✅ Low | ❌ No (uses reflection) |
| AutoMapper | 🐌 Moderate | ⚡ Moderate | ❌ No (uses reflection) |
| Custom Manual | ⚡⚡ Fastest | ✅✅ Lowest | ✅ Yes |

## When to Customize

Consider custom clone strategy when:

- **Complex Types**: Your settings contain types not supported by IDeepCloneable
- **Performance**: You need absolute best performance (manual cloning)
- **Existing Integration**: You already use a cloning library in your project
- **Special Requirements**: Custom initialization logic during cloning

## Complete Example

```csharp
using Configuration.Writable;
using System.Text.Json;
using System.Text.Json.Serialization;

var builder = Host.CreateApplicationBuilder(args);

// Source generation context
[JsonSourceGenerationOptions]
[JsonSerializable(typeof(ComplexSettings))]
partial class ComplexSettingsContext : JsonSerializerContext;

// Configure with custom clone strategy
builder.Services.AddWritableOptions<ComplexSettings>(conf =>
{
    conf.UseFile("settings.json");
    
    // Custom clone using JSON serialization
    conf.UseCustomCloneStrategy(original =>
    {
        var json = JsonSerializer.Serialize(
            original,
            ComplexSettingsContext.Default.ComplexSettings
        );
        return JsonSerializer.Deserialize(
            json,
            ComplexSettingsContext.Default.ComplexSettings
        )!;
    });
});

var app = builder.Build();
app.Run();

// Settings with complex types
public partial class ComplexSettings : IOptionsModel<ComplexSettings>
{
    public string Name { get; set; } = "";
    public Dictionary<string, NestedSettings> Nested { get; set; } = new();
    public List<int[]> ComplexList { get; set; } = new();
}

public class NestedSettings
{
    public string Value { get; set; } = "";
    public int[] Numbers { get; set; } = Array.Empty<int>();
}
```

## Manual Cloning Example

For maximum performance with simple types:

```csharp
public class AppSettings
{
    public string Name { get; set; } = "";
    public int Port { get; set; } = 8080;
    public List<string> AllowedHosts { get; set; } = new();
}

conf.UseCustomCloneStrategy(original => new AppSettings
{
    Name = original.Name,
    Port = original.Port,
    AllowedHosts = new List<string>(original.AllowedHosts)
});
```

## Avoiding Cloning

:::warning Not Recommended
While you technically *could* disable cloning by returning the same instance, this is **not recommended** as it breaks the cache isolation:

```csharp
// DON'T DO THIS - breaks cache isolation!
conf.UseCustomCloneStrategy(original => original);
```
:::

## Testing Clone Strategy

Verify your clone strategy creates true deep copies:

```csharp
[Fact]
public void CloneStrategy_CreatesDeepCopy()
{
    var original = new UserSetting
    {
        Name = "Original",
        Tags = new List<string> { "tag1" }
    };
    
    var cloned = CloneFunction(original);
    
    // Modify clone
    cloned.Name = "Modified";
    cloned.Tags.Add("tag2");
    
    // Original should be unchanged
    Assert.Equal("Original", original.Name);
    Assert.Single(original.Tags);
    Assert.Equal("tag1", original.Tags[0]);
}
```

## Best Practices

1. **Use Default When Possible**: IDeepCloneable works for most scenarios
2. **Benchmark Custom Strategies**: Measure performance impact
3. **Test Deep Copying**: Ensure collections and nested objects are cloned
4. **Consider NativeAOT**: Use source generation if you need NativeAOT
5. **Document Custom Logic**: Explain why custom strategy is needed

## Comparison

### Default (IDeepCloneable)

```csharp
// No configuration needed
public partial class UserSetting : IOptionsModel<UserSetting>
{
    public string Name { get; set; } = "";
    public List<int> Numbers { get; set; } = new();
}
// Automatic deep cloning via source generation
```

### Custom (JSON Serialization)

```csharp
conf.UseCustomCloneStrategy(original =>
{
    var json = JsonSerializer.Serialize(original);
    return JsonSerializer.Deserialize<UserSetting>(json)!;
});
// Manual control, works with any serializable type
```

## Next Steps

- [NativeAOT](./native-aot) - NativeAOT-compatible cloning
- [Testing](./testing) - Test with custom clone strategies
- [Performance Optimization](../customization/file-provider) - Optimize overall performance
