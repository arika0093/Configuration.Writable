using System.Threading.Tasks;
using Configuration.Writable.Testing;

namespace Configuration.Writable.Tests;

public class ProfiledOptionsStubTests
{
    [Fact]
    public async Task Profiles_ShouldCreateCopySwitchAndUpdateActiveProfile()
    {
        var profiles = ProfiledOptionsStub.Create(
            new ProfileSettings { Theme = "Light" },
            defaultProfile: "Personal"
        );

        await profiles.CreateProfileAsync("Work", "Personal");
        await profiles.SetActiveProfileAsync("Work");
        await profiles.SaveAsync(settings => settings.Theme = "Dark");

        profiles.ActiveProfileName.ShouldBe("Work");
        profiles.CurrentValue.Theme.ShouldBe("Dark");
        profiles.GetProfile("Personal").CurrentValue.Theme.ShouldBe("Light");
    }

    private class ProfileSettings
    {
        public string Theme { get; set; } = "";
    }
}
