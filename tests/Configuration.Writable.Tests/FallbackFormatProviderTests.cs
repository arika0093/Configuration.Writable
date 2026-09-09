using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Configuration.Writable.Configure;
using Configuration.Writable.FormatProvider;
using Configuration.Writable.Migration;
using Shouldly;
using Xunit;

namespace Configuration.Writable.Tests;

public class FallbackFormatProviderTests
{
    [Fact]
    public async Task FallbackFormat_ShouldLoadMigrateAndPromoteToCanonicalFormat()
    {
        var fileProvider = new InMemoryFileProvider();
        var builder = new WritableOptionsConfigBuilder<MigrationSupportTests.MySettingsV2>
        {
            FilePath = "fallback-settings",
            FileProvider = fileProvider,
            FormatProvider = new JsonFormatProvider(),
        };
        builder.AddFallbackFormatProvider(new LegacyJsonFormatProvider());
        builder.UseMigration<
            MigrationSupportTests.MySettingsV1,
            MigrationSupportTests.MySettingsV2
        >(source => new MigrationSupportTests.MySettingsV2 { Names = [source.Name] });

        var options = builder.BuildOptions("");
        var fallbackPath = Path.ChangeExtension(options.ConfigFilePath, "legacy");
        await fileProvider.SaveToFileAsync(
            fallbackPath,
            Encoding.UTF8.GetBytes("""{"Version":1,"Name":"legacy"}""")
        );

        var result = options.FormatProvider.LoadWithMigration(options);

        result.Names.ShouldBe(["legacy"]);
        fileProvider.FileExists(options.ConfigFilePath).ShouldBeTrue();
        fileProvider.ReadAllText(options.ConfigFilePath).ShouldContain("Names");
        fileProvider.FileExists(fallbackPath).ShouldBeTrue();
    }

    [Fact]
    public void AddFallbackFormatProvider_ShouldRejectDuplicateExtension()
    {
        var builder = new WritableOptionsConfigBuilder<MigrationSupportTests.SettingsWithoutVersion>
        {
            FormatProvider = new JsonFormatProvider(),
        };

        Should.Throw<InvalidOperationException>(() =>
            builder.AddFallbackFormatProvider(new JsonFormatProvider())
        );
    }

    private sealed class LegacyJsonFormatProvider : JsonFormatProvider
    {
        public override string FileExtension => "legacy";
    }
}
