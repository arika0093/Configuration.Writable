---
sidebar_position: 6
---

# Validation

Configuration.Writable supports multiple validation approaches to ensure settings are valid before saving.

## DataAnnotations (Default)

DataAnnotations validation is enabled by default. Add validation attributes to your settings class:

```csharp
using System.ComponentModel.DataAnnotations;
using Configuration.Writable;

public partial class UserSetting : IOptionsModel<UserSetting>
{
    [Required]
    [MinLength(3)]
    [MaxLength(50)]
    public string Name { get; set; } = "default";
    
    [Range(0, 150)]
    public int Age { get; set; } = 20;
    
    [EmailAddress]
    public string? Email { get; set; }
    
    [Url]
    public string? Website { get; set; }
}
```

### Validation Behavior

When validation fails, an `OptionsValidationException` is thrown:

```csharp
using Microsoft.Extensions.Options;

var options = WritableOptions.GetOptions<UserSetting>();

try
{
    await options.SaveAsync(setting =>
    {
        setting.Name = "ab"; // Too short - violates MinLength(3)
        setting.Age = 200;   // Out of range - violates Range(0, 150)
    });
}
catch (OptionsValidationException ex)
{
    Console.WriteLine($"Validation failed: {ex.Message}");
    foreach (var failure in ex.Failures)
    {
        Console.WriteLine($"  - {failure}");
    }
    // Settings are NOT saved when validation fails
}
```

### Disable DataAnnotations

To disable DataAnnotations validation:

```csharp
builder.Services.AddWritableOptions<UserSetting>(conf =>
{
    conf.UseDataAnnotationsValidation = false;
});
```

## Source-Generated Validation

For better performance and NativeAOT support, use source-generated validators:

```csharp
using Microsoft.Extensions.Options;

// Define a source-generated validator
[OptionsValidator]
public partial class UserSettingValidator : IValidateOptions<UserSetting>;

// Configure
builder.Services.AddWritableOptions<UserSetting>(conf =>
{
    // Disable attribute-based validation
    conf.UseDataAnnotationsValidation = false;
    
    // Enable source-generator-based validation
    conf.WithValidator<UserSettingValidator>();
});
```

Your settings class can still use DataAnnotations attributes - they'll be validated by the source generator:

```csharp
public partial class UserSetting : IOptionsModel<UserSetting>
{
    [Required, MinLength(3)]
    public string Name { get; set; } = "default";
    
    [Range(0, 150)]
    public int Age { get; set; } = 20;
}
```

## Custom Validation Function

Add custom validation logic using a function:

```csharp
builder.Services.AddWritableOptions<UserSetting>(conf =>
{
    conf.WithValidatorFunction(setting =>
    {
        // Custom validation logic
        if (setting.Name.Contains("invalid"))
            return ValidateOptionsResult.Fail(
                "Name must not contain 'invalid'");
        
        if (setting.Age < 0)
            return ValidateOptionsResult.Fail(
                "Age must be positive");
        
        // All checks passed
        return ValidateOptionsResult.Success;
    });
});
```

## Custom Validator Class

For complex validation, implement `IValidateOptions<T>`:

```csharp
using Microsoft.Extensions.Options;

public class UserSettingValidator : IValidateOptions<UserSetting>
{
    public ValidateOptionsResult Validate(string? name, UserSetting options)
    {
        var failures = new List<string>();
        
        // Validate name
        if (string.IsNullOrWhiteSpace(options.Name))
            failures.Add("Name is required");
        else if (options.Name.Length < 3)
            failures.Add("Name must be at least 3 characters");
        
        // Validate age
        if (options.Age < 0)
            failures.Add("Age must be positive");
        if (options.Age > 150)
            failures.Add("Age must be 150 or less");
        
        // Cross-property validation
        if (options.Age < 18 && string.IsNullOrEmpty(options.ParentEmail))
            failures.Add("Parent email is required for users under 18");
        
        return failures.Count > 0
            ? ValidateOptionsResult.Fail(failures)
            : ValidateOptionsResult.Success;
    }
}

// Register the validator
builder.Services.AddWritableOptions<UserSetting>(conf =>
{
    conf.WithValidator<UserSettingValidator>();
});
```

