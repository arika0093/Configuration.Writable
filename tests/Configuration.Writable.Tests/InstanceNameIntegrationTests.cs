using System;
using System.IO;
using System.Threading.Tasks;
using Configuration.Writable.FileWriter;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Configuration.Writable.Tests;

public class InstanceNameIntegrationTests
{
    private readonly InMemoryFileWriter _fileWriter = new();

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
        builder.AddUserConfig<UserSetting>(opt =>
        {
            opt.FilePath = firstFileName;
            opt.InstanceName = "First";
            opt.UseInMemoryFileWriter(_fileWriter);
        });

        // second setting
        builder.AddUserConfig<UserSetting>(opt =>
        {
            opt.FilePath = secondFileName;
            opt.InstanceName = "Second";
            opt.UseInMemoryFileWriter(_fileWriter);
        });

        var host = builder.Build();
        var config = host.Services.GetRequiredService<IWritableOptions<UserSetting>>();

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
        _fileWriter.FileExists(firstFileName).ShouldBeTrue();
        _fileWriter.FileExists(secondFileName).ShouldBeTrue();

        var firstFileContent = _fileWriter.ReadAllText(firstFileName);
        var secondFileContent = _fileWriter.ReadAllText(secondFileName);

        firstFileContent.ShouldContain("first name");
        firstFileContent.ShouldContain("25");
        secondFileContent.ShouldContain("second name");
        secondFileContent.ShouldContain("30");

        host.Dispose();
    }

    [Fact]
    public async Task MultipleInstanceNames_ShouldUseDifferentSectionNames()
    {
        var firstFileName = Path.GetRandomFileName();
        var secondFileName = Path.GetRandomFileName();

        var builder = Host.CreateApplicationBuilder();

        builder.AddUserConfig<UserSetting>(opt =>
        {
            opt.FilePath = firstFileName;
            opt.InstanceName = "First";
            opt.SectionRootName = "UserSetting";
            opt.UseInMemoryFileWriter(_fileWriter);
        });

        builder.AddUserConfig<UserSetting>(opt =>
        {
            opt.FilePath = secondFileName;
            opt.InstanceName = "Second";
            opt.SectionRootName = "UserSetting";
            opt.UseInMemoryFileWriter(_fileWriter);
        });

        var host = builder.Build();
        var config = host.Services.GetRequiredService<IWritableOptions<UserSetting>>();

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

        // Verify section names in the saved files
        var firstFileContent = _fileWriter.ReadAllText(firstFileName);
        var secondFileContent = _fileWriter.ReadAllText(secondFileName);

        // Should use "UserSetting-First" and "UserSetting-Second" as section names
        firstFileContent.ShouldContain("UserSetting-First");
        secondFileContent.ShouldContain("UserSetting-Second");

        host.Dispose();
    }

    [Fact]
    public async Task SaveAsync_WithoutInstanceName_WithMultipleInstances_ShouldThrow()
    {
        var firstFileName = Path.GetRandomFileName();
        var secondFileName = Path.GetRandomFileName();

        var builder = Host.CreateApplicationBuilder();

        builder.AddUserConfig<UserSetting>(opt =>
        {
            opt.FilePath = firstFileName;
            opt.InstanceName = "First";
            opt.UseInMemoryFileWriter(_fileWriter);
        });

        builder.AddUserConfig<UserSetting>(opt =>
        {
            opt.FilePath = secondFileName;
            opt.InstanceName = "Second";
            opt.UseInMemoryFileWriter(_fileWriter);
        });

        var host = builder.Build();
        var config = host.Services.GetRequiredService<IWritableOptions<UserSetting>>();

        // SaveAsync without instance name should throw when multiple instances are registered
        await Should.ThrowAsync<InvalidOperationException>(async () =>
            await config.SaveAsync(setting => setting.Name = "test")
        );

        host.Dispose();
    }

    [Fact]
    public async Task SingleInstance_ShouldWorkWithoutSpecifyingInstanceName()
    {
        var fileName = Path.GetRandomFileName();

        var builder = Host.CreateApplicationBuilder();

        builder.AddUserConfig<UserSetting>(opt =>
        {
            opt.FilePath = fileName;
            opt.UseInMemoryFileWriter(_fileWriter);
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
