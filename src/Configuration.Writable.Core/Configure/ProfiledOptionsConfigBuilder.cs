using System;
using System.Collections.Generic;
using System.Linq;

namespace Configuration.Writable.Configure;

/// <summary>
/// Configures a set of named profiles stored below a shared configuration section.
/// </summary>
/// <typeparam name="T">The type of the profile configuration.</typeparam>
public class ProfiledOptionsConfigBuilder<T> : WritableOptionsConfigBuilder<T>
    where T : class, new()
{
    private const string CatalogInstanceName = "__profiles_catalog__";

    /// <summary>
    /// Gets or sets the section that contains the profile catalog and individual profiles.
    /// </summary>
    public string ProfileSectionName { get; set; } = "Profiles";

    /// <summary>
    /// Gets or sets the section that stores the profile catalog.
    /// </summary>
    public string ProfileCatalogSectionName { get; set; } = "ProfileCatalog";

    /// <summary>
    /// Gets or sets the profile available before any profile catalog has been saved.
    /// </summary>
    public string DefaultProfile { get; set; } = "default";

    internal ProfiledOptionsConfiguration<T> Build()
    {
        if (string.IsNullOrWhiteSpace(DefaultProfile))
        {
            throw new InvalidOperationException("DefaultProfile cannot be empty.");
        }

        var template = BuildOptions(CatalogInstanceName);
        var profileSectionParts = template
            .SectionNameParts.Concat(SplitSectionName(ProfileSectionName))
            .ToList();
        var catalogSectionParts = template
            .SectionNameParts.Concat(SplitSectionName(ProfileCatalogSectionName))
            .ToList();

        if (profileSectionParts.Count == 0 || catalogSectionParts.Count == 0)
        {
            throw new InvalidOperationException(
                "ProfileSectionName and ProfileCatalogSectionName must contain at least one section name."
            );
        }

        var catalogConfiguration = new WritableOptionsConfiguration<ProfileCatalog>
        {
            FormatProvider = template.FormatProvider,
            FileProvider = template.FileProvider,
            ConfigFilePath = template.ConfigFilePath,
            InstanceName = "",
            SectionNameParts = catalogSectionParts,
            OnChangeDebounce = template.OnChangeDebounce,
            ConflictResolution = template.ConflictResolution,
            CloneMethod = ProfileCatalog.Clone,
            Logger = template.Logger,
        };

        return new ProfiledOptionsConfiguration<T>(
            template,
            catalogConfiguration,
            profileSectionParts,
            DefaultProfile
        );
    }

    internal WritableOptionsConfiguration<T> BuildProfileConfiguration(
        WritableOptionsConfiguration<T> template,
        IReadOnlyList<string> profileSectionParts,
        string profileName
    )
    {
        var profileConfiguration = BuildOptions(profileName);
        var sectionNameParts = profileSectionParts.Append(profileName).ToList();
        return profileConfiguration with
        {
            ConfigFilePath = template.ConfigFilePath,
            SectionNameParts = sectionNameParts,
        };
    }

    private static IEnumerable<string> SplitSectionName(string sectionName) =>
        sectionName.Split([":", "__"], StringSplitOptions.RemoveEmptyEntries);
}

internal sealed record ProfiledOptionsConfiguration<T>(
    WritableOptionsConfiguration<T> Template,
    WritableOptionsConfiguration<ProfileCatalog> Catalog,
    IReadOnlyList<string> ProfileSectionParts,
    string DefaultProfile
)
    where T : class, new();

/// <summary>
/// Stores the active profile and the list of available profiles.
/// </summary>
public sealed class ProfileCatalog
{
    /// <summary>
    /// Gets or sets the active profile name.
    /// </summary>
    public string? ActiveProfileName { get; set; }

    /// <summary>
    /// Gets or sets the available profile names.
    /// </summary>
    public List<string> ProfileNames { get; set; } = [];

    internal static ProfileCatalog Clone(ProfileCatalog catalog) =>
        new()
        {
            ActiveProfileName = catalog.ActiveProfileName,
            ProfileNames = new List<string>(catalog.ProfileNames),
        };
}
