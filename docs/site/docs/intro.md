---
sidebar_position: 1
---

# Introduction

**Configuration.Writable** is a lightweight .NET library that allows for easy saving and referencing of settings, with extensive customization options.

[![NuGet Version](https://img.shields.io/nuget/v/Configuration.Writable?style=flat-square&logo=NuGet&color=0080CC)](https://www.nuget.org/packages/Configuration.Writable/) ![GitHub Actions Workflow Status](https://img.shields.io/github/actions/workflow/status/arika0093/Configuration.Writable/test.yaml?branch=main&label=Test&style=flat-square)

## Features

* **Type-safe Settings** - Read and write user settings with full type safety
* **Atomic Operations** - Built-in atomic file writing, automatic retry, and backup creation
* **Change Detection** - Automatic detection of external changes to configuration files
* **Simple API** - Easy to use in applications both with and without DI
* **Partial Updates** - Supports partial updates to settings, usable with ASP.NET Core
* **Highly Customizable** - Extensive configuration options for methods, save locations, file formats, validation, logging, and more

## Quick Start

### Installation

Install `Configuration.Writable` from NuGet:

```bash
dotnet add package Configuration.Writable
```

### Basic Usage

Create a settings class:

```csharp
using Configuration.Writable;

// Add IOptionsModel and mark as partial class
public partial class UserSetting : IOptionsModel<UserSetting>
{
    public string Name { get; set; } = "default name";
    public int Age { get; set; } = 20;
}
```

Initialize and use:

```csharp
// Initialize once (at application startup)
WritableOptions.Initialize<UserSetting>();

// Get the writable config instance
var options = WritableOptions.GetOptions<UserSetting>();

// Get the UserSetting instance
var setting = options.CurrentValue;
Console.WriteLine($"Name: {setting.Name}");

// Save to storage
await options.SaveAsync(setting =>
{
    setting.Name = "new name";
});
```

## What's Next?

- [Getting Started](./getting-started/installation) - Learn how to install and set up
- [Usage Guide](./usage/simple-app) - Explore different usage scenarios
- [Customization](./customization/configuration) - Customize to your needs
- [API Reference](./api/interfaces) - Detailed API documentation
