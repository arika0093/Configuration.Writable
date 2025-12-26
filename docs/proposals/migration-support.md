## Support for Configuration Migration

### Issue
When updating the type of a configuration property (for example, changing a property from a single value to a list), a migration process is required. Currently, such support is not provided, so configuration classes must be updated carefully.

### Approach
Configuration.Writable will provide an interface like the following:

```csharp
namespace Configuration.Writable;
public interface IHasVersion
{
    public int Version { get; }
}
```

Users can implement this interface as shown below:

```csharp
public class MySettingV1 : IHasVersion
{
    public int Version { get; set; } =1;
    public string Name { get; set; }
}

public class MySettingV2 : IHasVersion
{
    public int Version { get; set; } =2;
    // Changed to a list
    public List<string> Names { get; set; }
}

public class MySettingV3 : IHasVersion
{
    public int Version { get; set; } =3;
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
* Create an interface `IHasVersion` with a `Version` property.
* UseMigration<TFrom, TTo> extension method to register migration logic.
    * マイグレーションチェーンを内部的に保持する。
    * バージョン情報をキャッシュするための`VersionCache`クラスに情報を追加する。
* MigrationLoaderExtensionを追加し、`LoadWithMigration<T>`メソッドを実装する。
    * `IFormatProvider`の拡張メソッドとして実装する。
    * まずは普通に読み込みを行い、`IHasVersion`を実装しているか確認する。
    * 実装している場合、Versionを確認し、登録されている型と異なる場合はマイグレーションを実行する。
        * Versionの値を見てどの型と一致するかを確認
        * LoadConfiguration(Type, ...)を使ってその型で読み込みを行う。
        * そこから登録されているマイグレーションチェーンをたどって最新の型に変換する。
* FormatProvider.LoadConfiguration<T>の代わりにLoadWithMigration<T>を呼び出すようにする。
* FormatProviderの実装で、LoadConfiguration<T>に追加でLoadConfiguration(Type, ...)を実装する。


### Considerations
* Only type `V3` is registered, but the FormatProvider must be able to read data for `V1` and `V2` as well.
    * Internally, `UseMigration` will store `typeof(T)`.
* First, only the version is read, and then deserialization is performed with the appropriate type according to that value.
    * Processing is switched depending on whether `IHasVersion` is implemented or not.
    * If `IHasVersion` is implemented, should it first be deserialized as `IHasVersion`?
        * If deserialized directly as `V3`, the Version value in the file may be overwritten.
    * Care is needed when a SectionName is specified. (The Version in the section should be read)
* Operation in NativeAOT environments must be confirmed.
    * Implementation should avoid reflection as much as possible.
* Downgrade is not supported.
    * If necessary, increase the version and implement logic to revert to the previous format.
    * If `UseMigration<V3,V2>` is detected, it should result in an error during initialization.
