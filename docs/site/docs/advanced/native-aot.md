---
sidebar_position: 1
---

# NativeAOT Support

Configuration.Writable fully supports NativeAOT compilation with a few configuration steps.

## Overview

To use Configuration.Writable in NativeAOT environments, you need to:

1. Use source-generated JSON serialization
2. Use source-generated validators (optional, but recommended)
3. Disable reflection-based features

## Complete Example

### 1. Define Settings Class

```csharp
using System.ComponentModel.DataAnnotations;
using Configuration.Writable;

public partial class SampleSetting : IOptionsModel<SampleSetting>
{
    [Required, MinLength(3)]
    public string Name { get; set; } = "default name";
    
    [Range(0, 150)]
    public int Age { get; set; } = 20;
    
    public DateTime LastUpdatedAt { get; set; } = DateTime.Now;
}
```

### 2. Create Source-Generated Context

```csharp
using System.Text.Json.Serialization;

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(SampleSetting))]
public partial class SampleSettingSerializerContext : JsonSerializerContext
{
}
```

### 3. Create Source-Generated Validator

```csharp
using Microsoft.Extensions.Options;

[OptionsValidator]
public partial class SampleSettingValidator : IValidateOptions<SampleSetting>
{
}
```

### 4. Configure WritableOptions

```csharp
using Configuration.Writable;
using Configuration.Writable.FormatProvider;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddWritableOptions<SampleSetting>(conf =>
{
    // Use JsonAotFormatProvider with source-generated context
    conf.FormatProvider = new JsonAotFormatProvider(
        SampleSettingSerializerContext.Default
    );
    
    // Disable reflection-based validation
    conf.UseDataAnnotationsValidation = false;
    
    // Use source-generated validator
    conf.WithValidator<SampleSettingValidator>();
});

var app = builder.Build();
app.Run();
```

## Console Application Example

Here's a complete NativeAOT console application:

```csharp
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Configuration.Writable;
using Configuration.Writable.FormatProvider;
using Microsoft.Extensions.Options;

// Settings class
public partial class SampleSetting : IOptionsModel<SampleSetting>
{
    [Required, MinLength(3)]
    public string Name { get; set; } = "default name";
    
    [Range(0, 150)]
    public int Age { get; set; } = 20;
}

// JSON serialization context
[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(SampleSetting))]
public partial class SampleSettingContext : JsonSerializerContext;

// Validator
[OptionsValidator]
public partial class SampleSettingValidator : IValidateOptions<SampleSetting>;

// Main program
class Program
{
    static async Task Main(string[] args)
    {
        // Initialize with NativeAOT support
        WritableOptions.Initialize<SampleSetting>(conf =>
        {
            conf.FormatProvider = new JsonAotFormatProvider(
                SampleSettingContext.Default
            );
            conf.UseDataAnnotationsValidation = false;
            conf.WithValidator<SampleSettingValidator>();
        });
        
        var options = WritableOptions.GetOptions<SampleSetting>();
        
        // Use settings
        var setting = options.CurrentValue;
        Console.WriteLine($"Current name: {setting.Name}");
        
        // Update settings
        await options.SaveAsync(s => s.Name = "NativeAOT App");
        Console.WriteLine("Settings saved!");
    }
}
```

## Project Configuration

### csproj Settings

Enable NativeAOT in your project file:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>enable</Nullable>
    <PublishAot>true</PublishAot>
    <InvariantGlobalization>false</InvariantGlobalization>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Configuration.Writable" Version="*" />
  </ItemGroup>
</Project>
```

### Build and Publish

```bash
# Restore dependencies
dotnet restore

# Build
dotnet build

# Publish as NativeAOT
dotnet publish -c Release
```

The output will be a native executable with no .NET runtime dependency.

## ASP.NET Core Web API Example

```csharp
using System.Text.Json.Serialization;
using Configuration.Writable;
using Configuration.Writable.FormatProvider;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateSlimBuilder(args);

// Source-generated context
[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(SampleSetting))]
partial class AppJsonContext : JsonSerializerContext;

// Configure JSON options
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.TypeInfoResolverChain.Insert(0, AppJsonContext.Default);
});

// Configure writable options
builder.Services.AddWritableOptions<SampleSetting>(conf =>
{
    conf.UseFile("appsettings.json");
    conf.SectionName = "MySetting";
    conf.FormatProvider = new JsonAotFormatProvider(AppJsonContext.Default);
    conf.UseDataAnnotationsValidation = false;
    conf.WithValidator<SampleSettingValidator>();
});

var app = builder.Build();

// API endpoints
app.MapGet("/config", (IReadOnlyOptions<SampleSetting> options) =>
    options.CurrentValue);

app.MapPost("/config", async (
    IWritableOptions<SampleSetting> options,
    SampleSetting newSettings) =>
{
    await options.SaveAsync(s =>
    {
        s.Name = newSettings.Name;
        s.Age = newSettings.Age;
    });
    return Results.Ok();
});

app.Run();

// Settings and validators...
public partial class SampleSetting : IOptionsModel<SampleSetting>
{
    public string Name { get; set; } = "Default";
    public int Age { get; set; } = 20;
}

[OptionsValidator]
partial class SampleSettingValidator : IValidateOptions<SampleSetting>;
```

## Key Points

### Required Changes

1. **Use `JsonAotFormatProvider`** instead of `JsonFormatProvider`
2. **Disable `UseDataAnnotationsValidation`** (it uses reflection)
3. **Use `[OptionsValidator]`** attribute for validation

### What Works

✅ All core functionality
✅ File watching and change detection
✅ All save location options
✅ Validation with source generators
✅ Named instances
✅ All interfaces (IWritableOptions, etc.)

### Limitations

❌ Cannot use reflection-based DataAnnotations validation
❌ Cannot use custom validators that require reflection
❌ Must use source-generated JSON contexts

## Performance Benefits

NativeAOT compilation provides:

- **Faster startup**: No JIT compilation
- **Smaller memory footprint**: No runtime overhead
- **Faster execution**: Direct native code
- **Self-contained**: Single executable, no runtime needed

## Example Project

See the complete example in the repository:
- [Example.ConsoleApp.NativeAot](https://github.com/arika0093/Configuration.Writable/tree/main/example/Example.ConsoleApp.NativeAot)

## Next Steps

- [Testing](./testing) - Test NativeAOT applications
- [Format Providers](../customization/format-provider) - Learn about JsonAotFormatProvider
- [Validation](../customization/validation) - Source-generated validation
