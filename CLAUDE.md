# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

Configuration.Writable is a .NET library that extends Microsoft.Extensions.Configuration to enable easy handling of writable user settings. The library provides type-safe reading and writing of configuration files with support for multiple formats (JSON, XML, YAML, Encrypted).

## Build and Test Commands

```bash
# Restore dependencies (required first)
dotnet restore --locked-mode

# Build the solution
dotnet build --no-restore

# Run all tests
dotnet test --no-build --verbosity normal

# Run tests for specific project
dotnet test tests/Configuration.Writable.Tests --no-build

# Build for specific target framework
dotnet build -f net8.0

# Pack NuGet packages
dotnet pack --no-build
```

## Architecture

### Core Structure
- **src/Configuration.Writable**: Core library with main configuration functionality
- **src/Configuration.Writable.{Xml,Yaml,Encrypt}**: Provider packages for different file formats
- **tests/**: Test projects corresponding to each source package
- **example/**: Example applications demonstrating usage patterns

### Key Components

**WritableConfig**: Static entry point for non-DI scenarios. Provides `Initialize<T>()` and `GetOption<T>()` methods.

**IWritableOptions<T>**: Main interface extending IOptionsMonitor<T> with save capabilities. Used in DI scenarios.

**Providers**: Implement `IWritableConfigProvider` to support different file formats:
- `WritableConfigJsonProvider`: Built-in JSON support
- `WritableConfigXmlProvider`: XML format (separate package)
- `WritableConfigYamlProvider`: YAML format (separate package)
- `WritableConfigEncryptProvider`: AES-256-CBC encrypted JSON (separate package)

**FileWriters**: Implement `IFileWriter` for different storage strategies:
- `CommonFileWriter`: Default with atomic writes, retries, and backup support
- `InMemoryFileWriter`: For testing without filesystem

### Configuration Pattern
Settings are stored in hierarchical structure:
```json
{
  "UserSettings": {
    "SettingTypeName": { /* user settings */ }
  }
}
```

## Development Guidelines

### Target Frameworks
- netstandard2.0, net8.0, net9.0 for source packages
- net6.0, net8.0, net9.0, net48 (Windows only) for test packages

### Testing Framework
- xUnit with Shouldly assertions
- Tests use `InMemoryFileWriter` to avoid filesystem dependencies
- Cross-platform testing on Ubuntu, macOS, and Windows via GitHub Actions

### Package Dependencies
- Uses Nerdbank.GitVersioning for versioning
- SonarAnalyzer.CSharp for code analysis
- PolySharp for polyfills
- Locked package restore via packages.lock.json

### API Design Patterns
- Two usage patterns: With DI (`AddUserConfigurationFile<T>()`) and without DI (`WritableConfig.Initialize<T>()`)
- Settings classes use C# properties with default values
- Support for multiple settings instances via `InstanceName`
- Configuration options builder pattern for customization