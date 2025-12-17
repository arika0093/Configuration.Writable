using System;
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
        builder.Services.AddWritableOptions<UserSetting>(opt =>
        {
            opt.FilePath = firstFileName;
            opt.InstanceName = "First";
            opt.UseInMemoryFileProvider(_FileProvider);
        });

        // second setting
        builder.Services.AddWritableOptions<UserSetting>(opt =>
        {
            opt.FilePath = secondFileName;
            opt.InstanceName = "Second";
            opt.UseInMemoryFileProvider(_FileProvider);
        });

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

        builder.Services.AddWritableOptions<UserSetting>(opt =>
        {
            opt.FilePath = firstFileName;
            opt.InstanceName = "First";
            opt.UseInMemoryFileProvider(_FileProvider);
        });

        builder.Services.AddWritableOptions<UserSetting>(opt =>
        {
            opt.FilePath = secondFileName;
            opt.InstanceName = "Second";
            opt.UseInMemoryFileProvider(_FileProvider);
        });

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

        builder.Services.AddWritableOptions<UserSetting>(opt =>
        {
            opt.FilePath = firstFileName;
            opt.InstanceName = "First";
            opt.UseInMemoryFileProvider(_FileProvider);
        });

        builder.Services.AddWritableOptions<UserSetting>(opt =>
        {
            opt.FilePath = secondFileName;
            opt.InstanceName = "Second";
            opt.UseInMemoryFileProvider(_FileProvider);
        });

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

        builder.Services.AddWritableOptions<UserSetting>(opt =>
        {
            opt.FilePath = fileName;
            opt.UseInMemoryFileProvider(_FileProvider);
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
}
