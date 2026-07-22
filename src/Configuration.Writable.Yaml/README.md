# Configuration.Writable.Yaml
[![NuGet Version](https://img.shields.io/nuget/v/Configuration.Writable?style=flat-square&logo=NuGet&color=0080CC)](https://www.nuget.org/packages/Configuration.Writable.Yaml/) ![GitHub Actions Workflow Status](https://img.shields.io/github/actions/workflow/status/arika0093/Configuration.Writable/test.yaml?branch=main&label=Test&style=flat-square) ![GitHub last commit (branch)](https://img.shields.io/github/last-commit/arika0093/Configuration.Writable?style=flat-square)

A library that extends `Configuration.Writable` to support YAML format for configuration files.

This provider uses [VYaml](https://github.com/hadashiA/VYaml), a source-generator-based YAML serializer, and supports **NativeAOT** environments.

## How to use
### Without DI

```csharp
using Configuration.Writable;

WritableOptions.Initialize<UserSecretSetting>(conf => {
    conf.FormatProvider = new YamlFormatProvider();
});
```

### With DI

```csharp
builder = new HostApplicationBuilder(args);
builder.Services.AddWritableOptions<UserSecretSetting>(conf => {
    conf.FormatProvider = new YamlFormatProvider();
});
```

## NativeAOT Support

`YamlFormatProvider` uses VYaml's source-generated formatters, making it compatible with NativeAOT and trimming.

### Requirements

1. Annotate your settings class with `[YamlObject]` (from `VYaml.Annotations`) and declare it as `partial`
2. Call the generated `__RegisterVYamlFormatter()` method at program startup to ensure formatters are not trimmed

```csharp
using VYaml.Annotations;

[YamlObject]
public partial class SampleSetting
{
    public string Name { get; set; } = "";
    public DateTime LastUpdatedAt { get; set; }
}
```

```csharp
// Register formatters at startup (required for NativeAOT)
SampleSetting.__RegisterVYamlFormatter();
```

### Example

See [Example.ConsoleApp.Yaml](../../example/Example.ConsoleApp.Yaml/) for a complete NativeAOT publishable example.
