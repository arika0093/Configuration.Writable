using System;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Threading.Tasks;
using Configuration.Writable.FileWriter;

namespace Configuration.Writable.Tests;

public class ValidationTests
{
    private readonly InMemoryFileWriter _fileWriter = new();

    [Fact]
    public async Task SaveAsync_WithValidationFunction_ShouldThrowWhenValidationFails()
    {
        var testFileName = Path.GetRandomFileName();

        var _instance = new WritableConfigSimpleInstance<ValidatableSettings>();
        _instance.Initialize(options =>
        {
            options.FilePath = testFileName;
            options.UseInMemoryFileWriter(_fileWriter);
            options.WithValidation(settings =>
            {
                if (settings.MaxConnections < 1)
                    return ValidationResult.Fail("MaxConnections must be positive");
                return ValidationResult.Ok();
            });
        });

        var invalidSettings = new ValidatableSettings
        {
            MaxConnections = 0,
            Email = "test@example.com",
        };

        var option = _instance.GetOption();

        var exception = await Should.ThrowAsync<ValidationException>(async () =>
        {
            await option.SaveAsync(invalidSettings);
        });

        exception.Errors.ShouldContain("MaxConnections must be positive");
        _fileWriter.FileExists(testFileName).ShouldBeFalse();
    }

    [Fact]
    public async Task SaveAsync_WithValidationFunction_ShouldSucceedWhenValidationPasses()
    {
        var testFileName = Path.GetRandomFileName();

        var _instance = new WritableConfigSimpleInstance<ValidatableSettings>();
        _instance.Initialize(options =>
        {
            options.FilePath = testFileName;
            options.UseInMemoryFileWriter(_fileWriter);
            options.WithValidation(settings =>
            {
                if (settings.MaxConnections < 1)
                    return ValidationResult.Fail("MaxConnections must be positive");
                return ValidationResult.Ok();
            });
        });

        var validSettings = new ValidatableSettings
        {
            MaxConnections = 10,
            Email = "test@example.com",
        };

        var option = _instance.GetOption();
        await option.SaveAsync(validSettings);

        _fileWriter.FileExists(testFileName).ShouldBeTrue();
        var loadedSettings = option.CurrentValue;
        loadedSettings.MaxConnections.ShouldBe(10);
    }

    [Fact]
    public async Task SaveAsync_WithMultipleValidators_ShouldCombineErrors()
    {
        var testFileName = Path.GetRandomFileName();

        var _instance = new WritableConfigSimpleInstance<ValidatableSettings>();
        _instance.Initialize(options =>
        {
            options.FilePath = testFileName;
            options.UseInMemoryFileWriter(_fileWriter);
            options.WithValidation(settings =>
            {
                if (settings.MaxConnections < 1)
                    return ValidationResult.Fail("MaxConnections must be positive");
                return ValidationResult.Ok();
            });
            options.WithValidation(settings =>
            {
                if (string.IsNullOrWhiteSpace(settings.Email))
                    return ValidationResult.Fail("Email is required");
                return ValidationResult.Ok();
            });
        });

        var invalidSettings = new ValidatableSettings { MaxConnections = 0, Email = "" };

        var option = _instance.GetOption();

        var exception = await Should.ThrowAsync<ValidationException>(async () =>
        {
            await option.SaveAsync(invalidSettings);
        });

        exception.Errors.Count.ShouldBe(2);
        exception.Errors.ShouldContain("MaxConnections must be positive");
        exception.Errors.ShouldContain("Email is required");
    }

