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
Use `WritableOptions` as the starting point for reading and writing settings.

```csharp
using Configuration.Writable;

// initialize once (at application startup)
WritableOptions.Initialize<SampleSetting>();

// -------------
// get the writable config instance with the specified setting class
var options = WritableOptions.GetOptions<SampleSetting>();

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
WritableOptions.Initialize<SampleSetting>(opt => { /* ... */ });

// With DI
builder.Services.AddWritableOptions<UserSetting>(opt => { /* ... */ });
```

### Save Location
Default behavior is to save to `{AppContext.BaseDirectory}/usersettings.json` (in general, the same directory as the executable).
If you want to change the save location, use `opt.FilePath` or `opt.UseXxxDirectory().AddFilePath(path)`.

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
opt.UseStandardSaveDirectory("MyAppId").AddFilePath("myconfig");
```

If you want to read files from multiple locations, you can call `AddFilePath` multiple times as follows.

```csharp
// The priority is determined by the following rules:
// 1. The target folder is writable
// 2. The target file already exists
// 3. The target folder already exists
// 4. Order of registration

opt.AddFilePath(@"D:\SpecialFolder\first");
opt.UseStandardSaveDirectory("MyAppId").AddFilePath("second");
opt.UseExecutableDirectory()
    .AddFilePath("third")
    .AddFilePath("child/fourth");

// If you run this without any special setup, third.json will likely be created in the executable's directory (matching rule 3).
// If D:\SpecialFolder already exists, first.json will be created there.
```

If you want to toggle between development and production environments, you can use `#if RELEASE` pattern or `builder.Environtment.IsProduction()`.

```csharp
// those pattern are saved to
// - development: ./mysettings.json (executable directory)
// - production:  %APPDATA%/MyAppId/mysettings.json (on Windows)

// without DI
WritableOptions.Initialize<UserSetting>(opt => {
#if RELEASE
    var isProduction = true;
#else
    var isProduction = false;
#endif
    opt.UseStandardSaveDirectory("MyAppId", enabled: isProduction)
        .AddFilePath("mysettings");
});

// if using IHostApplicationBuilder
builder.Services.AddWritableOptions<UserSetting>(opt => {
    opt.UseStandardSaveDirectory("MyAppId", enabled: builder.Environment.IsProduction())
        .AddFilePath("mysettings");
});
```

### FormatProvider
If you want to change the format when saving files, specify `opt.FormatProvider`.
Currently, the following providers are available:

