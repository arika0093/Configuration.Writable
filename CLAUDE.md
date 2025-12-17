# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

Configuration.Writable is a .NET library that provides read/write functionality for user settings with extensive customization options. It extends Microsoft.Extensions.Options interfaces to enable both reading and writing configuration files in various formats (JSON, XML, YAML, encrypted).

## Build and Test Commands

```bash
# Restore dependencies
dotnet restore --locked-mode

# Build the solution
dotnet build --no-restore

# Run all tests
dotnet test --no-build --verbosity normal

# Run tests for a specific project
dotnet test tests/Configuration.Writable.Tests --no-build

# Build and run tests together
dotnet test

# Clean build artifacts
dotnet clean
```

## Project Structure

The solution is organized into multiple packages:

### Core Packages
- **Configuration.Writable.Core**: Core abstractions and implementations
  - `IReadOnlyOptions<T>` / `IWritableOptions<T>`: Main interfaces extending `IOptionsMonitor<T>`
  - `OptionsMonitorImpl<T>`: Custom implementation of IOptionsMonitor
  - `WritableOptionsImpl<T>`: Main writable options implementation
  - Provider system for file format handling
  - FileProvider system for atomic file operations

- **Configuration.Writable**: Main package with convenience APIs
  - `WritableConfig`: Static entry point for non-DI scenarios
  - `WritableOptionsExtensions`: DI registration extensions (`AddWritableOptions<T>`)

### Format Provider Packages
- **Configuration.Writable.Xml**: XML format support via `WritableConfigXmlProvider`
- **Configuration.Writable.Yaml**: YAML format support via `WritableConfigYamlProvider`
- **Configuration.Writable.Encrypt**: AES-256-CBC encrypted JSON via `WritableConfigEncryptProvider`

Each provider package is separate to minimize dependencies and allow users to install only what they need.

## Architecture

### Key Components

**Provider System** (`src/Configuration.Writable.Core/Provider/`)
- `IWritableConfigProvider`: Interface for serialization/deserialization
- `WritableConfigProviderBase`: Base implementation handling section nesting (supports `:` and `__` separators)
- Providers implement `LoadConfiguration<T>`, `GetSaveContents<T>`, and `SaveAsync<T>`
- Built-in JSON provider uses System.Text.Json

**FileProvider System** (`src/Configuration.Writable.Core/FileProvider/`)
- `IFileProvider`: Interface for file I/O operations
- `CommonFileProvider`: Default implementation with:
  - Automatic retry on file access failures (default: 3 retries, 100ms delay)
  - Atomic file writing (write to temp file, then rename)
  - Backup file rotation by timestamp (optional, disabled by default)
  - Thread-safe via internal semaphore

**Options Implementation** (`src/Configuration.Writable.Core/Options/`)
- `WritableOptionsImpl<T>`: Main implementation of `IWritableOptions<T>`
  - Wraps `OptionsMonitorImpl<T>` for monitoring functionality
  - Handles validation before save (DataAnnotations or custom validators)
  - Updates internal cache after successful save
  - Uses JSON deep copy for configuration updates
- `OptionsMonitorImpl<T>`: Custom `IOptionsMonitor<T>` implementation with file change detection
- `OptionsImpl<T>` and `OptionsSnapshotImpl<T>`: Standard options pattern implementations

**Configuration Management**
- `WritableConfigurationOptions<T>`: Record containing all configuration for a specific settings type
  - Includes: Provider, FilePath, InstanceName, SectionName, Logger, Validator
- `WritableConfigurationOptionsBuilder<T>`: Fluent API for building configuration options
  - Methods like `UseStandardSaveLocation()`, `WithValidator()`, `UseDataAnnotationsValidation`

### Usage Patterns

**Without DI:**
```csharp
WritableConfig.Initialize<T>(opt => { /* configure */ });
var options = WritableConfig.GetOptions<T>();
await options.SaveAsync(setting => setting.Prop = value);
```

**With DI:**
```csharp
builder.Services.AddWritableOptions<T>(opt => { /* configure */ });
// Inject IReadOnlyOptions<T> or IWritableOptions<T>
```

**Multiple Instances:**
When managing multiple configurations of the same type, use different `InstanceName` for each and access via `Get(name)` and `SaveAsync(name, ...)`.

### File Change Detection

The library monitors configuration files for external changes:
- Uses `PhysicalFileProvider` when available to watch for file system changes
- Falls back to internal cache updates when file watching is not possible
- Cache is always updated after `SaveAsync` to ensure consistency
- Change notifications propagate through `IOptionsMonitor<T>.OnChange`

### Validation

Validation occurs before saving:
1. DataAnnotations validation enabled by default (`UseDataAnnotationsValidation = true`)
2. Custom validators via `WithValidator<TValidator>()` or `WithValidatorFunction(func)`
3. Source generator support via `[OptionsValidator]` attribute
4. Throws `OptionsValidationException` if validation fails

### Section Name Handling

Section names support hierarchical configuration:
- Separators: `:` (colon) or `__` (double underscore)
- Example: `SectionName = "MyApp:Settings"` creates `{ "MyApp": { "Settings": { ... } } }`
- Empty section name means root level

## Testing

Tests use xUnit and are organized by package:
- `Configuration.Writable.Tests`: Core functionality tests
- `Configuration.Writable.Xml.Tests`: XML provider tests
- `Configuration.Writable.Yaml.Tests`: YAML provider tests
- `Configuration.Writable.Encrypt.Tests`: Encryption provider tests
- `Configuration.Writable.Tests.PublicApi`: API surface verification tests

Test utilities available in `Configuration.Writable.Core/Testing/`:
- `WritableOptionsStub.Create(value)`: Creates test stub without file I/O
- `WritableOptionsSimpleInstance<T>`: Full instance for testing actual file operations

Reference files in test projects contain expected serialization outputs for verification.

## Target Frameworks

The library targets multiple frameworks for broad compatibility:
- .NET Standard 2.0 (for .NET Framework compatibility)
- .NET 6.0, 7.0, 8.0, 9.0, 10.0

When adding new features, consider compatibility across all target frameworks.
