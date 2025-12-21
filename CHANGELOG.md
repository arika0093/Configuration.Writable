## [unreleased]

### 🚀 Features

- Implement semaphore for thread-safe configuration updates in WritableOptionsImpl
- Add cloning strategy support for configuration options and update related implementations
- Add JSON cloning strategy support and update related configurations
- Enhance NativeAOT support with format provider and cloning strategy updates
- Implement cloning strategy in OptionsMonitor and OptionsSnapshot for improved value management

### 🐛 Bug Fixes

- Correct import statements for consistency in example projects
- Add missing format provider import in example console app
- Remove unnecessary closing code block in README for NativeAOT instructions
- Update version to 0.1 in version.json

### 🚜 Refactor

- Update namespaces and format provider references for consistency across the project
- Remove unused using directives across multiple files for cleaner code
- Update test method names and assertions for clarity in Options tests

### ⚙️ Miscellaneous Tasks

- Update changelog for release 0.1.0-alpha.159
- Update changelog for release 0.1.0-alpha.2
## [0.1.0-alpha.159] - 2025-12-19

### 🚀 Features

- Enhance instance name handling for writable options configuration (#26)
- Refactor file provider handling in writable options configuration
- Implement file and directory access checks in IFileProvider and its implementations
- Add GetSpecifiedInstance method to IWritableNamedOptions and IReadOnlyNamedOptions interfaces
- Enhance JSON serialization with source generation support and update documentation
- Introduce IReadOnlyOptionsMonitor and IWritableOptionsMonitor interfaces for enhanced options management

### 📚 Documentation

- Update README to clarify change detection and callback registration for options
- Update configuration options to use 'conf' instead of 'opt' for consistency
- Update README and API to reflect changes in save location configuration methods
- Update README to improve feature descriptions and formatting
- Update README to remove unnecessary link and add customization sections

### ⚙️ Miscellaneous Tasks

- Update changelog for release 0.1.0-alpha.149
## [0.1.0-alpha.149] - 2025-12-18

### 🚀 Features

- Add .NET Framework support and update test configurations
- Update .NET workflow to use fixed version for .NET 10 and reintroduce .NET Framework 4.8 testing
- Add dotnet-test-rerun for retrying failed tests and include GitHubActionsTestLogger package
- Update package dependencies for .NET Framework 4.6.2 support
- Separate options interfaces for clarity and consistency (#20)
- Implement named writable options and add integration tests (#21)
- Add default section name constant and refactor ConfigFilePath property
- Add-throttle-on-change (#23)
- Update ILocationBuilder interfaces to enhance save location functionality
- Update save location methods to UseStandardSaveDirectory for consistency
- Add OnChangeThrottleMs property to configuration options
- Enhance save location configuration with multiple file paths support
- Add-onchange-event-utility (#24)

### 🐛 Bug Fixes

- Add missing Collection attribute to PublicApiCheckTest class
- Update .NET Framework version references in workflow files
- Remove unnecessary framework specification in build commands for .NET tests
- Update test command to use simplified syntax for dotnet-test-rerun
- Update test command to include trx logger for .NET test rerun
- Add missing PackageReference for System.ValueTuple in project file
- Add System.ValueTuple dependency to multiple project lock files
- Remove System.ValueTuple dependency from project lock files and update project properties for binding redirects
- Add System.ValueTuple and related dependencies for net48 target in multiple test projects
- Add Windows environment variable setup in GitHub Actions workflow
- Remove Windows environment variable setup from GitHub Actions workflow
- Correct file path handling in save location logic

### 💼 Other

- *(deps)* Bump actions/checkout from 5 to 6 (#13)

### 🚜 Refactor

- Rename IConfigurationOptionsRegistry to IOptionsConfigRegistry for consistency
- Simplify .NET test steps by consolidating test commands
- Rename to format provider (#22)
- Remove unsupported target frameworks from Directory.Build.props
- Rename sections in README for clarity
- Update class to use IWritableNamedOptions for better clarity
- Update devcontainer configuration for improved clarity and functionality
- Remove IDisposable implementation from WritableOptionsImpl for clarity
- Filename and methodname are changed
- Remove listener notification from UpdateCache method
- Rename IWritableOptionConfigRegistry to IWritableOptionsConfigRegistry for consistency

### ⚙️ Miscellaneous Tasks

- Remove deprecated workflows and update CI configurations
- Update target frameworks and package references to version 10.*
- Update package versions
## [0.1.0-alpha.119] - 2025-10-21

### 🚀 Features

- Implement ZipFileProvider for managing multiple settings in a zip file
- Add ZipFileProvider implementation with file handling and backup support
- Implement IConfigurationOptionsRegistry and related classes for dynamic writable configuration management
- Add dynamic options management with IWritableOptionsRegistry

### 🐛 Bug Fixes

- Add SonarQubeExclude property to Directory.Build.props for test project configuration
- Update array initialization syntax in validation tests
- Improve error handling in Get method of ConfigurationOptionsRegistryImpl
- Update exception type from InvalidOperationException to KeyNotFoundException in integration tests
- Remove redundant test for handling large files in ZipFileProviderTests
- Enhance backup file management and isolation in tests
- Update backup file count assertion to allow for fewer backups
- Add retry logic for filesystem checks to handle delays in CI
- Add assembly attribute to disable test parallelization

### 🚜 Refactor

- Rename FileWriter to FileProvider
- Rename folder name
- Update generic type constraints to require a parameterless constructor
- Replace Activator.CreateInstance with new T() for default instance creation
- Reorder parameters in SaveToFileAsync method for consistency
- Remove FileReadStream property and update FileProvider assignment in WritableConfigurationOptionsBuilder
- Simplify FileProvider assignment in WritableConfig initialization

### 📚 Documentation

- Update README to include ZipFileProvider usage for multiple settings in a single file

### 🧪 Testing

- Update SaveToFileAsync_WithBackupMaxCount to use unique file names and increase backup limit test iterations
- Add unit tests for ConfigurationOptionsRegistry and update OptionsMonitorImpl usage
- Increase delay in concurrent writes test to avoid errors
## [0.1.0-alpha.106] - 2025-10-14

### 🚀 Features

- Introduce key-level manipulation operations for configuration properties
- Implement OptionOperations class for managing configuration property operations
- Add SaveAsync method to configuration providers for OptionOperations support
- Enhance configuration caching and key deletion handling in JSON provider
- Add advanced usage section for direct property manipulation in configuration

### 🚜 Refactor

- Change method visibility to static for key deletion methods in configuration providers

### 📚 Documentation

- Add usage example for source generators with DataAnnotations in README
- Add TODO item for property manipulation pattern in README
- Add CLAUDE.md for project guidance and usage instructions
- Fix typo
## [0.1.0-alpha.101] - 2025-10-13

### 🚜 Refactor

- Simplify configuration structure and update related documentation for clarity
- Remove TestConfiguration wrapper from JSON, XML, and YAML test files for consistency
- Update README for clarity and remove outdated TODO list
## [0.1.0-alpha.100] - 2025-10-12

### 🚀 Features

- Add methods to set configuration folder to executable or current directory and implement corresponding tests
- Add .claude/ to .gitignore to exclude Claude configuration files
- Add backward compatibility test for loading pre-encrypted configuration
- Enhance number comparison in JSON element equality check for format stability
- Add validation support for configuration options with custom validators and data annotations
- Add validation comments and examples for configuration options in SampleSetting
- Implement data annotations validation toggle in WritableConfigurationOptions
- Add generic validator method to WritableConfigurationOptionsBuilder
- Add validation support with DataAnnotations and custom validators in README
- Add System.ComponentModel.Annotations package for validation support
- Implement WritableOptions and WritableOptionsMonitor for custom configuration management
- Add WritableOptionsExtensions for configurable writable options and related unit tests
- Enhance file access handling with concurrent support and retry logic
- Add logging for configuration file changes and save operations
- Update .NET target framework to 10.x across workflows and package configurations
- Update devcontainer image to .NET 10.0-preview

### 🐛 Bug Fixes

- Update IValidator interface to use 'in' keyword for covariance
- Refactor retry logic in SaveToFileAsync to improve exception handling
- Enhance file replacement logic and improve exception handling in SaveToFileAsync
- Handle null failures in ValidateOptionsResult for improved validation robustness
- Update package description to correctly reference Microsoft.Extensions.Options

### 💼 Other

- Add using directive for Configuration.Writable.FileWriter in file writing example
- Add unit tests for readonly and writable option services
- Add Create method for named values; refactor WritableConfig initialization methods
- InMemoryFileWrite move to tests project

### 🚜 Refactor

- Standardize interface naming from IReadonlyOptions to IReadOnlyOptions
- Rename configUpdator to configUpdater for consistency
- Rename AddUserConfigurationFile to AddUserConfig for consistency
- Rename SectionRootName to SectionName for consistency across configuration options
- Simplify MemberNotNull attribute in UseInMemoryFileWriter method
- Remove UseCurrentDirectory test for cleaner test suite
- Centralize JSON comparison logic into JsonCompareUtility and update tests
- Remove MemberNotNull attributes for cleaner code and update public API approval
- Format XML test files for improved readability and maintainability
- Update .gitignore to include *.csproj.user and received test files
- Remove unused validation references and update validation result methods
- Remove WPF application files and related components
- Rename AddUserConfig to AddWritableOptions across documentation and codebase
- Rename GetOption to GetOptions across the codebase for consistency
- Remove unused OptionsSnapshot and DynamicOptionsWrapper classes
- Replace lock object with SemaphoreSlim for improved concurrency control
- Remove Microsoft.Extensions.Configuration package reference
- Simplify IOptions registration by removing IOptionsSnapshot and using IOptionsMonitor directly
- Remove Microsoft.Extensions.DependencyInjection.Abstractions package references across projects
- Replace custom validation interfaces and exceptions with Microsoft.Extensions.Options equivalents
- Remove unnecessary Microsoft.Extensions.Hosting.Abstractions references
- Update IWritableConfigProvider to allow public access for FileWriter and enhance WritableConfigurationOptionsBuilder with public BuildOptions method; add WritableOptionsCoreExtensions for service registration
- Rename WritableConfigSimpleInstance to WritableOptionsSimpleInstance and update related references across the codebase
- Update validation logic in SampleSetting to use DataAnnotations and add SampleSettingValidator for improved configuration validation
- Remove unused package references from XML and YAML project files
- Update test methods to use async/await for improved asynchronous handling
- Move WritableOptionsSimpleInstance class to a new location for improved organization
- Rename SaveAsync methods to SaveWithNameAsync for clarity and consistency
- Improve configuration options with additional logging and validation comments
- Update project references and target framework version in project files
- Streamline cache management in OptionsMonitorImpl and WritableOptionsImpl
- Enhance documentation on extending Microsoft.Extensions.Configuration and Options
- Improve clarity and consistency in documentation for Configuration.Writable
- Clarify dependency rationale and integration benefits of Configuration.Writable
- Update package description for clarity and consistency
- Update wording for clarity in why-this-library documentation
- Update configuration structure in documentation and tests for consistency

### 📚 Documentation

- *(README.md)* Add section for direct reference without option type; clarify lifecycle note
- *(README.md)* Clarify references to Microsoft.Extensions.Configuration as MS.E.C for consistency
- *(README.md)* Simplify instructions for custom file format providers and clarify package dependencies
- Fix punctuation in README and why-this-library documentation; add TODO file for future enhancements
- *(README.md)* Streamline section name customization instructions and clarify root level saving
- *(README.md)* Add example properties for UserSetting in customization section
- *(README.md)* Clarify reasons for configuration structure and update related sections
- *(README.md)* Update note on dynamic update functionality and lifecycle management for settings instance
- Update TODO.md to reflect changes in migration functionality and configuration support
- Add logging examples for configuration file changes and save operations

### 🧪 Testing

- Add unit tests for OptionsImpl, OptionsMonitorImpl, and OptionsSnapshotImpl to validate configuration behavior
## [0.1.0-alpha.40] - 2025-09-27

### 💼 Other

- Update CommonFileWriter features and clarify DI limitations
- Update features section and clarify usage instructions for DI and non-DI scenarios
- Clarify library name abbreviation and enhance file writer feature descriptions; add interfaces for IReadonlyOptions<T> and IWritableOptions<T>
- Clarify references to IHostApplicationBuilder and improve descriptions for IReadonlyOptions<T> and configuration management
- Enhance FilePath property with validation and improve ConfigFilePath logic
- Refactor FilePath property to simplify implementation and improve path handling logic
- Add test for ConfigFilePath with relative path to ensure correct base directory handling
- Improve null and whitespace check for FilePath in FilePathWithExtension property
## [0.1.0-alpha.18] - 2025-09-24

### 💼 Other

- Add methods for standard and temporary save locations
- Set temporary save location to system temp path
- Remove assembly attribute to enable test parallelization
- Enhance SectionRootName property with validation and add corresponding unit test
- Rename SectionName to SectionRootName for consistency and clarity
## [0.1.0-alpha.1] - 2025-09-23
