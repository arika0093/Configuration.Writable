---
sidebar_position: 3
---

# Format Provider

Format providers determine how settings are serialized and deserialized. Configuration.Writable supports multiple file formats through different providers.

## JSON Format (Default)

The default format provider uses `System.Text.Json`:

```csharp
using Configuration.Writable.FormatProvider;

conf.FormatProvider = new JsonFormatProvider()
{
    JsonSerializerOptions = new()
    {
        WriteIndented = true,
        // Add any other JsonSerializerOptions
    }
};
```

### Source-Generated JSON

For better performance and NativeAOT support, use source-generated JSON:

```csharp
using System.Text.Json.Serialization;
using Configuration.Writable.FormatProvider;

// Define source generation context
[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(UserSetting))]
public partial class UserSettingContext : JsonSerializerContext;

// Configure
conf.FormatProvider = new JsonFormatProvider()
{
    JsonSerializerOptions = new()
    {
        TypeInfoResolver = UserSettingContext.Default
    }
};
```

### NativeAOT JSON Format

For NativeAOT scenarios, use `JsonAotFormatProvider`:

```csharp
using Configuration.Writable.FormatProvider;

conf.FormatProvider = new JsonAotFormatProvider(
    UserSettingContext.Default
);
```

## XML Format

Save settings in XML format:

**Installation:**
```bash
dotnet add package Configuration.Writable.Xml
```

**Usage:**
```csharp
using Configuration.Writable.FormatProvider;

conf.FormatProvider = new XmlFormatProvider();
```

**Example output:**
```
<?xml version="1.0" encoding="utf-8"?>
<UserSetting>
  <Name>John Doe</Name>
  <Age>30</Age>
</UserSetting>
```

## YAML Format

Save settings in YAML format:

**Installation:**
```bash
dotnet add package Configuration.Writable.Yaml
```

**Usage:**
```csharp
using Configuration.Writable.FormatProvider;

conf.FormatProvider = new YamlFormatProvider();
```

**Example output:**
```yaml
name: John Doe
age: 30
```

## Encrypted Format

Save settings in an encrypted format using AES-256-CBC:

**Installation:**
```bash
dotnet add package Configuration.Writable.Encrypt
```

**Usage:**
```csharp
using Configuration.Writable.FormatProvider;

conf.FormatProvider = new EncryptFormatProvider("your-encryption-password");
```

:::warning Security Note
The encrypted format uses simple encryption. It should NOT be used for highly sensitive data. For production scenarios with sensitive data, use proper secret management solutions like Azure Key Vault, AWS Secrets Manager, or HashiCorp Vault.
:::

## Custom Format Provider

You can create your own format provider by implementing `IFormatProvider`:

```csharp
using Configuration.Writable.Provider;

public class MyCustomFormatProvider : FormatProviderBase
{
    public override string DefaultFileExtension => "custom";
    
    public override string? GetSaveContents<T>(
        T value,
        string sectionName,
        string? existingContents,
        ILogger? logger)
    {
        // Implement serialization logic
        // Use base.NestObject() to handle section names
        var nested = base.NestObject(value, sectionName);
        return SerializeToCustomFormat(nested);
    }
    
    public override T? LoadConfiguration<T>(
        string fileContents,
        string sectionName,
        ILogger? logger) where T : class
    {
        // Implement deserialization logic
        var data = DeserializeFromCustomFormat(fileContents);
        return base.UnnestObject<T>(data, sectionName);
    }
    
    // Implement your serialization methods
    private string SerializeToCustomFormat(object obj) { /* ... */ }
    private object? DeserializeFromCustomFormat(string content) { /* ... */ }
}
```

Then use it:

```csharp
conf.FormatProvider = new MyCustomFormatProvider();
```

## Comparing Formats

| Format    | Package                        | Pros                                      | Cons                          |
|-----------|--------------------------------|-------------------------------------------|-------------------------------|
| JSON      | Built-in                       | Widely supported, compact, fast           | Less human-readable           |
| XML       | Configuration.Writable.Xml     | Very human-readable, schema support       | Verbose                       |
| YAML      | Configuration.Writable.Yaml    | Most human-readable, concise              | Parsing overhead              |
| Encrypted | Configuration.Writable.Encrypt | Basic security for non-sensitive data     | Not suitable for secrets      |

## Examples

### JSON with Custom Options

```csharp
conf.FormatProvider = new JsonFormatProvider()
{
    JsonSerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() }
    }
};
```

### Switching Between Formats

```csharp
#if DEBUG
    // Use YAML in development for readability
    conf.FormatProvider = new YamlFormatProvider();
#else
    // Use JSON in production for performance
    conf.FormatProvider = new JsonFormatProvider();
#endif
```

## Next Steps

- [File Provider](./file-provider) - Customize file write operations
- [Validation](./validation) - Validate settings before saving
- [Advanced: NativeAOT](../advanced/native-aot) - Full NativeAOT configuration
