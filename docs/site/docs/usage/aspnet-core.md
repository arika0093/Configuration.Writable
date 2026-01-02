---
sidebar_position: 3
---

# ASP.NET Core

Configuration.Writable integrates seamlessly with ASP.NET Core applications, allowing you to update existing configuration files like `appsettings.json`.

## Basic Setup

Register your settings class with a section name:

```csharp
using Configuration.Writable;
using Configuration.Writable.FormatProvider;

var builder = WebApplication.CreateSlimBuilder(args);

// Register writable options with section name
builder.Services.AddWritableOptions<SampleSetting>(conf =>
{
    conf.UseFile("appsettings.json");
    conf.SectionName = "MySetting";
});

var app = builder.Build();
app.Run();
```

## Web API Example

Here's a complete Web API example with endpoints to read and update settings:

```csharp
using Configuration.Writable;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateSlimBuilder(args);

// Register writable options
builder.Services.AddWritableOptions<SampleSetting>(conf =>
{
    conf.UseFile("appsettings.json");
    conf.SectionName = "MySetting";
});

var app = builder.Build();

// Get settings endpoint
app.MapGet("/config/get", (IReadOnlyOptions<SampleSetting> options) =>
{
    return options.CurrentValue;
});

// Update settings endpoint
app.MapPost("/config/set", async (
    [FromServices] IWritableOptions<SampleSetting> options,
    [FromBody] SampleSetting newSettings
) =>
{
    try
    {
        await options.SaveAsync(setting =>
        {
            setting.Name = newSettings.Name;
            setting.Value = newSettings.Value;
        });
        return Results.Ok();
    }
    catch (Exception ex)
    {
        return Results.Problem(ex.Message);
    }
});

app.Run();

// Settings class
public partial class SampleSetting : IOptionsModel<SampleSetting>
{
    public string Name { get; set; } = "Default";
    public int Value { get; set; } = 100;
}
```

## Section Names

By specifying a `SectionName`, your settings are stored in a nested section of the configuration file. This allows you to coexist with other settings:

```csharp
builder.Services.AddWritableOptions<UserSetting>(conf =>
{
    conf.UseFile("appsettings.json");
    conf.SectionName = "MyApp:UserSettings";
});
```

The resulting `appsettings.json`:

```json
{
  "MyApp": {
    "UserSettings": {
      "Name": "John Doe",
      "Age": 30
    }
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information"
    }
  }
}
```

## Multiple Settings

You can register multiple settings classes in the same file using different section names:

```csharp
// Register UserSettings
builder.Services.AddWritableOptions<UserSetting>(conf =>
{
    conf.UseFile("appsettings.json");
    conf.SectionName = "UserSettings";
});

// Register AppSettings
builder.Services.AddWritableOptions<AppSetting>(conf =>
{
    conf.UseFile("appsettings.json");
    conf.SectionName = "AppSettings";
});
```

## Environment-Specific Configuration

You can use different files for different environments:

```csharp
builder.Services.AddWritableOptions<UserSetting>(conf =>
{
    var env = builder.Environment;
    var fileName = env.IsDevelopment() 
        ? "appsettings.Development.json" 
        : "appsettings.json";
    
    conf.UseFile(fileName);
    conf.SectionName = "UserSettings";
});
```

## NativeAOT Support

For NativeAOT scenarios, use source-generated JSON serialization:

```csharp
using System.Text.Json.Serialization;
using Configuration.Writable.FormatProvider;
using Microsoft.Extensions.Options;

// Define JSON source generation context
[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(SampleSetting))]
public partial class SampleSettingSerializerContext : JsonSerializerContext;

// Define source-generated validator
[OptionsValidator]
public partial class SampleSettingValidator : IValidateOptions<SampleSetting>;

// Configure in Program.cs
builder.Services.AddWritableOptions<SampleSetting>(conf =>
{
    conf.UseFile("appsettings.json");
    conf.SectionName = "MySetting";
    
    // Use NativeAOT-compatible format provider
    conf.FormatProvider = new JsonAotFormatProvider(
        SampleSettingSerializerContext.Default
    );
    
    // Use source-generated validator
    conf.UseDataAnnotationsValidation = false;
    conf.WithValidator<SampleSettingValidator>();
});
```

## Next Steps

- [Format Providers](../customization/format-provider) - Use XML, YAML, or encrypted formats
- [Validation](../customization/validation) - Add validation to your settings
- [Advanced: NativeAOT](../advanced/native-aot) - Full NativeAOT guide
