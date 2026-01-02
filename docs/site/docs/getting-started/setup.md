---
sidebar_position: 2
---

# Setup

Setting up Configuration.Writable requires creating a settings class that will represent your application's configuration.

## Creating a Settings Class

The simplest way to create a settings class is to implement the `IOptionsModel<T>` interface and mark the class as `partial`:

```csharp
using Configuration.Writable;

// Add IOptionsModel and mark as partial class
public partial class UserSetting : IOptionsModel<UserSetting>
{
    public string Name { get; set; } = "default name";
    public int Age { get; set; } = 20;
}
```

The `IOptionsModel<T>` interface automatically implements deep cloning capabilities through source generation.

## Alternative: Manual Setup

If you prefer not to use source generation, you can implement the interface manually:

```csharp
using Configuration.Writable;

public class UserSetting
{
    public string Name { get; set; } = "default name";
    public int Age { get; set; } = 20;
}
```

In this case, you'll need to configure a custom clone strategy if your settings class requires deep cloning.

## Adding Validation

You can add validation attributes to your settings properties:

```csharp
using System.ComponentModel.DataAnnotations;
using Configuration.Writable;

public partial class UserSetting : IOptionsModel<UserSetting>
{
    [Required, MinLength(3)]
    public string Name { get; set; } = "default name";
    
    [Range(0, 150)]
    public int Age { get; set; } = 20;
}
```

By default, DataAnnotations validation is enabled. If validation fails during save, an `OptionsValidationException` is thrown.

## Complex Settings

Your settings class can contain nested objects, collections, and other complex types:

```csharp
using Configuration.Writable;

public partial class AppSettings : IOptionsModel<AppSettings>
{
    public DatabaseSettings Database { get; set; } = new();
    public List<string> AllowedHosts { get; set; } = new();
    public Dictionary<string, string> FeatureFlags { get; set; } = new();
}

public class DatabaseSettings
{
    public string ConnectionString { get; set; } = "";
    public int MaxConnections { get; set; } = 100;
}
```

## Next Steps

- [Simple Application](../usage/simple-app) - Use without DI
- [Host Application](../usage/host-app) - Use with DI
- [Validation](../customization/validation) - Learn more about validation options
