using System;
using System.IO;
using Configuration.Writable.Configure;
using Configuration.Writable.FormatProvider;
using Shouldly;
using Xunit;

namespace Configuration.Writable.Tests;

public class FallbackFormatProviderPathValidationTests
{
    [Fact]
    public void BuildOptions_ShouldRejectCanonicalPathThatCollidesWithFallbackFormat()
    {
        var builder = new WritableOptionsConfigBuilder<MigrationSupportTests.SettingsWithoutVersion>
        {
            FilePath = "settings.json",
            FileProvider = new InMemoryFileProvider(),
            FormatProvider = new ExtensionJsonFormatProvider("yaml"),
        };
        builder.AddFallbackFormatProvider(new JsonFormatProvider());

        var exception = Should.Throw<InvalidOperationException>(() => builder.BuildOptions(""));

        exception.Message.ShouldContain("conflicts with the registered fallback format '.json'");
        exception.Message.ShouldContain("Both resolve to the same file");
        exception.Message.ShouldContain("Canonical format: '.yaml'");
        exception.Message.ShouldContain("Fallback format:  '.json'");
        exception.Message.ShouldContain("Use an extensionless file path");
    }

    [Fact]
    public void BuildOptions_ShouldAllowCustomCanonicalExtensionThatDoesNotCollide()
    {
        var builder = new WritableOptionsConfigBuilder<MigrationSupportTests.SettingsWithoutVersion>
        {
            FilePath = "settings.conf",
            FileProvider = new InMemoryFileProvider(),
            FormatProvider = new ExtensionJsonFormatProvider("yaml"),
        };
        builder.AddFallbackFormatProvider(new JsonFormatProvider());

        var options = builder.BuildOptions("");

        Path.GetExtension(options.ConfigFilePath).ShouldBe(".conf");
    }

    private sealed class ExtensionJsonFormatProvider(string extension) : JsonFormatProvider
    {
        public override string FileExtension => extension;
    }
}
