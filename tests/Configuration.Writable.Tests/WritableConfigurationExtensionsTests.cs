using System;
using System.IO;
using System.Threading.Tasks;
using Configuration.Writable.FileWriter;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Configuration.Writable.Tests;

public class WritableConfigurationExtensionsTests
{
    private readonly InMemoryFileWriter _fileWriter = new();

    public class TestSettings
    {
        public string Name { get; set; } = "default";
        public int Value { get; set; } = 42;
        public bool IsEnabled { get; set; } = true;
    }

    [Fact]
    public void AddUserConfigurationFile_WithServiceCollection_ShouldRegisterServices()
    {
        var config = new ConfigurationManager();
        var services = new ServiceCollection();
        services.AddUserConfigurationFile<TestSettings>(config);
        var serviceProvider = services.BuildServiceProvider();
        var writableOptions = serviceProvider.GetService<IWritableOptions<TestSettings>>();
        var readonlyOptions = serviceProvider.GetService<IReadonlyOptions<TestSettings>>();
        writableOptions.ShouldNotBeNull();
        readonlyOptions.ShouldNotBeNull();
    }

    [Fact]
    public void AddUserConfigurationFile_WithServiceCollection_ShouldUseCustomConfiguration()
    {
        var testFileName = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.json");

        var config = new ConfigurationManager();
        var services = new ServiceCollection();
        services.AddUserConfigurationFile<TestSettings>(
            config,
            options =>
            {
                options.FilePath = testFileName;
                options.UseInMemoryFileWriter(_fileWriter);
            }
        );
        var serviceProvider = services.BuildServiceProvider();
        var writableOptions = serviceProvider.GetService<IWritableOptions<TestSettings>>();
        var readonlyOptions = serviceProvider.GetService<IReadonlyOptions<TestSettings>>();
        writableOptions.ShouldNotBeNull();
        readonlyOptions.ShouldNotBeNull();
        writableOptions.GetWritableConfigurationOptions().ConfigFilePath.ShouldBe(testFileName);
    }

    [Fact]
    public void AddUserConfigurationFile_WithHostBuilder_ShouldRegisterServices()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.AddUserConfigurationFile<TestSettings>();

        var host = builder.Build();
        var writableOptions = host.Services.GetService<IWritableOptions<TestSettings>>();
        var readonlyOptions = host.Services.GetService<IReadonlyOptions<TestSettings>>();

        writableOptions.ShouldNotBeNull();
        readonlyOptions.ShouldNotBeNull();
    }

    [Fact]
    public void AddUserConfigurationFile_WithCustomOptions_ShouldUseCustomConfiguration()
    {
        var testFileName = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.json");

        var builder = Host.CreateApplicationBuilder();
        builder.AddUserConfigurationFile<TestSettings>(options =>
        {
            options.FilePath = testFileName;
            options.UseInMemoryFileWriter(_fileWriter);
        });

        var host = builder.Build();
        var writableOptions = host.Services.GetRequiredService<IWritableOptions<TestSettings>>();

        var configOptions = writableOptions.GetWritableConfigurationOptions();
        configOptions.ConfigFilePath.ShouldBe(testFileName);
    }

    [Fact]
    public async Task WritableOptions_SaveAsync_ShouldPersistData()
    {
        var testFileName = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.json");

        var builder = Host.CreateApplicationBuilder();
        builder.AddUserConfigurationFile<TestSettings>(options =>
        {
            options.FilePath = testFileName;
            options.UseInMemoryFileWriter(_fileWriter);
        });

        var host = builder.Build();
        var writableOptions = host.Services.GetRequiredService<IWritableOptions<TestSettings>>();

        var newSettings = new TestSettings
        {
            Name = "host_test",
            Value = 500,
            IsEnabled = false,
        };

        await writableOptions.SaveAsync(newSettings);

        _fileWriter.FileExists(testFileName).ShouldBeTrue();

        var currentValue = writableOptions.CurrentValue;
        currentValue.Name.ShouldBe("host_test");
        currentValue.Value.ShouldBe(500);
        currentValue.IsEnabled.ShouldBeFalse();
    }

    [Fact]
    public async Task WritableOptions_SaveAsyncWithAction_ShouldUpdateData()
    {
        var testFileName = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.json");

        var builder = Host.CreateApplicationBuilder();
        builder.AddUserConfigurationFile<TestSettings>(options =>
        {
            options.FilePath = testFileName;
            options.UseInMemoryFileWriter(_fileWriter);
        });

        var host = builder.Build();
        var writableOptions = host.Services.GetRequiredService<IWritableOptions<TestSettings>>();

        await writableOptions.SaveAsync(settings =>
        {
            settings.Name = "action_host_test";
            settings.Value = 600;
        });

        _fileWriter.FileExists(testFileName).ShouldBeTrue();

        var currentValue = writableOptions.CurrentValue;
        currentValue.Name.ShouldBe("action_host_test");
        currentValue.Value.ShouldBe(600);
    }
}
