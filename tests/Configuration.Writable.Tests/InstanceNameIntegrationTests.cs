using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Configuration.Writable.FileProvider;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Configuration.Writable.Tests;

public class InstanceNameIntegrationTests
{
    private readonly InMemoryFileProvider _FileProvider = new();

    public class UserSetting
    {
        public string Name { get; set; } = "default name";
        public int Age { get; set; } = 20;
    }

    [Fact]
    public async Task MultipleInstanceNames_ShouldManageSeparateSettings()
    {
        var firstFileName = Path.GetRandomFileName();
        var secondFileName = Path.GetRandomFileName();

        var builder = Host.CreateApplicationBuilder();

        // first setting
        builder.Services.AddWritableOptions<UserSetting>(
            "First",
            conf =>
            {
                conf.FilePath = firstFileName;
                conf.UseInMemoryFileProvider(_FileProvider);
            }
        );

        // second setting
        builder.Services.AddWritableOptions<UserSetting>(
            "Second",
            conf =>
            {
                conf.FilePath = secondFileName;
                conf.UseInMemoryFileProvider(_FileProvider);
            }
        );

        var host = builder.Build();
        var config = host.Services.GetRequiredService<IWritableNamedOptions<UserSetting>>();

        // Verify initial default values for both instances
        var firstSetting = config.Get("First");
        var secondSetting = config.Get("Second");

        firstSetting.Name.ShouldBe("default name");
        firstSetting.Age.ShouldBe(20);
        secondSetting.Name.ShouldBe("default name");
        secondSetting.Age.ShouldBe(20);

        // Save different values to each instance
        await config.SaveAsync(
            "First",
            setting =>
            {
                setting.Name = "first name";
                setting.Age = 25;
            }
        );

        await config.SaveAsync(
            "Second",
            setting =>
            {
                setting.Name = "second name";
                setting.Age = 30;
            }
        );

        // Verify that each instance has its own values
        firstSetting = config.Get("First");
        secondSetting = config.Get("Second");

        firstSetting.Name.ShouldBe("first name");
        firstSetting.Age.ShouldBe(25);
        secondSetting.Name.ShouldBe("second name");
        secondSetting.Age.ShouldBe(30);

        // Verify that the files are saved separately
        _FileProvider.FileExists(firstFileName).ShouldBeTrue();
        _FileProvider.FileExists(secondFileName).ShouldBeTrue();

        var firstFileContent = _FileProvider.ReadAllText(firstFileName);
        var secondFileContent = _FileProvider.ReadAllText(secondFileName);

        firstFileContent.ShouldContain("first name");
        firstFileContent.ShouldContain("25");
        secondFileContent.ShouldContain("second name");
        secondFileContent.ShouldContain("30");

        host.Dispose();
    }

    [Fact]
    public async Task MultipleInstanceNames_ShouldSaveToSeparateFiles()
    {
        var firstFileName = Path.GetRandomFileName();
        var secondFileName = Path.GetRandomFileName();

        var builder = Host.CreateApplicationBuilder();

        builder.Services.AddWritableOptions<UserSetting>(
            "First",
            conf =>
            {
                conf.FilePath = firstFileName;
                conf.UseInMemoryFileProvider(_FileProvider);
            }
        );

        builder.Services.AddWritableOptions<UserSetting>(
            "Second",
            conf =>
            {
                conf.FilePath = secondFileName;
                conf.UseInMemoryFileProvider(_FileProvider);
            }
        );

        var host = builder.Build();
        var config = host.Services.GetRequiredService<IWritableNamedOptions<UserSetting>>();

        await config.SaveAsync(
            "First",
            setting =>
            {
                setting.Name = "first setting";
            }
        );

        await config.SaveAsync(
            "Second",
            setting =>
            {
                setting.Name = "second setting";
            }
        );

        // Verify content in the saved files (both use root level by default)
        var firstFileContent = _FileProvider.ReadAllText(firstFileName);
        var secondFileContent = _FileProvider.ReadAllText(secondFileName);

        // Both should contain the settings at root level
        firstFileContent.ShouldContain("first setting");
        secondFileContent.ShouldContain("second setting");

        host.Dispose();
    }

