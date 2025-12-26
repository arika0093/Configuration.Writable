## Providing a FormatProvider for JsonSerializerContext

### Problem
Currently, `JsonFormatProvider` uses the standard serializer from `System.Text.Json`. While this is generally sufficient, it becomes somewhat cumbersome when using source-generated serializers with `JsonSerializerContext`. Additionally, the migration feature for settings implemented in #34 cannot be used in AOT contexts.

### Proposal
Provide a dedicated `JsonAotFormatProvider`. Usage would look like the following:

```csharp
builder.Services.AddWritableOptions<SampleSettingV2>(conf => {
    conf.FormatProvider = new JsonAotFormatProvider(SampleSettingSerializerContext.Default);
    // or
    // conf.UseJsonFormatProvider(SampleSettingSerializerContext.Default);
});

// ---
// support multiple versions
[JsonSerializable(typeof(SampleSettingV1))]
[JsonSerializable(typeof(SampleSettingV2))]
[JsonSerializable(typeof(SampleSettingV3))]
[JsonSerializable(typeof(SampleSettingV4))]
public partial class SampleSettingSerializerContext : JsonSerializerContext;
```

The library API is envisioned as follows:

```csharp
public class JsonAotFormatProvider(ITypeInfoProvider typeInfoProvider) : IFormatProvider
{
    // if needed, allow overriding options
    public JsonSerializerOptions Options { get; init; } = typeInfoProvider.Options;

    public Load(Type type)
    {
        // Conceptually, obtain JsonTypeInfo from the provided type and deserialize
        var jsonTypeInfo = typeInfoProvider.GetTypeInfo(type);
        return JsonSerializer.Deserialize(jsonString, jsonTypeInfo, Options);
    }
}
```
