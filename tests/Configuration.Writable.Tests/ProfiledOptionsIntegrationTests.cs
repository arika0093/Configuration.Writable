using System.IO;
using System.Threading.Tasks;
using Configuration.Writable.FileProvider;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Configuration.Writable.Tests;

public class ProfiledOptionsIntegrationTests
{
    private readonly InMemoryFileProvider _fileProvider = new();

    [Fact]
    public async Task Profiles_ShouldPersistCatalogAndProfileValues()
    {
        var fileName = Path.GetRandomFileName();
        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddProfiledWritableOptions<ProfileSettings>(options =>
        {
            options.FilePath = fileName;
            options.UseInMemoryFileProvider(_fileProvider);
        });

        using var host = builder.Build();
        var profiles = host.Services.GetRequiredService<
            IProfiledWritableOptions<ProfileSettings>
        >();

        profiles.ProfileNames.ShouldBe(["default"]);

        await profiles.SaveAsync(settings =>
        {
            settings.Theme = "Light";
            settings.FontSize = 12;
        });
        await profiles.CreateProfileAsync("Work", "default");
        await profiles.SetActiveProfileAsync("Work");

        profiles.ActiveProfileName.ShouldBe("Work");
        profiles.ProfileNames.ShouldBe(["default", "Work"], ignoreOrder: true);
        profiles.CurrentValue.Theme.ShouldBe("Light");
        profiles.CurrentValue.FontSize.ShouldBe(12);

        await profiles.GetProfile("Work").SaveAsync(settings => settings.Theme = "Dark");

        profiles.CurrentValue.Theme.ShouldBe("Dark");
        profiles.GetProfile("default").CurrentValue.Theme.ShouldBe("Light");

        var contents = _fileProvider.ReadAllText(fileName);
        contents.ShouldContain("ProfileCatalog");
        contents.ShouldContain("Default");
        contents.ShouldContain("Work");

        await profiles.RemoveProfileAsync("Work");

        profiles.ProfileNames.ShouldBe(["default"]);
        profiles.ActiveProfileName.ShouldBe("default");
    }

    [Fact]
    public async Task Profiles_ShouldRestoreCatalogWhenApplicationRestarts()
    {
        var fileName = Path.GetRandomFileName();

        using (var firstHost = CreateHost(fileName))
        {
            var profiles = firstHost.Services.GetRequiredService<
                IProfiledWritableOptions<ProfileSettings>
            >();
            await profiles.SaveAsync(settings => settings.Theme = "Light");
            await profiles.CreateProfileAsync("Work");
            await profiles.GetProfile("Work").SaveAsync(settings => settings.Theme = "Dark");
            await profiles.SetActiveProfileAsync("Work");
        }

        using var secondHost = CreateHost(fileName);
        var restoredProfiles = secondHost.Services.GetRequiredService<
            IProfiledWritableOptions<ProfileSettings>
        >();

        restoredProfiles.ProfileNames.ShouldBe(["default", "Work"], ignoreOrder: true);
        restoredProfiles.ActiveProfileName.ShouldBe("Work");
        restoredProfiles.CurrentValue.Theme.ShouldBe("Dark");
    }

    private IHost CreateHost(string fileName)
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddProfiledWritableOptions<ProfileSettings>(options =>
        {
            options.FilePath = fileName;
            options.UseInMemoryFileProvider(_fileProvider);
        });
        return builder.Build();
    }

    private class ProfileSettings
    {
        public string Theme { get; set; } = "System";
        public int FontSize { get; set; } = 10;
    }
}
