---
sidebar_position: 2
---

# Save Location

Configuration.Writable provides flexible options for specifying where settings files are saved.

## Default Behavior

By default, settings are saved to `usersettings.json` in the application's base directory:

```csharp
// Default location: {AppContext.BaseDirectory}/usersettings.json
WritableOptions.Initialize<UserSetting>();
```

## Simple File Path

Use `UseFile()` to specify a custom file path:

```csharp
// Relative to application directory
conf.UseFile("config/mysettings.json");

// Parent directory
conf.UseFile("../shared/settings.json");

// Absolute path
conf.UseFile("/etc/myapp/settings.json");
```

The file extension is optional - it's determined by the FormatProvider.

## Directory Methods

For more control, use directory methods combined with `AddFilePath()`:

### Executable Directory

Save to the application's executable directory:

```csharp
conf.UseExecutableDirectory()
    .AddFilePath("settings.json");

// Equivalent to:
conf.UseFile("./settings.json");
```

### Standard Save Directory

Use platform-specific configuration directories:

```csharp
conf.UseStandardSaveDirectory("MyAppId")
    .AddFilePath("settings.json");
```

This maps to:
- **Windows**: `%APPDATA%/MyAppId/settings.json`
- **macOS**: `~/Library/Application Support/MyAppId/settings.json`
- **Linux**: `~/.config/MyAppId/settings.json` (or `$XDG_CONFIG_HOME/MyAppId`)

### Custom Directory

Specify any custom directory:

```csharp
conf.UseCustomDirectory("/var/lib/myapp")
    .AddFilePath("settings.json");
```

## Multiple Locations

You can specify multiple potential locations. The library will choose the best one based on priority rules:

```csharp
// Priority 1: Custom location with explicit priority
conf.UseStandardSaveDirectory("MyApp")
    .AddFilePath("settings.json", priority: 10);

// Priority 2: Existing file with write access
conf.UseExecutableDirectory()
    .AddFilePath("child/existing.json"); // if this file exists

// Priority 3: Existing directory with create permission
conf.UseExecutableDirectory()
    .AddFilePath("settings.json"); // if directory exists

// Priority 4: First registered
conf.UseCustomDirectory("/opt/myapp")
    .AddFilePath("settings.json"); // new location
```

### Priority Rules

When multiple locations are specified, the selection follows these rules in order:

1. **Explicit priority** (higher numbers = higher priority)
2. **File exists and is writable**
3. **Directory exists and is writable**
4. **Order of registration** (earlier = higher priority)

## Environment-Specific Locations

Toggle between development and production environments:

### Without DI

```csharp
WritableOptions.Initialize<UserSetting>(conf =>
{
#if DEBUG
    var isProduction = false;
#else
    var isProduction = true;
#endif
    
    // Development: ./settings.json
    // Production: %APPDATA%/MyApp/settings.json
    conf.UseStandardSaveDirectory("MyApp", enabled: isProduction)
        .AddFilePath("settings.json");
});
```

### With DI

```csharp
builder.Services.AddWritableOptions<UserSetting>(conf =>
{
    var isProd = builder.Environment.IsProduction();
    
    conf.UseStandardSaveDirectory("MyApp", enabled: isProd)
        .AddFilePath("settings.json");
    
    // This will use standard directory in production,
    // executable directory in development
});
```

## Conditional Locations

Enable/disable locations based on conditions:

```csharp
conf.UseStandardSaveDirectory("MyApp", enabled: isProd)
    .AddFilePath("settings.json");

conf.UseExecutableDirectory(enabled: !isProd)
    .AddFilePath("settings.json");
```

## Getting Current Location

You can retrieve the actual file path being used:

```csharp
var options = WritableOptions.GetOptions<UserSetting>();
var config = options.GetOptionsConfiguration();
Console.WriteLine($"Settings saved to: {config.ConfigFilePath}");
```

## Example Scenarios

### Portable Application

Settings in the same directory as the executable:

```csharp
conf.UseExecutableDirectory()
    .AddFilePath("settings.json");
```

### System-Wide Settings

Settings in a system directory:

```csharp
conf.UseCustomDirectory("/etc/myapp")
    .AddFilePath("settings.json", priority: 10);

// Fallback to user directory if no write permission
conf.UseStandardSaveDirectory("MyApp")
    .AddFilePath("settings.json", priority: 5);
```

### Per-User Settings

Settings in user's config directory:

```csharp
conf.UseStandardSaveDirectory("MyApp")
    .AddFilePath("settings.json");
```

## Next Steps

- [Format Provider](./format-provider) - Choose file format
- [File Provider](./file-provider) - Customize file operations
- [Section Name](./section-name) - Organize settings in sections
