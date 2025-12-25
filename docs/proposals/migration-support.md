## Support for Configuration Migration

### Issue
When updating the type of a configuration property (for example, changing a property from a single value to a list), a migration process is required. Currently, such support is not provided, so configuration classes must be updated carefully.

### Approach
Configuration.Writable will provide an interface like the following:

```csharp
namespace Configuration.Writable;
public interface IOptionsModel
{
    public int Version { get; }
}
```

Users can implement this interface as shown below:

```csharp
public class MySettingV1 : IOptionsModel
{
    public int Version => 1;
    public string Name { get; set; }
}

public class MySettingV2 : IOptionsModel
{
    public int Version => 2;
    // Changed to a list
    public List<string> Names { get; set; }
}

public class MySettingV3 : IOptionsModel
{
    public int Version => 3;
    // Changed to a more complex type
    public List<FooConfig> Configs { get; set; }
}
```

Then, register as follows:

```csharp
// Register with the latest type
builder.Services.AddWritableOptions<MySettingV3>(conf => {
    conf.UseFile("mysettings");
    // When this is specified, V1 is registered in the builder and becomes available in the FormatProvider
    conf.UseMigration<MySettingV1, MySettingV2>(old => {
        return new MySettingV2
        {
            // Write migration logic from the old version here
            Names = [ old.Name ]
        };
    });
    // If there are multiple migrations, define them in a chain like V1 -> V2 -> V3
    // (The actual application will also be chained in the same way)
    conf.UseMigration<MySettingV2, MySettingV3>(old => {
        return new MySettingV3
        {
            Configs = old.Names.Select(name => new FooConfig { Name = name }).ToList()
        };
    });
});
```

### Implementation Plan
TODO

### Considerations
* Only type `V3` is registered, but the FormatProvider must be able to read data for `V1` and `V2` as well.
    * Internally, `UseMigration` will store `typeof(T)`.
* First, only the version is read, and then deserialization is performed with the appropriate type according to that value.
    * Processing is switched depending on whether `IOptionsModel` is implemented or not.
    * If `IOptionsModel` is implemented, should it first be deserialized as `IOptionsModel`?
        * If deserialized directly as `V3`, the Version value in the file may be overwritten.
    * Care is needed when a SectionName is specified. (The Version in the section should be read)
* Operation in NativeAOT environments must be confirmed.
    * Implementation should avoid reflection as much as possible.
* Downgrade is not supported.
    * If necessary, increase the version and implement logic to revert to the previous format.
    * If `UseMigration<V3,V2>` is detected, it should result in an error during initialization.
