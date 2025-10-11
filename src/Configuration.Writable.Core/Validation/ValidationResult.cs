using System;
using System.Collections.Generic;
using System.Linq;

namespace Configuration.Writable;

/// <summary>
/// Represents the result of a validation operation.
/// </summary>
public sealed class ValidationResult
{
    /// <summary>
    /// Gets a value indicating whether the validation was successful.
    /// </summary>
    public bool IsValid { get; }

    /// <summary>
    /// Gets the collection of error messages from the validation.
    /// </summary>
    public IReadOnlyList<string> Errors { get; }

    private ValidationResult(bool isValid, IReadOnlyList<string> errors)
    {
        IsValid = isValid;
        Errors = errors;
    }

    /// <summary>
    /// Creates a successful validation result.
    /// </summary>
    /// <returns>A successful validation result.</returns>
    public static ValidationResult Ok() => new(true, []);

    /// <summary>
    /// Creates a failed validation result with one or more error messages.
    /// </summary>
    /// <param name="errors">The error messages describing why validation failed.</param>
    /// <returns>A failed validation result.</returns>
    /// <exception cref="ArgumentException">Thrown when no errors are provided.</exception>
    public static ValidationResult Fail(params string[] errors)
    {
        if (errors == null || errors.Length == 0)
        {
            throw new ArgumentException(
                "At least one error message is required for a failed validation result.",
                nameof(errors)
            );
        }

        return new(false, errors);
    }

    /// <summary>
    /// Creates a failed validation result with a collection of error messages.
    /// </summary>
    /// <param name="errors">The error messages describing why validation failed.</param>
    /// <returns>A failed validation result.</returns>
    /// <exception cref="ArgumentException">Thrown when no errors are provided.</exception>
    public static ValidationResult Failure(IEnumerable<string> errors)
    {
        var errorList = errors?.ToList() ?? [];
        if (errorList.Count == 0)
        {
            throw new ArgumentException(
                "At least one error message is required for a failed validation result.",
                nameof(errors)
            );
        }

        return new(false, errorList);
    }

    /// <summary>
    /// Combines multiple validation results into a single result.
    /// </summary>
    /// <param name="results">The validation results to combine.</param>
    /// <returns>A successful result if all results are successful; otherwise, a failed result containing all errors.</returns>
    public static ValidationResult Combine(params ValidationResult[] results)
    {
        if (results == null || results.Length == 0)
        {
            return Ok();
        }

        var allErrors = results.Where(r => !r.IsValid).SelectMany(r => r.Errors).ToList();

        return allErrors.Count == 0 ? Ok() : Failure(allErrors);
    }
}
