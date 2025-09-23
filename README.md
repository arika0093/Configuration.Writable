# Configuration.Writable
[![NuGet Version](https://img.shields.io/nuget/v/Configuration.Writable?style=flat-square&logo=NuGet&color=0080CC)](https://www.nuget.org/packages/Configuration.Writable/) ![GitHub Actions Workflow Status](https://img.shields.io/github/actions/workflow/status/arika0093/Configuration.Writable/test.yaml?branch=main&label=Test&style=flat-square) ![GitHub last commit (branch)](https://img.shields.io/github/last-commit/arika0093/Configuration.Writable?style=flat-square)

A lightweight library that extends Microsoft.Extensions.Configuration to easily handle user settings.

## Features
* Read and write user settings with type safety.
* Extends `Microsoft.Extensions.Configuration` and integrates seamlessly with `IHostApplication`.
* Simple API that can be easily used in applications both with and without DI.
* Supports various file formats (Json, Xml, Yaml, Encrypted, etc...) via providers.

[More...](#why-this-library)

## Usage
### Setup
Install `Configuration.Writable` from NuGet.

```shell
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

### Without DI (Console, WinForms, WPF, etc.)
Use `WritableConfig` as the starting point for reading and writing settings.

```csharp
using Configuration.Writable;

// get the writable config instance with the specified setting class
var options = WritableConfig.GetInstance<SampleSetting>();

// initialize at once before use
options.Initialize();

// get the config instance
var sampleSetting = options.CurrentValue;
Console.WriteLine($">> Name: {sampleSetting.Name}");

// save the config instance
await options.SaveAsync(setting =>
{
    setting.Name = "new name";
});
// By default, it's saved to ./usersettings.json
```

### With DI (ASP.NET Core, Blazor, Worker Service, etc.)
First, call `builder.AddUserConfigurationFile` to register the settings.

```csharp
// Program.cs
builder.AddUserConfigurationFile<UserSetting>();

// If you're not using IHostApplication, do the following:
var configuration = new ConfigurationManager();
services.AddUserConfigurationFile<UserSetting>(configuration);
```

Then, obtain and use `IReadonlyOptions<T>` or `IWritableOptions<T>` from the DI container as follows:

```csharp
// read config in your class
// you can also use IOptions<T>, IOptionsMonitor<T> or IOptionsSnapshot<T>
public class ConfigReadClass(IReadonlyOptions<UserSetting> config)
{
    public void Print()
    {
        var sampleSetting = config.CurrentValue;
        Console.WriteLine($">> Name: {sampleSetting.Name}");
    }
}

// read and write config in your class
public class ConfigReadWriteClass(IWritableOptions<UserSetting> config)
{
    public async Task UpdateAsync()
    {
        var sampleSetting = config.CurrentValue;
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
var options = WritableConfig.GetInstance<UserSetting>();
options.Initialize(opt => { /* ... */ });

// With DI
builder.AddUserConfigurationFile<UserSetting>(opt => { /* ... */ });
```

### Main Configuration Options
Major options you can set include:

```csharp
{
    // File name to save (default: "usersettings")
    // For example, if you want to save to the parent directory, specify like "../usersettings"
    // The extension is automatically added by the provider
    opt.FileName = "usersettings"; 

    // If you want to save to a common settings directory, execute this function.
    // * on Windows it saves to %APPDATA%/MyAppId/
    // * on Linux it saves to ~/.config/MyAppId/ or $XDG_CONFIG_HOME/MyAppId/
    // * on macOS it saves to ~/Library/Application Support/MyAppId/
    opt.UseStandardSaveLocation("MyAppId");

    // Configuration file save format.
    // for example, use Json format with indentation
    opt.Provider = new WritableConfigJsonProvider() {
        JsonSerializerOptions = { WriteIndented = true },
    };

    // File writer to use when saving files.
    // for example, if you want to keep backup files, use CommonFileWriter with BackupMaxCount > 0
    opt.FileWriter = new CommonFileWriter() { BackupMaxCount = 5 };
}
```

### Provider
If you want to change the format when saving files, create a class that implements `IWritableConfigProvider` and specify it in `opt.Provider`.
Currently, the following providers are available:

| Provider                     | Description              | NuGet Package                |
|------------------------------|---------------------------|------------------------------|
| `WritableConfigJsonProvider` | save in Json format.     | Configuration.Writable (Built-in) |
| `WritableConfigXmlProvider`  | save in Xml format.      | Configuration.Writable.Xml  |
| `WritableConfigYamlProvider` | save in Yaml format.     | Configuration.Writable.Yaml |
| `WritableConfigEncryptProvider` | save in encrypted (AES-256-CBC) Json format. | Configuration.Writable.Encrypt |



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
It provides many features such as multi-file integration, support for various formats including environment variables, and configuration change reflection, and integrates seamlessly with `IHostApplication`.
However, since it's primarily designed for application settings, it's insufficient for handling user settings. The major problem is that configuration writing is not supported.
Another issue is that it's based on DI, making it somewhat cumbersome to use in console applications.
Apps that want to use configuration files are more likely to not use DI (examples include `WinForms`, `WPF`, `console apps`, etc.).

### `Configuration.Writable`
The preamble has gotten long, but it's time for promotion!
This library extends `Microsoft.Extensions.Configuration` to make writing user settings easy.
It's also designed to be easily usable in applications that don't use DI.

