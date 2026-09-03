using System;
using System.IO;
using Configuration.Writable.FileProvider;
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

    [Fact]
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
            .GetRequiredService<IWritableOptionsConfigurationAccessor<FirstSettings>>()
            .GetOptionsConfiguration();
        var second = serviceProvider
            .GetRequiredService<IWritableOptionsConfigurationAccessor<SecondSettings>>()
            .GetOptionsConfiguration();

        first.FileProvider.ShouldBeSameAs(provider);
        second.FileProvider.ShouldBeSameAs(provider);
        first.ConfigFilePath.ShouldBe(Path.GetFullPath(Path.Combine("grouped", "first.json")));
        second.ConfigFilePath.ShouldBe(Path.GetFullPath(Path.Combine("grouped", "second.json")));
    }

    [Fact]
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
            .GetRequiredService<IWritableOptionsConfigurationAccessor<FirstSettings>>()
            .GetOptionsConfiguration();
        configuration.ConfigFilePath.ShouldBe(Path.GetFullPath("specific.json"));
        configuration.SectionNameParts.ShouldBe(["Specific"]);
    }

    [Fact]
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

    [Fact]
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

        var configuration = (
            (IWritableOptionsConfigurationAccessor<ReinitializedSettings>)instance.GetOptions()
        ).GetOptionsConfiguration();
        configuration.FileProvider.ShouldBeSameAs(provider);
        configuration.ConfigFilePath.ShouldBe(Path.GetFullPath("existing.json"));
    }

    [Fact]
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

        var configuration = (
            (IWritableOptionsConfigurationAccessor<ReinitializedSettings>)
                WritableOptions.GetOptions<ReinitializedSettings>()
        ).GetOptionsConfiguration();
        configuration.FileProvider.ShouldBeSameAs(provider);
        configuration.ConfigFilePath.ShouldBe(Path.GetFullPath("existing.json"));
    }

    private sealed class RejectingFileProvider : CommonFileProvider
    {
        public override bool EnsureDirectoryExists(string path) => false;
    }
}