## Multiple Validators

You can register multiple validators - they all run before saving:

```csharp
builder.Services.AddWritableOptions<UserSetting>(conf =>
{
    // Keep DataAnnotations validation
    conf.UseDataAnnotationsValidation = true;
    
    // Add custom function validator
    conf.WithValidatorFunction(setting =>
    {
        if (setting.Name == "admin")
            return ValidateOptionsResult.Fail("'admin' is reserved");
        return ValidateOptionsResult.Success;
    });
    
    // Add custom class validator
    conf.WithValidator<BusinessRuleValidator>();
});
```

## Validation with DI

Validators can use dependency injection:

```csharp
public class DatabaseValidator : IValidateOptions<UserSetting>
{
    private readonly IUserRepository _repository;
    
    public DatabaseValidator(IUserRepository repository)
    {
        _repository = repository;
    }
    
    public ValidateOptionsResult Validate(string? name, UserSetting options)
    {
        // Check if name already exists in database
        if (_repository.NameExists(options.Name))
            return ValidateOptionsResult.Fail("Name already exists");
        
        return ValidateOptionsResult.Success;
    }
}

// Register validator with DI
builder.Services.AddSingleton<IValidateOptions<UserSetting>, DatabaseValidator>();

// Then use it
builder.Services.AddWritableOptions<UserSetting>(conf =>
{
    conf.WithValidator<DatabaseValidator>();
});
```

## Validation at Startup

:::info
Configuration.Writable intentionally does NOT validate settings at startup. This is by design for user settings scenarios.

**Reason**: With user settings, it's better to prompt for correction when a validation error occurs rather than preventing the application from starting.
:::

If you need startup validation, implement it manually:

```csharp
var app = builder.Build();

// Validate settings at startup
var options = app.Services.GetRequiredService<IWritableOptions<UserSetting>>();
var validator = app.Services.GetRequiredService<IValidateOptions<UserSetting>>();

var result = validator.Validate(null, options.CurrentValue);
if (result.Failed)
{
    Console.WriteLine("Invalid settings detected:");
    foreach (var failure in result.Failures)
        Console.WriteLine($"  - {failure}");
    
    // Handle as appropriate for your application
    Environment.Exit(1);
}

app.Run();
```

## Example: Complete Validation

```csharp
using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Options;

// Settings class with annotations
public partial class AppSettings : IOptionsModel<AppSettings>
{
    [Required, MinLength(3)]
    public string AppName { get; set; } = "MyApp";
    
    [Range(1024, 65535)]
    public int Port { get; set; } = 8080;
    
    [Url]
    public string? ApiEndpoint { get; set; }
    
    public string? AdminEmail { get; set; }
}

// Custom validator
public class AppSettingsValidator : IValidateOptions<AppSettings>
{
    public ValidateOptionsResult Validate(string? name, AppSettings options)
    {
        // Business rule: If ApiEndpoint is set, AdminEmail is required
        if (!string.IsNullOrEmpty(options.ApiEndpoint) && 
            string.IsNullOrEmpty(options.AdminEmail))
        {
            return ValidateOptionsResult.Fail(
                "AdminEmail is required when ApiEndpoint is configured");
        }
        
        return ValidateOptionsResult.Success;
    }
}

// Configuration
builder.Services.AddWritableOptions<AppSettings>(conf =>
{
    conf.UseFile("appsettings.json");
    
    // Enable DataAnnotations validation
    conf.UseDataAnnotationsValidation = true;
    
    // Add custom business rules
    conf.WithValidator<AppSettingsValidator>();
});
```

## Next Steps

- [Change Detection](./change-detection) - Monitor configuration changes
- [Logging](./logging) - Configure logging
- [Advanced: Testing](../advanced/testing) - Test with validation
