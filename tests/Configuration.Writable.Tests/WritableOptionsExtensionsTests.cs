using System.IO;
using System.Threading.Tasks;
using Configuration.Writable.FileProvider;
using Configuration.Writable.Migration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

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

    public class TestSettingsV1 : IHasVersion
    {
        public int Version { get; set; } = 1;
        public string OldName { get; set; } = "old_default";
    }

    public class TestSettingsV2 : IHasVersion
    {
        public int Version { get; set; } = 2;
        public string Name { get; set; } = "default";
        public int Value { get; set; } = 42;
    }

    public class ValidatableSettings
    {
        public string Name { get; set; } = "default";
        public int Count { get; set; } = 0;
    }

    public class ValidatableSettingsValidator : IValidateOptions<ValidatableSettings>
    {
        public ValidateOptionsResult Validate(string? name, ValidatableSettings options)
        {
            if (string.IsNullOrWhiteSpace(options.Name))
            {
                return ValidateOptionsResult.Fail("Name cannot be empty");
            }
            if (options.Count < 0)
            {
                return ValidateOptionsResult.Fail("Count must be non-negative");
            }
            return ValidateOptionsResult.Success;
        }
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

    [Fact]
    public async Task AddWritableOptions_WithMigration_ShouldApplyMigration()
    {
        var testFileName = Path.GetRandomFileName();

        // Create an old version file
        var oldContent = """
        {
            "Version": 1,
            "OldName": "migrated_value"
        }
        """;
        await _FileProvider.SaveToFileAsync(testFileName, System.Text.Encoding.UTF8.GetBytes(oldContent));

        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddWritableOptions<TestSettingsV2>(options =>
        {
            options.FilePath = testFileName;
            options.UseInMemoryFileProvider(_FileProvider);
            options.UseMigration<TestSettingsV1, TestSettingsV2>(v1 => new TestSettingsV2
            {
                Name = v1.OldName,
                Value = 100
            });
        });

        var host = builder.Build();
        var writableOptions = host.Services.GetRequiredService<IWritableOptions<TestSettingsV2>>();

        var currentValue = writableOptions.CurrentValue;
        currentValue.Version.ShouldBe(2);
        currentValue.Name.ShouldBe("migrated_value");
        currentValue.Value.ShouldBe(100);
    }

    [Fact]
    public async Task AddWritableOptions_WithValidation_ShouldValidateOnSave()
    {
        var testFileName = Path.GetRandomFileName();

        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddWritableOptions<ValidatableSettings>(options =>
        {
            options.FilePath = testFileName;
            options.UseInMemoryFileProvider(_FileProvider);
            options.WithValidator<ValidatableSettingsValidator>();
        });

        var host = builder.Build();
        var writableOptions = host.Services.GetRequiredService<IWritableOptions<ValidatableSettings>>();

        // Valid settings should save successfully
        var validSettings = new ValidatableSettings
        {
            Name = "valid_name",
            Count = 10
        };
        await writableOptions.SaveAsync(validSettings);
        writableOptions.CurrentValue.Name.ShouldBe("valid_name");
        writableOptions.CurrentValue.Count.ShouldBe(10);

        // Invalid settings should throw exception
        var invalidSettings = new ValidatableSettings
        {
            Name = "",
            Count = -5
        };
        var exception = await Should.ThrowAsync<OptionsValidationException>(
            async () => await writableOptions.SaveAsync(invalidSettings)
        );
        exception.Message.ShouldContain("Name cannot be empty");
    }

    [Fact]
    public async Task AddWritableOptions_WithValidatorFunction_ShouldValidateOnSave()
    {
        var testFileName = Path.GetRandomFileName();

        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddWritableOptions<ValidatableSettings>(options =>
        {
            options.FilePath = testFileName;
            options.UseInMemoryFileProvider(_FileProvider);
            options.WithValidatorFunction(settings =>
            {
                if (settings.Count > 100)
                {
                    return ValidateOptionsResult.Fail("Count must be less than or equal to 100");
                }
                return ValidateOptionsResult.Success;
            });
        });

        var host = builder.Build();
        var writableOptions = host.Services.GetRequiredService<IWritableOptions<ValidatableSettings>>();

        // Valid settings should save successfully
        var validSettings = new ValidatableSettings
        {
            Name = "test",
            Count = 50
        };
        await writableOptions.SaveAsync(validSettings);
        writableOptions.CurrentValue.Count.ShouldBe(50);

        // Invalid settings should throw exception
        var invalidSettings = new ValidatableSettings
        {
            Name = "test",
            Count = 150
        };
        var exception = await Should.ThrowAsync<OptionsValidationException>(
            async () => await writableOptions.SaveAsync(invalidSettings)
        );
        exception.Message.ShouldContain("Count must be less than or equal to 100");
    }
}