    [Fact]
    public async Task SaveAsync_WithoutInstanceName_WithMultipleInstances_ShouldThrow()
    {
        var firstFileName = Path.GetRandomFileName();
        var secondFileName = Path.GetRandomFileName();

        var builder = Host.CreateApplicationBuilder();

        builder.Services.AddWritableOptions<UserSetting>(
            "First",
            conf =>
            {
                conf.FilePath = firstFileName;
                conf.UseInMemoryFileProvider(_FileProvider);
            }
        );

        builder.Services.AddWritableOptions<UserSetting>(
            "Second",
            conf =>
            {
                conf.FilePath = secondFileName;
                conf.UseInMemoryFileProvider(_FileProvider);
            }
        );

        var host = builder.Build();
        var config = host.Services.GetRequiredService<IWritableOptions<UserSetting>>();

        // SaveAsync without instance name should throw when multiple instances are registered
        await Should.ThrowAsync<KeyNotFoundException>(async () =>
            await config.SaveAsync(setting => setting.Name = "test")
        );

        host.Dispose();
    }

    [Fact]
    public async Task SingleInstance_ShouldWorkWithoutSpecifyingInstanceName()
    {
        var fileName = Path.GetRandomFileName();

        var builder = Host.CreateApplicationBuilder();

        builder.Services.AddWritableOptions<UserSetting>(conf =>
        {
            conf.FilePath = fileName;
            conf.UseInMemoryFileProvider(_FileProvider);
        });

        var host = builder.Build();
        var config = host.Services.GetRequiredService<IWritableOptions<UserSetting>>();

        // With single instance, CurrentValue should work
        var setting = config.CurrentValue;
        setting.Name.ShouldBe("default name");

        // SaveAsync without instance name should work
        await config.SaveAsync(setting =>
        {
            setting.Name = "updated name";
        });

        // Verify the change
        setting = config.CurrentValue;
        setting.Name.ShouldBe("updated name");

        host.Dispose();
    }

    [Fact]
    public void GetSpecifiedInstance_ShouldReturnBoundInstance()
    {
        var firstFileName = Path.GetRandomFileName();
        var secondFileName = Path.GetRandomFileName();

        var builder = Host.CreateApplicationBuilder();

        builder.Services.AddWritableOptions<UserSetting>(
            "First",
            conf =>
            {
                conf.FilePath = firstFileName;
                conf.UseInMemoryFileProvider(_FileProvider);
            }
        );

        builder.Services.AddWritableOptions<UserSetting>(
            "Second",
            conf =>
            {
                conf.FilePath = secondFileName;
                conf.UseInMemoryFileProvider(_FileProvider);
            }
        );

        var host = builder.Build();
        var namedOptions = host.Services.GetRequiredService<IWritableNamedOptions<UserSetting>>();

        // Get bound instances
        var firstOptions = namedOptions.GetSpecifiedInstance("First");
        var secondOptions = namedOptions.GetSpecifiedInstance("Second");

        // Verify they are not null
        firstOptions.ShouldNotBeNull();
        secondOptions.ShouldNotBeNull();

        // Verify they have the correct initial values
        firstOptions.CurrentValue.Name.ShouldBe("default name");
        secondOptions.CurrentValue.Name.ShouldBe("default name");

        host.Dispose();
    }

    [Fact]
    public async Task GetSpecifiedInstance_CurrentValue_ShouldReturnCorrectInstance()
    {
        var firstFileName = Path.GetRandomFileName();
        var secondFileName = Path.GetRandomFileName();

        var builder = Host.CreateApplicationBuilder();

        builder.Services.AddWritableOptions<UserSetting>(
            "First",
            conf =>
            {
                conf.FilePath = firstFileName;
                conf.UseInMemoryFileProvider(_FileProvider);
            }
        );

        builder.Services.AddWritableOptions<UserSetting>(
            "Second",
            conf =>
            {
                conf.FilePath = secondFileName;
                conf.UseInMemoryFileProvider(_FileProvider);
            }
        );

        var host = builder.Build();
        var namedOptions = host.Services.GetRequiredService<IWritableNamedOptions<UserSetting>>();

        // Save different values to each instance
        await namedOptions.SaveAsync("First", setting => setting.Name = "first value");
        await namedOptions.SaveAsync("Second", setting => setting.Name = "second value");

        // Get bound instances
        var firstOptions = namedOptions.GetSpecifiedInstance("First");
        var secondOptions = namedOptions.GetSpecifiedInstance("Second");

        // Verify CurrentValue returns the correct instance
        firstOptions.CurrentValue.Name.ShouldBe("first value");
        secondOptions.CurrentValue.Name.ShouldBe("second value");

        host.Dispose();
    }

