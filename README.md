# Configuration.Writable
[![NuGet Version](https://img.shields.io/nuget/v/Configuration.Writable?style=flat-square&logo=NuGet&color=0080CC)](https://www.nuget.org/packages/Configuration.Writable/) ![GitHub Actions Workflow Status](https://img.shields.io/github/actions/workflow/status/arika0093/Configuration.Writable/test.yaml?branch=main&label=Test&style=flat-square) ![GitHub last commit (branch)](https://img.shields.io/github/last-commit/arika0093/Configuration.Writable?style=flat-square)

A lightweight library that extends `Microsoft.Extensions.Configuration`(`MS.E.C`) to easily handle user settings.

## Features
* Read and write user settings with type safety.
* [Built-in](#filewriter) Atomic file writing, automatic retry, and backup creation.
* Extends `Microsoft.Extensions.Configuration` and integrates seamlessly with `IHostApplicationBuilder`.
* Simple API that can be easily used in applications both [with](#with-di) and [without](#without-di) DI.
* Supports various file formats (Json, Xml, Yaml, Encrypted, etc...) via [providers](#provider).

[More...](#why-this-library)

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
If you are not using DI (for example, in `WinForms`, `WPF`, `console apps`, etc.),
Use `WritableConfig` as the starting point for reading and writing settings.

```csharp
using Configuration.Writable;

// initialize at once (application startup)
WritableConfig.Initialize<SampleSetting>();

// -------------
// get the writable config instance with the specified setting class
var options = WritableConfig.GetOption<SampleSetting>();

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
If you are using DI (for example, in `ASP.NET Core`, `Blazor`, `Worker Service`, etc.), integrate with `IHostApplicationBuilder` or `IServiceCollection`.
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
public class ConfigReadService(IReadonlyOptions<UserSetting> config)
{
    public void Print()
    {
        // get the UserSetting instance
        var sampleSetting = config.CurrentValue;
        Console.WriteLine($">> Name: {sampleSetting.Name}");
    }
}

// read and write config in your class
public class ConfigReadWriteService(IWritableOptions<UserSetting> config)
{
    public async Task UpdateAsync()
    {
        // get the cUserSetting instance
        var sampleSetting = config.CurrentValue;
        // and save to storage
        await config.SaveAsync(setting =>
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
Default behavior save to `./usersettings.json`.  
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

If you want toggle between development and production environments, you can use `#if DEBUG` pattern or `Environtment.IsDevelopment()`.

```csharp
// without DI
WritableConfig.Initialize<UserSetting>(opt => {
#if DEBUG
    opt.FilePath = "devsettings.json";
#else
    opt.UseStandardSaveLocation("MyAppId");
#endif
});

// if use IHostApplicationBuilder
builder.AddUserConfigurationFile<UserSetting>(opt => {
    if (builder.Environment.IsDevelopment()) {
        opt.FilePath = "devsettings.json";
    }
    else {
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

For example, if you want to keep backup files when saving, use `CommonFileWriter` with `BackupMaxCount > 0`.
```csharp
// keep 5 backup files when saving
opt.FileWriter = new CommonFileWriter() { BackupMaxCount = 5 };
```

### Logging
TODO

### SectionName
In default, the entire settings are saved in the following structure:
```jsonc
{
  // "SectionRootName" is specified by opt.SectionName (default is "UserSettings")
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
// change the section name to "MyAppSettings"
opt.SectionName = "MyAppSettings";

// nothing use section (written at the root, not recommended)
opt.SectionName = "";
```

### InstanceName
If you want to manage multiple settings of the same type, you must specify different `InstanceName` for each setting.

```csharp
// first setting
builder.AddUserConfigurationFile<UserSetting>(opt => {
    opt.FilePath = "firstsettings.json";
    opt.InstanceName = "First";
    // save section will be "UserSettings-First"
});
// second setting
builder.AddUserConfigurationFile<UserSetting>(opt => {
    opt.FilePath = "secondsettings.json";
    opt.InstanceName = "Second";
    // save section will be "UserSettings-Second"
});

// and get each setting from DI
public class MyService(IWritableOptions<UserSetting> config)
{
    public void GetAndSave()
    {
        // cannot use .CurrentValue if multiple settings of the same type are registered
        var firstSetting = config.Get("First");
        var secondSetting = config.Get("Second");
        // and you can must specify instance name when saving
        await config.SaveAsync("First", setting => {
            setting.Name = "first name";
        });
        await config.SaveAsync("Second", setting => {
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

## Testing
To prepare a disposable configuration instance, use `WritableConfigurationSimpleInstance`.
and you can use `InMemoryFileWriter` to test reading and writing settings without touching the file system.  

```csharp
// setup
var sampleFilePath = Path.GetRandomFilePath();
var instance = new WritableConfigurationSimpleInstance();
var fileWriter = new InMemoryFileWriter();

instance.Initialize<UserSetting>(opt => {
    opt.FilePath = sampleFilePath;
    opt.UseInMemoryFileWriter(fileWriter);
});
var option = instance.GetOption<UserSetting>();

// and your test execution
await options.SaveAsync(setting => {
    setting.Name = "test name";
    setting.Age = 99;
});

// check the saved content
Assert.True(fileWriter.FileExists(sampleFilePath));
var savedText = fileWriter.ReadAllText(sampleFilePath);
Assert.Contains("test name", savedText);
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

## Why This Library?
There are many ways to handle user settings in C# applications. However, each has some drawbacks, and no de facto standard exists.

### Using `app.config` (`Settings.settings`)
This is an old-fashioned approach that yields many search results (unfortunately!). When you start using it, you'll likely encounter the following issues:

* You need to manually write XML-based configuration files (or use Visual Studio's cumbersome GUI)
* It lacks type safety and is unsuitable for complex settings
* Files may be included in distributions without careful consideration, risking settings reset during updates

### Reading and Writing Configuration Files Yourself
When considering type safety, the first approach that comes to mind is creating and reading/writing your own configuration files.  
This method isn't bad, but the drawback is that there are too many things to consider.

* You need to write configuration management code yourself
* You need to implement many features like backup creation, settings merging, and update handling
* Integrating multiple configuration sources requires extra effort
* You need to implement configuration change reflection yourself

### Using (Any Configuration Library)
Since there's so much boilerplate code, there must be some configuration library available.  
Indeed, just [searching for `Config` on NuGet](https://www.nuget.org/packages?q=config) yields many libraries.  
I examined the major ones among these, but couldn't adopt them for the following reasons:

* [DotNetConfig](https://github.com/dotnetconfig/dotnet-config)
  * The file format uses a proprietary format (`.netconfig`)
  * It appears to be primarily designed for `dotnet tools`
* [Config.Net](https://github.com/aloneguid/config)
  * It supports various providers but uses a [unique storage format](https://github.com/aloneguid/config#flatline-syntax)
  * Collection writing is [not supported](https://github.com/aloneguid/config#json) in the JSON provider

### Using `Microsoft.Extensions.Configuration`
Considering these current situations, `Microsoft.Extensions.Configuration` (`MS.E.C`) can be said to be the most standardized configuration management method in modern times.  
It provides many features such as multi-file integration, support for various formats including environment variables, and configuration change reflection, and integrates seamlessly with `IHostApplicationBuilder`.  
However, since it's primarily designed for application settings, it's insufficient for handling user settings. The major problem is that configuration writing is not supported.  
Another issue is that, being based on DI (Dependency Injection), it can be somewhat cumbersome to use in certain types of applications.
For example, applications like `WinForms`, `WPF`, or `Console Apps` that want to use configuration files are less likely to utilize DI.

### `Configuration.Writable`
The preamble has gotten long, but it's time for promotion!  
This library extends `MS.E.C` to make writing user settings easy.  
It's also designed to be easily usable in applications that don't use DI.  


## License
This project is licensed under the Apache-2.0 License.