    [Fact]
    public async Task SaveAsync_WithIValidator_ShouldValidateCorrectly()
    {
        var testFileName = Path.GetRandomFileName();

        var _instance = new WritableConfigSimpleInstance<ValidatableSettings>();
        _instance.Initialize(options =>
        {
            options.FilePath = testFileName;
            options.UseInMemoryFileWriter(_fileWriter);
            options.WithValidator(new ValidatableSettingsValidator());
        });

        var invalidSettings = new ValidatableSettings { MaxConnections = -5, Email = "invalid" };

        var option = _instance.GetOption();

        var exception = await Should.ThrowAsync<ValidationException>(async () =>
        {
            await option.SaveAsync(invalidSettings);
        });

        exception.Errors.Count.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task SaveAsync_WithDataAnnotations_ShouldValidateCorrectly()
    {
        var testFileName = Path.GetRandomFileName();

        var _instance = new WritableConfigSimpleInstance<AnnotatedSettings>();
        _instance.Initialize(options =>
        {
            options.FilePath = testFileName;
            options.UseInMemoryFileWriter(_fileWriter);
            options.EnableDataAnnotationsValidation();
        });

        var invalidSettings = new AnnotatedSettings
        {
            MaxConnections = 2000, // Over the range
            Email = "not-an-email",
        };

        var option = _instance.GetOption();

        var exception = await Should.ThrowAsync<ValidationException>(async () =>
        {
            await option.SaveAsync(invalidSettings);
        });

        exception.Errors.Count.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task SaveAsync_WithDataAnnotations_ShouldSucceedWhenValid()
    {
        var testFileName = Path.GetRandomFileName();

        var _instance = new WritableConfigSimpleInstance<AnnotatedSettings>();
        _instance.Initialize(options =>
        {
            options.FilePath = testFileName;
            options.UseInMemoryFileWriter(_fileWriter);
            options.EnableDataAnnotationsValidation();
        });

        var validSettings = new AnnotatedSettings
        {
            MaxConnections = 50,
            Email = "test@example.com",
            Name = "Valid Name",
        };

        var option = _instance.GetOption();
        await option.SaveAsync(validSettings);

        _fileWriter.FileExists(testFileName).ShouldBeTrue();
        var loadedSettings = option.CurrentValue;
        loadedSettings.MaxConnections.ShouldBe(50);
    }

    [Fact]
    public async Task SaveAsync_WithDataAnnotationsAndCustomValidators_ShouldCombineAll()
    {
        var testFileName = Path.GetRandomFileName();

        var _instance = new WritableConfigSimpleInstance<AnnotatedSettings>();
        _instance.Initialize(options =>
        {
            options.FilePath = testFileName;
            options.UseInMemoryFileWriter(_fileWriter);
            options.EnableDataAnnotationsValidation();
            options.WithValidation(settings =>
            {
                if (settings.Name == "forbidden")
                    return ValidationResult.Fail("Name 'forbidden' is not allowed");
                return ValidationResult.Ok();
            });
        });

        var invalidSettings = new AnnotatedSettings
        {
            MaxConnections = 2000, // Over range - Data Annotation error
            Email = "test@example.com",
            Name = "forbidden", // Custom validation error
        };

        var option = _instance.GetOption();

        var exception = await Should.ThrowAsync<ValidationException>(async () =>
        {
            await option.SaveAsync(invalidSettings);
        });

        exception.Errors.Count.ShouldBeGreaterThan(1);
        exception.Errors.ShouldContain("Name 'forbidden' is not allowed");
    }

    [Fact]
    public async Task SaveAsync_WithActionUpdater_ShouldStillValidate()
    {
        var testFileName = Path.GetRandomFileName();

        var _instance = new WritableConfigSimpleInstance<ValidatableSettings>();
        _instance.Initialize(options =>
        {
            options.FilePath = testFileName;
            options.UseInMemoryFileWriter(_fileWriter);
            options.WithValidation(settings =>
            {
                if (settings.MaxConnections < 1)
                    return ValidationResult.Fail("MaxConnections must be positive");
                return ValidationResult.Ok();
            });
        });

        var option = _instance.GetOption();

        var exception = await Should.ThrowAsync<ValidationException>(async () =>
        {
            await option.SaveAsync(settings =>
            {
                settings.MaxConnections = -10;
            });
        });

        exception.Errors.ShouldContain("MaxConnections must be positive");
    }

    [Fact]
    public void ValidationResult_Success_ShouldCreateSuccessfulResult()
    {
        var result = ValidationResult.Ok();
        result.IsValid.ShouldBeTrue();
        result.Errors.ShouldBeEmpty();
    }

    [Fact]
    public void ValidationResult_Failure_ShouldCreateFailedResult()
    {
        var result = ValidationResult.Fail("Error 1", "Error 2");
        result.IsValid.ShouldBeFalse();
        result.Errors.Count.ShouldBe(2);
        result.Errors.ShouldContain("Error 1");
        result.Errors.ShouldContain("Error 2");
    }

    [Fact]
    public void ValidationResult_Failure_WithNoErrors_ShouldThrow()
    {
        Should.Throw<ArgumentException>(() => ValidationResult.Fail());
    }

    [Fact]
    public void ValidationResult_Combine_ShouldMergeResults()
    {
        var result1 = ValidationResult.Ok();
        var result2 = ValidationResult.Fail("Error 1");
        var result3 = ValidationResult.Fail("Error 2", "Error 3");

        var combined = ValidationResult.Combine(result1, result2, result3);

        combined.IsValid.ShouldBeFalse();
        combined.Errors.Count.ShouldBe(3);
        combined.Errors.ShouldContain("Error 1");
        combined.Errors.ShouldContain("Error 2");
        combined.Errors.ShouldContain("Error 3");
    }

    [Fact]
    public void ValidationResult_Combine_AllSuccess_ShouldReturnSuccess()
    {
        var result1 = ValidationResult.Ok();
        var result2 = ValidationResult.Ok();

        var combined = ValidationResult.Combine(result1, result2);

        combined.IsValid.ShouldBeTrue();
        combined.Errors.ShouldBeEmpty();
    }

    [Fact]
    public void ValidationException_ShouldFormatMessageCorrectly()
    {
        var exception = new ValidationException("Error 1", "Error 2");
        exception.Message.ShouldContain("Error 1");
        exception.Message.ShouldContain("Error 2");
        exception.Errors.Count.ShouldBe(2);
    }
}

file class ValidatableSettings
{
    public int MaxConnections { get; set; } = 10;
    public string Email { get; set; } = "";
}

file class ValidatableSettingsValidator : IValidator<ValidatableSettings>
{
    public ValidationResult Validate(ValidatableSettings value)
    {
        var errors = new System.Collections.Generic.List<string>();

        if (value.MaxConnections < 1 || value.MaxConnections > 1000)
        {
            errors.Add("MaxConnections must be between 1 and 1000");
        }

        if (string.IsNullOrWhiteSpace(value.Email) || !value.Email.Contains("@"))
        {
            errors.Add("Valid email is required");
        }

        return errors.Count == 0 ? ValidationResult.Ok() : ValidationResult.Failure(errors);
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
