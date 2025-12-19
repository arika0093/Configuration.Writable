# Configuration.Writable
[![NuGet Version](https://img.shields.io/nuget/v/Configuration.Writable?style=flat-square&logo=NuGet&color=0080CC)](https://www.nuget.org/packages/Configuration.Writable/) ![GitHub Actions Workflow Status](https://img.shields.io/github/actions/workflow/status/arika0093/Configuration.Writable/test.yaml?branch=main&label=Test&style=flat-square) 

A lightweight library that allows for easy saving and referencing of settings, with extensive customization options.

## Features
* Read and write user settings with type safety.
* [Built-in](#FileProvider): Atomic file writing, automatic retry, and backup creation.
* [Automatic detection](#change-detection) of external changes to configuration files and reflection of the latest settings.
* Simple API that can be easily used in applications both [with](#with-di) and [without](#without-di) DI.
* Highly [customizable configuration](#customization) methods, save locations, file formats, validation, logging, and more.

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
- [Configuration Method](#configuration-method)
- [Save Location](#save-location)
- [FormatProvider](#formatprovider)
- [FileProvider](#fileprovider)
- [Change Detection](#change-detection)
- [RegisterInstanceToContainer](#registerinstancetocontainer)
- [Logging](#logging)
- [SectionName](#sectionname)
- [Validation](#validation)

### Configuration Method
You can change various settings as arguments to `Initialize` or `AddWritableOptions`.

```csharp
// Without DI
WritableOptions.Initialize<SampleSetting>(conf => { /* ... */ });

// With DI
builder.Services.AddWritableOptions<UserSetting>(conf => { /* ... */ });
```

### Save Location
Default behavior is to save to `{AppContext.BaseDirectory}/usersettings.json` (in general, the same directory as the executable).
If you want to change the save location, use `conf.UseFile(path)` or `conf.UseXxxDirectory().AddFilePath(path)`.

For example:
```csharp
// to save to the parent directory
conf.UseFile("../myconfig");
// to save to child directory
conf.UseFile("config/myconfig");

// to save to a common settings directory
//   in Windows: %APPDATA%/MyAppId
//   in macOS: $XDG_CONFIG_HOME/MyAppId or ~/Library/Application Support/MyAppId
//   in Linux: $XDG_CONFIG_HOME/MyAppId or ~/.config/MyAppId
conf.UseStandardSaveDirectory("MyAppId")
    .AddFilePath("myconfig");
```

If you want to read/write files from multiple locations, you can call `UseXxxDirectory().AddFilePath` multiple times as follows.  
When multiple locations are specified, the load/save destination is determined on initialization on the following priority order:

1. Explicit priority (descending)
1. Target file already exists and able to open with write access
1. Target directory already exists and able to create file
1. Order of registration (earlier registrations have higher priority)

```csharp
conf.UseCustomDirectory(@"D:\SpecialFolder\")
    .AddFilePath("first");        // is not existing folder/file yet
conf.UseStandardSaveDirectory("MyAppId")
    .AddFilePath("second", priority: 10);
conf.UseExecutableDirectory()
    .AddFilePath("third")         // is already exist directory but not file
    .AddFilePath("child/fourth"); // is already exist file

// In this case, the priorities are as follows:
// 1: %APPDATA%/MyAppId/second (priority 10)
// 2: ./child/fourth (target file exists)
// 3: ./third (target directory exists)
// 4: D:\SpecialFolder\first (target directory/file does not exist)
```

If you want to toggle between development and production environments, you can use `#if RELEASE` pattern or `builder.Environtment.IsProduction()`.

```csharp
// those pattern are saved to
// - development: ./mysettings.json (executable directory)
// - production:  %APPDATA%/MyAppId/mysettings.json (on Windows)

// without DI
WritableOptions.Initialize<UserSetting>(conf => {
#if DEBUG
    var isProduction = false;
#else
    var isProduction = true;
#endif
    conf.UseStandardSaveDirectory("MyAppId", enabled: isProduction)
        .AddFilePath("mysettings");
});

// if using IHostApplicationBuilder
builder.Services.AddWritableOptions<UserSetting>(conf => {
    var isProd = builder.Environment.IsProduction();
    conf.UseStandardSaveDirectory("MyAppId", enabled: isProd)
        .AddFilePath("mysettings");
});
```

### FormatProvider
By default, files are saved in JSON format. If you want to customize the format, specify `conf.FormatProvider` as follows.

```csharp
// use Json format with indentation
conf.FormatProvider = new JsonFormatProvider() {
    JsonSerializerOptions = {
        // you can customize JsonSerializerOptions as needed
        WriteIndented = true
        // for source-generation-based serialize/deserialize, set TypeInfoResolver here
        TypeInfoResolver = SampleSettingSerializerContext.Default,
    },
};

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(SampleSetting))]
public partial class SampleSettingSerializerContext : JsonSerializerContext;
```

If you want to save in other formats, install the required packages and specify the corresponding provider.
Currently, the following providers are available:

| Provider                     | Description              | NuGet Package                |
|------------------------------|---------------------------|------------------------------|
| [JsonFormatProvider](https://github.com/arika0093/Configuration.Writable/blob/main/src/Configuration.Writable/Provider/JsonFormatProvider.cs) | save in JSON format.     | Built-in |
| [XmlFormatProvider](https://github.com/arika0093/Configuration.Writable/blob/main/src/Configuration.Writable.Xml/XmlFormatProvider.cs)  | save in XML format.      | [![NuGet Version](https://img.shields.io/nuget/v/Configuration.Writable.Xml?style=flat-square&logo=NuGet&color=0080CC)](https://www.nuget.org/packages/Configuration.Writable.Xml/)  |
| [YamlFormatProvider](https://github.com/arika0093/Configuration.Writable/blob/main/src/Configuration.Writable.Yaml/YamlFormatProvider.cs) | save in YAML format.     | [![NuGet Version](https://img.shields.io/nuget/v/Configuration.Writable.Yaml?style=flat-square&logo=NuGet&color=0080CC)](https://www.nuget.org/packages/Configuration.Writable.Yaml/)  |
| [EncryptFormatProvider](https://github.com/arika0093/Configuration.Writable/blob/main/src/Configuration.Writable.Encrypt/EncryptFormatProvider.cs) | save in AES-256-CBC encrypted JSON format. | [![NuGet Version](https://img.shields.io/nuget/v/Configuration.Writable.Encrypt?style=flat-square&logo=NuGet&color=0080CC)](https://www.nuget.org/packages/Configuration.Writable.Encrypt/)  |

```csharp
// use Yaml format
// (you need to install Configuration.Writable.Yaml package)
conf.FormatProvider = new YamlFormatProvider();

// use encrypted format
// NOTE: Be aware that this is a simple encryption.
// (you need to install Configuration.Writable.Encrypt package)
conf.FormatProvider = new EncryptFormatProvider("any-encrypt-password");
```

### FileProvider
Default FileProvider (`CommonFileProvider`) supports the following features:

* Automatically retry when file access fails (default is max 3 times, wait 100ms each)
* Create backup files rotated by timestamp (default is disabled)
* Atomic file writing (write to a temporary file first, then rename it)
* Thread-safe: uses internal semaphore to ensure safe concurrent access

If you want to change the way files are written, create a class that implements `IFileProvider` and specify it in `conf.FileProvider`.

```csharp
using Configuration.Writable.FileProvider;

conf.FileProvider = new CommonFileProvider() {
    // retry up to 5 times when file access fails
    MaxRetryCount = 5,
    // wait 100ms, 200ms, 300ms, ... before each retry
    RetryDelay = (attempt) => 100 * attempt,
    // keep 5 backup files when saving
    BackupMaxCount = 5,
};
```

### Change Detection
You can automatically detect changes to the file and use the latest settings.  
For example:

```csharp
public class MyService(IWritableOptions<UserSetting> options) : IDisposable
{
    public void WatchStart()
    {
        // register change callback
        var disposable = options.OnChange(newSetting => {
            // called when the configuration file is changed externally
            Console.WriteLine($">> Settings changed: Name={newSetting.Name}, Age={newSetting.Age}");
        });
    }

    public async Task UpdateAsync()
    {
        // get the UserSetting instance
        var sampleSetting = options.CurrentValue;
        // and save to storage
        await options.SaveAsync(setting =>
        {
            setting.Name = "new name";
        });
        // this will trigger the OnChange callback
    }

    public void Dispose() => disposable?.Dispose();

    private IDisposable? disposable;
}
```

By default, throttling is enabled to suppress high-frequency file changes. Additional changes within 1 second from change detection are ignored by default.  
If you want to change the throttle duration, specify `conf.OnChangeThrottleMs`.

```csharp
conf.OnChangeThrottleMs = 500; // customize to 500ms
conf.OnChangeThrottleMs = 0;   // disable throttling
```

### RegisterInstanceToContainer
If you want to directly reference the settings class, specify `conf.RegisterInstanceToContainer = true`.

> [!NOTE]
> The dynamic update functionality provided by `IReadOnlyOptions<T>` will no longer be available.
> Be mindful of lifecycle management, as settings applied during instance creation will be reflected.

```csharp
builder.Services.AddWritableOptions<UserSetting>(conf => {
    conf.RegisterInstanceToContainer = true;
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
If you are not using DI, or if you want to override the logging settings, you can enable logging by specifying `conf.Logger`.

```csharp
// without DI
// package add Microsoft.Extensions.Logging.Console
conf.Logger = LoggerFactory
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

To organize settings under a specific section, use `conf.SectionName`.

```jsonc
{
  // conf.SectionName = "MyAppSettings:Foo:Bar"
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

builder.Services.AddWritableOptions<UserSetting>(conf => {
    // if you want to disable validation of DataAnnotations, do the following:
    // conf.UseDataAnnotationsValidation = false;
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
builder.Services.AddWritableOptions<UserSetting>(conf => {
    // disable attributes-based validation
    conf.UseDataAnnotationsValidation = false;
    // enable source-generator-based validation
    conf.WithValidator<UserSettingValidator>();
});

internal class UserSetting { /* ... */ }

[OptionsValidator]
public partial class UserSettingValidator : IValidateOptions<UserSetting>;
```

Alternatively, you can add custom validation using `WithValidatorFunction` or `WithValidator`.

```csharp
using Microsoft.Extensions.Options;

builder.Services.AddWritableOptions<UserSetting>(conf => {
    // add custom validation function
    conf.WithValidatorFunction(setting => {
        if (setting.Name.Contains("invalid"))
            return ValidateOptionsResult.Fail("Name must not contain 'invalid'.");
        return ValidateOptionsResult.Success;
    });
    // or use a custom validator class
    conf.WithValidator<MyCustomValidator>();
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
builder.Services.AddWritableOptions<UserSetting>("First", conf => {
    conf.UseFile("firstsettings.json");
});
// second setting
builder.Services.AddWritableOptions<UserSetting>("Second", conf => {
    conf.UseFile("secondsettings.json");
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

        // If specifying the name each time is cumbersome, you can also use GetSpecifiedInstance
        // By doing so, you can handle it in the same way as regular IReadOnlyOptions/IWritableOptions.
        var firstOptions = options.GetSpecifiedInstance("First");
        var firstSetting2 = firstOptions.CurrentValue;
        await firstOptions.SaveAsync(setting => {
            setting.Name = "first name 2";
        });
    }
}

// Alternatively, you can also use IWritableOptions<T> with the [FromKeyedService] attribute
public class MyOtherService(
    [FromKeyedService("First")]
    IWritableOptions<UserSetting> firstOptions
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
        registry.TryAdd(instanceName, conf => {
            conf.UseFile(filePath);
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
        });
    }
}
```

### Multiple Settings in a Single File
Using `ZipFileProvider`, you can save multiple settings classes in a single configuration file.
for example, to save `Foo`(foo.json) and `Bar`(bar.json) in `configurations.zip`:

```csharp
var zipFileProvider = new ZipFileProvider { ZipFileName = "configurations.zip" };

// initialize each setting with the same file provider
builder.Services.AddWritableOptions<Foo>(conf =>
{
    conf.UseFile("foo");
    conf.FileProvider = zipFileProvider;
});
builder.Services.AddWritableOptions<Bar>(conf =>
{
    conf.UseFile("bar");
    conf.FileProvider = zipFileProvider;
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
instance.Initialize(conf => {
    conf.UseFile(sampleFilePath);
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
Change detection is done by registering a callback with the `OnChange(Action<T, string> listener)` method. Since changes for both unnamed and named instances are detected, you need to identify the target name from the second string argument as needed.

This is identical to MS.E.O.'s [`IOptionsMonitor<T>`](https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.options.ioptionsmonitor-1).

### `IReadOnlyOptions<T>` / `IReadOnlyNamedOptions<T>`
Provides the latest value at the current time.
When the configuration file is updated, the latest value is automatically reflected.  
These are very similar to the above `IOptionsMonitor<T>`, but differ in the following ways:

* You can retrieve configuration options (such as file save location) via the `GetOptionsConfiguration` method.
* The interfaces are split into two, depending on whether named access is supported:
  * `IReadOnlyOptions<T>`: Does not support named access, only accessible via `.CurrentValue`.
  * `IReadOnlyNamedOptions<T>`: Supports named access only via `.Get(name)`.
* The method of registering `OnChange` callbacks is different:
  * `IReadOnlyOptions<T>`: `OnChange(Action<T> listener)` method to register a callback for changes to the unnamed instance.
  * `IReadOnlyNamedOptions<T>`: `OnChange(string name, Action<T> listener)` method to register a callback for changes to a specific named instance.
  * The traditional `OnChange(Action<T, string> listener)` is also available.

### `IWritableOptions<T>` / `IWritableNamedOptions<T>`  
In addition to the features of `IReadOnly(Named)Options<T>`, these support saving settings.  
Other than the addition of the `SaveAsync` method, they are the same as the above `IReadOnly(Named)Options<T>`.

## License
This project is licensed under the Apache-2.0 License.
