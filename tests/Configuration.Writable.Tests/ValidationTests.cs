using System;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Configuration.Writable.FileWriter;
using Microsoft.Extensions.Options;

namespace Configuration.Writable.Tests;

public class ValidationTests
{
    private readonly InMemoryFileWriter _fileWriter = new();

    [Fact]
    public async Task SaveAsync_WithValidationFunction_ShouldThrowWhenValidationFails()
    {
        var testFileName = Path.GetRandomFileName();

        var _instance = new WritableOptionsSimpleInstance<ValidatableSettings>();
        _instance.Initialize(options =>
        {
            options.FilePath = testFileName;
            options.UseInMemoryFileWriter(_fileWriter);
            options.WithValidatorFunction(settings =>
            {
                if (settings.MaxConnections < 1)
                    return ValidateOptionsResult.Fail("MaxConnections must be positive");
                return ValidateOptionsResult.Success;
            });
        });

        var invalidSettings = new ValidatableSettings
        {
            MaxConnections = 0,
            Email = "test@example.com",
        };

        var option = _instance.GetOptions();

        var exception = await Should.ThrowAsync<OptionsValidationException>(async () =>
        {
            await option.SaveAsync(invalidSettings);
        });

        exception.Failures.ShouldContain("MaxConnections must be positive");
        _fileWriter.FileExists(testFileName).ShouldBeFalse();
    }

    [Fact]
    public async Task SaveAsync_WithValidationFunction_ShouldSucceedWhenValidationPasses()
    {
        var testFileName = Path.GetRandomFileName();

        var _instance = new WritableOptionsSimpleInstance<ValidatableSettings>();
        _instance.Initialize(options =>
        {
            options.FilePath = testFileName;
            options.UseInMemoryFileWriter(_fileWriter);
            options.WithValidatorFunction(settings =>
            {
                if (settings.MaxConnections < 1)
                    return ValidateOptionsResult.Fail("MaxConnections must be positive");
                return ValidateOptionsResult.Success;
            });
        });

        var validSettings = new ValidatableSettings
        {
            MaxConnections = 10,
            Email = "test@example.com",
        };

        var option = _instance.GetOptions();
        await option.SaveAsync(validSettings);

        _fileWriter.FileExists(testFileName).ShouldBeTrue();
        var loadedSettings = option.CurrentValue;
        loadedSettings.MaxConnections.ShouldBe(10);
    }

    [Fact]
    public async Task SaveAsync_WithMultipleValidators_ShouldCombineErrors()
    {
        var testFileName = Path.GetRandomFileName();

        var _instance = new WritableOptionsSimpleInstance<ValidatableSettings>();
        _instance.Initialize(options =>
        {
            options.FilePath = testFileName;
            options.UseInMemoryFileWriter(_fileWriter);
            options.WithValidatorFunction(settings =>
            {
                if (settings.MaxConnections < 1)
                    return ValidateOptionsResult.Fail("MaxConnections must be positive");
                return ValidateOptionsResult.Success;
            });
            options.WithValidatorFunction(settings =>
            {
                if (string.IsNullOrWhiteSpace(settings.Email))
                    return ValidateOptionsResult.Fail("Email is required");
                return ValidateOptionsResult.Success;
            });
        });

        var invalidSettings = new ValidatableSettings { MaxConnections = 0, Email = "" };

        var option = _instance.GetOptions();

        var exception = await Should.ThrowAsync<OptionsValidationException>(async () =>
        {
            await option.SaveAsync(invalidSettings);
        });

        exception.Failures.Count().ShouldBe(2);
        exception.Failures.ShouldContain("MaxConnections must be positive");
        exception.Failures.ShouldContain("Email is required");
    }

