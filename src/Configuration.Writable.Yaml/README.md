# Configuration.Writable.Yaml
[![NuGet Version](https://img.shields.io/nuget/v/Configuration.Writable?style=flat-square&logo=NuGet&color=0080CC)](https://www.nuget.org/packages/Configuration.Writable.Yaml/) ![GitHub Actions Workflow Status](https://img.shields.io/github/actions/workflow/status/arika0093/Configuration.Writable/test.yaml?branch=main&label=Test&style=flat-square) ![GitHub last commit (branch)](https://img.shields.io/github/last-commit/arika0093/Configuration.Writable?style=flat-square)

A library that extends `Configuration.Writable` to support Yaml format for configuration files.

## How to use
### Without DI

```csharp
using Configuration.Writable;

WritableConfig.Initialize<UserSecretSetting>(opt => {
    opt.Provider = new YamlFormatProvider();
});
```

### With DI

```csharp
builder = new HostApplicationBuilder(args);
builder.Services.AddWritableOptions<UserSecretSetting>(opt => {
    opt.Provider = new YamlFormatProvider();
});
```
