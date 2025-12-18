using System;
using System.IO;
using System.Threading.Tasks;
using Configuration.Writable.FileProvider;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Configuration.Writable.Tests;

public class WritableOptionsExtensionsTests
{
    private readonly InMemoryFileProvider _FileProvider = new();

    public class TestSettings
    {
        public string Name { get; set; } = "default";
        public int Value { get; set; } = 42;
        public bool IsEnabled { get; set; } = true;
    }

    [Fact]
    public void AddWritableOptions_WithServiceCollection_ShouldRegisterServices()
    {
        var services = new ServiceCollection();
        services.AddWritableOptions<TestSettings>();
        var serviceProvider = services.BuildServiceProvider();
        var writableOptions = serviceProvider.GetService<IWritableOptions<TestSettings>>();
        var readonlyOptions = serviceProvider.GetService<IReadOnlyOptions<TestSettings>>();
        writableOptions.ShouldNotBeNull();
        readonlyOptions.ShouldNotBeNull();
    }

    [Fact]
    public void AddWritableOptions_WithServiceCollection_ShouldUseCustomConfiguration()
    {
        var testFilePath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

        var services = new ServiceCollection();
        services.AddWritableOptions<TestSettings>(options =>
        {
            options.FilePath = testFilePath;
            options.UseInMemoryFileProvider(_FileProvider);
        });
        var serviceProvider = services.BuildServiceProvider();
        var writableOptions = serviceProvider.GetService<IWritableOptions<TestSettings>>();
        var readonlyOptions = serviceProvider.GetService<IReadOnlyOptions<TestSettings>>();
        writableOptions.ShouldNotBeNull();
        readonlyOptions.ShouldNotBeNull();
        writableOptions.GetOptionsConfiguration().ConfigFilePath.ShouldBe(testFilePath);
    }

    [Fact]
    public void AddWritableOptions_WithHostBuilder_ShouldRegisterServices()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddWritableOptions<TestSettings>();

        var host = builder.Build();
        var writableOptions = host.Services.GetService<IWritableOptions<TestSettings>>();
        var readonlyOptions = host.Services.GetService<IReadOnlyOptions<TestSettings>>();

        writableOptions.ShouldNotBeNull();
        readonlyOptions.ShouldNotBeNull();
    }

    [Fact]
    public void AddWritableOptions_WithCustomOptions_ShouldUseCustomConfiguration()
    {
        var testFilePath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddWritableOptions<TestSettings>(options =>
        {
            options.FilePath = testFilePath;
            options.UseInMemoryFileProvider(_FileProvider);
        });

        var host = builder.Build();
        var writableOptions = host.Services.GetRequiredService<IWritableOptions<TestSettings>>();

        var configOptions = writableOptions.GetOptionsConfiguration();
        configOptions.ConfigFilePath.ShouldBe(testFilePath);
    }

    [Fact]
    public async Task WritableOptions_SaveAsync_ShouldPersistData()
    {
        var testFileName = Path.GetRandomFileName();

        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddWritableOptions<TestSettings>(options =>
        {
            options.FilePath = testFileName;
            options.UseInMemoryFileProvider(_FileProvider);
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

        _FileProvider.FileExists(testFileName).ShouldBeTrue();

        var currentValue = writableOptions.CurrentValue;
        currentValue.Name.ShouldBe("host_test");
        currentValue.Value.ShouldBe(500);
        currentValue.IsEnabled.ShouldBeFalse();
    }

    [Fact]
    public async Task WritableOptions_SaveAsyncWithAction_ShouldUpdateData()
    {
        var testFileName = Path.GetRandomFileName();

        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddWritableOptions<TestSettings>(options =>
        {
            options.FilePath = testFileName;
            options.UseInMemoryFileProvider(_FileProvider);
        });

        var host = builder.Build();
        var writableOptions = host.Services.GetRequiredService<IWritableOptions<TestSettings>>();

        await writableOptions.SaveAsync(settings =>
        {
            settings.Name = "action_host_test";
            settings.Value = 600;
        });

        _FileProvider.FileExists(testFileName).ShouldBeTrue();

        var currentValue = writableOptions.CurrentValue;
        currentValue.Name.ShouldBe("action_host_test");
        currentValue.Value.ShouldBe(600);
    }
}
