using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Configuration.Writable.Tests;

public class IOptionsIntegrationTests
{
    public class TestSettings
    {
        public string Name { get; set; } = "default";
        public int Value { get; set; } = 42;
        public bool IsEnabled { get; set; } = true;
    }

    [Fact]
    public void IOptions_ShouldProvideCurrentValue()
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
            var optionsService = host.Services.GetRequiredService<IOptions<TestSettings>>();

            var settings = optionsService.Value;
            settings.ShouldNotBeNull();
            settings.Name.ShouldBe("default");
            settings.Value.ShouldBe(42);
            settings.IsEnabled.ShouldBeTrue();
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
    public void IOptionsSnapshot_ShouldProvideCurrentValue()
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

            using var scope = host.Services.CreateScope();
            var optionsService = scope.ServiceProvider.GetRequiredService<
                IOptionsSnapshot<TestSettings>
            >();

            var settings = optionsService.Value;
            settings.ShouldNotBeNull();
            settings.Name.ShouldBe("default");
            settings.Value.ShouldBe(42);
            settings.IsEnabled.ShouldBeTrue();
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
    public void IOptionsMonitor_ShouldProvideCurrentValue()
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
            var optionsService = host.Services.GetRequiredService<IOptionsMonitor<TestSettings>>();

            var settings = optionsService.CurrentValue;
            settings.ShouldNotBeNull();
            settings.Name.ShouldBe("default");
            settings.Value.ShouldBe(42);
            settings.IsEnabled.ShouldBeTrue();
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
    public void IOptionsMonitor_Get_WithName_ShouldWork()
    {
        var testFileName = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.json");

        try
        {
            var builder = Host.CreateApplicationBuilder();
            builder.AddUserConfigurationFile<TestSettings>(options =>
            {
                options.FileName = Path.GetFileNameWithoutExtension(testFileName);
                options.ConfigFolder = Path.GetDirectoryName(testFileName)!;
                options.InstanceName = "custom";
            });

            var host = builder.Build();
            var optionsService = host.Services.GetRequiredService<IOptionsMonitor<TestSettings>>();

            var settings = optionsService.Get("custom");
            settings.ShouldNotBeNull();
            settings.Name.ShouldBe("default");
            settings.Value.ShouldBe(42);
            settings.IsEnabled.ShouldBeTrue();
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
    public async Task IOptions_WithSavedConfiguration_ShouldReturnSavedValues()
    {
        var testFileName = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.json");

        try
        {
            // First, save some configuration
            var builder1 = Host.CreateApplicationBuilder();
            builder1.AddUserConfigurationFile<TestSettings>(options =>
            {
                options.FileName = Path.GetFileNameWithoutExtension(testFileName);
                options.ConfigFolder = Path.GetDirectoryName(testFileName)!;
            });

            var host1 = builder1.Build();
            var writableOptions = host1.Services.GetRequiredService<
                IWritableOptions<TestSettings>
            >();

            await writableOptions.SaveAsync(
                new TestSettings
                {
                    Name = "saved_name",
                    Value = 999,
                    IsEnabled = false,
                }
            );

            host1.Dispose();

            // Then, read it back using IOptions
            var builder2 = Host.CreateApplicationBuilder();
            builder2.AddUserConfigurationFile<TestSettings>(options =>
            {
                options.FileName = Path.GetFileNameWithoutExtension(testFileName);
                options.ConfigFolder = Path.GetDirectoryName(testFileName)!;
            });

            var host2 = builder2.Build();
            var optionsService = host2.Services.GetRequiredService<IOptions<TestSettings>>();

            var settings = optionsService.Value;
            settings.Name.ShouldBe("saved_name");
            settings.Value.ShouldBe(999);
            settings.IsEnabled.ShouldBeFalse();

            host2.Dispose();
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
