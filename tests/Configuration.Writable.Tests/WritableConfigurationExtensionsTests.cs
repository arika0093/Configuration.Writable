using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Configuration.Writable.Tests;

public class WritableConfigurationExtensionsTests
{
    public class TestSettings
    {
        public string Name { get; set; } = "default";
        public int Value { get; set; } = 42;
        public bool IsEnabled { get; set; } = true;
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

        try
        {
            var builder = Host.CreateApplicationBuilder();
            builder.AddUserConfigurationFile<TestSettings>(options =>
            {
                options.FileName = Path.GetFileNameWithoutExtension(testFileName);
                options.ConfigFolder = Path.GetDirectoryName(testFileName)!;
            });

            var host = builder.Build();
            var writableOptions = host.Services.GetRequiredService<IWritableOptions<TestSettings>>();

            var configOptions = writableOptions.GetWritableConfigurationOptions();
            configOptions.ConfigFilePath.ShouldBe(testFileName);
        }
        finally
        {
            if (File.Exists(testFileName))
            {
                File.Delete(testFileName);
            }
        }
    }

    [Fact]
    public async Task WritableOptions_SaveAsync_ShouldPersistData()
    {
        var testFileName = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.json");

        try
        {
            var builder = Host.CreateApplicationBuilder();
            builder.AddUserConfigurationFile<TestSettings>(options =>
            {
                options.FileName = Path.GetFileNameWithoutExtension(testFileName);
                options.ConfigFolder = Path.GetDirectoryName(testFileName)!;
            });

            var host = builder.Build();
            var writableOptions = host.Services.GetRequiredService<IWritableOptions<TestSettings>>();

            var newSettings = new TestSettings
            {
                Name = "host_test",
                Value = 500,
                IsEnabled = false
            };

            await writableOptions.SaveAsync(newSettings);

            File.Exists(testFileName).ShouldBeTrue();

            var currentValue = writableOptions.CurrentValue;
            currentValue.Name.ShouldBe("host_test");
            currentValue.Value.ShouldBe(500);
            currentValue.IsEnabled.ShouldBeFalse();
        }
        finally
        {
            if (File.Exists(testFileName))
            {
                File.Delete(testFileName);
            }
        }
    }

    [Fact]
    public async Task WritableOptions_SaveAsyncWithAction_ShouldUpdateData()
    {
        var testFileName = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.json");

        try
        {
            var builder = Host.CreateApplicationBuilder();
            builder.AddUserConfigurationFile<TestSettings>(options =>
            {
                options.FileName = Path.GetFileNameWithoutExtension(testFileName);
                options.ConfigFolder = Path.GetDirectoryName(testFileName)!;
            });

            var host = builder.Build();
            var writableOptions = host.Services.GetRequiredService<IWritableOptions<TestSettings>>();

            await writableOptions.SaveAsync(settings =>
            {
                settings.Name = "action_host_test";
                settings.Value = 600;
            });

            File.Exists(testFileName).ShouldBeTrue();

            var currentValue = writableOptions.CurrentValue;
            currentValue.Name.ShouldBe("action_host_test");
            currentValue.Value.ShouldBe(600);
        }
        finally
        {
            if (File.Exists(testFileName))
            {
                File.Delete(testFileName);
            }
        }
    }
}