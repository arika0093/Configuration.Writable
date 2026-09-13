using System;
using System.IO;
using Configuration.Writable.Configure;
using Shouldly;

namespace Configuration.Writable.Tests;

public class FallbackFormatPathValidationTests
{
    [Test]
    public void BuildOptions_ShouldRejectCanonicalPathThatCollidesWithFallbackFormat()
    {
        var builder = new WritableOptionsConfigBuilder<MigrationSupportTests.SettingsWithoutVersion>
        {
            FilePath = "settings.json",
            FileBackend = new InMemoryFileBackend(),
            FormatOptions = new ExtensionJsonFileOptions("yaml"),
        };
        builder.AddFallbackFormat(new JsonFileOptions());

        var exception = Should.Throw<InvalidOperationException>(() => builder.BuildOptions(""));

        exception.Message.ShouldContain("conflicts with the registered fallback format '.json'");
        exception.Message.ShouldContain("Both resolve to the same file");
        exception.Message.ShouldContain("Canonical format: '.yaml'");
        exception.Message.ShouldContain("Fallback format:  '.json'");
        exception.Message.ShouldContain("Use an extensionless file path");
    }

    [Test]
    public void BuildOptions_ShouldAllowCustomCanonicalExtensionThatDoesNotCollide()
    {
        var builder = new WritableOptionsConfigBuilder<MigrationSupportTests.SettingsWithoutVersion>
        {
            FilePath = "settings.conf",
            FileBackend = new InMemoryFileBackend(),
            FormatOptions = new ExtensionJsonFileOptions("yaml"),
        };
        builder.AddFallbackFormat(new JsonFileOptions());

        var options = builder.BuildOptions("");

        Path.GetExtension(options.ConfigFilePath).ShouldBe(".conf");
    }

    [Test]
    public void BuildOptions_ShouldAllowCaseDistinctFallbackPathForLogicalFileProvider()
    {
        var builder = new WritableOptionsConfigBuilder<MigrationSupportTests.SettingsWithoutVersion>
        {
            FilePath = "settings.JSON",
            FileBackend = new InMemoryFileBackend(),
            FormatOptions = new ExtensionJsonFileOptions("yaml"),
        };
        builder.AddFallbackFormat(new JsonFileOptions());

        var options = builder.BuildOptions("");

        Path.GetExtension(options.ConfigFilePath).ShouldBe(".JSON");
    }

    private sealed class ExtensionJsonFileOptions(string extension) : JsonFileOptions
    {
        public override string FileExtension => extension;
    }
}
