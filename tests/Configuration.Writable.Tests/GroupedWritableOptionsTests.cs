using System;
using System.IO;
using Configuration.Writable.FileProvider;
using Configuration.Writable.FormatProvider;
using Configuration.Writable.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Configuration.Writable.Tests;

public partial class GroupedWritableOptionsTests
{
    [OptionsModel]
    public partial class FirstSettings
    {
        public string Value { get; set; } = "first";
    }

    [OptionsModel]
    public partial class SecondSettings
    {
        public string Value { get; set; } = "second";
    }

    [OptionsModel]
    public partial class NamedSettings
    {
        public string Value { get; set; } = "named";
    }

    [OptionsModel]
    public partial class ReinitializedSettings
    {
        public string Value { get; set; } = "retained";
    }

    [Test]
    public void GroupedRegistration_AppliesSharedConfigurationAfterRecipesAreCollected()
    {
        var provider = new InMemoryFileProvider();
        var services = new ServiceCollection();

        services.AddWritableOptions(options =>
        {
            options.Add<FirstSettings>(x => x.AddFilePath("first.json"));
            options.Add<SecondSettings>(x => x.AddFilePath("second.json"));
            options.UseCustomDirectory("grouped");
            options.FileProvider = provider;
        });

        using var serviceProvider = services.BuildServiceProvider();
        var first = serviceProvider
            .GetRequiredService<IWritableOptionsConfigRegistry<FirstSettings>>()
            .Get(string.Empty);
        var second = serviceProvider
            .GetRequiredService<IWritableOptionsConfigRegistry<SecondSettings>>()
            .Get(string.Empty);

        first.FileProvider.ShouldBeSameAs(provider);
        second.FileProvider.ShouldBeSameAs(provider);
        first.ConfigFilePath.ShouldBe(Path.GetFullPath(Path.Combine("grouped", "first.json")));
        second.ConfigFilePath.ShouldBe(Path.GetFullPath(Path.Combine("grouped", "second.json")));
    }

    [Test]
    public void GroupedRegistration_TypeConfigurationOverridesSharedConfiguration()
    {
        var provider = new InMemoryFileProvider();
        var services = new ServiceCollection();
        services.AddWritableOptions(options =>
        {
            options.FileProvider = provider;
            options.UseFile("shared.json");
            options.SectionName = "Shared";
            options.Add<FirstSettings>(x =>
            {
                x.UseFile("specific.json");
                x.SectionName = "Specific";
            });
        });

        using var serviceProvider = services.BuildServiceProvider();
        var configuration = serviceProvider
            .GetRequiredService<IWritableOptionsConfigRegistry<FirstSettings>>()
            .Get(string.Empty);
        configuration.ConfigFilePath.ShouldBe(Path.GetFullPath("specific.json"));
        configuration.SectionNameParts.ShouldBe(["Specific"]);
    }

    [Test]
    public void GroupedRegistration_ClonesFallbackProvidersPerType()
    {
        var provider = new InMemoryFileProvider();
        var services = new ServiceCollection();
        services.AddWritableOptions(options =>
        {
            options.FileProvider = provider;
            options.UseFile("shared.json");
            options.Add<FirstSettings>(builder =>
                builder.AddFallbackFormatProvider(new LegacyJsonFormatProvider("first"))
            );
            options.Add<SecondSettings>(builder =>
                builder.AddFallbackFormatProvider(new LegacyJsonFormatProvider("second"))
            );
        });

        using var serviceProvider = services.BuildServiceProvider();
        var first = serviceProvider
            .GetRequiredService<IWritableOptionsConfigRegistry<FirstSettings>>()
            .Get(string.Empty);
        var second = serviceProvider
            .GetRequiredService<IWritableOptionsConfigRegistry<SecondSettings>>()
            .Get(string.Empty);
        var firstProvider = first.FormatProvider.ShouldBeOfType<FallbackFormatProvider>();
        var secondProvider = second.FormatProvider.ShouldBeOfType<FallbackFormatProvider>();

        firstProvider.ShouldNotBeSameAs(secondProvider);
        firstProvider.FallbackProviders.Count.ShouldBe(1);
        secondProvider.FallbackProviders.Count.ShouldBe(1);
        firstProvider.FallbackProviders[0].FileExtension.ShouldBe("first");
        secondProvider.FallbackProviders[0].FileExtension.ShouldBe("second");
    }

    [Test]
    public void StaticGroupedInitialization_RetainsEveryNamedRegistration()
    {
        var provider = new InMemoryFileProvider();
        WritableOptions.Initialize(options =>
        {
            options.FileProvider = provider;
            options.Add<NamedSettings>("First", conf => conf.UseFile("first.json"));
            options.Add<NamedSettings>("Second", conf => conf.UseFile("second.json"));
        });

        var writableOptions =
            (IWritableOptionsMonitor<NamedSettings>)WritableOptions.GetOptions<NamedSettings>();
        writableOptions.Get("First").ShouldNotBeNull();
        writableOptions.Get("Second").ShouldNotBeNull();
    }

    [Test]
    public void SimpleInstance_ReinitializationFailureRetainsPreviousConfiguration()
    {
        var provider = new InMemoryFileProvider();
        var instance = new WritableOptionsSimpleInstance<ReinitializedSettings>();
        instance.Initialize(options =>
        {
            options.FileProvider = provider;
            options.UseFile("existing.json");
        });

        Should.Throw<InvalidOperationException>(() =>
            instance.Initialize(options =>
            {
                options.FileProvider = new RejectingFileProvider();
                options.UseFile("replacement.json");
            })
        );

        instance
            .GetOptions()
            .ConfigurationInfo.WritePath.ShouldBe(Path.GetFullPath("existing.json"));
    }

    [Test]
    public void StaticGroupedInitialization_FailureRetainsPreviousConfiguration()
    {
        var provider = new InMemoryFileProvider();
        WritableOptions.Initialize<ReinitializedSettings>(options =>
        {
            options.FileProvider = provider;
            options.UseFile("existing.json");
        });

        Should.Throw<InvalidOperationException>(() =>
            WritableOptions.Initialize(options =>
            {
                options.FileProvider = new RejectingFileProvider();
                options.Add<ReinitializedSettings>(conf => conf.UseFile("replacement.json"));
            })
        );

        WritableOptions
            .GetOptions<ReinitializedSettings>()
            .ConfigurationInfo.WritePath.ShouldBe(Path.GetFullPath("existing.json"));
    }

    private sealed class RejectingFileProvider : CommonFileProvider
    {
        public override bool EnsureDirectoryExists(string path) => false;
    }

    private sealed class LegacyJsonFormatProvider(string extension) : JsonFormatProvider
    {
        public override string FileExtension => extension;
    }
}
