# CLAUDE.md
## Project Overview
Configuration.Writable is a .NET library that provides read/write functionality for user settings with extensive customization options. It extends the Microsoft.Extensions.Options interfaces to enable both reading and writing configuration files in various formats (JSON, XML, YAML, encrypted).
More details can be found in the README.md file.

## Testing
### Setup
Be sure to run the following commands to restore dependencies and tools:
```bash
dotnet restore
dotnet tool restore
```

### Running Tests
Tests may fail sporadically if run in parallel, so always use **test-rerun**:

```bash
dotnet test-rerun --verbosity normal --deleteReports 
```

### Specific Target Framework
You can set the `TFMS` environment variable to run tests for specific target frameworks. If not specified, the latest target framework will be used.

```bash
export TFMS=net8.0;net10.0
```