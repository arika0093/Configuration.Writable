---
sidebar_position: 8
---

# Section Name

Section names allow you to organize settings within a configuration file, making it possible to store multiple settings in the same file or integrate with existing configuration files.

## Basic Usage

By default, settings are stored at the root level:

```json
{
  "Name": "John Doe",
  "Age": 30
}
```

With a section name, settings are nested:

```csharp
builder.Services.AddWritableOptions<UserSetting>(conf =>
{
    conf.UseFile("appsettings.json");
    conf.SectionName = "UserSettings";
});
```

Results in:

```json
{
  "UserSettings": {
    "Name": "John Doe",
    "Age": 30
  }
}
```

## Nested Sections

Use `:` or `__` as separators for nested sections:

```csharp
conf.SectionName = "MyApp:User:Settings";
// or
conf.SectionName = "MyApp__User__Settings";
```

Both result in:

```json
{
  "MyApp": {
    "User": {
      "Settings": {
        "Name": "John Doe",
        "Age": 30
      }
    }
  }
}
```

## ASP.NET Core Integration

Section names are essential for ASP.NET Core integration:

```csharp
var builder = WebApplication.CreateSlimBuilder(args);

builder.Services.AddWritableOptions<AppSettings>(conf =>
{
    conf.UseFile("appsettings.json");
    conf.SectionName = "MyApp";
});
```

This allows your writable settings to coexist with other configuration:

```json
{
  "MyApp": {
    "DatabasePath": "/data/app.db",
    "EnableFeatureX": true
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information"
    }
  },
  "AllowedHosts": "*"
}
```

## Multiple Settings in One File

Store multiple settings classes in different sections:

```csharp
// User settings
builder.Services.AddWritableOptions<UserSettings>(conf =>
{
    conf.UseFile("appsettings.json");
    conf.SectionName = "UserSettings";
});

// App settings
builder.Services.AddWritableOptions<AppSettings>(conf =>
{
    conf.UseFile("appsettings.json");
    conf.SectionName = "AppSettings";
});

// Database settings
builder.Services.AddWritableOptions<DatabaseSettings>(conf =>
{
    conf.UseFile("appsettings.json");
    conf.SectionName = "Database";
});
```

Results in:

```json
{
  "UserSettings": {
    "Name": "John",
    "Theme": "Dark"
  },
  "AppSettings": {
    "EnableFeatures": true,
    "MaxConnections": 100
  },
  "Database": {
    "ConnectionString": "...",
    "Timeout": 30
  }
}
```

## Root Level (No Section)

To store settings at the root level, leave `SectionName` empty or null:

```csharp
conf.SectionName = null; // or don't set it at all
```

:::warning
Be careful when using root level with multiple settings classes in the same file - they may overwrite each other!
:::

## Section Name vs. File Name

You can combine different section names with different files:

```csharp
// Development settings in dev file
builder.Services.AddWritableOptions<DevSettings>(conf =>
{
    conf.UseFile("appsettings.Development.json");
    conf.SectionName = "DevSettings";
});

// Production settings in main file
builder.Services.AddWritableOptions<ProdSettings>(conf =>
{
    conf.UseFile("appsettings.json");
    conf.SectionName = "ProdSettings";
});
```

## Example: Complete ASP.NET Core Setup

```csharp
var builder = WebApplication.CreateSlimBuilder(args);

// Writable user preferences
builder.Services.AddWritableOptions<UserPreferences>(conf =>
{
    conf.UseFile("appsettings.json");
    conf.SectionName = "Preferences";
});

// Writable feature flags
builder.Services.AddWritableOptions<FeatureFlags>(conf =>
{
    conf.UseFile("appsettings.json");
    conf.SectionName = "Features";
});

var app = builder.Build();

// API to update preferences
app.MapPost("/api/preferences", async (
    IWritableOptions<UserPreferences> options,
    UserPreferences newPrefs) =>
{
    await options.SaveAsync(prefs =>
    {
        prefs.Theme = newPrefs.Theme;
        prefs.Language = newPrefs.Language;
    });
    return Results.Ok();
});

// API to toggle features
app.MapPost("/api/features/{name}/toggle", async (
    IWritableOptions<FeatureFlags> options,
    string name) =>
{
    await options.SaveAsync(features =>
    {
        // Toggle feature by name using reflection or dictionary
        var prop = features.GetType().GetProperty(name);
        if (prop?.PropertyType == typeof(bool))
        {
            prop.SetValue(features, !(bool)prop.GetValue(features));
        }
    });
    return Results.Ok();
});

app.Run();
```

Resulting `appsettings.json`:

```json
{
  "Preferences": {
    "Theme": "Dark",
    "Language": "en"
  },
  "Features": {
    "EnableNewUI": true,
    "EnableBetaFeatures": false
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information"
    }
  }
}
```

## Next Steps

- [ASP.NET Core Usage](../usage/aspnet-core) - Full ASP.NET Core integration
- [Format Provider](./format-provider) - Different file formats
- [Save Location](./save-location) - File location options