    [Fact]
    public async Task GetSpecifiedInstance_SaveAsync_ShouldSaveToCorrectInstance()
    {
        var firstFileName = Path.GetRandomFileName();
        var secondFileName = Path.GetRandomFileName();

        var builder = Host.CreateApplicationBuilder();

        builder.Services.AddWritableOptions<UserSetting>(
            "First",
            conf =>
            {
                conf.FilePath = firstFileName;
                conf.UseInMemoryFileProvider(_FileProvider);
            }
        );

        builder.Services.AddWritableOptions<UserSetting>(
            "Second",
            conf =>
            {
                conf.FilePath = secondFileName;
                conf.UseInMemoryFileProvider(_FileProvider);
            }
        );

        var host = builder.Build();
        var namedOptions = host.Services.GetRequiredService<IWritableNamedOptions<UserSetting>>();

        // Get bound instances
        var firstOptions = namedOptions.GetSpecifiedInstance("First");
        var secondOptions = namedOptions.GetSpecifiedInstance("Second");

        // Save using bound instances (no need to specify name)
        await firstOptions.SaveAsync(setting =>
        {
            setting.Name = "saved via first instance";
            setting.Age = 35;
        });

        await secondOptions.SaveAsync(setting =>
        {
            setting.Name = "saved via second instance";
            setting.Age = 40;
        });

        // Verify values were saved to correct instances
        namedOptions.Get("First").Name.ShouldBe("saved via first instance");
        namedOptions.Get("First").Age.ShouldBe(35);
        namedOptions.Get("Second").Name.ShouldBe("saved via second instance");
        namedOptions.Get("Second").Age.ShouldBe(40);

        // Also verify via bound instances
        firstOptions.CurrentValue.Name.ShouldBe("saved via first instance");
        firstOptions.CurrentValue.Age.ShouldBe(35);
        secondOptions.CurrentValue.Name.ShouldBe("saved via second instance");
        secondOptions.CurrentValue.Age.ShouldBe(40);

        host.Dispose();
    }

    [Fact]
    public void GetSpecifiedInstance_GetOptionsConfiguration_ShouldReturnCorrectConfiguration()
    {
        var firstFileName = Path.GetRandomFileName();
        var secondFileName = Path.GetRandomFileName();

        var builder = Host.CreateApplicationBuilder();

        builder.Services.AddWritableOptions<UserSetting>(
            "First",
            conf =>
            {
                conf.FilePath = firstFileName;
                conf.UseInMemoryFileProvider(_FileProvider);
            }
        );

        builder.Services.AddWritableOptions<UserSetting>(
            "Second",
            conf =>
            {
                conf.FilePath = secondFileName;
                conf.UseInMemoryFileProvider(_FileProvider);
            }
        );

        var host = builder.Build();
        var namedOptions = host.Services.GetRequiredService<IWritableNamedOptions<UserSetting>>();

        // Get bound instances
        var firstOptions = namedOptions.GetSpecifiedInstance("First");
        var secondOptions = namedOptions.GetSpecifiedInstance("Second");

        // Get configurations
        var firstConfig = firstOptions.GetOptionsConfiguration();
        var secondConfig = secondOptions.GetOptionsConfiguration();

        // Verify configurations
        firstConfig.InstanceName.ShouldBe("First");
        Path.GetFileName(firstConfig.ConfigFilePath).ShouldBe(firstFileName);
        secondConfig.InstanceName.ShouldBe("Second");
        Path.GetFileName(secondConfig.ConfigFilePath).ShouldBe(secondFileName);

        host.Dispose();
    }
}
