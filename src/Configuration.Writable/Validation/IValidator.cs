#pragma warning disable S3246 // Generic type parameters should be co/contravariant when possible
namespace Configuration.Writable.Validation;

/// <summary>
/// Defines a validator for a specific type.
/// </summary>
/// <typeparam name="T">The type to validate.</typeparam>
public interface IValidator<T>
    where T : class
{
    /// <summary>
    /// Validates the specified value.
    /// </summary>
    /// <param name="value">The value to validate.</param>
    /// <returns>A <see cref="ValidationResult"/> indicating whether validation succeeded or failed.</returns>
    ValidationResult Validate(T value);
}
