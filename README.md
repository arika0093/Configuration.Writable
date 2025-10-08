# Configuration.Writable
[![NuGet Version](https://img.shields.io/nuget/v/Configuration.Writable?style=flat-square&logo=NuGet&color=0080CC)](https://www.nuget.org/packages/Configuration.Writable/) ![GitHub Actions Workflow Status](https://img.shields.io/github/actions/workflow/status/arika0093/Configuration.Writable/test.yaml?branch=main&label=Test&style=flat-square) ![GitHub last commit (branch)](https://img.shields.io/github/last-commit/arika0093/Configuration.Writable?style=flat-square)

A lightweight library that extends `Microsoft.Extensions.Configuration` to easily write settings with type safety.

## Features
* Read and write user settings with type safety.
* [Built-in](#filewriter) Atomic file writing, automatic retry, and backup creation.
* Extends `Microsoft.Extensions.Configuration` and integrates seamlessly with `IHostApplicationBuilder`.
* Simple API that can be easily used in applications both [with](#with-di) and [without](#without-di) DI.
* Supports various file formats (Json, Xml, Yaml, Encrypted, etc...) via [providers](#provider).

[See more...](./docs/article/why-this-library.md)

## Usage
### Setup
Install `Configuration.Writable` from NuGet.

```bash
dotnet add package Configuration.Writable
```

Then, prepare a class (`UserSetting`) that you want to read and write as settings in advance.

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

// initialize at once (application startup)
WritableConfig.Initialize<SampleSetting>();

// -------------
// get the writable config instance with the specified setting class
var option = WritableConfig.GetOption<SampleSetting>();

// get the UserSetting instance
var sampleSetting = option.CurrentValue;
Console.WriteLine($">> Name: {sampleSetting.Name}");

// and save to storage
await option.SaveAsync(setting =>
{
    setting.Name = "new name";
});
// By default, it's saved to ./usersettings.json
```

### With DI
If you are using DI (for example, in ASP.NET Core, Blazor, Worker Service, etc.), integrate with `IHostApplicationBuilder` or `IServiceCollection`.
First, call `builder.AddUserConfigurationFile` to register the settings.

```csharp
// Program.cs
builder.AddUserConfigurationFile<UserSetting>();

// If you're not using IHostApplicationBuilder, do the following:
var configuration = new ConfigurationManager();
services.AddUserConfigurationFile<UserSetting>(configuration);
```

Then, inject `IReadonlyOptions<T>` or `IWritableOptions<T>` to read and write settings.

```csharp
// read config in your class
// you can also use IOptions<T>, IOptionsMonitor<T> or IOptionsSnapshot<T>
public class ConfigReadService(IReadonlyOptions<UserSetting> option)
{
    public void Print()
    {
        // get the UserSetting instance
        var sampleSetting = option.CurrentValue;
        Console.WriteLine($">> Name: {sampleSetting.Name}");
    }
}

// read and write config in your class
public class ConfigReadWriteService(IWritableOptions<UserSetting> option)
{
    public async Task UpdateAsync()
    {
        // get the cUserSetting instance
        var sampleSetting = option.CurrentValue;
        // and save to storage
        await option.SaveAsync(setting =>
        {
            setting.Name = "new name";
        });
    }
}
```

## Customization
### Configuration Method
You can change various settings as arguments to `Initialize` or `AddUserConfigurationFile`.

```csharp
// Without DI
WritableConfig.Initialize<SampleSetting>(opt => { /* ... */ });

// With DI
builder.AddUserConfigurationFile<UserSetting>(opt => { /* ... */ });
```

### Save Location
Default behavior save to `{AppContext.BaseDirectory}/usersettings.json` (in general, the same directory as the executable).
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

If you want toggle between development and production environments, you can use `#if RELEASE` pattern or `builder.Environtment.IsProduction()`.

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

// if use IHostApplicationBuilder
builder.AddUserConfigurationFile<UserSetting>(opt => {
    opt.FilePath = "mysettings.json";
    if (builder.Environment.IsProduction()) {
        opt.UseStandardSaveLocation("MyAppId");
    }
});
```

### Provider
If you want to change the format when saving files, create a class that implements `IWritableConfigProvider` and specify it in `opt.Provider`.
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

// use encrypted Json format
// (you need to install Configuration.Writable.Encrypt package)
opt.Provider = new WritableConfigEncryptProvider("any-encrypt-password");
```

### FileWriter
Default FileWriter (`CommonFileWriter`) support following features:

* Automatically retry when file access fails (default is max 3 times, wait 100ms each)
* Create backup files rotated by timestamp (default is disabled)
* Atomic file writing (write to a temporary file first, then rename it)

If you want to change the way files are written, create a class that implements `IFileWriter` and specify it in `opt.FileWriter`.

```csharp
using Configuration.Writable.FileWriter;

opt.FileWriter = new CommonFileWriter() {
    // retry up to 5 times when file access fails
    MaxRetryCount = 5,
    // wait 100ms, 200ms, 300ms, ... before each retry
    RetryDelay = (attempt) => 100 * attempt,
    // keep 5 backup files when saving
    BackupMaxCount = 5,
};
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

### SectionName
In default, the entire settings are saved in the following structure:
```jsonc
{
  // opt.SectionRootName value (default is "UserSettings")
  "UserSettings": {
    // the type name of the setting class ("UserSetting" here)
    // InstanceName is appended if specified (e.g. "UserSetting-First")
    "UserSetting": {
      "Name": "custom name",
      "Age": 30
    }
  }
}
```

As you can see, the settings are written under the “UserSettings” section.
This serves as a guardrail to prevent conflicts with settings from other libraries (such as `ASP.NET Core`).
You can freely change this section name.

```csharp
// change the section root name to "MyAppSettings"
opt.SectionRootName = "MyAppSettings";

// nothing use root section (written at the root, not recommended)
opt.SectionRootName = "";
```

### InstanceName
If you want to manage multiple settings of the same type, you must specify different `InstanceName` for each setting.

```csharp
// first setting
builder.AddUserConfigurationFile<UserSetting>(opt => {
    opt.FilePath = "firstsettings.json";
    opt.InstanceName = "First";
    // save section will be "UserSettings:UserSetting-First"
});
// second setting
builder.AddUserConfigurationFile<UserSetting>(opt => {
    opt.FilePath = "secondsettings.json";
    opt.InstanceName = "Second";
    // save section will be "UserSettings:UserSetting-Second"
});

// and get each setting from DI
public class MyService(IWritableOptions<UserSetting> option)
{
    public void GetAndSave()
    {
        // cannot use .CurrentValue if multiple settings of the same type are registered
        var firstSetting = option.Get("First");
        var secondSetting = option.Get("Second");
        // and you can must specify instance name when saving
        await option.SaveAsync("First", setting => {
            setting.Name = "first name";
        });
        await option.SaveAsync("Second", setting => {
            setting.Name = "second name";
        });
    }
}
```

> [!WARNING]
> When not using DI (directly using `WritableConfig`), managing multiple settings is intentionally not supported to prevent complicating the usage.

## Tips
### Default Values
Due to the specifications of `MS.E.C`, properties that do not exist in the configuration file will use their default values.  
If a new property is added to the settings class during an update, that property will not exist in the configuration file, so the default value will be used.

```csharp
// if the settings file contains only {"Name": "custom name"}
var setting = options.CurrentValue;
// setting.Name is "custom name"
// setting.Age is 20 (the default value)
```

### Secret Value (Password, API Key, etc.)
A good way to include user passwords and the like in settings is to split the class and save one part encrypted.

```csharp
WritableConfig.Initialize<UserSetting>(opt => {
    opt.FilePath = "usersettings";
});
WritableConfig.Initialize<UserSecretSetting>(opt => {
    opt.FilePath = "my-secret-folder/secrets";
    // dotnet add package Configuration.Writable.Encrypt
    opt.Provider = new WritableConfigEncryptProvider("any-encrypt-password");
});

// and get/save each setting
var userOptions = WritableConfig.GetOption<UserSetting>();
var secretOptions = WritableConfig.GetOption<UserSecretSetting>();
// ...

// ---
// setting classes
public class UserSetting(string Name, int Age);  // non-secret
public class UserSecretSetting(string Password); // secret
```

> [!WARNING]
> Do not store values that must not be disclosed to others (e.g., database passwords). This feature is solely intended to prevent others from viewing values entered by the user.

## Testing
If you simply want to obtain `IReadonlyOptions<T>` or `IWritableOptions<T>`, using `WritableOptionsStub` is straightforward.

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

If you want to perform tests that actually involve writing to the file system, use `WritableConfigSimpleInstance`.

```csharp
var sampleFilePath = Path.GetTempFileName();
var instance = new WritableConfigSimpleInstance<UserSetting>();
instance.Initialize(opt => {
    opt.FilePath = sampleFilePath;
});
var option = instance.GetOption();

// and use options in your test
var yourService = new YourService(options);
yourService.DoSomething();

// sampleFilePath now contains the updated settings
var json = File.ReadAllText(sampleFilePath);
Assert.Contains("expected name", json);
```

## Interfaces
### IReadonlyOptions<T>
An interface for reading the settings of the registered type `T`.  
It automatically reflects the latest settings when the underlying configuration is updated.  
This interface provides functionality equivalent to [`IOptionsMonitor<T>`](https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.options.ioptionsmonitor-1) from `MS.E.C`.

```csharp
public interface IReadonlyOptions<T> : IOptionsMonitor<T> where T : class
```

The main differences (additional features) compared to `IOptionsMonitor<T>` are as follows:

* The `GetConfigurationOptions` method to retrieve configuration options (`WritableConfigOptions`)
* Even in environments where file change detection is not possible (for example, on network shares or in Docker environments, [change detection is not supported by default](https://learn.microsoft.com/en-us/dotnet/core/extensions/options#ioptionsmonitor)), you can always get the latest settings by using the cache maintained when saving via `IWritableOptions<T>`

### IWritableOptions<T>
An interface for reading and writing the settings of the registered type `T`.  
It provides the same functionality as `IReadonlyOptions<T>`, with additional support for saving settings.

```csharp
public interface IWritableOptions<T> : IReadonlyOptions<T> where T : class
```

## Limitations
this library currently does **not** support the following features.

### Saving Integrated Settings
`MS.E.C` provides a feature to integrate multiple configuration sources, but saving settings in this scenario introduces a problem.  
Since the settings are presented as a merged view, it becomes unclear "which source" should be updated with "which value" when saving.  
Therefore, this library currently does not support saving integrated (merged) settings.

### Dynamic Addition and Removal of Configuration Files
For example, in applications like `VSCode`, in addition to global settings, you can manage settings by dynamically adding or removing files such as `.vscode/settings.json` found in the currently opened folder.
This library assumes that configuration files are added all at once during application startup, and does not support dynamic addition or removal of configuration files at runtime.
(Also, related to the first limitation, it becomes unclear which configuration file should be saved to.)

## License
This project is licensed under the Apache-2.0 License.
