---
sidebar_position: 1
---

# Simple Application (Without DI)

If you are not using Dependency Injection (for example, in WinForms, WPF, console apps, etc.), use `WritableOptions` as the starting point for reading and writing settings.

## Basic Usage

### Initialize

Initialize the configuration system once at application startup:

```csharp
using Configuration.Writable;

// Initialize once (at application startup)
WritableOptions.Initialize<UserSetting>();
```

By default, settings are saved to `./usersettings.json` in the application's base directory.

### Read Settings

Get the writable config instance and read current values:

```csharp
// Get the writable config instance
var options = WritableOptions.GetOptions<UserSetting>();

// Get the UserSetting instance
var setting = options.CurrentValue;
Console.WriteLine($"Name: {setting.Name}");
```

### Write Settings

Save updated settings to storage:

```csharp
await options.SaveAsync(setting =>
{
    setting.Name = "new name";
    setting.Age = 25;
});
```

## Complete Example

Here's a complete console application example:

```csharp
using Configuration.Writable;

// Define your settings class
public partial class UserSetting : IOptionsModel<UserSetting>
{
    public string Name { get; set; } = "default name";
    public int Age { get; set; } = 20;
    public DateTime LastUpdatedAt { get; set; } = DateTime.Now;
}

// Initialize once (at application startup)
WritableOptions.Initialize<UserSetting>();

// Get the config instance
var options = WritableOptions.GetOptions<UserSetting>();

// Read current settings
var setting = options.CurrentValue;
Console.WriteLine($"Current Name: {setting.Name}");
Console.WriteLine($"Current Age: {setting.Age}");
Console.WriteLine($"Last Updated: {setting.LastUpdatedAt}");

// Update settings
Console.Write("Enter new name: ");
var newName = Console.ReadLine();

await options.SaveAsync(setting =>
{
    setting.Name = newName ?? "default name";
    setting.LastUpdatedAt = DateTime.Now;
});

Console.WriteLine("Settings saved!");
Console.WriteLine($"Saved to: {options.GetOptionsConfiguration().ConfigFilePath}");
```

## Watch for Changes

You can register a callback to be notified when settings change:

```csharp
// Register change listener
var disposable = options.OnChange(setting =>
{
    Console.WriteLine($"Settings changed! Name: {setting.Name}");
});

// Later, dispose to stop watching
disposable.Dispose();
```

## Custom Configuration

You can customize the configuration during initialization:

```csharp
using Configuration.Writable;
using Configuration.Writable.FormatProvider;

WritableOptions.Initialize<UserSetting>(conf =>
{
    // Save to a custom location
    conf.UseFile("./config/mysettings.json");
    
    // Use XML format instead of JSON
    // (requires Configuration.Writable.Xml package)
    // conf.FormatProvider = new XmlFormatProvider();
    
    // Customize JSON options
    conf.FormatProvider = new JsonFormatProvider()
    {
        JsonSerializerOptions = { WriteIndented = true }
    };
});
```

## Next Steps

- [Host Application](./host-app) - Usage with Dependency Injection
- [ASP.NET Core](./aspnet-core) - Integrate with ASP.NET Core
- [Customization](../customization/configuration) - Learn about all configuration options
