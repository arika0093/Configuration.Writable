# Configuration.Writable
[![NuGet Version](https://img.shields.io/nuget/v/Configuration.Writable?style=flat-square&logo=NuGet&color=0080CC)](https://www.nuget.org/packages/Configuration.Writable/) ![GitHub Actions Workflow Status](https://img.shields.io/github/actions/workflow/status/arika0093/Configuration.Writable/test.yaml?branch=main&label=Test&style=flat-square) 

A lightweight library that allows for easy saving and referencing of settings, with extensive customization options.

## Features
* Read and write user settings with type safety.
* [Built-in](#FileProvider) Atomic file writing, automatic retry, and backup creation.
* [Automatic detection](#change-detection) of external changes to configuration files and reflection of the latest settings.
* Simple API that can be easily used in applications both [with](#host-application-with-di) and [without](#simple-application-without-di) DI.
* Partial updates to settings make it usable even with [ASP.NET Core](#sectionname).
* Highly [customizable configuration](#customization) methods, save locations, file formats, validation, logging, and more.

## Usage
### Setup
Install `Configuration.Writable` from NuGet.

```bash
dotnet add package Configuration.Writable
```

Then, prepare a class (`UserSetting`) in advance that you want to read and write as settings.

```csharp
using Configuration.Writable;

// add IOptionsModel and mark as partial class
public partial class UserSetting : IOptionsModel<UserSetting>
{
    public string Name { get; set; } = "default name";
    public int Age { get; set; } = 20;
}
```

> [!NOTE]
> By adding `IOptionsModel<T>`, the `DeepClone` method is automatically generated via [IDeepCloneable](https://github.com/arika0093/IDeepCloneable).
> Therefore, you do not need to implement the `DeepClone` method yourself.

### Simple Application (Without DI)
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

### Host Application (With DI)
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

### ASP.NET Core (With DI)
By explicitly specifying the [SectionName](#sectionname), you can dynamically update existing configuration files such as `appsettings.json`.

```csharp
var builder = WebApplication.CreateSlimBuilder(args);
builder.Services.AddWritableOptions<SampleSetting>(conf =>
{
    conf.UseFile("appsettings.json");
    conf.SectionName = "MySetting";
});

// In this case, the settings will be saved under the `MySetting` section in `appsettings.json`.
```

Reading and writing settings is performed in the same way as described above in [Host Application](#host-application-with-di).

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
using Configuration.Writable.FormatProvider;

// use Json format with indentation
conf.FormatProvider = new JsonFormatProvider() {
    JsonSerializerOptions = new () {
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
using Configuration.Writable.FormatProvider;

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
When saving settings, they are written to a configuration file in a structured format.
By default, settings are stored directly at the root level:

```jsonc
{
  // properties of UserSetting are stored directly at the root level
  "Name": "custom name",
  "Age": 30
}
```

For example, if you want to write to `appsettings.json` and coexist with other settings, you can use `conf.SectionName` to group settings in a specific section.  
To write settings to a specific section, only that section is updated while the rest remains unchanged.

```csharp
// configure to save under MyAppSettings:Foo:Bar section
builder.Services.AddWritableOptions<UserSetting>(conf => {
    conf.UseFile("appsettings.json");
    conf.SectionName = "MyAppSettings:Foo:Bar";
});

// and save settings
options.SaveAsync(setting => {
    setting.Name = "custom name";
    setting.Age = 30;
});
```

The resulting `appsettings.json` will look like this:

```jsonc
{
  "MyAppSettings": {
    "Foo": {
      "Bar": {
        // saved under MyAppSettings:Foo:Bar section
        "Name": "custom name",
        "Age": 30
      }
    }
  },
  // another settings remain unchanged
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft": "Warning"
    }
  }
}
```

By using this method, it is possible to save multiple configuration classes in different sections within the same file.

```csharp
builder.Services.AddWritableOptions<UserSettingA>(conf => {
    conf.UseFile("appsettings.json");
    conf.SectionName = "SettingsA";
});
builder.Services.AddWritableOptions<UserSettingB>(conf => {
    conf.UseFile("appsettings.json");
    conf.SectionName = "SettingsB";
});
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

### Migration
Provides a mechanism for automatically migrating old version configuration files to the new version when the structure of the configuration file changes.
First, prepare a settings class for each version and implement `IVersionedOptionsModel<T>`.

```csharp
// Version 1
public partial class UserSettingV1 : IVersionedOptionsModel<UserSettingV1>
{
    public int Version { get; set; } = 1;
    public string Name { get; set; } = "default name";
}

// Version 2
public partial class UserSettingV2 : IVersionedOptionsModel<UserSettingV2>
{
    public int Version { get; set; } = 2;
    public List<string> Names { get; set; } = [];
}
```

Next, register the migration as follows:
```csharp
// register latest version (V2)
builder.Services.AddWritableOptions<UserSettingV2>(conf => {
    // and register migration from V1 to V2
    conf.WithMigration<UserSettingV1, UserSettingV2>(oldSetting => {
        return new UserSettingV2 {
            Names = [oldSetting.Name] // migrate Name to Names list
        };
    });
});
```

That's it. When reading the settings, if an old version configuration file is detected, migration will be performed automatically, and the new version format will be saved the next time you save the settings.


## Advanced Usage
### Support NativeAOT
With a few settings, you can use this library in NativeAOT environments. The following three steps are required:
1. Prepare a `JsonSerializerContext` and `OptionsValidator`
2. Use `JsonAotFormatProvider` instead of `JsonFormatProvider`
3. Disable the built-in validation and use a Source Generator-based validator

```csharp
// 1. prepare JsonSerializerContext and OptionsValidator
[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(SampleSetting))]
public partial class SampleSettingSerializerContext : JsonSerializerContext;

[OptionsValidator]
public partial class SampleSettingValidator : IValidateOptions<SampleSetting>;

// -----
// 2. use JsonAotFormatProvider
conf.FormatProvider = new JsonAotFormatProvider(SampleSettingSerializerContext.Default);

// 3. disable built-in validation and use source-generator-based validator
conf.UseDataAnnotationsValidation = false;
conf.WithValidator<SampleSettingValidator>();
```

For more details, please refer to the [Example.ConsoleApp.NativeAOT](./example/Example.ConsoleApp.NativeAot/) project.

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

### CloneStrategy
To improve performance, the configuration file is not read every time. Instead, it is loaded and stored as an internal cache when a change event is detected.  
To prevent direct editing of this cache, a deep copy is created and provided to the user each time it is retrieved or saved.

By default, deep copying of the settings class is supported via [IDeepCloneable](https://github.com/arika0093/IDeepCloneable).  
This is sufficient for most cases, but if you want to use a different cloning method, you can customize it with `conf.UseCustomCloneStrategy`.

```csharp
conf.UseCustomCloneStrategy(original => {
    // Any custom cloning library can be used
    return original.DeepClone();
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

<img src="./assets/interfaces.drawio.svg" alt="Interfaces Diagram" width="600"/>

### `IOptions`
Provides the value at application startup.
Even if the configuration file is updated later, accessing through this interface will not reflect the changes.  
Named access is not supported. Only the unnamed instance is accessible via the `.Value` property.  

This is identical to MS.E.O.'s [`IOptions`](https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.options.ioptions-1).

### `IOptionsSnapshot`
Provides the latest value per request (Scoped). The content of the configuration file at the time the object is created is reflected, and even if the configuration file is updated later, the latest value is not reflected.  
Named access is supported via the `.Get(name)` method.

This is identical to MS.E.O.'s [`IOptionsSnapshot`](https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.options.ioptionssnapshot-1).

### `IOptionsMonitor`
Provides the latest value at the current time.
When the configuration file is updated, the latest value is automatically reflected.  
Both named and unnamed access are supported; for unnamed access, use `.CurrentValue`, and for named access, use `.Get(name)`.  
Change detection is done by registering a callback with the `OnChange(Action<T, string> listener)` method. Since changes for both unnamed and named instances are detected, you need to identify the target name from the second string argument as needed.

This is identical to MS.E.O.'s [`IOptionsMonitor`](https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.options.ioptionsmonitor-1).

### `IReadOnlyOptions` / `IReadOnlyNamedOptions`
Provides the latest values at the current point in time. When the configuration file is updated, the latest values are automatically reflected.  
These are very similar to the `IOptionsMonitor` mentioned above but have been improved for easier use in the following ways. Unless there is a specific reason, it is recommended to use these interfaces.

* [`IReadOnlyOptions`](./src/Configuration.Writable.Core/Abstractions/IReadOnlyOptions.cs)
    * A simple read-only options interface that does not support named access.
    * Use the `.CurrentValue` property to access the current value.
    * Use the `OnChange(Action<T> listener)` method to monitor changes to the options.
* [`IReadOnlyNamedOptions`](./src/Configuration.Writable.Core/Abstractions/IReadOnlyNamedOptions.cs)
    * A read-only options interface that supports named access.
    * Use the `.Get(name)` method to access named options.
    * Use the `OnChange(string name, Action<T> listener)` method to monitor changes to specific named options.
    * Use `GetSpecifiedInstance(name)` to retrieve a pre-specified `IReadOnlyOptions` instance.
* Both interfaces allow you to retrieve configuration options (e.g., file save locations) using the `GetOptionsConfiguration` method.

### `IWritableOptions` / `IWritableNamedOptions`
In addition to the features of `IReadOnly(Named)Options`, these support saving settings.
Other than the addition of the `SaveAsync` method, they are the same as the above `IReadOnly(Named)Options`.

### `IReadOnlyOptionsMonitor<T>`
This interface combines the functionalities of `IReadOnlyOptions`, `IReadOnlyNamedOptions`, and `IOptionsMonitor<T>`.
They are provided mainly to ensure compatibility with codebases that already use `IOptionsMonitor<T>`.  
Therefore, you typically do not need to use these interfaces explicitly.

### `IWritableOptionsMonitor<T>`
This interface combines the functionalities of `IWritableOptions`, `IWritableNamedOptions`, and `IOptionsMonitor<T>`.
Like the above `IReadOnlyOptionsMonitor<T>`, you typically do not need to use these interfaces explicitly.

## License
This project is licensed under the Apache-2.0 License.