| Provider                     | Description              | NuGet Package                |
|------------------------------|---------------------------|------------------------------|
| [JsonFormatProvider](https://github.com/arika0093/Configuration.Writable/blob/main/src/Configuration.Writable/Provider/JsonFormatProvider.cs) | save in JSON format.     | Built-in |
| [XmlFormatProvider](https://github.com/arika0093/Configuration.Writable/blob/main/src/Configuration.Writable.Xml/XmlFormatProvider.cs)  | save in XML format.      | [![NuGet Version](https://img.shields.io/nuget/v/Configuration.Writable.Xml?style=flat-square&logo=NuGet&color=0080CC)](https://www.nuget.org/packages/Configuration.Writable.Xml/)  |
| [YamlFormatProvider](https://github.com/arika0093/Configuration.Writable/blob/main/src/Configuration.Writable.Yaml/YamlFormatProvider.cs) | save in YAML format.     | [![NuGet Version](https://img.shields.io/nuget/v/Configuration.Writable.Yaml?style=flat-square&logo=NuGet&color=0080CC)](https://www.nuget.org/packages/Configuration.Writable.Yaml/)  |
| [EncryptFormatProvider](https://github.com/arika0093/Configuration.Writable/blob/main/src/Configuration.Writable.Encrypt/EncryptFormatProvider.cs) | save in AES-256-CBC encrypted JSON format. | [![NuGet Version](https://img.shields.io/nuget/v/Configuration.Writable.Encrypt?style=flat-square&logo=NuGet&color=0080CC)](https://www.nuget.org/packages/Configuration.Writable.Encrypt/)  |

```csharp
// use Json format with indentation
opt.FormatProvider = new JsonFormatProvider() {
    JsonSerializerOptions = { WriteIndented = true },
};

// use Yaml format
// (you need to install Configuration.Writable.Yaml package)
opt.FormatProvider = new YamlFormatProvider();

// use encrypted format
// NOTE: Be aware that this is a simple encryption.
// (you need to install Configuration.Writable.Encrypt package)
opt.FormatProvider = new EncryptFormatProvider("any-encrypt-password");
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

### RegisterInstanceToContainer
If you want to directly reference the settings class, specify `opt.RegisterInstanceToContainer = true`.

> [!NOTE]
> The dynamic update functionality provided by `IReadOnlyOptions<T>` will no longer be available.
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

### Validation
By default, validation using `DataAnnotations` is enabled.
If validation fails, an `OptionsValidationException` is thrown and the settings are not saved.

```csharp
using Microsoft.Extensions.Options;

builder.Services.AddWritableOptions<UserSetting>(opt => {
    // if you want to disable validation of DataAnnotations, do the following:
    // opt.UseDataAnnotationsValidation = false;
});

var options = WritableOptions.GetOptions<UserSetting>();
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
### InstanceName
If you want to manage multiple settings of the same type, you must specify different `InstanceName` for each setting.

```csharp
// first setting
builder.Services.AddWritableOptions<UserSetting>(opt => {
    opt.FilePath = "firstsettings.json";
    opt.InstanceName = "First"; // here
});
// second setting
builder.Services.AddWritableOptions<UserSetting>(opt => {
    opt.FilePath = "secondsettings.json";
    opt.InstanceName = "Second"; // here
});
```

And use `IReadOnlyNamedOptions<T>` and `IWritableNamedOptions<T>` to access them.

```csharp
// use IReadOnlyNamedOptions<T> to read, IWritableNamedOptions<T> to read and write
public class MyService(IWritableNamedOptions<UserSetting> options)
{
    public async Task GetAndSaveAsync()
    {
        var firstSetting = options.Get("First");
        var secondSetting = options.Get("Second");
        await options.SaveAsync("First", setting => {
            setting.Name = "first name";
        });
        await options.SaveAsync("Second", setting => {
            setting.Name = "second name";
        });
    }
}

// Alternatively, you can also use IWritableOptions<T> with the [FromKeyedService] attribute
public class MyOtherService(
    [FromKeyedService("First")] IWritableOptions<UserSetting> firstOptions
)
{
    public async Task GetAndSaveAsync()
    {
        var firstSetting = firstOptions.CurrentValue;
        await firstOptions.SaveAsync(setting => {
            setting.Name = "first name";
        });
    }
}
```

If `RegisterInstanceToContainer` is enabled, you can access it as follows:

```csharp
public class MyService([FromKeyedService("First")] UserSetting options)
{
    public void DirectUseNamedInstance()
    {
        // you can use the instance directly
        Console.WriteLine($">> Name: {options.Name}");
    }
}
```

> [!NOTE]
> When not using DI (direct use of WritableOptions), managing multiple configurations is intentionally not supported.
> This is to avoid complicating usage.

### Dynamic Add/Remove Options
You can dynamically add or remove writable options at runtime using `IWritableOptionsConfigRegistry`.
for example, in addition to common application settings, it is useful when you want to have individual settings for each document opened by the user.

```csharp
// use IWritableOptionsConfigRegistry from DI
public class DynamicOptionsService(IWritableOptionsConfigRegistry<UserSetting> registry)
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

// and you can access IOptionsNamedMonitor<T> or IWritableNamedOptions<T> as usual
public class MyService(IWritableNamedOptions<UserSetting> options)
{
    public void UseOptions()
    {
        var commonSetting = options.Get("Common");
        var documentSetting = options.Get("UserDocument1");
        var name = documentSetting.Name ?? commonSetting.Name ?? "default";
        Console.WriteLine($">> Name: {name}");

        // and save to specific instance
        await options.SaveAsync("UserDocument1", setting => {
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
Here, we describe the main interfaces provided by this library.

### `IOptions<T>`
Provides the value at application startup.
Even if the configuration file is updated later, accessing through this interface will not reflect the changes.  
Named access is not supported.  

This is identical to MS.E.O.'s [`IOptions<T>`](https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.options.ioptions-1).

### `IOptionsMonitor<T>`
Provides the latest value at the current time.
When the configuration file is updated, the latest value is automatically reflected.  
Both named and unnamed access are supported; for unnamed access, use `.CurrentValue`, and for named access, use `.Get(name)`.  

This is identical to MS.E.O.'s [`IOptionsMonitor<T>`](https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.options.ioptionsmonitor-1).

### `IReadOnlyOptions<T>` / `IReadOnlyNamedOptions<T>`
Provides the latest value at the current time.  
When the configuration file is updated, the latest value is automatically reflected.  
These are very similar to the above `IOptionsMonitor<T>`, but differ in the following ways:

* You can retrieve configuration options (such as file save location) via the `GetOptionsConfiguration` method.
* The interfaces are split into two, depending on whether named access is supported:
  * `IReadOnlyOptions<T>`: Does not support named access, only accessible via `.CurrentValue`.
  * `IReadOnlyNamedOptions<T>`: Supports named access only via `.Get(name)`.

### `IWritableOptions<T>` / `IWritableNamedOptions<T>`  
In addition to the features of `IReadOnly(Named)Options<T>`, these support saving settings.  
Other than the addition of the `SaveAsync` method, they are the same as the above `IReadOnly(Named)Options<T>`.

## License
This project is licensed under the Apache-2.0 License.
