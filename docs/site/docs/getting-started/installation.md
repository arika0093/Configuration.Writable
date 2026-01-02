---
sidebar_position: 1
---

# Installation

Configuration.Writable is distributed as NuGet packages. Choose the packages based on your needs.

## Core Package

The main package provides JSON format support out of the box:

```bash
dotnet add package Configuration.Writable
```

This package includes:
- Core functionality for reading and writing settings
- JSON format provider (using System.Text.Json)
- Built-in file provider with atomic writes and retry logic
- Change detection and monitoring

## Additional Format Providers

Install additional packages for other file formats:

### XML Format

```bash
dotnet add package Configuration.Writable.Xml
```

### YAML Format

```bash
dotnet add package Configuration.Writable.Yaml
```

### Encrypted Format

For storing settings in an encrypted format (AES-256-CBC):

```bash
dotnet add package Configuration.Writable.Encrypt
```

:::warning
The encrypted format uses simple encryption. It should not be used for highly sensitive data. Consider using proper secret management solutions for production environments.
:::

## Target Frameworks

Configuration.Writable supports the following target frameworks:

- .NET Standard 2.0 (for .NET Framework compatibility)
- .NET 6.0
- .NET 7.0
- .NET 8.0
- .NET 9.0
- .NET 10.0

## Next Steps

- [Setup Guide](./setup) - Learn how to set up your settings class
- [Simple Application](../usage/simple-app) - Usage without DI
- [Host Application](../usage/host-app) - Usage with DI
