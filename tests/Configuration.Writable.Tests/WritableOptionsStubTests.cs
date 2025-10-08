using System.Threading.Tasks;
using Configuration.Writable;
using Configuration.Writable.Testing;

namespace Configuration.Writable.Tests;

public class WritableOptionsStubTests
{
    [Fact]
    public async Task WritableOptionsStub_UseReadonlyOptionServiceTest()
    {
        var setting = new UserSettings { Name = "InitialName", Age = 20 };
        var options = new WritableOptionsStub<UserSettings>(setting);
        var service = new SampleReadonlyService(options);
        var act = await service.ReadName();
        act.ShouldBe("InitialName");
    }

    [Fact]
    public async Task WritableOptionsStub_UseWritableOptionServiceTest()
    {
        var setting = new UserSettings { Name = "InitialName", Age = 20 };
        var options = new WritableOptionsStub<UserSettings>(setting);
        var service = new SampleWritableService(options);
        await service.UpdateSettings("UpdatedName", 30);
        setting.Name.ShouldBe("UpdatedName");
        setting.Age.ShouldBe(30);
    }

    [Fact]
    public async Task WritableOptionsStub_UseReadonlyOptionService_NoGenerics_Test()
    {
        var setting = new UserSettings { Name = "InitialName", Age = 20 };
        var options = WritableOptionsStub.Create(setting);
        var service = new SampleReadonlyService(options);
        var act = await service.ReadName();
        act.ShouldBe("InitialName");
    }

    [Fact]
    public async Task WritableOptionsStub_UseWritableOptionService_NoGenerics_Test()
    {
        var setting = new UserSettings { Name = "InitialName", Age = 20 };
        var options = WritableOptionsStub.Create(setting);
        var service = new SampleWritableService(options);
        await service.UpdateSettings("UpdatedName", 30);
        setting.Name.ShouldBe("UpdatedName");
        setting.Age.ShouldBe(30);
    }
}

file class SampleReadonlyService(IReadonlyOptions<UserSettings> option)
{
    public async Task<string?> ReadName()
    {
        return await Task.FromResult(option.CurrentValue.Name);
    }
}

file class SampleWritableService(IWritableOptions<UserSettings> option)
{
    public async Task UpdateSettings(string name, int age)
    {
        await option.SaveAsync(settings =>
        {
            settings.Name = name;
            settings.Age = age;
        });
    }
}

file class UserSettings
{
    public string? Name { get; set; }
    public int Age { get; set; }
}
