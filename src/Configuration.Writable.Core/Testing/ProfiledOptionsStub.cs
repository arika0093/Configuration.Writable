using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Configuration.Writable.Testing;

/// <summary>
/// An in-memory implementation of <see cref="IProfiledWritableOptions{T}"/> for testing.
/// </summary>
/// <typeparam name="T">The type of the profile configuration.</typeparam>
public class ProfiledOptionsStub<T> : IProfiledWritableOptions<T>
    where T : class, new()
{
    private readonly WritableOptionsStub<T> _options;
    private IReadOnlyCollection<string> _profileNames = [];

    /// <summary>
    /// Initializes a new instance of the <see cref="ProfiledOptionsStub{T}"/> class.
    /// </summary>
    /// <param name="defaultValue">The initial value of the default profile.</param>
    /// <param name="defaultProfile">The name of the default profile.</param>
    public ProfiledOptionsStub(T defaultValue, string defaultProfile = "default")
    {
        if (string.IsNullOrWhiteSpace(defaultProfile))
        {
            throw new ArgumentException("The default profile name cannot be empty.", nameof(defaultProfile));
        }

        DefaultProfile = defaultProfile;
        ActiveProfileName = defaultProfile;
        _options = new WritableOptionsStub<T>(
            new Dictionary<string, T> { [defaultProfile] = defaultValue }
        );
        RefreshProfileNames();
    }

    /// <summary>
    /// Gets the name of the default profile.
    /// </summary>
    public string DefaultProfile { get; }

    /// <summary>
    /// Gets the name of the active profile.
    /// </summary>
    public string ActiveProfileName { get; private set; }

    /// <summary>
    /// Gets the names of all available profiles.
    /// </summary>
    public IReadOnlyCollection<string> ProfileNames => _profileNames;

    /// <summary>
    /// Gets the current value of the active profile.
    /// </summary>
    public T CurrentValue => _options.Get(ActiveProfileName);

    /// <inheritdoc />
    public IReadOnlyOptions<T> GetProfile(string name)
    {
        EnsureProfileExists(name);
        return ((IReadOnlyNamedOptions<T>)_options).GetInstance(name);
    }

    /// <inheritdoc />
    IWritableOptions<T> IProfiledWritableOptions<T>.GetProfile(string name)
    {
        EnsureProfileExists(name);
        return ((IWritableNamedOptions<T>)_options).GetInstance(name);
    }

    /// <inheritdoc />
    public IDisposable? OnChange(Action<T> listener)
    {
        if (listener == null)
        {
            throw new ArgumentNullException(nameof(listener));
        }

        return _options.OnChange(
            (value, name) =>
            {
                if (name == ActiveProfileName)
                {
                    listener(value);
                }
            }
        );
    }

    /// <inheritdoc />
    public Task SaveAsync(T newConfig, CancellationToken cancellationToken = default) =>
        _options.SaveAsync(ActiveProfileName, newConfig, cancellationToken);

    /// <inheritdoc />
    public Task SaveAsync(
        Action<T> configUpdater,
        CancellationToken cancellationToken = default
    ) => _options.SaveAsync(ActiveProfileName, configUpdater, cancellationToken);

    /// <inheritdoc />
    public Task SaveAsync(
        Func<T, Task> configUpdater,
        CancellationToken cancellationToken = default
    ) => _options.SaveAsync(ActiveProfileName, configUpdater, cancellationToken);

    /// <inheritdoc />
    public async Task CreateProfileAsync(
        string name,
        string? copyFrom = null,
        CancellationToken cancellationToken = default
    )
    {
        ValidateNewProfileName(name);
        var value = copyFrom == null ? new T() : Clone(GetProfileValue(copyFrom));
        await _options.SaveAsync(name, value, cancellationToken).ConfigureAwait(false);
        RefreshProfileNames();
    }

    /// <inheritdoc />
    public Task RemoveProfileAsync(string name, CancellationToken cancellationToken = default)
    {
        EnsureProfileExists(name);
        if (string.Equals(name, DefaultProfile, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The default profile cannot be removed.");
        }

        _options.NamedValues.Remove(name);
        RefreshProfileNames();
        if (string.Equals(name, ActiveProfileName, StringComparison.Ordinal))
        {
            ActiveProfileName = DefaultProfile;
        }
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task SetActiveProfileAsync(string name, CancellationToken cancellationToken = default)
    {
        EnsureProfileExists(name);
        ActiveProfileName = name;
        foreach (var listener in _options.ChangeListeners.ToArray())
        {
            listener(CurrentValue, ActiveProfileName);
        }
        return Task.CompletedTask;
    }

    private T GetProfileValue(string name)
    {
        EnsureProfileExists(name);
        return _options.Get(name);
    }

    private void EnsureProfileExists(string name)
    {
        if (!_options.NamedValues.ContainsKey(name))
        {
            throw new KeyNotFoundException($"The profile '{name}' does not exist.");
        }
    }

    private void ValidateNewProfileName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Profile names cannot be empty.", nameof(name));
        }
        if (_options.NamedValues.ContainsKey(name))
        {
            throw new InvalidOperationException($"The profile '{name}' already exists.");
        }
    }

    private void RefreshProfileNames() => _profileNames = _options.NamedValues.Keys.ToArray();

    private static T Clone(T value) =>
        JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value))!;
}

/// <summary>
/// Provides factory methods for <see cref="ProfiledOptionsStub{T}"/>.
/// </summary>
public static class ProfiledOptionsStub
{
    /// <summary>
    /// Creates an in-memory profiled options stub.
    /// </summary>
    /// <typeparam name="T">The type of the profile configuration.</typeparam>
    /// <param name="defaultValue">The initial value of the default profile.</param>
    /// <param name="defaultProfile">The name of the default profile.</param>
    /// <returns>A profiled options stub.</returns>
    public static ProfiledOptionsStub<T> Create<T>(T defaultValue, string defaultProfile = "default")
        where T : class, new() => new(defaultValue, defaultProfile);
}
