using System;
using System.Collections.Generic;
using System.Linq;

namespace Configuration.Writable.Validation;

/// <summary>
/// Exception thrown when configuration validation fails.
/// </summary>
public sealed class ValidationException : Exception
{
    /// <summary>
    /// Gets the collection of validation error messages.
    /// </summary>
    public IReadOnlyList<string> Errors { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ValidationException"/> class with the specified error messages.
    /// </summary>
    /// <param name="errors">The validation error messages.</param>
    public ValidationException(IEnumerable<string> errors)
        : base(FormatErrorMessage(errors))
    {
        Errors = errors?.ToList() ?? [];
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ValidationException"/> class with the specified error messages.
    /// </summary>
    /// <param name="errors">The validation error messages.</param>
    public ValidationException(params string[] errors)
        : base(FormatErrorMessage(errors))
    {
        Errors = errors ?? Array.Empty<string>();
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ValidationException"/> class with the specified validation result.
    /// </summary>
    /// <param name="validationResult">The validation result containing the error messages.</param>
    public ValidationException(ValidationResult validationResult)
        : base(FormatErrorMessage(validationResult?.Errors))
    {
        Errors = validationResult?.Errors ?? [];
    }

    private static string FormatErrorMessage(IEnumerable<string>? errors)
    {
        var errorList = errors?.ToList() ?? [];
        if (errorList.Count == 0)
        {
            return "Configuration validation failed.";
        }

        if (errorList.Count == 1)
        {
            return $"Configuration validation failed: {errorList[0]}";
        }

        return $"Configuration validation failed with {errorList.Count} error(s):{Environment.NewLine}{string.Join(Environment.NewLine, errorList.Select(e => $"  - {e}"))}";
    }
}