    [Fact]
    public async Task SaveAsync_WithIValidator_ShouldValidateCorrectly()
    {
        var testFileName = Path.GetRandomFileName();

        var _instance = new WritableOptionsSimpleInstance<ValidatableSettings>();
        _instance.Initialize(options =>
        {
            options.FilePath = testFileName;
            options.UseInMemoryFileWriter(_fileWriter);
            options.WithValidator(new ValidatableSettingsValidator());
        });

        var invalidSettings = new ValidatableSettings { MaxConnections = -5, Email = "invalid" };

        var option = _instance.GetOptions();

        var exception = await Should.ThrowAsync<OptionsValidationException>(async () =>
        {
            await option.SaveAsync(invalidSettings);
        });

        exception.Failures.Count().ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task SaveAsync_WithDataAnnotations_ShouldValidateCorrectly()
    {
        var testFileName = Path.GetRandomFileName();

        var _instance = new WritableOptionsSimpleInstance<AnnotatedSettings>();
        _instance.Initialize(options =>
        {
            options.FilePath = testFileName;
            options.UseInMemoryFileWriter(_fileWriter);
            options.UseDataAnnotationsValidation = true;
        });

        var invalidSettings = new AnnotatedSettings
        {
            MaxConnections = 2000, // Over the range
            Email = "not-an-email",
        };

        var option = _instance.GetOptions();

        var exception = await Should.ThrowAsync<OptionsValidationException>(async () =>
        {
            await option.SaveAsync(invalidSettings);
        });

        exception.Failures.Count().ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task SaveAsync_WithDataAnnotations_ShouldSucceedWhenValid()
    {
        var testFileName = Path.GetRandomFileName();

        var _instance = new WritableOptionsSimpleInstance<AnnotatedSettings>();
        _instance.Initialize(options =>
        {
            options.FilePath = testFileName;
            options.UseInMemoryFileWriter(_fileWriter);
            options.UseDataAnnotationsValidation = true;
        });

        var validSettings = new AnnotatedSettings
        {
            MaxConnections = 50,
            Email = "test@example.com",
            Name = "Valid Name",
        };

        var option = _instance.GetOptions();
        await option.SaveAsync(validSettings);

        _fileWriter.FileExists(testFileName).ShouldBeTrue();
        var loadedSettings = option.CurrentValue;
        loadedSettings.MaxConnections.ShouldBe(50);
    }

    [Fact]
    public async Task SaveAsync_WithDataAnnotationsAndCustomValidators_ShouldCombineAll()
    {
        var testFileName = Path.GetRandomFileName();

        var _instance = new WritableOptionsSimpleInstance<AnnotatedSettings>();
        _instance.Initialize(options =>
        {
            options.FilePath = testFileName;
            options.UseInMemoryFileWriter(_fileWriter);
            options.UseDataAnnotationsValidation = true;
            options.WithValidatorFunction(settings =>
            {
                if (settings.Name == "forbidden")
                    return ValidateOptionsResult.Fail("Name 'forbidden' is not allowed");
                return ValidateOptionsResult.Success;
            });
        });

        var invalidSettings = new AnnotatedSettings
        {
            MaxConnections = 2000, // Over range - Data Annotation error
            Email = "test@example.com",
            Name = "forbidden", // Custom validation error
        };

        var option = _instance.GetOptions();

        var exception = await Should.ThrowAsync<OptionsValidationException>(async () =>
        {
            await option.SaveAsync(invalidSettings);
        });

        exception.Failures.Count().ShouldBeGreaterThan(1);
        exception.Failures.ShouldContain("Name 'forbidden' is not allowed");
    }

    [Fact]
    public async Task SaveAsync_WithActionUpdater_ShouldStillValidate()
    {
        var testFileName = Path.GetRandomFileName();

        var _instance = new WritableOptionsSimpleInstance<ValidatableSettings>();
        _instance.Initialize(options =>
        {
            options.FilePath = testFileName;
            options.UseInMemoryFileWriter(_fileWriter);
            options.WithValidatorFunction(settings =>
            {
                if (settings.MaxConnections < 1)
                    return ValidateOptionsResult.Fail("MaxConnections must be positive");
                return ValidateOptionsResult.Success;
            });
        });

        var option = _instance.GetOptions();

        var exception = await Should.ThrowAsync<OptionsValidationException>(async () =>
        {
            await option.SaveAsync(settings =>
            {
                settings.MaxConnections = -10;
            });
        });

        exception.Failures.ShouldContain("MaxConnections must be positive");
    }

    [Fact]
    public void ValidateOptionsResult_Success_ShouldCreateSuccessfulResult()
    {
        var result = ValidateOptionsResult.Success;
        result.Succeeded.ShouldBeTrue();
        result.Failed.ShouldBeFalse();
    }

    [Fact]
    public void ValidateOptionsResult_Failure_ShouldCreateFailedResult()
    {
        var result = ValidateOptionsResult.Fail(new[] { "Error 1", "Error 2" });
        result.Failed.ShouldBeTrue();
        result.Succeeded.ShouldBeFalse();
        result.Failures.Count().ShouldBe(2);
        result.Failures.ShouldContain("Error 1");
        result.Failures.ShouldContain("Error 2");
    }

    [Fact]
    public void ValidateOptionsResult_Failure_WithSingleError_ShouldCreateFailedResult()
    {
        var result = ValidateOptionsResult.Fail("Single error");
        result.Failed.ShouldBeTrue();
        result.Succeeded.ShouldBeFalse();
        result.Failures.Count().ShouldBe(1);
        result.Failures.ShouldContain("Single error");
    }

    [Fact]
    public void OptionsValidationException_ShouldContainFailures()
    {
        var failures = new[] { "Error 1", "Error 2" };
        var exception = new OptionsValidationException(
            "TestOptions",
            typeof(ValidatableSettings),
            failures
        );
        exception.Failures.Count().ShouldBe(2);
        exception.Failures.ShouldContain("Error 1");
        exception.Failures.ShouldContain("Error 2");
        exception.OptionsName.ShouldBe("TestOptions");
        exception.OptionsType.ShouldBe(typeof(ValidatableSettings));
    }
}

file class ValidatableSettings
{
    public int MaxConnections { get; set; } = 10;
    public string Email { get; set; } = "";
}

file class ValidatableSettingsValidator : IValidateOptions<ValidatableSettings>
{
    public ValidateOptionsResult Validate(string? name, ValidatableSettings options)
    {
        var errors = new System.Collections.Generic.List<string>();

        if (options.MaxConnections < 1 || options.MaxConnections > 1000)
        {
            errors.Add("MaxConnections must be between 1 and 1000");
        }

        if (string.IsNullOrWhiteSpace(options.Email) || !options.Email.Contains("@"))
        {
            errors.Add("Valid email is required");
        }

        return errors.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(errors);
    }
}

file class AnnotatedSettings
{
    [Range(1, 1000)]
    public int MaxConnections { get; set; }

    [Required]
    [EmailAddress]
    public string Email { get; set; } = "";

    [Required]
    [MinLength(3)]
    public string Name { get; set; } = "";
}
