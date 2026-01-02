---
sidebar_position: 1
---

# Configuration Methods

You can customize various settings when initializing Configuration.Writable. The configuration method differs slightly depending on whether you're using DI or not.

## Without DI

Use the `Initialize` method with a configuration lambda:

```csharp
using Configuration.Writable;

WritableOptions.Initialize<SampleSetting>(conf =>
{
    // Configure options here
    conf.UseFile("./config/settings.json");
    // ... more configuration
});
```

## With DI

Use the `AddWritableOptions` extension method:

```csharp
using Configuration.Writable;

builder.Services.AddWritableOptions<UserSetting>(conf =>
{
    // Configure options here
    conf.UseFile("appsettings.json");
    conf.SectionName = "UserSettings";
    // ... more configuration
});
```

## Available Configuration Options

The configuration builder provides the following options:

### File Location

```csharp
// Specify a file path directly
conf.UseFile("./config/settings.json");

// Or use directory methods
conf.UseExecutableDirectory().AddFilePath("settings.json");
conf.UseStandardSaveDirectory("MyApp").AddFilePath("settings.json");
conf.UseCustomDirectory("/path/to/dir").AddFilePath("settings.json");
```

### Format Provider

```csharp
using Configuration.Writable.FormatProvider;

// JSON (default)
conf.FormatProvider = new JsonFormatProvider()
{
    JsonSerializerOptions = { WriteIndented = true }
};

// XML (requires Configuration.Writable.Xml)
// conf.FormatProvider = new XmlFormatProvider();

// YAML (requires Configuration.Writable.Yaml)
// conf.FormatProvider = new YamlFormatProvider();

// Encrypted (requires Configuration.Writable.Encrypt)
// conf.FormatProvider = new EncryptFormatProvider("password");
```

### File Provider

```csharp
using Configuration.Writable.FileProvider;

conf.FileProvider = new CommonFileProvider()
{
    MaxRetryCount = 5,
    RetryDelay = (attempt) => 100 * attempt,
    BackupMaxCount = 5
};
```

### Section Name

```csharp
// Store in a specific section
conf.SectionName = "MyApp:Settings";

// Supports both : and __ as separators
conf.SectionName = "MyApp__Settings"; // equivalent to above
```

### Validation

```csharp
// Data Annotations (enabled by default)
conf.UseDataAnnotationsValidation = true;

// Custom validator
conf.WithValidator<MyCustomValidator>();

// Validator function
conf.WithValidatorFunction(setting =>
{
    if (setting.Age < 0)
        return ValidateOptionsResult.Fail("Age must be positive");
    return ValidateOptionsResult.Success;
});
```

### Logging

```csharp
// Without DI
conf.Logger = LoggerFactory
    .Create(builder => builder.AddConsole())
    .CreateLogger("Configuration.Writable");

// With DI - automatically uses ILogger from DI container
```

### Change Detection

```csharp
// Throttle change notifications (default: 1000ms)
conf.OnChangeThrottleMs = 500; // 500ms
conf.OnChangeThrottleMs = 0;   // disable throttling
```

### Instance Registration

```csharp
// Register the settings class directly in DI container
conf.RegisterInstanceToContainer = true;

// Then you can inject UserSetting directly
public class MyService(UserSetting setting) { }
```

## Complete Example

Here's a comprehensive configuration example:

```csharp
builder.Services.AddWritableOptions<UserSetting>(conf =>
{
    // File location
    var isProd = builder.Environment.IsProduction();
    conf.UseStandardSaveDirectory("MyApp", enabled: isProd)
        .AddFilePath("settings.json");
    
    // Format
    conf.FormatProvider = new JsonFormatProvider()
    {
        JsonSerializerOptions = { WriteIndented = true }
    };
    
    // File operations
    conf.FileProvider = new CommonFileProvider()
    {
        MaxRetryCount = 3,
        BackupMaxCount = 5
    };
    
    // Section
    conf.SectionName = "UserSettings";
    
    // Validation
    conf.UseDataAnnotationsValidation = true;
    conf.WithValidatorFunction(s =>
    {
        if (s.Age > 150)
            return ValidateOptionsResult.Fail("Age unrealistic");
        return ValidateOptionsResult.Success;
    });
    
    // Change detection
    conf.OnChangeThrottleMs = 1000;
});
```

## Next Steps

- [Save Location](./save-location) - Learn about file location options
- [Format Provider](./format-provider) - Explore different file formats
- [File Provider](./file-provider) - Customize file operations
