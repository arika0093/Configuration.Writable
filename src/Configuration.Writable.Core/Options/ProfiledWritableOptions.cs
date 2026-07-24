using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Configuration.Writable.Configure;

namespace Configuration.Writable.Options;

internal sealed class ProfiledWritableOptions<T>(
    IWritableOptionsConfigRegistry<T> registry,
    IWritableNamedOptions<T> namedOptions,
    IWritableOptions<ProfileCatalog> catalogOptions,
    ProfiledOptionsConfigBuilder<T> profileBuilder,
    ProfiledOptionsConfiguration<T> configuration
) : IProfiledWritableOptions<T>
    where T : class, new()
{
    private static readonly HashSet<string> ReservedProfileNames =
        new(StringComparer.Ordinal) { nameof(ProfileCatalog.ActiveProfileName), nameof(ProfileCatalog.ProfileNames) };

    public string ActiveProfileName
    {
        get
        {
            var activeProfileName = catalogOptions.CurrentValue.ActiveProfileName;
            if (string.IsNullOrWhiteSpace(activeProfileName))
            {
                return DefaultProfile;
            }
            return activeProfileName ?? DefaultProfile;
        }
    }

    public string DefaultProfile => configuration.DefaultProfile;

    public IReadOnlyCollection<string> ProfileNames =>
        catalogOptions.CurrentValue.ProfileNames.Count == 0
            ? new[] { DefaultProfile }
            : catalogOptions.CurrentValue.ProfileNames.AsReadOnly();

    public T CurrentValue => namedOptions.Get(ActiveProfileName);

    public IReadOnlyOptions<T> GetProfile(string name)
    {
        EnsureProfileExists(name);
        return namedOptions.GetInstance(name);
    }

    IWritableOptions<T> IProfiledWritableOptions<T>.GetProfile(string name)
    {
        EnsureProfileExists(name);
        return namedOptions.GetInstance(name);
    }

    public async Task SaveAsync(T newConfig, CancellationToken cancellationToken = default)
    {
        await EnsureCatalogAsync(cancellationToken).ConfigureAwait(false);
        await namedOptions.SaveAsync(ActiveProfileName, newConfig, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task SaveAsync(
        Action<T> configUpdater,
        CancellationToken cancellationToken = default
    )
    {
        await EnsureCatalogAsync(cancellationToken).ConfigureAwait(false);
        await namedOptions.SaveAsync(ActiveProfileName, configUpdater, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task SaveAsync(
        Func<T, Task> configUpdater,
        CancellationToken cancellationToken = default
    )
    {
        await EnsureCatalogAsync(cancellationToken).ConfigureAwait(false);
        await namedOptions.SaveAsync(ActiveProfileName, configUpdater, cancellationToken)
            .ConfigureAwait(false);
    }

    public IDisposable? OnChange(Action<T> listener)
    {
        if (listener == null)
        {
            throw new ArgumentNullException(nameof(listener));
        }

        var profileSubscription = namedOptions.OnChange(
            (value, name) =>
            {
                if (name == ActiveProfileName)
                {
                    listener(value);
                }
            }
        );
        var catalogSubscription = catalogOptions.OnChange(_ =>
        {
            if (!string.IsNullOrWhiteSpace(catalogOptions.CurrentValue.ActiveProfileName))
            {
                listener(CurrentValue);
            }
        });

        return new CompositeDisposable(profileSubscription, catalogSubscription);
    }

    public async Task CreateProfileAsync(
        string name,
        string? copyFrom = null,
        CancellationToken cancellationToken = default
    )
    {
        ValidateProfileName(name);
        var source = copyFrom == null ? null : GetProfileValue(copyFrom);
        await EnsureCatalogAsync(cancellationToken).ConfigureAwait(false);

        await catalogOptions
            .SaveAsync(
                catalog =>
                {
                    if (catalog.ProfileNames.Contains(name, StringComparer.Ordinal))
                    {
                        throw new InvalidOperationException($"The profile '{name}' already exists.");
                    }

                    catalog.ProfileNames.Add(name);
                    if (string.IsNullOrWhiteSpace(catalog.ActiveProfileName))
                    {
                        catalog.ActiveProfileName = name;
                    }
                },
                cancellationToken
            )
            .ConfigureAwait(false);

        EnsureProfileRegistered(name);
        if (source != null)
        {
            await namedOptions.SaveAsync(name, source, cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task RemoveProfileAsync(
        string name,
        CancellationToken cancellationToken = default
    )
    {
        EnsureProfileExists(name);
        if (string.Equals(name, DefaultProfile, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The default profile cannot be removed.");
        }

        await catalogOptions
            .SaveAsync(
                catalog =>
                {
                    catalog.ProfileNames.RemoveAll(profileName =>
                        string.Equals(profileName, name, StringComparison.Ordinal)
                    );
                    if (string.Equals(catalog.ActiveProfileName, name, StringComparison.Ordinal))
                    {
                        catalog.ActiveProfileName = catalog.ProfileNames.FirstOrDefault();
                    }
                },
                cancellationToken
            )
            .ConfigureAwait(false);
        registry.TryRemove(name);
    }

    public async Task SetActiveProfileAsync(
        string name,
        CancellationToken cancellationToken = default
    )
    {
        EnsureProfileExists(name);
        await EnsureCatalogAsync(cancellationToken).ConfigureAwait(false);
        await catalogOptions
            .SaveAsync(catalog => catalog.ActiveProfileName = name, cancellationToken)
            .ConfigureAwait(false);
    }

    internal void RegisterPersistedProfiles()
    {
        EnsureProfileRegistered(DefaultProfile);
        foreach (var name in ProfileNames)
        {
            ValidateProfileName(name);
            EnsureProfileRegistered(name);
        }
    }

    private T GetProfileValue(string name)
    {
        EnsureProfileExists(name);
        return namedOptions.Get(name);
    }

    private void EnsureProfileExists(string name)
    {
        ValidateProfileName(name);
        if (!ProfileNames.Contains(name, StringComparer.Ordinal))
        {
            throw new KeyNotFoundException($"The profile '{name}' does not exist.");
        }
        EnsureProfileRegistered(name);
    }

    private void EnsureProfileRegistered(string name)
    {
        var profileConfiguration = profileBuilder.BuildProfileConfiguration(
            configuration.Template,
            configuration.ProfileSectionParts,
            name
        );
        registry.TryAdd(profileConfiguration);
    }

    private Task EnsureCatalogAsync(CancellationToken cancellationToken)
    {
        if (catalogOptions.CurrentValue.ProfileNames.Count != 0)
        {
            return Task.CompletedTask;
        }

        return catalogOptions.SaveAsync(
            catalog =>
            {
                catalog.ProfileNames.Add(DefaultProfile);
                catalog.ActiveProfileName = DefaultProfile;
            },
            cancellationToken
        );
    }

    private static void ValidateProfileName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Profile names cannot be empty.", nameof(name));
        }
        if (name.Contains(':') || name.Contains("__") || ReservedProfileNames.Contains(name))
        {
            throw new ArgumentException($"'{name}' is not a valid profile name.", nameof(name));
        }
    }

    private sealed class CompositeDisposable(IDisposable? first, IDisposable? second) : IDisposable
    {
        public void Dispose()
        {
            first?.Dispose();
            second?.Dispose();
        }
    }
}
