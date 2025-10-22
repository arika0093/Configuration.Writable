# Configuration.Writable
[![NuGet Version](https://img.shields.io/nuget/v/Configuration.Writable?style=flat-square&logo=NuGet&color=0080CC)](https://www.nuget.org/packages/Configuration.Writable/) ![GitHub Actions Workflow Status](https://img.shields.io/github/actions/workflow/status/arika0093/Configuration.Writable/test.yaml?branch=main&label=Test&style=flat-square) ![GitHub last commit (branch)](https://img.shields.io/github/last-commit/arika0093/Configuration.Writable?style=flat-square)

A lightweight library that allows for easy saving and referencing of settings, with extensive customization options.

## Features
* Read and write user settings with type safety.
* [Built-in](#FileProvider): Atomic file writing, automatic retry, and backup creation.
* Automatic detection of external changes to configuration files and reflection of the latest settings.
* Simple API that can be easily used in applications both [with](#with-di) and [without](#without-di) DI.
* Extends `Microsoft.Extensions.Options` interfaces, so it works seamlessly with existing code using `IOptions<T>`, `IOptionsMonitor<T>`, etc.
* Supports various file formats (Json, Xml, Yaml, Encrypted, etc...) via [providers](#provider).

[See more...](./docs/article/why-this-library.md)

## Usage
### Setup
Install `Configuration.Writable` from NuGet.

```bash
dotnet add package Configuration.Writable
```

Then, prepare a class (`UserSetting`) in advance that you want to read and write as settings.

```csharp
public class UserSetting
{
    public string Name { get; set; } = "default name";
    public int Age { get; set; } = 20;
}
```

### Without DI
If you are not using DI (for example, in WinForms, WPF, console apps, etc.),
Use `WritableConfig` as the starting point for reading and writing settings.

```csharp
using Configuration.Writable;

// initialize once (at application startup)
WritableConfig.Initialize<SampleSetting>();

// -------------
// get the writable config instance with the specified setting class
var options = WritableConfig.GetOptions<SampleSetting>();

// get the UserSetting instance
var sampleSetting = options.CurrentValue;
Console.WriteLine($">> Name: {sampleSetting.Name}");

// and save to storage
await options.SaveAsync(setting =>
{
    setting.Name = "new name";
});
// By default, it's saved to ./usersettings.json
```

### With DI
If you are using DI (for example, in ASP.NET Core, Blazor, Worker Service, etc.), register `IReadOnlyOptions<T>` and `IWritableOptions<T>` in the DI container.
First, call `AddWritableOptions<T>` to register the settings class.

```csharp
// Program.cs
builder.Services.AddWritableOptions<UserSetting>();
```

Then, inject `IReadOnlyOptions<T>` or `IWritableOptions<T>` to read and write settings.

```csharp
// read config in your class
// you can also use IOptions<T>, IOptionsMonitor<T> or IOptionsSnapshot<T>
public class ConfigReadService(IReadOnlyOptions<UserSetting> options)
{
    public void Print()
    {
        // get the UserSetting instance
        var sampleSetting = options.CurrentValue;
        Console.WriteLine($">> Name: {sampleSetting.Name}");
    }
}

// read and write config in your class
public class ConfigReadWriteService(IWritableOptions<UserSetting> options)
{
    public async Task UpdateAsync()
    {
        // get the UserSetting instance
        var sampleSetting = options.CurrentValue;
        // and save to storage
        await options.SaveAsync(setting =>
        {
            setting.Name = "new name";
        });
    }
}
```

## Customization
### Configuration Method
You can change various settings as arguments to `Initialize` or `AddWritableOptions`.

```csharp
// Without DI
WritableConfig.Initialize<SampleSetting>(opt => { /* ... */ });

// With DI
builder.Services.AddWritableOptions<UserSetting>(opt => { /* ... */ });
```

### Save Location
Default behavior is to save to `{AppContext.BaseDirectory}/usersettings.json` (in general, the same directory as the executable).
If you want to change the save location, use `opt.FilePath` or `opt.UseStandardSaveLocation("MyAppId")`.

For example:
```csharp
// to save to the parent directory
opt.FilePath = "../myconfig";

// to save to child directory
opt.FilePath = "config/myconfig";

// to save to a common settings directory
//   in Windows: %APPDATA%/MyAppId
//   in macOS: $XDG_CONFIG_HOME/MyAppId or ~/Library/Application Support/MyAppId
//   in Linux: $XDG_CONFIG_HOME/MyAppId or ~/.config/MyAppId
opt.UseStandardSaveLocation("MyAppId");
```

If you want to toggle between development and production environments, you can use `#if RELEASE` pattern or `builder.Environtment.IsProduction()`.

```csharp
// those pattern are saved to
// - development: ./mysettings.json (executable directory)
// - production:  %APPDATA%/MyAppId/mysettings.json (on Windows)

// without DI
WritableConfig.Initialize<UserSetting>(opt => {
    opt.FilePath = "mysettings.json";
#if RELEASE
    opt.UseStandardSaveLocation("MyAppId");
#endif
});

// if using IHostApplicationBuilder
builder.Services.AddWritableOptions<UserSetting>(opt => {
    opt.FilePath = "mysettings.json";
    if (builder.Environment.IsProduction()) {
        opt.UseStandardSaveLocation("MyAppId");
    }
});
```

### Provider
If you want to change the format when saving files, specify `opt.Provider`.
Currently, the following providers are available:

| Provider                     | Description              | NuGet Package                |
|------------------------------|---------------------------|------------------------------|
| [WritableConfigJsonProvider](https://github.com/arika0093/Configuration.Writable/blob/main/src/Configuration.Writable/Provider/WritableConfigJsonProvider.cs) | save in JSON format.     | Built-in |
| [WritableConfigXmlProvider](https://github.com/arika0093/Configuration.Writable/blob/main/src/Configuration.Writable.Xml/WritableConfigXmlProvider.cs)  | save in XML format.      | [![NuGet Version](https://img.shields.io/nuget/v/Configuration.Writable.Xml?style=flat-square&logo=NuGet&color=0080CC)](https://www.nuget.org/packages/Configuration.Writable.Xml/)  |
| [WritableConfigYamlProvider](https://github.com/arika0093/Configuration.Writable/blob/main/src/Configuration.Writable.Yaml/WritableConfigYamlProvider.cs) | save in YAML format.     | [![NuGet Version](https://img.shields.io/nuget/v/Configuration.Writable.Yaml?style=flat-square&logo=NuGet&color=0080CC)](https://www.nuget.org/packages/Configuration.Writable.Yaml/)  |
| [WritableConfigEncryptProvider](https://github.com/arika0093/Configuration.Writable/blob/main/src/Configuration.Writable.Encrypt/WritableConfigEncryptProvider.cs) | save in AES-256-CBC encrypted JSON format. | [![NuGet Version](https://img.shields.io/nuget/v/Configuration.Writable.Encrypt?style=flat-square&logo=NuGet&color=0080CC)](https://www.nuget.org/packages/Configuration.Writable.Encrypt/)  |

```csharp
// use Json format with indentation
opt.Provider = new WritableConfigJsonProvider() {
    JsonSerializerOptions = { WriteIndented = true },
};

// use Yaml format
// (you need to install Configuration.Writable.Yaml package)
opt.Provider = new WritableConfigYamlProvider();

// use encrypted format
// NOTE: Be aware that this is a simple encryption.
// (you need to install Configuration.Writable.Encrypt package)
opt.Provider = new WritableConfigEncryptProvider("any-encrypt-password");
```

> [!NOTE]
> To reduce dependencies and allow users to choose only the features they need, providers are offered as separate packages.
> That said, the JSON provider is built into the main package because since many users are likely to use the JSON format.

### FileProvider
Default FileProvider (`CommonFileProvider`) supports the following features:

* Automatically retry when file access fails (default is max 3 times, wait 100ms each)
* Create backup files rotated by timestamp (default is disabled)
* Atomic file writing (write to a temporary file first, then rename it)
* Thread-safe: uses internal semaphore to ensure safe concurrent access

If you want to change the way files are written, create a class that implements `IFileProvider` and specify it in `opt.FileProvider`.

```csharp
using Configuration.Writable.FileProvider;

opt.FileProvider = new CommonFileProvider() {
    // retry up to 5 times when file access fails
    MaxRetryCount = 5,
    // wait 100ms, 200ms, 300ms, ... before each retry
    RetryDelay = (attempt) => 100 * attempt,
    // keep 5 backup files when saving
    BackupMaxCount = 5,
};
```

### Direct Reference Without Option Type
If you want to directly reference the settings class, specify `opt.RegisterInstanceToContainer = true`.

> [!NOTE]
> The dynamic update functionality provided by `IOptionsMonitor<T>` will no longer be available.
> Be mindful of lifecycle management, as settings applied during instance creation will be reflected.

```csharp
builder.Services.AddWritableOptions<UserSetting>(opt => {
    opt.RegisterInstanceToContainer = true;
});

// you can use UserSetting directly
public class MyService(UserSetting setting)
{
    public void Print()
    {
        Console.WriteLine($">> Name: {setting.Name}");
    }
}

// and you can also use IReadOnlyOptions<T> as usual
public class MyOtherService(IReadOnlyOptions<UserSetting> options)
{
    public void Print()
    {
        var setting = options.CurrentValue;
        Console.WriteLine($">> Name: {setting.Name}");
    }
}
```

### Logging
Logging is enabled by default in DI environments.  
If you are not using DI, or if you want to override the logging settings, you can enable logging by specifying `opt.Logger`.

```csharp
// without DI
// package add Microsoft.Extensions.Logging.Console
opt.Logger = LoggerFactory
    // enable console logging 
    .Create(builder => builder.AddConsole())
    .CreateLogger("Configuration.Writable");

// with DI
// no setup required (uses the logger from DI)
```

When the output level is set to Information, mainly the following two logs are output.

```log
info: Configuration.Writable[0]
      Configuration file change detected: mysettings.json (Renamed)
info: Configuration.Writable[0]
      Configuration saved successfully to mysettings.json
```

### SectionName
When saving settings, they are written to a configuration file in a structured format. By default, settings are stored directly at the root level:

```jsonc
{
  // properties of UserSetting are stored directly at the root level
  "Name": "custom name",
  "Age": 30
}
```

To organize settings under a specific section, use `opt.SectionName`.

```jsonc
{
  // opt.SectionName = "MyAppSettings:Foo:Bar"
  "MyAppSettings": {
    "Foo": {
      "Bar": {
        // properties of UserSetting
        "Name": "custom name",
        "Age": 30
      }
    }
  }
}
```

### InstanceName
If you want to manage multiple settings of the same type, you must specify different `InstanceName` for each setting.

```csharp
// first setting
builder.Services.AddWritableOptions<UserSetting>(opt => {
    opt.FilePath = "firstsettings.json";
    opt.InstanceName = "First";
});
// second setting
builder.Services.AddWritableOptions<UserSetting>(opt => {
    opt.FilePath = "secondsettings.json";
    opt.InstanceName = "Second";
});

// and get each setting from DI
public class MyService(IWritableOptions<UserSetting> options)
{
    public void GetAndSave()
    {
        // cannot use .CurrentValue if multiple settings of the same type are registered
        var firstSetting = options.Get("First");
        var secondSetting = options.Get("Second");
        // and you must specify instance name when saving
        await options.SaveWithNameAsync("First", setting => {
            setting.Name = "first name";
        });
        await options.SaveWithNameAsync("Second", setting => {
            setting.Name = "second name";
        });
    }
}
```

> [!NOTE]
> When not using DI (direct use of WritableConfig), managing multiple configurations is intentionally not supported.
> This is to avoid complicating usage.

### Validation
By default, validation using `DataAnnotations` is enabled.
If validation fails, an `OptionsValidationException` is thrown and the settings are not saved.

```csharp
using Microsoft.Extensions.Options;

builder.Services.AddWritableOptions<UserSetting>(opt => {
    // if you want to disable validation of DataAnnotations, do the following:
    // opt.UseDataAnnotationsValidation = false;
});

var options = WritableConfig.GetOptions<UserSetting>();
try {
    await options.SaveAsync(setting => {
        setting.Name = "ab"; // too short
        setting.Age = 200;  // out of range
    });
}
catch (OptionsValidationException ex)
{
    Console.WriteLine($">> Validation failed: {ex.Message}");
    // setting is not saved if validation fails
}

internal class UserSetting
{
    [Required, MinLength(3)]
    public string Name { get; set; } = "default name";
    [Range(0, 150)]
    public int Age { get; set; } = 20;
}
```

To use source generators for DataAnnotations, use the following pattern.

```csharp
builder.Services.AddWritableOptions<UserSetting>(opt => {
    // disable attributes-based validation
    opt.UseDataAnnotationsValidation = false;
    // enable source-generator-based validation
    opt.WithValidator<UserSettingValidator>();
});

internal class UserSetting { /* ... */ }

[OptionsValidator]
public partial class UserSettingValidator : IValidateOptions<UserSetting>;
```

Alternatively, you can add custom validation using `WithValidatorFunction` or `WithValidator`.

```csharp
using Microsoft.Extensions.Options;

builder.Services.AddWritableOptions<UserSetting>(opt => {
    // add custom validation function
    opt.WithValidatorFunction(setting => {
        if (setting.Name.Contains("invalid"))
            return ValidateOptionsResult.Fail("Name must not contain 'invalid'.");
        return ValidateOptionsResult.Success;
    });
    // or use a custom validator class
    opt.WithValidator<MyCustomValidator>();
});

// IValidateOptions sample
internal class MyCustomValidator : IValidateOptions<UserSetting>
{
    public ValidateOptionsResult Validate(string? name, UserSetting options)
    {
        if (options.Age < 10)
            return ValidateOptionsResult.Fail("Age must be at least 10.");
        if (options.Age > 100)
            return ValidateOptionsResult.Fail("Age must be 100 or less.");
        return ValidateOptionsResult.Success;
    }
}
```

> [!NOTE]
> Validation at startup is intentionally not provided. The reason is that in the case of user settings, it is preferable to prompt for correction rather than prevent startup when a validation error occurs.

## Advanced Usage
### Dynamic Add/Remove Options
You can dynamically add or remove writable options at runtime using `IOptionsConfigRegistry`.
for example, in addition to common application settings, it is useful when you want to have individual settings for each document opened by the user.

```csharp
// use IOptionsConfigRegistry from DI
public class DynamicOptionsService(IOptionsConfigRegistry<UserSetting> registry)
{
    public void AddNewOptions(string instanceName, string filePath)
    {
        registry.TryAdd(opt => {
            opt.InstanceName = instanceName;
            opt.FilePath = filePath;
        });
    }

    public void RemoveOptions(string instanceName)
    {
        registry.TryRemove(instanceName);
    }
}

// and you can access IOptionsMonitor<T> or IWritableOptions<T> as usual
public class MyService(IWritableOptions<UserSetting> options)
{
    public void UseOptions()
    {
        var commonSetting = options.Get("Common");
        var documentSetting = options.Get("UserDocument1");
        var name = documentSetting.Name ?? commonSetting.Name ?? "default";
        Console.WriteLine($">> Name: {name}");

        // and save to specific instance
        await options.SaveWithNameAsync("UserDocument1", setting => {
            setting.Name = "document specific name";
        }
    }
}
```

### Multiple Settings in a Single File
Using `ZipFileProvider`, you can save multiple settings classes in a single configuration file.
for example, to save `Foo`(foo.json) and `Bar`(bar.json) in `configurations.zip`:

```csharp
var zipFileProvider = new ZipFileProvider { ZipFileName = "configurations.zip" };

// initialize each setting with the same file provider
builder.Services.AddWritableOptions<Foo>(opt =>
{
    opt.FilePath = "foo";
    opt.FileProvider = zipFileProvider;
});
builder.Services.AddWritableOptions<Bar>(opt =>
{
    opt.FilePath = "bar";
    opt.FileProvider = zipFileProvider;
});
```

### Direct Property Manipulation
You can directly manipulate configuration properties at the key level using `IOptionOperator<T>`. This is useful for operations like deleting specific keys from the configuration file.

```csharp
await options.SaveAsync((setting, op) =>
{
    // Update settings as usual
    setting.Name = "new name";
    // Delete specific keys from the configuration file
    op.DeleteKey(s => s.SomeProperty);
    op.DeleteKey(s => s.Parent.Child);
});
```

This pattern allows you to:
- Delete keys without affecting other properties in the configuration file
- Perform key-level operations that go beyond simple value updates
- Maintain more control over the configuration file structure


## Testing
If you simply want to obtain `IReadOnlyOptions<T>` or `IWritableOptions<T>`, using `WritableOptionsStub` is straightforward.

```csharp
using Configuration.Writable.Testing;

var settingValue = new UserSetting();
var options = WritableOptionsStub.Create(settingValue);

// and use options in your test
var yourService = new YourService(options);
yourService.DoSomething();

// settingValue is updated when yourService changes it
Assert.Equal("expected name", settingValue.Name);
```

If you want to perform tests that actually involve writing to the file system, use `WritableOptionsSimpleInstance`.

```csharp
var sampleFilePath = Path.GetTempFileName();
var instance = new WritableOptionsSimpleInstance<UserSetting>();
instance.Initialize(opt => {
    opt.FilePath = sampleFilePath;
});
var option = instance.GetOptions();

// and use options in your test
var yourService = new YourService(options);
yourService.DoSomething();

// sampleFilePath now contains the updated settings
var json = File.ReadAllText(sampleFilePath);
Assert.Contains("expected name", json);
```

## Interfaces
### IReadOnlyOptions<T>
An interface for reading the settings of the registered type `T`.  
It automatically reflects the latest settings when the underlying configuration is updated.  
This interface provides functionality equivalent to [`IOptionsMonitor<T>`](https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.options.ioptionsmonitor-1) from MS.E.O.

```csharp
public interface IReadOnlyOptions<T> : IOptionsMonitor<T> where T : class, new()
```

The additional features compared to `IOptionsMonitor<T>` are as follows:

* The `GetConfigurationOptions` method to retrieve configuration options.
* In environments where file change detection is not possible, you can always get the latest settings (internal cached value is updated when `SaveAsync` is called).

### IWritableOptions<T>
An interface for reading and writing the settings of the registered type `T`.
It provides the same functionality as `IReadOnlyOptions<T>`, with additional support for saving settings.

```csharp
public interface IWritableOptions<T> : IReadOnlyOptions<T> where T : class, new()
```

## ToDo
* Support version update migration
* Support multiple configurations merging

## License
This project is licensed under the Apache-2.0 License.
