using System.IO;
using Configuration.Writable.FileProvider;
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
            .GetRequiredService<IWritableOptions<FirstSettings>>()
            .GetOptionsConfiguration();
        var second = serviceProvider
            .GetRequiredService<IWritableOptions<SecondSettings>>()
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
            .GetRequiredService<IWritableOptions<FirstSettings>>()
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
}
