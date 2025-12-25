## Partial Write

### Issue
The current FormatProvider basically writes all data to the file at once.
However, there are use cases where you want to update only part of a file, such as when editing an existing `appsettings.json`.

### Approach
* Partial write support will be provided only when a `SectionName` is specified.
    * Since partial writes are expected to have performance issues, it is better to support them only when explicitly specified.
* If no section is specified, the result of serializing the type will be written as before.

### Implementation Plan
1. If a `SectionName` is specified, first read the existing file and parse it as a simple JsonDocument.
2. Find the JsonElement corresponding to the specified `SectionName` and replace that part with the new data.
3. Serialize the entire updated JsonDocument again and write it to the file.

### Considerations
* Until now, it was assumed that the specified type `<T>` would be deserialized, so reading was simple. What should be done if the type is unclear?
* In JsonFormatProvider, can SourceGenerator support be implemented without issues? (Support for `JsonSerializerContext`)

---

## Implementation Status: ✅ Completed

### Overview
Partial write functionality has been successfully implemented for all format providers (JSON, XML, YAML). When a `SectionName` is specified, only that section of the file is updated while preserving other sections.

### Behavior

#### With SectionName Specified (Partial Write)
When `SectionName` is configured:
1. Reads the existing file if it exists
2. Parses the file into a document structure (JsonDocument, XDocument, or Dictionary)
3. Navigates to the specified section path (supports `:` and `__` separators)
4. Replaces only the target section with new configuration data
5. Preserves all other sections in the file
6. Writes the merged result back to the file

If the existing file is invalid or doesn't exist, creates a new file with the nested section structure.

#### Without SectionName (Full Overwrite)
When `SectionName` is not configured:
- Behaves as before - serializes the configuration directly and overwrites the entire file

### Format-Specific Implementation

#### JsonFormatProvider
Location: `src/Configuration.Writable.Core/FormatProvider/JsonFormatProvider.cs`

**Key Methods:**
- `GetPartialSaveContents<T>()`: Reads existing JSON file and performs partial update
- `WritePartialUpdate<T>()`: Recursively merges JSON elements while preserving existing structure

**Features:**
- Uses `JsonDocument.Parse()` to read existing file
- Handles nested sections by recursively traversing the JSON tree
- Copies non-target properties as-is using `JsonElement.WriteTo()`
- Gracefully handles invalid JSON by creating new structure

#### XmlFormatProvider
Location: `src/Configuration.Writable.Xml/XmlFormatProvider.cs`

**Key Methods:**
- `GetPartialSaveContents<T>()`: Reads existing XML file and performs partial update

**Features:**
- Uses `XDocument.Load()` to read existing XML
- Navigates XML hierarchy using `XElement.Element()`
- Replaces or adds target section while preserving siblings
- Creates intermediate sections if they don't exist
- Handles invalid XML by creating new structure

#### YamlFormatProvider
Location: `src/Configuration.Writable.Yaml/YamlFormatProvider.cs`

**Key Methods:**
- `GetPartialSaveContents<T>()`: Reads existing YAML file and performs partial update
- `DeepCopyDictionary()`: Deep copies `Dictionary<string, object>` structures
- `DeepCopyObjectDictionary()`: Converts and deep copies `Dictionary<object, object>` structures
- `MergeSection()`: Recursively merges new configuration into existing dictionary

**Features:**
- Deserializes YAML to `Dictionary<string, object>`
- Performs deep copy to avoid mutating original structure
- Handles both `Dictionary<string, object>` and `Dictionary<object, object>` types from YamlDotNet
- Preserves all non-target sections
- Handles invalid YAML by creating new structure

### Usage Example

```csharp
// Existing appsettings.json:
// {
//   "AppSettings": { "Name": "OldApp", "Version": 1 },
//   "Logging": { "LogLevel": { "Default": "Information" } },
//   "ConnectionStrings": { "DefaultConnection": "..." }
// }

WritableOptions.Initialize<AppSettings>(config =>
{
    config.FilePath = "appsettings.json";
    config.SectionName = "AppSettings";  // Only update this section
    config.FormatProvider = new JsonFormatProvider
    {
        JsonSerializerOptions = new() { WriteIndented = true }
    };
});

var options = WritableOptions.GetOptions<AppSettings>();
await options.SaveAsync(settings =>
{
    settings.Name = "NewApp";
    settings.Version = 2;
});

// Result - only AppSettings section is updated, others are preserved:
// {
//   "AppSettings": { "Name": "NewApp", "Version": 2 },
//   "Logging": { "LogLevel": { "Default": "Information" } },
//   "ConnectionStrings": { "DefaultConnection": "..." }
// }
```

### Nested Sections

All format providers support nested section paths using `:` or `__` separators:

```csharp
config.SectionName = "App:Settings";  // or "App__Settings"

// Updates only App.Settings while preserving App.Other:
// {
//   "App": {
//     "Settings": { <updated content> },
//     "Other": { <preserved content> }
//   }
// }
```

### Testing

Comprehensive tests have been added for each format provider:
- **JsonPartialWriteTests**: 5 tests covering various partial write scenarios
- **XmlPartialWriteTests**: 5 tests covering XML-specific scenarios
- **YamlPartialWriteTests**: 5 tests covering YAML-specific scenarios

Test scenarios include:
- Updating existing sections while preserving others
- Nested section updates
- Adding new sections to existing files
- Creating new files when none exist
- Full overwrite when no section name is specified

All tests pass successfully alongside existing tests (197 total tests passing).

### Performance Considerations

Partial write operations have additional overhead:
1. Reading existing file
2. Parsing into document structure
3. Merging/updating specific sections
4. Re-serializing entire document

For this reason, partial write is only enabled when `SectionName` is explicitly configured. Applications that don't need this feature experience no performance impact.

### Known Limitations

1. **Encryption Provider**: Partial write is not implemented for `EncryptFormatProvider` as it would require decrypting the entire file, updating a section, and re-encrypting, which may not align with security requirements.

2. **File Format Preservation**: While section content is preserved, some formatting details (like comment preservation, property order in JSON, etc.) may not be maintained depending on the serialization library behavior.

3. **Concurrent Access**: Like all write operations, partial writes should be coordinated when multiple processes might access the same file.
