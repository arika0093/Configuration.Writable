using System;
using System.Collections.Generic;

namespace Configuration.Writable.Options;

internal sealed class OptionsConfigurationInfo : IOptionsConfigurationInfo
{
    public OptionsConfigurationInfo(
        string instanceName,
        string readPath,
        string writePath,
        string formatFileExtension,
        IReadOnlyList<string> sectionNameParts
    )
    {
        InstanceName = instanceName;
        ReadPath = readPath;
        WritePath = writePath;
        FormatFileExtension = formatFileExtension.TrimStart('.');

        var copiedSectionNameParts = new string[sectionNameParts.Count];
        for (var index = 0; index < sectionNameParts.Count; index++)
        {
            copiedSectionNameParts[index] = sectionNameParts[index];
        }
        SectionNameParts = copiedSectionNameParts;
    }

    public string InstanceName { get; }

    public string ReadPath { get; }

    public string WritePath { get; }

    public string FormatFileExtension { get; }

    public IReadOnlyList<string> SectionNameParts { get; }

    public static IOptionsConfigurationInfo From<T>(WritableOptionsConfiguration<T> options)
        where T : class, new() =>
        new OptionsConfigurationInfo(
            options.InstanceName,
            options.ConfigFilePath,
            options.ConfigFilePath,
            options.FormatProvider.FileExtension,
            options.SectionNameParts
        );
}
